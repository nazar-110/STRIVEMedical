using PacketProcessor;
using UnityEngine;

namespace StriveMedical.UI
{
    /// <summary>
    /// Attach this to a GameObject and wire QuitApplication() to a UI Button OnClick event.
    /// It shuts down known background workers before exiting Play Mode / the application.
    /// </summary>
    public class QuitHandler : MonoBehaviour
    {
        private bool _isQuitting;
        private static bool _isQuitRequested;

        public void QuitApplication()
        {
            if (_isQuitting)
            {
                return;
            }

            _isQuitting = true;
            RequestQuit();
        }

        public static void RequestQuit()
        {
            if (_isQuitRequested)
            {
                return;
            }

            _isQuitRequested = true;
            Debug.Log("QuitHandler: Stopping background threads before quitting.");

            ShutdownPacketProcessors();
            ShutdownUSBManagers();
            ShutdownTeensyReaders();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void ShutdownPacketProcessors()
        {
            foreach (PacketProcessor.PacketProcessor processor in FindObjectsOfType<PacketProcessor.PacketProcessor>())
            {
                processor.ShutdownForQuit();
            }
        }

        private static void ShutdownUSBManagers()
        {
            foreach (USB.USBManager manager in FindObjectsOfType<USB.USBManager>())
            {
                manager.ShutdownForQuit();
            }
        }

        private static void ShutdownTeensyReaders()
        {
            foreach (TeensyReceiver reader in FindObjectsOfType<TeensyReceiver>())
            {
                reader.ShutdownForQuit();
            }
        }
    }
}
