#pragma warning disable CA1416
//Serial warnings for ios and android

using System;
using System.IO;
using System.IO.Ports;
using System.Threading;

namespace Ibus
{
    class Program
    {
        public const int SENSOR_DEVICES = 5;
        private static IOInterface io;
        private static byte[] sendBuffer = new byte[64];
        public static void Main(string[] args)
        {
            string[] serialPorts = SerialPort.GetPortNames();
            if (serialPorts.Length > 1 && args.Length != 1)
            {
                Console.WriteLine("Please specify which serial port to use with the program arguments:");
                foreach (string validPort in serialPorts)
                {
                    Console.WriteLine(validPort);
                }
                return;
            }
            string serialPortName;
            if (serialPorts.Length > 0)
            {
                serialPortName = serialPorts[0];
            }
            if (args.Length > 0)
            {
                serialPortName = args[0];
            }

            //Swap to switch to serial
            //io = new SerialIO(serialPortName);
            io = new FileIO();
            Decoder decoder = new Decoder(MessageEvent, SensorEvent, io);

            bool running = true;
            byte[] buffer = new byte[64];
            while (running)
            {
                int bytesAvailable = io.Available();
                if (bytesAvailable > 0)
                {
                    int bytesRead = bytesAvailable;
                    if (bytesRead > buffer.Length)
                    {
                        bytesRead = buffer.Length;
                    }
                    io.Read(buffer, bytesRead);
                    decoder.Decode(buffer, bytesRead);
                }
                else
                {
                    //Debugging
                    if (io is FileIO)
                    {
                        running = false;
                    }
                }
                if (bytesAvailable < buffer.Length)
                {
                    Thread.Sleep(1000);
                }
            }
        }

        //TODO: Fill in stuff here
        private static ushort SensorEvent(int id)
        {
            switch (id)
            {
                case 0:
                    return 100;
                case 1:
                    return 200;
                case 3:
                    return 300;
            }
            return 0;
        }

        private static void MessageEvent(Message m)
        {
            Console.WriteLine($"message {m.channels[0]}");
        }
    }
}
