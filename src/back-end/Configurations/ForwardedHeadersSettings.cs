using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace back_end.Configurations;

// Behind a reverse proxy every request arrives from the proxy's address, so the per-IP rate limits
// (e.g. guest sign-in) would become one limit for everyone. When enabled, the client address is
// taken from X-Forwarded-For, but only when the request comes from a trusted proxy.
public sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    public bool Enabled { get; set; }

    // Proxies trusted on top of loopback (127.0.0.1 and ::1, always trusted), e.g. "10.0.0.5"
    public string[] KnownProxies { get; set; } = [];

    // Proxy networks in CIDR form, e.g. "10.0.0.0/24"
    public string[] KnownNetworks { get; set; } = [];

    // How many X-Forwarded-For entries to read from the right: one per trusted proxy in front of the app
    public int ForwardLimit { get; set; } = 1;

    // Throws FormatException on an invalid address, so a typo stops startup instead of being ignored
    public ForwardedHeadersOptions ToOptions()
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = ForwardLimit
        };

        foreach (var proxy in KnownProxies)
        {
            options.KnownProxies.Add(IPAddress.Parse(proxy.Trim()));
        }

        foreach (var network in KnownNetworks)
        {
            var parsed = System.Net.IPNetwork.Parse(network.Trim());
            options.KnownNetworks.Add(
                new Microsoft.AspNetCore.HttpOverrides.IPNetwork(parsed.BaseAddress, parsed.PrefixLength));
        }

        return options;
    }
}
