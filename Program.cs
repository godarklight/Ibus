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
        private static long startupTime = DateTime.UtcNow.Ticks;
        private static IOInterface io;
        private static byte[] sendBuffer = new byte[64];
        public static void Main(string[] args)
        {
            /*
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
            */

            //Set up sensors
            Sensor[] sensors = new Sensor[15];
            sensors[1] = new Sensor(SensorType.CELL, () => { return 3600; });
            sensors[2] = new Sensor(SensorType.ALT, GetAltitude);

            //Swap to switch to serial
            //io = new SerialIO(serialPortName);
            //io = new FileIO();
            io = new TCPIO(5867);
            Decoder decoder = new Decoder(MessageEvent, sensors, io);

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
                    Thread.Sleep(1);
                }
            }
        }

        
        private static int GetAltitude()
        {
            long currentTime = DateTime.UtcNow.Ticks;
            return (int)((currentTime - startupTime) / TimeSpan.TicksPerSecond);
        }

        private static void MessageEvent(Message m)
        {
            //Console.WriteLine($"message {m.channels[0]}");
        }
    }
}
