using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// PacketBus is a small, process-wide message bus implemented with System.Threading.Channels.
/// It provides two bounded channels:
///  - Receive channel (Teensy -> Unity): USBManager writes incoming raw packets here.
///  - Send channel (Unity -> Teensy): PacketProcessor (or any producer) writes outgoing packets here.
///
/// Channels are used because they provide a thread-safe, efficient producer/consumer queue that
/// supports both synchronous and asynchronous producers/consumers. The channels are bounded to
/// 100 items to avoid unbounded memory growth if the writer produces data faster than readers consume it.
/// </summary>
public static class PacketBus
{
    // Teensy -> Unity (incoming)
    private static readonly Channel<byte[]> _receiveChannel = Channel.CreateBounded<byte[]>(1024);
    public static ChannelWriter<byte[]> ReceiveWriter => _receiveChannel.Writer; // Used by USBManager to push received packets into Unity.
    public static ChannelReader<byte[]> ReceiveReader => _receiveChannel.Reader; // Used by PacketProcessor to read packets as they arrive.

    // Unity -> Teensy (outgoing)
    private static readonly Channel<byte[]> _sendChannel = Channel.CreateBounded<byte[]>(100);
    public static ChannelWriter<byte[]> SendWriter => _sendChannel.Writer; // Used by PacketProcessor and other senders to queue packets for USBManager to transmit.
    public static ChannelReader<byte[]> SendReader => _sendChannel.Reader; // Used by USBManager to read outgoing packets and write them to the serial port.
}

namespace PacketProcessor
{
    /// <summary>
    /// PacketProcessor owns the logic for consuming packets from PacketBus.ReceiveReader,
    /// parsing them into higher-level Packet objects, and dispatching them to registered handlers.
    ///
    /// It also provides helper methods to create and send command packets via PacketBus.SendWriter.
    /// </summary>
    public class PacketProcessor : MonoBehaviour
    {
        // Singleton instance so other parts of the code (UI, InputHandler) can easily access processor methods.
        public static PacketProcessor Instance { get; private set; }

        // Written from background thread (HandleJointData), read from main thread (Update).
        // volatile ensures the main thread always sees the latest value without CPU/JIT caching.
        private volatile bool _triggerPressed;
        public bool TriggerPressed => _triggerPressed;

        public float J0;
        public float J1;
        public float J2;
        public float J3;
        public float J4;
        public float J5;
        public bool HasJointData;

        // Arm operating mode. Written from background thread (HandleStatus / HandleLogMessage),
        // read from main thread (Update). volatile int avoids a separate lock.
        private volatile int _armMode = (int)ArmOperatingMode.Unknown;
        public ArmOperatingMode ArmMode => (ArmOperatingMode)_armMode;

        // Tracks the last mode value we fired OnArmModeChanged for (main thread only).
        private ArmOperatingMode _lastNotifiedMode = ArmOperatingMode.Unknown;

        /// <summary>
        /// Raised on the Unity main thread whenever the arm's operating mode changes.
        /// Subscribers (HUD, status pills) use this to update UI without polling.
        /// </summary>
        public event Action<ArmOperatingMode> OnArmModeChanged;


        // CancellationTokenSource used to cancel the background packet processing loop(s) when USB disconnects
        // or when the object is destroyed. Canceling this token signals the Task/loops to stop cleanly.
        private CancellationTokenSource _cts;
        private Task _packetLoopTask;

        // Dictionary mapping packet types to handler delegates.
        // The key is a PacketType enum value and the value is an Action<Packet> to call when a packet of that type arrives.
        // This is a simple dispatch table: O(1) lookup for handler based on packet type.
        private Dictionary<PacketType, Action<Packet>> _packetHandlers;
        private int _jointTelemetryLogDivider = 0;
        private static readonly string[] JointLabels =
        {
            "ROTATE",   // J0 — ODrive 0
            "REACH",    // J1 — ODrive 1
            "LIFT",     // J2 — ODrive 2
            "PITCH",    // J3 — Encoder 0
            "YAW",      // J4 — Encoder 1
            "ROLL"      // J5 — Encoder 2
        };

