using FluentAssertions;
using Xunit;

namespace Ndggr.Tests.Unit;

public class DdgSearchOptionsTests
{
    [Fact]
    public void DefaultOptions_HaveExpectedDefaults()
    {
        var options = new DdgSearchOptions();

        options.NumResults.Should().Be(10);
        options.Region.Should().Be("us-en");
        options.TimeFilter.Should().Be("");
        options.Site.Should().BeNull();
        options.SafeSearch.Should().BeTrue();
        options.SendUserAgent.Should().BeTrue();
        options.Proxy.Should().BeNull();
    }

    [Fact]
    public void Options_CanBeCustomized()
    {
        var options = new DdgSearchOptions
        {
            NumResults = 25,
            Region = "de-de",
            TimeFilter = "w",
            Site = "github.com",
            SafeSearch = false,
            SendUserAgent = false,
            Proxy = new Uri("http://proxy:8080")
        };

        options.NumResults.Should().Be(25);
        options.Region.Should().Be("de-de");
        options.TimeFilter.Should().Be("w");
        options.Site.Should().Be("github.com");
        options.SafeSearch.Should().BeFalse();
        options.SendUserAgent.Should().BeFalse();
        options.Proxy.Should().Be(new Uri("http://proxy:8080"));
    }
}
