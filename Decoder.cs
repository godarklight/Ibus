using System;
using System.Collections.Generic;

namespace Ibus
{
    public class Decoder
    {
        private RingBuffer incomingBuffer = new RingBuffer();
        private bool syncronised = false;
        private byte[] processBuffer = new byte[32];
        private byte[] sendBuffer = new byte[32];
        private int processBufferPos = 0;
        private Action<Message> messageEvent;
        private Func<int, ushort> sensorEvent;
        private IOInterface io;

        public Decoder(Action<Message> messageEvent, Func<int, ushort> sensorEvent, IOInterface io)
        {
            this.messageEvent = messageEvent;
            this.sensorEvent = sensorEvent;
            this.io = io;
        }

        public void Decode(byte[] bytes, int length)
        {
            incomingBuffer.Write(bytes, 0, length);

            while (incomingBuffer.Available > 1)
            {
                //Find header
                while (!syncronised && incomingBuffer.Available > 2)
                {
                    if (incomingBuffer.ReadByte() == 0x20 && incomingBuffer.ReadByte() == 0x40)
                    {
                        syncronised = true;
                        processBuffer[0] = 0x20;
                        processBuffer[1] = 0x40;
                        processBufferPos = 2;
                    }
                }

                //We have syncronised, now we need to wait until we have enough buffer to read the 2040 message
                if (!syncronised)
                {
                    return;
                }

                //We need to read the header
                if (processBufferPos == 0)
                {
                    if (!FillProcessBufferTo(2))
                    {
                        return;
                    }
                }

                bool headerOk = false;
                //Channel message
                if (processBuffer[0] == 0x20 && processBuffer[1] == 0x40)
                {
                    headerOk = true;
                    if (!FillProcessBufferTo(32))
                    {
                        return;
                    }
                    if (Checksum(30))
                    {
                        Message m = new Message();
                        for (int i = 0; i < 14; i++)
                        {
                            m.channelsRaw[i] = BitConverter.ToUInt16(processBuffer, 2 + (i * 2));
                            m.channels[i] = -1f + (m.channelsRaw[i] - 500) / 1000f;
                        }
                        messageEvent(m);
                        processBufferPos = 0;
                        continue;
                    }
                }

                //Sensor discover
                if (processBuffer[0] == 4 && (processBuffer[1] & 0xF0) == 0x80)
                {
                    headerOk = true;
                    if (!FillProcessBufferTo(4))
                    {
                        return;
                    }
                    if (Checksum(2))
                    {
                        int sensorID = processBuffer[1] & 0b00001111;
                        if (sensorID < Program.SENSOR_DEVICES)
                        {
                            sendBuffer[0] = 0x4;
                            sendBuffer[1] = processBuffer[1];
                            SetSendChecksum(2);
                            io.Write(sendBuffer, 4);
                        }
                        processBufferPos = 0;
                        continue;
                    }
                }

                //Sensor description message
                if (processBuffer[0] == 0x4 && (processBuffer[1] & 0xF0) == 0x90)
                {
                    headerOk = true;
                    if (!FillProcessBufferTo(4))
                    {
                        return;
                    }
                    if (Checksum(2))
                    {
                        int sensorID = processBuffer[1] & 0b00001111;
                        int returnValue = sensorEvent(sensorID);
                        sendBuffer[0] = 0x6;
                        sendBuffer[1] = processBuffer[1];
                        //Type
                        sendBuffer[2] = 0;
                        //Size
                        sendBuffer[3] = 2;
                        SetSendChecksum(4);
                        io.Write(sendBuffer, 6);
                        processBufferPos = 0;
                        continue;
                    }
                }

                //Sensor data message
                if (processBuffer[0] == 0x4 && (processBuffer[1] & 0xF0) == 0xA0)
                {
                    headerOk = true;
                    if (!FillProcessBufferTo(4))
                    {
                        return;
                    }
                    if (Checksum(2))
                    {
                        int sensorID = processBuffer[1] & 0b00001111;
                        sendBuffer[0] = 0x6;
                        sendBuffer[1] = processBuffer[1];
                        ushort sensorData = sensorEvent(sensorID);
                        BitConverter.GetBytes(sensorData).CopyTo(sendBuffer, 2);
                        SetSendChecksum(4);
                        io.Write(sendBuffer, 6);
                        processBufferPos = 0;
                        continue;
                    }
                }

                if (!headerOk)
                {
                    Console.WriteLine($"Uknown header {processBuffer[0]},{processBuffer[1]}");
                    syncronised = false;
                }
            }
        }

        private bool FillProcessBufferTo(int position)
        {
            int bufferToRead = position - processBufferPos;
            if (bufferToRead > incomingBuffer.Available)
            {
                bufferToRead = incomingBuffer.Available;
            }
            if (bufferToRead == 0)
            {
                return false;
            }
            incomingBuffer.Read(processBuffer, processBufferPos, bufferToRead);
            processBufferPos += bufferToRead;
            return position == processBufferPos;
        }

        private bool Checksum(int positionOfChecksum)
        {
            ushort compute = 0xFFFF;
            for (int i = 0; i < positionOfChecksum; i++)
            {
                compute -= processBuffer[i];
            }
            ushort messageChecksum = BitConverter.ToUInt16(processBuffer, positionOfChecksum);
            if (compute != messageChecksum)
            {
                syncronised = false;
            }
            return compute == messageChecksum;
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