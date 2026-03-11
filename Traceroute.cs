using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

namespace lab2
{
    public class Traceroute
    {
        private readonly bool _resolveNames;
        private readonly IPAddress _targetIp;
        private readonly ushort _icmpId;
        private readonly DnsService _resolver;
        private readonly Sender _icmpSender;
        private readonly Receiver _icmpReceiver;
        private int _sequenceNumber;
        private const int MaximumTtl = 30;
        private const int ReceiveTimeout = 3000;
        private const int AttemptsPerHop = 3;

        public Traceroute(bool dnsMode, IPAddress targetAddress, ushort processId)
        {
            _resolveNames = dnsMode;
            _targetIp = targetAddress;
            _icmpId = processId;
            _resolver = new DnsService();

            var receiveSocket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp);
            receiveSocket.Bind(new IPEndPoint(IPAddress.Any, 0));
            receiveSocket.ReceiveTimeout = ReceiveTimeout;

            var transmitSocket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp);

            _icmpSender = new Sender(transmitSocket, _targetIp, _icmpId);
            _icmpReceiver = new Receiver(receiveSocket, _icmpId);
        }

        public void StartTraceroute()
        {
            var destinationView = ResolveNodeName(_targetIp, ReceiveTimeout);
            Console.WriteLine($"\nТрассировка маршрута к {destinationView} [{_targetIp}]");
            Console.WriteLine($"с максимальным числом прыжков {MaximumTtl}:\n");

            bool destinationReached = false;

            for (int currentTtl = 1; currentTtl <= MaximumTtl && !destinationReached; currentTtl++)
            {
                Console.Write($"{currentTtl, 3} ");

                var currentHopAddresses = new HashSet<IPAddress>();
                var responseTimes = new List<long>();

                for (int attempt = 0; attempt < AttemptsPerHop; attempt++)
                {
                    int currentSequence = _sequenceNumber++;
                    var timer = Stopwatch.StartNew();

                    _icmpSender.Send(currentTtl, currentSequence);
                    var icmpReply = _icmpReceiver.Receive(timer, currentSequence);

                    timer.Stop();

                    if (icmpReply?.Address == null)
                    {
                        Console.Write("    *   ");
                        continue;
                    }

                    currentHopAddresses.Add(icmpReply.Address);
                    responseTimes.Add(icmpReply.Rtt);
                    Console.Write($"{icmpReply.Rtt, 4}ms  ");

                    if (icmpReply.Address.Equals(_targetIp))
                        destinationReached = true;
                }

                PrintHopResult(currentHopAddresses);
                Console.WriteLine();
            }

            Console.WriteLine("\nТрассировка завершена.");
        }

        private void PrintHopResult(HashSet<IPAddress> addressesOnHop)
        {
            if (addressesOnHop.Count == 0)
            {
                Console.Write("  Превышен интервал ожидания для запроса.");
                return;
            }

            var hopIp = addressesOnHop.Last();
            var hopView = ResolveNodeName(hopIp, 500);

            if (!string.Equals(hopView, hopIp.ToString(), StringComparison.OrdinalIgnoreCase))
                Console.Write($"  {hopView} [{hopIp}]");
            else
                Console.Write($"  {hopIp}");
        }

        private string ResolveNodeName(IPAddress ip, int timeoutMs)
        {
            if (!_resolveNames)
                return ip.ToString();

            return _resolver.GetNameByAddress(ip, timeoutMs);
        }
    }
}
