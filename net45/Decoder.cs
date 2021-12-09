/*
Message frame is:
Length (1 byte) | MessageType(0xF0) & SensorID(0x0F) (1 byte) | Data(variable length) | Checksum(2)
The length field includes the length byte and the checksum bytes
Checksum is computed 0xFFFF - each byte before the checksum.
*/


using System;
using System.Collections.Generic;

namespace Ibus
{
    public class Decoder
    {
        private RingBuffer incomingBuffer = new RingBuffer();
        private bool syncronised = false;
        private byte[] processMessage = new byte[32];
        private int processMessagePos = 0;
        private Handler handler;

        public Decoder(Handler handler)
        {
            this.handler = handler;
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
                    }
                }

                //We can't continue unless syncronised, wait for more buffer. Need at least 1 byte to read the size or part of a message.
                if (!syncronised || incomingBuffer.Available == 0)
                {
                    return;
                }

                //Read size
                if (processMessagePos == 0)
                {
                    incomingBuffer.Read(processMessage, processMessagePos, 1);
                    processMessagePos += 1;
                    //All messages must be at least 4 bytes, 1 length, 1 messagetype/sensorID, 2 checksum.
                    if (processMessage[0] < 4)
                    {
                        syncronised = false;
                        continue;
                    }
                    //Channel messages are the biggest message at 32 bytes each, it's safe to assume the stream has desyncronised here.
                    if (processMessage[0] > 32)
                    {
                        syncronised = false;
                        continue;
                    }
                }

                //Read message
                if (incomingBuffer.Available > 0)
                {
                    int bytesToRead = processMessage[0] - processMessagePos;
                    if (bytesToRead > incomingBuffer.Available)
                    {
                        bytesToRead = incomingBuffer.Available;
                    }
                    incomingBuffer.Read(processMessage, processMessagePos, bytesToRead);
                    processMessagePos += bytesToRead;
                }

                //Message not yet fully received, wait.
                if (processMessagePos != processMessage[0])
                {
                    return;
                }

                //Check the message checksum
                if (!Checksum(processMessage[0] - 2))
                {
                    syncronised = false;
                    continue;
                }

                handler.HandleMessage(processMessage);
                processMessagePos = 0;
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
    }
}