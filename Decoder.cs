using System;
using System.Collections.Generic;

namespace Ibus
{
    public class Decoder
    {
        private RingBuffer incomingBuffer = new RingBuffer();
        private bool syncronised = false;
        private byte[] sendBuffer = new byte[32];
        private byte[] processMessage = new byte[32];
        private int processMessagePos = 0;
        private int processMessageSize = 0;
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
                //Syncronise the stream by finding a 0x2040 header
                while (!syncronised && incomingBuffer.Available > 2)
                {
                    if (incomingBuffer.ReadByte() == 0x20 && incomingBuffer.ReadByte() == 0x40)
                    {
                        syncronised = true;
                        processMessage[0] = 0x20;
                        processMessage[1] = 0x40;
                        processMessagePos = 2;
                        processMessageSize = processMessage[0];
                    }
                }

                //We have syncronised, now we need to wait until we have enough buffer to read the 2040 message
                if (!syncronised)
                {
                    return;
                }

                //Read size
                if (processMessagePos == 0 && incomingBuffer.Available > 0)
                {
                    incomingBuffer.Read(processMessage, processMessagePos, 1);
                    processMessagePos += 1;
                    processMessageSize = processMessage[0];
                    //Maximum protocol length is 32 bytes, anything else must be a bit slip.
                    if (processMessageSize > 32)
                    {
                        syncronised = false;
                        continue;
                    }
                }

                //Read message
                if (incomingBuffer.Available >= processMessageSize)
                {
                    int bytesToRead = processMessageSize - processMessagePos;
                    incomingBuffer.Read(processMessage, processMessagePos, bytesToRead);
                    processMessagePos += bytesToRead;
                }
                else
                {
                    return;
                }

                //Check the message checksum
                if (!Checksum(processMessage[0] - 2))
                {
                    syncronised = false;
                    continue;
                }

                int messageType = processMessage[1] & 0xF0;
                int sensorID = processMessage[1] & 0x0F;
                bool handled = false;

                //Channel message
                if (messageType == 0x40)
                {
                    handled = true;
                    Message m = new Message();
                    for (int i = 0; i < 14; i++)
                    {
                        m.channelsRaw[i] = BitConverter.ToUInt16(processMessage, 2 + (i * 2));
                        m.channels[i] = -1f + (m.channelsRaw[i] - 500) / 1000f;
                    }
                    messageEvent(m);
                }

                //Sensor discover? I have no idea. I *think* we are supposed to reply 0x90 sensor description messages.
                if (messageType == 0x80)
                {
                    handled = true;
                    Console.WriteLine($"TODO: {messageType.ToString("X2")}");
                }

                //Sensor description message. We send these I think, don't receive.
                if (messageType == 0x90)
                {
                    handled = true;
                    Console.WriteLine($"TODO: {messageType.ToString("X2")}");
                }

                //Sensor data request
                if (messageType == 0xA0)
                {
                    handled = true;
                    //This needs reworking to send both 2 byte and 4 byte sensor data
                    sendBuffer[0] = 0x6;
                    sendBuffer[1] = processMessage[1];
                    //This event needs to be changed to some other class where you can configure sensors
                    ushort sensorData = sensorEvent(sensorID);
                    BitConverter.GetBytes(sensorData).CopyTo(sendBuffer, 2);
                    SetSendChecksum(4);
                    io.Write(sendBuffer, 6);
                }

                //I really don't know what these are
                if (messageType == 0xF0)
                {
                    handled = true;
                    Console.WriteLine($"TODO: {messageType.ToString("X2")}");
                }


                if (!handled)
                {
                    Console.WriteLine($"Uknown message type {messageType.ToString("X2")}");
                    syncronised = false;
                }

                processMessagePos = 0;
                processMessageSize = 0;
            }
        }

        private bool Checksum(int positionOfChecksum)
        {
            ushort compute = 0xFFFF;
            for (int i = 0; i < positionOfChecksum; i++)
            {
                compute -= processMessage[i];
            }
            ushort messageChecksum = BitConverter.ToUInt16(processMessage, positionOfChecksum);
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