using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.Gripper
{
    public class Kinematics_Testbench
    {
        public void Write_rand_Packet()
        {
            byte[] telemdata = new byte[60];

            telemdata[0] = 0x84;
            telemdata[1] = 0x38;
            Array.Copy(BitConverter.GetBytes(UnityEngine.Random.Range(-180.0f, 180.0f)), 0, telemdata, 2, 4); // Example float value for joint 1
            Array.Copy(BitConverter.GetBytes(0f), 0, telemdata, 6, 4); // Example float value for Velocity (not used in this test)
            Array.Copy(BitConverter.GetBytes(UnityEngine.Random.Range(-180.0f, 180.0f)), 0, telemdata, 10, 4); // Example float value for joint 2
            Array.Copy(BitConverter.GetBytes(0f), 0, telemdata, 14, 4); // Example float value for Velocity (not used in this test)
            Array.Copy(BitConverter.GetBytes(UnityEngine.Random.Range(-180.0f, 180.0f)), 0, telemdata, 18, 4); // Example float value for joint 3
            Array.Copy(BitConverter.GetBytes(0f), 0, telemdata, 22, 4); // Example float value for Velocity (not used in this test)
            Array.Copy(BitConverter.GetBytes(UnityEngine.Random.Range(-180.0f, 180.0f)), 0, telemdata, 26, 4); // Example float value for joint 4
            Array.Copy(BitConverter.GetBytes(0f), 0, telemdata, 30, 4); // Example float value for Velocity (not used in this test)
            Array.Copy(BitConverter.GetBytes(UnityEngine.Random.Range(-180.0f, 180.0f)), 0, telemdata, 34, 4); // Example float value for joint 5
            Array.Copy(BitConverter.GetBytes(0f), 0, telemdata, 38, 4); // Example float value for Velocity (not used in this test)
            Array.Copy(BitConverter.GetBytes(UnityEngine.Random.Range(-180.0f, 180.0f)), 0, telemdata, 42, 4); // Example float value for joint 6
            Array.Copy(BitConverter.GetBytes(0f), 0, telemdata, 46, 4); // Example float value for Velocity (not used in this test)
            Array.Copy(BitConverter.GetBytes(UnityEngine.Random.Range(-180.0f, 180.0f)), 0, telemdata, 50, 4); // Example float value for joint 7
            Array.Copy(BitConverter.GetBytes(0f), 0, telemdata, 54, 4); // Example float value for Velocity (not used in this test)

            ushort crc = 0;
            for (int i = 0; i < 58; i++)
            {
                crc = UpdateCRC(crc, telemdata[i]);
            }

            telemdata[58] = (byte)(crc & 0xFF); // CRC low byte
            telemdata[59] = (byte)(crc >> 8); // CRC high byte

            Debug.Log($"Generated telemetry packet: {BitConverter.ToString(telemdata)}");

            bool success = PacketBus.ReceiveWriter.TryWrite(telemdata);
            if (!success)
            {
                Debug.LogWarning("Failed to write telemetry packet to PacketBus.");
            }
        }

        private static ushort UpdateCRC(ushort crc, byte data)
        {
            crc ^= data;
            for (int i = 0; i < 8; i++)
            {
                if ((crc & 0x0001) != 0)
                    crc = (ushort)(crc >> 1 ^ 0xA001);
                else
                    crc = (ushort)(crc >> 1);
            }
            return crc;
        }
    }
}
