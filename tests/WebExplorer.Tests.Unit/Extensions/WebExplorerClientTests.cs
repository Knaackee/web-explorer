using FluentAssertions;
using WebExplorer.Extensions;
using Xunit;

namespace WebExplorer.Tests.Unit.Extensions;

public class WebExplorerClientTests
{
    [Fact]
    public void WebExplorerClient_DefaultConstructor_CreatesSuccessfully()
    {
        var act = () => { using var _ = new WebExplorerClient(); };

        act.Should().NotThrow();
    }

    [Fact]
    public void WebExplorerClient_WithSearchOptions_CreatesSuccessfully()
    {
        var options = new SearchOptions { Region = "de-de" };

        var act = () => { using var _ = new WebExplorerClient(options); };

        act.Should().NotThrow();
    }

    [Fact]
    public void WebExplorerClient_Disposes_WithoutError()
    {
        var client = new WebExplorerClient();

        var act = () => client.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task SearchAsync_NullQuery_Throws()
    {
        using var client = new WebExplorerClient();

        var act = () => client.SearchAsync(null!);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
