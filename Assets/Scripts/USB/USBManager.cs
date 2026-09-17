using UnityEngine;
using System.IO.Ports;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;

namespace USB
{
    /// <summary>
    /// USBManager manages the serial connection to the Teensy (or other USB-serial device).
    /// Responsibilities:
    ///  - Establish and close SerialPort connections.
    ///  - Spawn background tasks to read incoming bytes and write outgoing packets.
    ///  - Parse the incoming byte stream into framed packets and push them to PacketBus.ReceiveWriter.
    ///  - Read outgoing packets from PacketBus.SendReader and write them to the serial port.
    ///
    /// Events:
    ///  - OnUSBConnected: raised when a serial port is opened successfully.
    ///  - OnUSBDisconnected: raised when the device disconnects or the port is closed.
    /// </summary>
    public class USBManager : MonoBehaviour
    {
        public static event Action OnUSBConnected;
        public static event Action OnUSBDisconnected;

        // Singleton instance — persists across scene loads.
        private static USBManager _instance;
        public static USBManager Instance => _instance;

        // Public flag that other code can check to see whether hardware is currently connected.
        public static bool IsConnected { get; private set; } = false;

        // If an IOException occurs on a background thread, we set this flag and call Disconnect() on the main thread in Update.
        private volatile bool _hardwareDisconnected = false;

        public string portName = "";
        private SerialPort _serialPort;
        private CancellationTokenSource _cts;
        private Task _readTask;
        private Task _writeTask;

        // Constants used by packet framing code. SYNC_BYTES matches the two sync bytes at packet start (0x7F, 0xFE).
        private const uint SYNC_BYTES = 0x7FFE;
        private const int HEADER_SIZE = 4;

