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
            int incomingReadLeft = length;
            int incomingReadPos = 0;

            while (incomingReadLeft > 0)
            {
                //Syncronise the stream by finding a 0x2040 header
                while (!syncronised && incomingReadLeft > 0)
                {
                    processMessage[processMessagePos] = bytes[incomingReadPos];
                    incomingReadPos++;
                    incomingReadLeft--;
                    if (processMessagePos == 0 && processMessage[0] == 0x20)
                    {
                        processMessagePos = 1;
                        continue;
                    }
                    if (processMessagePos == 1 && processMessage[1] == 0x40)
                    {
                        processMessagePos = 2;
                        syncronised = true;
                    }
                }

                //We can't continue unless syncronised
                if (!syncronised)
                {
                    return;
                }

                //Read size
                if (processMessagePos == 0)
                {
                    processMessage[processMessagePos] = bytes[incomingReadPos];
                    incomingReadPos++;
                    incomingReadLeft--;
                    processMessagePos = 1;
                    //All messages must be at least 4 bytes, 1 length, 1 messagetype/sensorID, 2 checksum.
                    if (processMessage[0] < 4)
                    {
                        processMessagePos = 0;
                        syncronised = false;
                        continue;
                    }
                    //Channel messages are the biggest message at 32 bytes each, it's safe to assume the stream has desyncronised here.
                    if (processMessage[0] > 32)
                    {
                        processMessagePos = 0;
                        syncronised = false;
                        continue;
                    }
                }

                //Read message
                if (incomingReadLeft > 0)
                {
                    int bytesToRead = processMessage[0] - processMessagePos;
                    if (bytesToRead > incomingReadLeft)
                    {
                        bytesToRead = incomingReadLeft;
                    }
                    Array.Copy(bytes, incomingReadPos, processMessage, processMessagePos, bytesToRead);
                    processMessagePos += bytesToRead;
                    incomingReadPos += bytesToRead;
                    incomingReadLeft -= bytesToRead;
                }

                //Message not yet fully received, wait.
                if (processMessagePos != processMessage[0])
                {
                    return;
                }

                //Check the message checksum
                if (!Checksum(processMessage[0] - 2))
                {
                    processMessagePos = 0;
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