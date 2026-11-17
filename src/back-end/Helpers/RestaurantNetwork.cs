using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace back_end.Helpers
{
    /// <summary>
    /// The Wi-Fi check: guests can only join a table from the restaurant's own network.
    /// A browser can't tell us which Wi-Fi a phone is on, but every phone on the restaurant Wi-Fi
    /// reaches us from the restaurant's public address (or, for IPv6, its /64 network). So a location
    /// lists those networks (Locations.Allowed_Networks) and join compares the request's address.
    /// Needs forwarded headers turned on, or every request looks like it came from the Next.js server.
    /// </summary>
    public static class RestaurantNetwork
    {
        public const int MaxNetworks = 10;

        public const string OffSiteMessage =
            "Please connect to the restaurant Wi-Fi to join this table. Scan the Wi-Fi QR code on your table, then tap Try again.";

        // True when the address is in one of the networks. No networks means the check is off
        public static bool IsOnSite(IPAddress? clientIp, string? allowedNetworks)
        {
            var networks = Parse(allowedNetworks);
            if (networks.Count == 0) return true;
            if (clientIp == null) return false;
            if (clientIp.IsIPv4MappedToIPv6) clientIp = clientIp.MapToIPv4();

            return networks.Any(n => n.Contains(clientIp));
        }

        // The stored list as separate entries, e.g. ["203.0.113.10/32", "2001:db8:1:2::/64"]
        public static List<string> Split(string? allowedNetworks) =>
            (allowedNetworks ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

        /// <summary>
        /// Checks what an admin typed and turns it into the stored form, or returns why not.
        /// A single address becomes /32 (IPv4) or /128 (IPv6). An empty list turns the check off (null).
        /// </summary>
        public static (string? Stored, string? Error) Normalize(IEnumerable<string>? entries)
        {
            var normalized = new List<string>();

            foreach (var raw in entries ?? Enumerable.Empty<string>())
            {
                var entry = raw?.Trim() ?? "";
                if (entry.Length == 0) continue;

                if (!entry.Contains('/') && IPAddress.TryParse(entry, out var single))
                {
                    if (single.IsIPv4MappedToIPv6) single = single.MapToIPv4();
                    entry = $"{single}/{(single.AddressFamily == AddressFamily.InterNetwork ? 32 : 128)}";
                }

                if (!IPNetwork.TryParse(entry, out var network))
                {
                    return (null, $"\"{raw}\" isn't a valid address or network. Use an address like 203.0.113.10, or a network like 192.168.1.0/24.");
                }

                var text = network.ToString();
                if (!normalized.Contains(text)) normalized.Add(text);
            }

            if (normalized.Count > MaxNetworks)
            {
                return (null, $"A location can have at most {MaxNetworks} networks.");
            }

            return (normalized.Count == 0 ? null : string.Join(",", normalized), null);
        }

        /// <summary>
        /// The network to suggest for an address: the address itself for IPv4 (a restaurant shares one
        /// public address), its /64 for IPv6 (each phone gets its own address inside the restaurant's /64).
        /// </summary>
        public static string? SuggestedNetwork(IPAddress? address)
        {
            if (address == null) return null;
            if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
            if (address.AddressFamily == AddressFamily.InterNetwork) return $"{address}/32";

            var bytes = address.GetAddressBytes();
            Array.Clear(bytes, 8, 8);
            return $"{new IPAddress(bytes)}/64";
        }

        private static List<IPNetwork> Parse(string? allowedNetworks) =>
            Split(allowedNetworks)
                .Select(n => IPNetwork.TryParse(n, out var network) ? network : (IPNetwork?)null)
                .Where(n => n != null)
                .Select(n => n!.Value)
                .ToList();
    }
}