        private void Awake()
        {
            // Ensure only one PacketProcessor exists (singleton pattern). If duplicates exist (e.g. scene reload),
            // destroy the extra GameObject to avoid multiple loops and duplicate handlers.
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(this.gameObject);

            // Initialize the dispatch dictionary with the default handlers.
            // You can subscribe additional handlers later with Subscribe(...)
            _packetHandlers = new Dictionary<PacketType, Action<Packet>>
            {
                { PacketType.RESP_PONG, HandlePongResponse },
                { PacketType.RESP_ACK, HandleAckResponse },
                { PacketType.RESP_NACK, HandleNackResponse },
                { PacketType.TELEM_JOINT_DATA, HandleJointData },
                { PacketType.TELEM_STATUS, HandleStatus },
                { PacketType.LOG_MESSAGE, HandleLogMessage },
                { PacketType.ERROR_MESSAGE, HandleErrorMessage }
            };

            // Subscribe in Awake so we don't miss a USB connected event fired during other components' Start().
            USB.USBManager.OnUSBConnected += HandleUSBConnected;
            USB.USBManager.OnUSBDisconnected += HandleUSBDisconnected;

            // If USBManager already set IsConnected before Awake, start the packet loop immediately.
            // This handles the case where the USBManager connected earlier and already signaled.
            if (USB.USBManager.IsConnected)
            {
                HandleUSBConnected();
            }
        }

        /// <summary>
        /// Register an additional handler for a packet type.
        /// If a handler already exists for the type, the provided handler is added (multicast delegate),
        /// so multiple subscribers can receive the same packet type.
        ///
        /// Example: _packetHandlers[type] += handler adds another invocation to the multicast delegate.
        /// </summary>
        public void Subscribe(PacketType type, Action<Packet> handler)
        {
            if (_packetHandlers.ContainsKey(type))
            {
                _packetHandlers[type] += handler;
            }
            else
            {
                _packetHandlers[type] = handler;
            }
        }

        private void Start()
        {
            // No-op: subscriptions moved to Awake to avoid races with USBManager.Start().
        }

        private void Update()
        {
            if (!USB.USBManager.IsConnected) return;

            // NOTE: Joint data is auto-streamed by the firmware every 5 ms while in
            // READY state — no periodic CMD_REQUEST_TELEM is needed. The request
            // command is still available (SendRequestTelem) for one-shot use when
            // data is needed outside of READY (e.g. during HOMING or testing).

            // Fire arm mode change event on the main thread if mode changed since last check.
            ArmOperatingMode currentMode = ArmMode;
            if (currentMode != _lastNotifiedMode)
            {
                _lastNotifiedMode = currentMode;
                OnArmModeChanged?.Invoke(currentMode);
            }
        }

        private void OnDestroy()
        {
            // Cancel background loops BEFORE unsubscribing so the old loop stops
            // consuming channel packets before the new PacketProcessor instance starts.
            // Without this, the old loop races with the new instance's startup ping/pong
            // handshake and steals the pong, causing a false 2-second timeout.
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            // Unsubscribe to avoid memory leaks or invoking methods on a destroyed object.
            USB.USBManager.OnUSBConnected -= HandleUSBConnected;
            USB.USBManager.OnUSBDisconnected -= HandleUSBDisconnected;
        }

        /// <summary>
        /// Called when USBManager signals a successful connection.
        /// This cancels any previous processing loops, creates a fresh CancellationTokenSource,
        /// sends an initial ping and starts the packet processing loop in the background.
        ///
        /// Note: "_ = RunPacketLoopAsync(_cts.Token);" starts the async method but intentionally
        /// discards the returned Task so Start does not await completion. This launches a background task.
        /// </summary>
        private volatile bool _startupPongReceived = false;
        private async void HandleUSBConnected()
        {
            // Reset arm mode so stale state from a previous session doesn't persist.
            _armMode = (int)ArmOperatingMode.Unknown;
            Debug.Log("PacketProcessor detected USB connection. Starting loops.");

            // If there was an existing CTS, cancel and dispose it to stop previous loops before creating a new one.
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            
            _packetLoopTask = RunPacketLoopAsync(_cts.Token);

            // 2. RESET THE FLAG & SEND PING
            _startupPongReceived = false;

            // Optionally send a ping immediately to validate comms.
            if (!SendPing())
            {
                Debug.LogWarning("Connection Warning: Initial ping enqueue failed.");
            }

            // 3. WAIT FOR THE PONG (Non-blocking)
            int timeoutMs = 2000; // Wait up to 2 seconds
            int waitedMs = 0;
            int checkInterval = 50; // Check every 50 milliseconds
            
            while (!_startupPongReceived && waitedMs < timeoutMs)
            {
                await Task.Delay(checkInterval); // Pause here, let Unity keep running, then check again
                waitedMs += checkInterval;
            }

            // 4. EVALUATE THE RESULT
            if (_startupPongReceived)
            {
                Debug.Log("<color=green>Connection Validated: Initial Pong Received!</color>");
                // Joint data auto-streams from firmware every 5 ms once it enters READY state.
                // No manual request needed — the stream will begin as soon as the arm is ready.
            }
            else
            {
                Debug.LogWarning("<color=red>Connection Warning: Timed out waiting for initial Pong.</color>");
                // (Optional) You could force a disconnect here if you require a strict handshake
                // USB.USBManager.Disconnect(); 
            }
            // Start the background loop that reads packets. The returned Task is intentionally not awaited
            // so the loop runs concurrently on the thread pool / synchronization context as appropriate.

        }

