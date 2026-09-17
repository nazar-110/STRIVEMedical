using System;
using System.IO.Ports;
using Microsoft.Win32;
using UnityEngine;

namespace USB
{
    /// <summary>
    /// Locates the Teensy 4.1 COM port using the Windows registry (VID/PID lookup),
    /// with a ranked fallback so the most likely port is tried before brute-forcing.
    ///
    /// Detection order:
    ///   1. Registry — looks for any device with a PJRC VID (16C0) under
    ///      HKLM\SYSTEM\CurrentControlSet\Enum\USB and reads its PortName value.
    ///      This is exact and works even when many COM ports are present.
    ///   2. Port-name heuristic — prefers higher COM numbers (virtual / USB-serial
    ///      devices tend to get higher numbers than hardware UARTs / Bluetooth).
    ///   3. First port — original fallback so nothing regresses.
    /// </summary>
    public class USBDetector
    {
        // PJRC (Teensy manufacturer) USB Vendor ID.
        // All Teensy models share this VID; the PID varies by product/mode but the VID alone
        // is sufficient to distinguish the device from Bluetooth / RS-232 / other COM ports.
        private const string TeensyVendorId = "VID_16C0";

        public string portName  = "";
        public string hardwareId = "";

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the COM port most likely belonging to the Teensy.
        /// Logs a clear message for whichever detection method succeeded.
        /// </summary>
        public (string port, string hwid) FindTeensyComPort(string serialNum = null)
        {
            // 1. Registry lookup — most reliable on Windows.
            string registryPort = FindPortInRegistry(TeensyVendorId);
            if (!string.IsNullOrEmpty(registryPort))
            {
                Debug.Log($"USBDetector: Teensy found via registry → {registryPort}");
                return (registryPort, TeensyVendorId);
            }

            // 2. Fallback: get all ports and pick the best candidate.
            string[] ports = SerialPort.GetPortNames();
            if (ports == null || ports.Length == 0)
            {
                Debug.LogWarning("USBDetector: No COM ports found.");
                return (null, null);
            }

            if (ports.Length == 1)
            {
                Debug.Log($"USBDetector: Only one COM port available → {ports[0]}");
                return (ports[0], null);
            }

            // Multiple ports: prefer higher COM numbers (USB-serial devices typically get
            // higher numbers than built-in hardware UARTs or Bluetooth virtual ports).
            string bestPort = PickHighestNumberedPort(ports);
            Debug.LogWarning(
                $"USBDetector: Registry lookup found no Teensy. " +
                $"Multiple ports present ({string.Join(", ", ports)}). " +
                $"Trying highest-numbered port {bestPort}. " +
                $"If this is wrong, set the port manually in the USBManager Inspector.");
            return (bestPort, null);
        }

        /// <summary>Legacy method kept for backwards compatibility.</summary>
        public void ReadCOMs()
        {
            (string port, string hwid) = FindTeensyComPort();
            if (!string.IsNullOrEmpty(port))
            {
                portName    = port;
                hardwareId  = hwid ?? "";
                Debug.Log($"USBDetector: Using port {portName}");
            }
            else
            {
                Debug.LogWarning("USBDetector: No usable COM port found.");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Scans HKLM\SYSTEM\CurrentControlSet\Enum\USB for any sub-key whose name
        /// contains <paramref name="vendorId"/> and returns the first PortName value found.
        /// Returns null if the registry is unavailable or no matching device is found.
        /// </summary>
        private static string FindPortInRegistry(string vendorId)
        {
            try
            {
                using RegistryKey usbKey =
                    Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB");

                if (usbKey == null) return null;

                foreach (string deviceKeyName in usbKey.GetSubKeyNames())
                {
                    // Match on VID, case-insensitive.
                    if (deviceKeyName.IndexOf(vendorId, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    using RegistryKey deviceKey = usbKey.OpenSubKey(deviceKeyName);
                    if (deviceKey == null) continue;

                    foreach (string instanceName in deviceKey.GetSubKeyNames())
                    {
                        // Each instance may have a "Device Parameters" sub-key with PortName.
                        using RegistryKey paramKey =
                            deviceKey.OpenSubKey($@"{instanceName}\Device Parameters");

                        string portName = paramKey?.GetValue("PortName") as string;
                        if (!string.IsNullOrWhiteSpace(portName))
                            return portName;
                    }
                }
            }
            catch (Exception ex)
            {
                // Registry access may be restricted in some environments — log and continue.
                Debug.LogWarning($"USBDetector: Registry query failed ({ex.Message}). Falling back to port heuristic.");
            }

            return null;
        }

        /// <summary>
        /// Among an array of port names ("COM4", "COM16", …) returns the one
        /// with the highest numeric suffix. Ties go to the last entry in the array.
        /// </summary>
        private static string PickHighestNumberedPort(string[] ports)
        {
            string best = ports[0];
            int    bestNum = ParsePortNumber(ports[0]);

            for (int i = 1; i < ports.Length; i++)
            {
                int n = ParsePortNumber(ports[i]);
                if (n >= bestNum)
                {
                    bestNum = n;
                    best    = ports[i];
                }
            }
            return best;
        }

        private static int ParsePortNumber(string portName)
        {
            // "COM4" → 4, "COM16" → 16, anything else → 0
            if (portName != null &&
                portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(portName.Substring(3), out int n))
                return n;
            return 0;
        }
    }
}
