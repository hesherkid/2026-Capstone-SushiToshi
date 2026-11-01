using System.Net;
using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.HttpOverrides;
using back_end.Configurations;

namespace back_end.Tests.Configurations;

// Trusted proxies come from configuration; a typo must stop startup rather than be ignored
public class ForwardedHeadersSettingsTests
{
    [Fact]
    public void ToOptions_TrustsLoopbackPlusTheConfiguredProxiesAndNetworks()
    {
        var options = new ForwardedHeadersSettings
        {
            KnownProxies = ["10.0.0.5"],
            KnownNetworks = ["172.16.0.0/12"],
            ForwardLimit = 2
        }.ToOptions();

        options.ForwardedHeaders.Should().Be(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
        options.ForwardLimit.Should().Be(2);
        // ASP.NET's defaults: ::1 as a proxy and 127.0.0.0/8 as a network
        options.KnownProxies.Should().Contain([IPAddress.IPv6Loopback, IPAddress.Parse("10.0.0.5")]);
        options.KnownNetworks.Should().Contain(n => n.Contains(IPAddress.Loopback));
        options.KnownNetworks.Should().Contain(n =>
            n.Prefix.Equals(IPAddress.Parse("172.16.0.0")) && n.PrefixLength == 12);
    }

    [Theory]
    [InlineData("10.0.0.300", null)]
    [InlineData(null, "10.0.0.0/40")]
    public void ToOptions_InvalidAddress_Throws(string? proxy, string? network)
    {
        var settings = new ForwardedHeadersSettings
        {
            KnownProxies = proxy == null ? [] : [proxy],
            KnownNetworks = network == null ? [] : [network]
        };

        settings.Invoking(s => s.ToOptions()).Should().Throw<FormatException>();
    }
}
