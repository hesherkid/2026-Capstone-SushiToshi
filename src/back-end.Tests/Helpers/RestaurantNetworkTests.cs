using System.Net;
using Xunit;
using FluentAssertions;
using back_end.Helpers;

namespace back_end.Tests.Helpers;

public class RestaurantNetworkTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsOnSite_NoNetworks_EveryoneCanJoin(string? networks)
    {
        RestaurantNetwork.IsOnSite(IPAddress.Parse("198.51.100.7"), networks).Should().BeTrue();
        RestaurantNetwork.IsOnSite(null, networks).Should().BeTrue();
    }

    [Theory]
    [InlineData("203.0.113.10", true)]
    [InlineData("203.0.113.11", false)]
    [InlineData("192.168.1.42", true)]
    [InlineData("192.168.2.42", false)]
    [InlineData("2001:db8:1:2::abcd", true)]
    [InlineData("2001:db8:1:3::abcd", false)]
    [InlineData("::ffff:203.0.113.10", true)]   // IPv4 seen through an IPv6 socket
    public void IsOnSite_MatchesAnyListedNetwork(string address, bool expected)
    {
        const string networks = "203.0.113.10/32,192.168.1.0/24,2001:db8:1:2::/64";

        RestaurantNetwork.IsOnSite(IPAddress.Parse(address), networks).Should().Be(expected);
    }

    [Fact]
    public void IsOnSite_CheckOnButNoAddress_IsRefused()
    {
        RestaurantNetwork.IsOnSite(null, "203.0.113.10/32").Should().BeFalse();
    }

    [Fact]
    public void Normalize_SingleAddressesBecomeOneAddressNetworks()
    {
        var (stored, error) = RestaurantNetwork.Normalize(new[] { " 203.0.113.10 ", "2001:db8::1", "192.168.1.0/24" });

        error.Should().BeNull();
        stored.Should().Be("203.0.113.10/32,2001:db8::1/128,192.168.1.0/24");
    }

    [Fact]
    public void Normalize_DropsBlanksAndDuplicates()
    {
        var (stored, _) = RestaurantNetwork.Normalize(new[] { "203.0.113.10", "", "203.0.113.10/32" });

        stored.Should().Be("203.0.113.10/32");
    }

    [Fact]
    public void Normalize_EmptyList_TurnsTheCheckOff()
    {
        RestaurantNetwork.Normalize(new string[0]).Should().Be((null, null));
        RestaurantNetwork.Normalize(null).Should().Be((null, null));
    }

    [Theory]
    [InlineData("not an address")]
    [InlineData("300.1.1.1")]
    [InlineData("192.168.1.0/40")]
    public void Normalize_InvalidEntry_SaysWhichOne(string entry)
    {
        var (stored, error) = RestaurantNetwork.Normalize(new[] { "203.0.113.10", entry });

        stored.Should().BeNull();
        error.Should().Contain(entry);
    }

    [Fact]
    public void Normalize_TooManyNetworks_IsRefused()
    {
        var entries = Enumerable.Range(1, RestaurantNetwork.MaxNetworks + 1).Select(i => $"203.0.113.{i}");

        RestaurantNetwork.Normalize(entries).Error.Should().NotBeNull();
    }

    [Theory]
    [InlineData("203.0.113.10", "203.0.113.10/32")]
    [InlineData("::ffff:203.0.113.10", "203.0.113.10/32")]
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", "2001:db8:1:2::/64")]
    public void SuggestedNetwork_IsTheAddressForIpv4AndTheSlash64ForIpv6(string address, string expected)
    {
        RestaurantNetwork.SuggestedNetwork(IPAddress.Parse(address)).Should().Be(expected);
    }
}
