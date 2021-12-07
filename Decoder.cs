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
        private Sensor[] sensors;
        private IOInterface io;
        private long ignoreSensorUntil;

        public Decoder(Action<Message> messageEvent, Sensor[] sensors, IOInterface io)
        {
            this.messageEvent = messageEvent;
            this.sensors = sensors;
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
                    if (processMessageSize < 4)
                    {
                        syncronised = false;
                        continue;
                    }
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

                //Sensor discover
                if (messageType == 0x80)
                {
                    handled = true;
                    long currentTime = DateTime.UtcNow.Ticks;
                    if (currentTime > ignoreSensorUntil)
                    {
                        //Because these get echoed back onto the serial line we will see our own message and go into an infinite loop. Frames are 7ms to 5ms seems safe.
                        ignoreSensorUntil = currentTime + 5 * TimeSpan.TicksPerMillisecond;
                        //Echo message if we have the sensor
                        if (sensors[sensorID] != null)
                        {
                            io.Write(processMessage, 4);
                        }
                    }
                }

                //Sensor description message
                if (messageType == 0x90)
                {
                    handled = true;
                    //If it's length 4 we know the other side has requested sensor info. Anything bigger is our response
                    if (processMessageSize > 4 && sensors[sensorID] != null)
                    {
                        Sensor s = sensors[sensorID];
                        sendBuffer[0] = 6;
                        sendBuffer[1] = (byte)(0x90 | sensorID);
                        sendBuffer[2] = (byte)s.type;
                        sendBuffer[3] = (byte)s.length;
                        SetSendChecksum(4);
                        io.Write(sendBuffer, 6);
                    }
                }

                //Sensor data request
                if (messageType == 0xA0)
                {
                    handled = true;
                    //If it's length 4 we know the other side has requested sensor data. Anything bigger is our response
                    if (processMessageSize > 4 && sensors[sensorID] != null)
                    {
                        Sensor s = sensors[sensorID];
                        s.WriteValue(sensorID, sendBuffer);
                        SetSendChecksum(2 + s.length);
                        io.Write(sendBuffer, 4 + s.length);
                    }
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