using System.Net;

namespace lab2
{
    class Program
    {
        private static readonly ushort processId = (ushort)(DateTime.Now.Ticks & 0xFFFF);
        private static void PrintUsage()
        {
            Console.WriteLine("Использование: dotnet run -- [-use] <цель>");
        }
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return;
            }

            bool useDns = false;
            string? targetHost = null;

            foreach (var arg in args)
            {
                if (arg.Equals("-use", StringComparison.OrdinalIgnoreCase))
                {
                    useDns = true;
                }
                else
                {
                    targetHost ??= arg;
                }    
            }

            if (string.IsNullOrWhiteSpace(targetHost))
            {
                Console.WriteLine("Цель не указана.");
                PrintUsage();
                return;
            }

            try
            {
                var dnsResolver = new DnsService();
                var targetAddress = dnsResolver.GetAddressByName(targetHost);
                var traceroute = new Traceroute(useDns, targetAddress, processId);
                traceroute.StartTraceroute();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }
    }
}