        /// <summary>
        /// Called when USBManager signals a disconnection.
        /// Cancel the cancellation token to stop background tasks.
        /// </summary>
        private void HandleUSBDisconnected()
        {
            Debug.Log("PacketProcessor detected USB disconnection. Stopping loops.");
            _armMode = (int)ArmOperatingMode.Unknown;
            _cts?.Cancel();
        }

        private void OnApplicationQuit()
        {
            // Ensure loops stop when the application exits.
            ShutdownForQuit();
        }

        public void ShutdownForQuit()
        {
            _cts?.Cancel();

            if (_packetLoopTask != null)
            {
                try
                {
                    _packetLoopTask.Wait(1000);
                }
                catch (AggregateException ex)
                {
                    foreach (Exception inner in ex.Flatten().InnerExceptions)
                    {
                        if (inner is OperationCanceledException)
                        {
                            continue;
                        }

                        Debug.LogError($"PacketProcessor shutdown error: {inner}");
                    }
                }
            }
        }

        /// <summary>
        /// Wrapper to run the packet loop on a thread pool task. Running the loop with Task.Run
        /// ensures it doesn't block the Unity main thread. Exceptions are caught and logged.
        ///
        /// Task.Run schedules the provided delegate to run on the CLR thread pool.
        /// The method awaits the Task so exceptions thrown inside will be observed here.
        /// </summary>
        private async Task RunPacketLoopAsync(CancellationToken token)
        {
            try
            {
                // Run the synchronous-like packet loop on a thread pool thread.
                await Task.Run(() => PacketProcessorLoop(token));
            }
            catch (OperationCanceledException)
            {
                Debug.Log("PacketProcessor loop was cancelled.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Unexpected error in PacketProcessor loop: {ex}");
            }
        }

        /// <summary>
        /// Main packet processing loop.
        /// Uses C# 8 async enumerable support: ReadAllAsync(token) yields each packet as it becomes available.
        /// The loop will await when there are no packets (non-blocking) and resume when new items arrive.
        ///
        /// For each raw packet, construct a Packet object, validate it, and dispatch to the handler mapped in _packetHandlers.
        /// If a handler exists, it is invoked on the current thread (thread pool thread).
        /// </summary>
        private async Task PacketProcessorLoop(CancellationToken token)
        {
            while (await PacketBus.ReceiveReader.WaitToReadAsync(token))
            {
                Debug.Log("PacketProcessor loop: Detected available packet(s) to read.");
                while (PacketBus.ReceiveReader.TryRead(out var recPacket))
                {
                    if(recPacket != null) Debug.Log($"Received raw packet of length {recPacket.Length} bytes");
                    // Parse raw bytes into a Packet instance (parsing, CRC checks, header extraction, etc).
                    Packet parser = new Packet(recPacket);
                    if (parser.IsValid == false)
                    {
                        // Invalid packet (bad CRC, malformed), ignore and continue.
                        Debug.LogWarning("Received invalid packet (failed CRC or malformed). Ignoring.");
                        continue;
                    }

                    // Attempt to locate a handler in the dispatch dictionary.
                    if (_packetHandlers.TryGetValue(parser.Header.Type, out Action<Packet> handler))
                    {
                        // Invoke the handler. Because we allowed multicast delegates in Subscribe,
                        // this may call multiple registered subscribers.
                        Debug.Log("Dispatching packet to handler(s) for type: " + parser.Header.Type);
                        handler?.Invoke(parser);
                    }
                }
            }
        }

        // The following methods are simple packet handlers that currently log receipt.
        // They can be expanded to update game state, telemetry, UI, etc.

        private void HandlePongResponse(Packet packet)
        {
            _startupPongReceived = true; // Tell the startup sequence we got it!
            Debug.Log("Received Pong Response");
            // Additional processing can be done here if needed
        }

