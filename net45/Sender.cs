using System;

namespace Ibus
{
    public class Sender
    {
        byte[] sendBuffer = new byte[32];
        IOInterface io;

        public Sender(IOInterface io)
        {
            this.io = io;
        }

        public void SendDiscover(int sensorID)
        {
            Console.WriteLine($"TX DISCOVER {sensorID}");
            sendBuffer[0] = 4;
            sendBuffer[1] = (byte)(0x80 | sensorID);
            SetSendChecksum(2);
            io.Write(sendBuffer, 4);
        }

        public void SendSensorDescribe(int sensorID, Sensor sensor)
        {
            Console.WriteLine($"TX DESCRIBE {sensorID}");
            sendBuffer[0] = 6;
            sendBuffer[1] = (byte)(0x90 | sensorID);
            sendBuffer[2] = (byte)sensor.type;
            sendBuffer[3] = (byte)sensor.length;
            SetSendChecksum(4);
            io.Write(sendBuffer, 6);
        }

        public void SendSensorData(int sensorID, Sensor sensor)
        {
            Console.WriteLine($"TX DATA {sensorID}");
            int length = sensor.WriteValue(sensorID, sendBuffer);
            SetSendChecksum(2 + length);
            io.Write(sendBuffer, 4 + length);
        }


        private void SetSendChecksum(int positionOfChecksum)
        {
            ushort compute = 0xFFFF;
            for (int i = 0; i < positionOfChecksum; i++)
            {
                compute -= sendBuffer[i];
            }
            BitConverter.GetBytes(compute).CopyTo(sendBuffer, positionOfChecksum);
        }
    }
}