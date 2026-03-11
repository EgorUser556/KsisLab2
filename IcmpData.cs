using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

namespace lab2
{
    public class Receiver
    {
        private readonly Socket _socket;
        private readonly ushort _icmpId;

        public Receiver(Socket receiveSocket, ushort processId)
        {
            _socket = receiveSocket;
            _icmpId = processId;
        }

        public Reply? Receive(Stopwatch timer, int requiredSequence)
        {
            try
            {
                byte[] datagram = new byte[4096];
                EndPoint remotePoint = new IPEndPoint(IPAddress.Any, 0);
                int bytesRead = _socket.ReceiveFrom(datagram, ref remotePoint);

                if (bytesRead < 20)
                    return null;

                int outerIpHeaderLength = (datagram[0] & 0x0F) * 4;
                if (datagram[9] != 1)
                    return null;

                int icmpStart = outerIpHeaderLength;
                if (icmpStart + 4 > bytesRead)
                    return null;

                IPAddress senderIp = new IPAddress(datagram.Skip(12).Take(4).ToArray());
                byte icmpType = datagram[icmpStart];

                bool TryReadEmbeddedEcho(int icmpOffset, out int packetId, out int packetSequence)
                {
                    packetId = 0;
                    packetSequence = 0;

                    int embeddedIpStart = icmpOffset + 8;
                    if (embeddedIpStart + 20 > bytesRead)
                        return false;

                    int embeddedIpHeaderLength = (datagram[embeddedIpStart] & 0x0F) * 4;
                    int embeddedIcmpStart = embeddedIpStart + embeddedIpHeaderLength;

                    if (embeddedIcmpStart + 8 > bytesRead)
                        return false;

                    if (datagram[embeddedIcmpStart] != 8)
                        return false;

                    packetId = (datagram[embeddedIcmpStart + 4] << 8) | datagram[embeddedIcmpStart + 5];
                    packetSequence = (datagram[embeddedIcmpStart + 6] << 8) | datagram[embeddedIcmpStart + 7];
                    return true;
                }

                if (icmpType == 0)
                {
                    if (icmpStart + 8 > bytesRead)
                        return null;

                    int replyId = (datagram[icmpStart + 4] << 8) | datagram[icmpStart + 5];
                    int replySequence = (datagram[icmpStart + 6] << 8) | datagram[icmpStart + 7];

                    if (replyId != _icmpId || replySequence != requiredSequence)
                        return null;

                    return new Reply
                    {
                        Address = senderIp,
                        Rtt = timer.ElapsedMilliseconds
                    };
                }

                if (icmpType == 11 || icmpType == 3)
                {
                    bool ok = TryReadEmbeddedEcho(icmpStart, out int embeddedId, out int embeddedSequence);

                    if (!ok)
                        return null;

                    if (embeddedId != _icmpId || embeddedSequence != requiredSequence)
                        return null;

                    return new Reply
                    {
                        Address = senderIp,
                        Rtt = timer.ElapsedMilliseconds
                    };
                }
            }
            catch 
            {
            }

            return null;
        }
    }

    public class Sender
    {
        private readonly Socket _socket;
        private readonly IPAddress _destination;
        private readonly ushort _icmpId;

        public Sender(Socket sendSocket, IPAddress targetAddress, ushort processId)
        {
            _socket = sendSocket;
            _destination = targetAddress;
            _icmpId = processId;
        }

        public void Send(int ttl, int sequenceNumber)
        {
            try
            {
                _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, ttl);

                byte[] icmpPacket = new byte[40];
                icmpPacket[0] = 8;
                icmpPacket[1] = 0;

                icmpPacket[4] = (byte)(_icmpId >> 8);
                icmpPacket[5] = (byte)(_icmpId & 0xFF);

                icmpPacket[6] = (byte)(sequenceNumber >> 8);
                icmpPacket[7] = (byte)(sequenceNumber & 0xFF);

                string payloadText = "TraceroutePayload";
                for (int index = 8; index < icmpPacket.Length; index++)
                {
                    icmpPacket[index] = (byte)payloadText[(index - 8) % payloadText.Length];
                }

                ushort controlSum = CreateControlSum(icmpPacket);
                icmpPacket[2] = (byte)(controlSum >> 8);
                icmpPacket[3] = (byte)(controlSum & 0xFF);

                EndPoint destinationPoint = new IPEndPoint(_destination, 0);
                _socket.SendTo(icmpPacket, destinationPoint);
            }
            catch 
            {
            }
        }

        private ushort CreateControlSum(byte[] packetBytes)
        {
            long total = 0;

            for (int position = 0; position < packetBytes.Length; position += 2)
            {
                if (position + 1 < packetBytes.Length)
                    total += (packetBytes[position] << 8) | packetBytes[position + 1];
                else
                    total += packetBytes[position] << 8;
            }

            while ((total >> 16) != 0)
            {
                total = (total & 0xFFFF) + (total >> 16);
            }

            return (ushort)~total;
        }
    }

    public class Reply
    {
        public IPAddress? Address { get; set; }
        public long Rtt { get; set; }
    }
}