        private void HandleAckResponse(Packet packet)
        {
            Debug.Log("Received ACK Response");
            // Additional processing can be done here if needed
        }

        private void HandleNackResponse(Packet packet)
        {
            Debug.LogWarning("Received NACK Response");
            // Additional processing can be done here if needed
        }

        private void HandleJointData(Packet packet)
        {
            if (!packet.JointData.HasValue)
            {
                Debug.LogWarning($"JointData parse failed — payload is {packet.PayloadBytes?.Length} bytes, " +
                                 $"expected {Marshal.SizeOf<Payload_TelemJointData>()} bytes.");
                return;
            }

            var payload = packet.JointData.Value;

            J0 = payload.J0_AngleRaw;
            J1 = payload.J1_AngleRaw;
            J2 = payload.J2_AngleRaw;
            J3 = payload.J3_AngleRaw;
            J4 = payload.J4_AngleRaw;
            J5 = payload.J5_AngleRaw;
            _triggerPressed = payload.TriggerPressed != 0;

            HasJointData = true;

            // Throttle telemetry logs to 1 per 100 packets to avoid console spam.
            if ((++_jointTelemetryLogDivider % 100) == 0)
            {
                Debug.Log(
                    $"Joint telemetry | " +
                    $"J0(ROTATE)={J0:F3}  J1(REACH)={J1:F3}  J2(LIFT)={J2:F3}  " +
                    $"J3(PITCH)={J3:F3}  J4(YAW)={J4:F3}  J5(ROLL)={J5:F3}  " +
                    $"Trigger={TriggerPressed}");
            }
        }

        private void HandleStatus(Packet packet)
        {
            if (!packet.StatusData.HasValue) return;
            var s = packet.StatusData.Value;

            // ArmStatus byte directly mirrors the firmware armStatusCode enum (0x00–0x08).
            // Any value not defined in ArmOperatingMode falls back to Unknown (0xFF).
            bool defined = System.Enum.IsDefined(typeof(ArmOperatingMode), s.ArmStatus);
            _armMode = defined ? s.ArmStatus : (int)ArmOperatingMode.Unknown;
        }

        private void HandleLogMessage(Packet packet)
        {
            string logMsg = Encoding.ASCII.GetString(packet.PayloadBytes);
            Debug.Log($"[Firmware] {logMsg}");

            // Belt-and-suspenders mode tracking: parse well-known firmware log strings
            // to catch transitions that arrive before the next TELEM_STATUS packet.
            // Uses updated enum names that match firmware armStatusCode values.
            if (logMsg.Contains("[ASSIST] suspending admittance") ||
                logMsg.Contains("[HOMING] autonomous return-home started"))
            {
                _armMode = (int)ArmOperatingMode.AutoReturnHome;
            }
            else if (logMsg.Contains("[HOMING] autonomous return-home complete") ||
                     logMsg.Contains("[ASSIST] admittance restored"))
            {
                _armMode = (int)ArmOperatingMode.AssistActive;
            }
        }

        private void HandleErrorMessage(Packet packet)
        {
            string errorMsg = Encoding.ASCII.GetString(packet.PayloadBytes);
            Debug.LogError($"[Firmware Error] {errorMsg}");
        }

        // The following methods construct command packets and write them to PacketBus.SendWriter.
        // These are non-blocking uses of TryWrite; if the channel is full, TryWrite returns false and the packet is dropped.
        // If stronger delivery guarantees are required, consider using WriteAsync which can await until space is available.

        public bool SendPing()
        {
            if (!USB.USBManager.IsConnected)
            {
                Debug.LogWarning("Ignored Ping command: USB is disconnected.");
                return false; // Exit early when hardware is not available.
            }
            PacketType Type = PacketType.CMD_PING;
            byte[] Payload = new byte[0];
            Packet packet = new Packet(Type, Payload);

            // TryWrite places the packet into the bounded channel immediately if there is capacity.
            bool queued = PacketBus.SendWriter.TryWrite(packet.ToBytes());
            if (!queued)
            {
                Debug.LogWarning("Failed to enqueue Ping packet: send channel is full.");
                return false;
            }
            Debug.Log("<color=blue>Sent Ping Packet</color>");
            return true;
        }
        
        public void SendReset()
        {
            if (!USB.USBManager.IsConnected)
            {
                Debug.LogWarning("Ignored Reset command: USB is disconnected.");
                return;
            }
            PacketType Type = PacketType.CMD_RESET_DEVICE;
            byte[] Payload = new byte[0];
            Packet packet = new Packet(Type, Payload);
            PacketBus.SendWriter.TryWrite(packet.ToBytes());
            Debug.Log("<color=blue>Sent Reset Packet</color>");
        }

