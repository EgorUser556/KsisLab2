using System.Net;
using System.Net.Sockets;

namespace lab2
{
    public class DnsService
    {
        public string GetNameByAddress(IPAddress ipAddress, int timeoutMilliseconds)
        {
            try
            {
                var dnsTask = Task.Run(() => Dns.GetHostEntry(ipAddress));

                if (!dnsTask.Wait(timeoutMilliseconds))
                    return ipAddress.ToString();

                return dnsTask.Result.HostName;
            }
            catch
            {
                return ipAddress.ToString();
            }
        }

        public IPAddress GetAddressByName(string hostOrIp)
        {
            if (IPAddress.TryParse(hostOrIp, out IPAddress? parsedIp))
                return parsedIp;

            var hostEntry = Dns.GetHostEntry(hostOrIp);

            foreach (var ip in hostEntry.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip;
            }

            Console.WriteLine($"Не удалось получить IPv4 адрес для узла '{hostOrIp}'");
            throw new Exception();
        }
    }
}
