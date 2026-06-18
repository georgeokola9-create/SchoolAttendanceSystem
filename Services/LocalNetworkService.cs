using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace BiometricAttendanceSystem.Services
{
    public class LocalNetworkService : ILocalNetworkService
    {
        public string? GetPreferredIPv4Address()
            => GetPreferredAddress()?.ToString();

        public string? GetPreferredSubnetPrefix()
        {
            var address = GetPreferredAddress();
            if (address == null) return null;

            var bytes = address.GetAddressBytes();
            return $"{bytes[0]}.{bytes[1]}.{bytes[2]}.";
        }

        private static IPAddress? GetPreferredAddress()
        {
            var candidates = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic =>
                    nic.OperationalStatus == OperationalStatus.Up &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .SelectMany(nic =>
                {
                    var props = nic.GetIPProperties();
                    var hasGateway = props.GatewayAddresses.Any(g =>
                        g.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(g.Address));

                    return props.UnicastAddresses
                        .Where(a =>
                            a.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !IPAddress.IsLoopback(a.Address) &&
                            IsPrivateIPv4(a.Address))
                        .Select(a => new
                        {
                            a.Address,
                            Score = GetInterfaceScore(nic, hasGateway)
                        });
                })
                .OrderByDescending(c => c.Score)
                .ToList();

            return candidates.FirstOrDefault()?.Address;
        }

        private static int GetInterfaceScore(NetworkInterface nic, bool hasGateway)
        {
            var score = hasGateway ? 100 : 0;

            score += nic.NetworkInterfaceType switch
            {
                NetworkInterfaceType.Wireless80211 => 50,
                NetworkInterfaceType.Ethernet      => 40,
                _                                  => 10
            };

            return score;
        }

        private static bool IsPrivateIPv4(IPAddress address)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10 ||
                   bytes[0] == 192 && bytes[1] == 168 ||
                   bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31;
        }
    }
}