        public void SendRequestTelem()
        {
            if (!USB.USBManager.IsConnected)
            {
                Debug.LogWarning("Ignored Request Telemetry command: USB is disconnected.");
                return;
            }
            PacketType Type = PacketType.CMD_REQUEST_TELEM;
            byte[] Payload = new byte[0];
            Packet packet = new Packet(Type, Payload);
            PacketBus.SendWriter.TryWrite(packet.ToBytes());
            Debug.Log("<color=blue>Sent Request Telemetry Packet</color>");
        }

        public void SendStartHoming()
        {
            if (!USB.USBManager.IsConnected)
            {
                Debug.LogWarning("Ignored Start Homing command: USB is disconnected.");
                return;
            }
            if (ArmMode == ArmOperatingMode.AutoReturnHome ||
                ArmMode == ArmOperatingMode.ManualHomeAssist)
            {
                Debug.LogWarning("Ignored duplicate CMD_START_HOMING: arm is already homing.");
                return;
            }
            // Set mode optimistically so a second call before firmware responds is also blocked.
            _armMode = (int)ArmOperatingMode.AutoReturnHome;

            PacketType Type = PacketType.CMD_START_HOMING;
            byte[] Payload = new byte[0];
            Packet packet = new Packet(Type, Payload);
            PacketBus.SendWriter.TryWrite(packet.ToBytes());
            Debug.Log("<color=blue>Sent Start Homing Packet</color>");
        }

        public void SendConfirmHome()
        {
            if (!USB.USBManager.IsConnected)
            {
                Debug.LogWarning("Ignored Confirm Home command: USB is disconnected.");
                return;
            }
            PacketType Type = PacketType.CMD_CONFIRM_HOME;
            byte[] Payload = new byte[0];
            Packet packet = new Packet(Type, Payload);
            PacketBus.SendWriter.TryWrite(packet.ToBytes());
            Debug.Log("<color=blue>Sent Confirm Home Packet</color>");
        }

        public void SendEStop()
        {
            if (!USB.USBManager.IsConnected)
            {
                Debug.LogWarning("Ignored Emergency Stop command: USB is disconnected.");
                return;
            }
            PacketType Type = PacketType.CMD_ESTOP;
            byte[] Payload = new byte[0];
            Packet packet = new Packet(Type, Payload);
            PacketBus.SendWriter.TryWrite(packet.ToBytes());
            Debug.Log("<color=blue>Sent E-Stop Packet</color>");
        }

        public void SendSetODState(Payload_SetODriveState States)
        {
            if (!USB.USBManager.IsConnected)
            {
                Debug.LogWarning("Ignored Set ODrive State command: USB is disconnected.");
                return;
            }
            PacketType Type = PacketType.CMD_SET_ODRIVE_STATE;
            byte[] Payload = Packet.StructToBytes(States); // Helper that marshals struct into byte[].
            Packet packet = new Packet(Type, Payload);
            PacketBus.SendWriter.TryWrite(packet.ToBytes());
            Debug.Log("<color=blue>Sent Set ODrive State Packet</color>");
        }

        public void SendSetJointTargets(Payload_SetJointTargets Targets)
        {
            if (!USB.USBManager.IsConnected)
            {
                Debug.LogWarning("Ignored Set Joint Targets command: USB is disconnected.");
                return;
            }
            PacketType Type = PacketType.CMD_SET_JOINT_TARGETS;
            byte[] Payload = Packet.StructToBytes(Targets);
            Packet packet = new Packet(Type, Payload);
            PacketBus.SendWriter.TryWrite(packet.ToBytes());
            Debug.Log("<color=blue>Sent Set Joint Targets Packet</color>");
        }

        public void SendSetJointParams(Payload_SetJointParameter Params)
        {
            if (!USB.USBManager.IsConnected)
            {
                Debug.LogWarning("Ignored Set Joint Parameter command: USB is disconnected.");
                return;
            }
            PacketType Type = PacketType.CMD_SET_JOINT_PARAMETER;
            byte[] Payload = Packet.StructToBytes(Params);
            Packet packet = new Packet(Type, Payload);
            PacketBus.SendWriter.TryWrite(packet.ToBytes());
            Debug.Log("<color=blue>Sent Set Joint Params Packet<color>");
        }
    }
}
