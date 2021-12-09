#pragma warning disable CA1416
//Serial warnings for ios and android

using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;

namespace Ibus
{
    public class SerialIO : IOInterface
    {
        SerialPort sp;
        FileStream fs;
        UdpClient udp;
        IPEndPoint endpoint;

        public SerialIO(string serialPortName)
        {
            sp = new SerialPort(serialPortName, 115200, Parity.None, 8, StopBits.One);
            sp.Open();
            File.Delete("debug.txt");
            fs = new FileStream("debug.txt", FileMode.Create);
            udp = new UdpClient(AddressFamily.InterNetworkV6);
            IPAddress[] addrs = Dns.GetHostAddresses("chrislinux.godarklight.privatedns.org");
            foreach (IPAddress addr in addrs)
            {
                if (addr.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    endpoint = new IPEndPoint(addr, 5687);
                }
            }
        }

        public int Available()
        {
            return sp.BytesToRead;
        }

        public void Read(byte[] buffer, int length)
        {
            sp.Read(buffer, 0, length);
            fs.Write(buffer, 0, length);
            fs.Flush();
            udp.Send(buffer, length, endpoint);
        }

        public void Write(byte[] buffer, int length)
        {
            sp.Write(buffer, 0, length);
        }
    }
}
