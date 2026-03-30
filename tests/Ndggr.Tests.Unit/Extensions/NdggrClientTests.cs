using FluentAssertions;
using Ndggr.Extensions;
using Xunit;

namespace Ndggr.Tests.Unit.Extensions;

public class NdggrClientTests
{
    [Fact]
    public void NdggrClient_DefaultConstructor_CreatesSuccessfully()
    {
        var act = () => { using var _ = new NdggrClient(); };

        act.Should().NotThrow();
    }

    [Fact]
    public void NdggrClient_WithSearchOptions_CreatesSuccessfully()
    {
        var options = new DdgSearchOptions { Region = "de-de" };

        var act = () => { using var _ = new NdggrClient(options); };

        act.Should().NotThrow();
    }

    [Fact]
    public void NdggrClient_Disposes_WithoutError()
    {
        var client = new NdggrClient();

        var act = () => client.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task SearchAsync_NullQuery_Throws()
    {
        using var client = new NdggrClient();

        var act = () => client.SearchAsync(null!);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