        void Awake()
        {
            // Singleton: if another USBManager already exists (from a previous scene), destroy this duplicate.
            if (_instance != null && _instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(this.gameObject);
        }

        void Start()
        {
            // Guard: if Awake() destroyed us as a duplicate, do nothing.
            if (_instance != this) return;

            Debug.Log("USB Manager ready. Attempting automatic connection to Teensy device...");
            // Automatically detect and connect to Teensy on startup
            ConnectToTeensy();
        }

        private void Update()
        {
            // If a background thread detected hardware disconnection via IOException, it sets _hardwareDisconnected.
            // We respond on the main thread by calling Disconnect to ensure proper Unity lifecycle behavior.
            if (_hardwareDisconnected)
            {
                _hardwareDisconnected = false; 
                Disconnect();
            }
        }

        /// <summary>
        /// Open the serial port and start reader/writer background tasks.
        /// Tasks are started with Task.Run and run on thread pool threads so they don't block the Unity main thread.
        /// </summary>
        public void ConnectToTeensy()
        {
            if (IsConnected || (_serialPort != null && _serialPort.IsOpen))
            {
                Debug.Log("USBManager: Already connected.");
                return;
            }

            // Build the list of ports to try.
            // If a port is pinned in the Inspector, try only that one.
            // Otherwise ask USBDetector for a ranked list (registry → heuristic → all ports).
            string[] portsToTry;
            if (!string.IsNullOrWhiteSpace(portName))
            {
                portsToTry = new[] { portName };
            }
            else
            {
                portsToTry = BuildCandidatePortList();
            }

            if (portsToTry == null || portsToTry.Length == 0)
            {
                Debug.LogError("USBManager: No COM ports detected. Connect the Teensy and try again.");
                return;
            }

            foreach (string candidate in portsToTry)
            {
                if (TryOpenPort(candidate))
                    return; // Connected — done.
            }

            Debug.LogError(
                "USBManager: Could not open any candidate COM port. " +
                "Close any serial monitors (Arduino IDE, PuTTY, etc.) that may be holding the port, " +
                "then re-enter Play mode. You can also pin the correct port in the USBManager Inspector.");
        }

        /// <summary>
        /// Returns a ranked list of ports to try: registry-detected Teensy first,
        /// then remaining ports ordered by COM number (descending — USB-serial devices
        /// typically get higher numbers than hardware UARTs / Bluetooth adapters).
        /// </summary>
        private static string[] BuildCandidatePortList()
        {
            var detector = new USBDetector();
            (string best, _) = detector.FindTeensyComPort();

            string[] all = System.IO.Ports.SerialPort.GetPortNames();
            if (all == null || all.Length == 0) return System.Array.Empty<string>();

            // Sort descending by COM number so higher (USB-serial) ports are tried first.
            System.Array.Sort(all, (a, b) =>
            {
                int na = ParsePortNum(a), nb = ParsePortNum(b);
                return nb.CompareTo(na); // descending
            });

            // Move the registry-detected best port to the front if present.
            if (!string.IsNullOrEmpty(best))
            {
                var list = new System.Collections.Generic.List<string>(all);
                list.Remove(best);
                list.Insert(0, best);
                return list.ToArray();
            }

            return all;
        }

        private static int ParsePortNum(string name)
        {
            if (name != null && name.StartsWith("COM", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(name.Substring(3), out int n)) return n;
            return 0;
        }

        /// <summary>
        /// Attempts to open <paramref name="candidate"/> at 115200 baud.
        /// Returns true and fires OnUSBConnected on success; returns false and logs on failure.
        /// </summary>
        private bool TryOpenPort(string candidate)
        {
            Debug.Log($"USBManager: Trying {candidate}...");
            var sp = new SerialPort(candidate, 115200, Parity.None, 8, StopBits.One)
            {
                ReadTimeout  = 50,
                WriteTimeout = 250,
                DtrEnable    = true,
                RtsEnable    = true
            };

            try
            {
                sp.Open();
                sp.DiscardInBuffer();
                sp.DiscardOutBuffer();

                _serialPort = sp;
                portName    = candidate;

                _cts = new CancellationTokenSource();
                IsConnected = true;
                OnUSBConnected?.Invoke();

                _readTask = Task.Run(() => ReadSerialData(_cts.Token));
                _writeTask = Task.Run(() => WriteSerialData(_cts.Token));

                Debug.Log($"<color=green>USBManager: Connected on {candidate}</color>");
                return true;
            }
            catch (Exception e)
            {
                try { sp.Dispose(); } catch { }
                Debug.LogWarning($"USBManager: Could not open {candidate} — {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gracefully disconnect: cancel tasks, close and dispose serial port, and raise disconnect event.
        /// </summary>
        public void Disconnect()
        {
            if (!IsConnected)
            {
                Debug.Log("USB already Disconnected");
                return;
            }
            
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (_serialPort != null)
            {
                try { _serialPort.Close(); }
                catch { Debug.Log("Port already closed"); }
                _serialPort.Dispose();
                _serialPort = null;
            }

            IsConnected = false;
            OnUSBDisconnected?.Invoke();
        }

        public void ShutdownForQuit()
        {
            if (_cts != null)
            {
                _cts.Cancel();
            }

            WaitForTaskToFinish(_readTask, "USBManager read");
            WaitForTaskToFinish(_writeTask, "USBManager write");

            _readTask = null;
            _writeTask = null;

            Disconnect();
        }

        /// <summary>
        /// Background read loop that reads raw bytes from the serial port and frames them into packets.
        /// It accumulates bytes into streamBuffer until there are enough bytes for a full packet, then extracts it.
        ///
        /// Packet framing rules (as used here):
        ///  - Two sync bytes at the start: 0x7F, 0xFE
        ///  - Header size is 4 bytes; the payload length is located in byte index 3 of the header.
        ///  - Total packet size = HEADER_SIZE + payloadLength + 2 (CRC size).
        ///
        /// After extracting a complete packet, it writes the framed packet (minus the two leading sync bytes)
        /// into PacketBus.ReceiveWriter for the PacketProcessor to parse and handle.
        /// </summary>
        private async Task ReadSerialData(CancellationToken token)
        {
            byte[] readBuffer = new byte[1024];
            List<byte> streamBuffer = new List<byte>();
            int droppedPacketCount = 0;
            DateTime lastDropLogUtc = DateTime.UtcNow;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    Debug.Log("Read Serial Data Thing is running...");
                    if (_serialPort == null || !_serialPort.IsOpen)
                    {
                        break;
                    }

                    int count = _serialPort.Read(readBuffer, 0, readBuffer.Length);
                    if (count <= 0)
                    {
                        continue;
                    }

                    for (int i = 0; i < count; i++)
                    {
                        streamBuffer.Add(readBuffer[i]);
                    }

                    while (streamBuffer.Count >= HEADER_SIZE + 2)
                    {
                        Debug.Log("USBManager: Read " + streamBuffer.Count + " bytes from serial port, looking for packets...");
                        if (streamBuffer[0] != 0x7F || streamBuffer[1] != 0xFE)
                        {
                            streamBuffer.RemoveAt(0);
                            continue;
                        }
                        Debug.Log("USBManager: Sync bytes found. Attempting to parse packet...");

                        byte payloadLength = streamBuffer[3];
                        int totalPacketSize = HEADER_SIZE + payloadLength + 2;
                        if (streamBuffer.Count < totalPacketSize)
                        {
                            break;
                        }



                        byte[] packetData = streamBuffer.GetRange(2, totalPacketSize - 2).ToArray();
                        bool success = PacketBus.ReceiveWriter.TryWrite(packetData);
                        if (!success)
                        {
                            droppedPacketCount++;
                            DateTime nowUtc = DateTime.UtcNow;
                            if ((nowUtc - lastDropLogUtc).TotalSeconds >= 1.0)
                            {
                                Debug.LogWarning($"USBManager: Receive packet bus full; dropped {droppedPacketCount} packets in the last second.");
                                droppedPacketCount = 0;
                                lastDropLogUtc = nowUtc;
                            }
                        }
                        streamBuffer.RemoveRange(0, totalPacketSize);
                    }
                }
                catch (TimeoutException)
                {
                    // Expected because we use a short timeout to allow regular cancellation checks.
                }
                catch (IOException)
                {
                    Debug.LogWarning("USBManager: Hardware USB disconnected during read.");
                    _hardwareDisconnected = true;
                    break;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error reading serial data: {e.Message}");
                }

                await Task.Delay(10);
            }
        }

        /// <summary>
        /// Background writer loop that awaits packets from PacketBus.SendReader and writes them to the serial port.
        /// Using "await foreach" will asynchronously wait for items and support cancellation via the token.
        /// </summary>
        private async Task WriteSerialData(CancellationToken token)
        {
            try
            {
                await foreach (var packet in PacketBus.SendReader.ReadAllAsync(token))
                {
                    if (_serialPort == null || !_serialPort.IsOpen)
                    {
                        Debug.LogWarning("USBManager: Dropped outgoing packet because the serial port is not open.");
                        continue;
                    }

                    _serialPort.Write(packet, 0, packet.Length);
                    Debug.Log($"USBManager: Wrote {packet.Length} bytes to {portName}.");
                }
            }
            catch (IOException)
            {
                Debug.LogWarning("USBManager: Hardware USB disconnected during write.");
                _hardwareDisconnected = true;
            }
            catch (OperationCanceledException)
            {
                Debug.Log("USBManager: Writer thread shutting down.");
            }
            catch (Exception e)
            {
                Debug.LogError($"Writer thread error: {e.Message}");
            }
        }

        void OnDestroy()
        {
            // Only clean up if we are the active singleton (not a duplicate that was already destroyed).
            if (_instance == this)
            {
                Disconnect();
                _instance = null;
            }
        }

        void OnApplicationQuit()
        {
            ShutdownForQuit();
        }
        
        /// <summary>
        /// Simple CRC-16 calculation (polynomial 0xA001) used for packet integrity checks.
        /// </summary>
        public static ushort CalculateCRC(byte[] data)
        {
            ushort crc = 0x0000;
            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 1) != 0)
                        crc = (ushort)((crc >> 1) ^ 0xA001);
                    else
                        crc >>= 1;
                }
            }
            return crc;
        }

        private static void WaitForTaskToFinish(Task task, string taskName)
        {
            if (task == null)
            {
                return;
            }

            try
            {
                task.Wait(1000);
            }
            catch (AggregateException ex)
            {
                foreach (Exception inner in ex.Flatten().InnerExceptions)
                {
                    if (inner is OperationCanceledException)
                    {
                        continue;
                    }

                    Debug.LogError($"{taskName} shutdown error: {inner}");
                }
            }
        }
    }
}
