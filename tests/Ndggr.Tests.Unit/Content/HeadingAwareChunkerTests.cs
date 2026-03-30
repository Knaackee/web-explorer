using FluentAssertions;
using Ndggr.Content.Chunking;
using Xunit;

namespace Ndggr.Tests.Unit.Content;

public class HeadingAwareChunkerTests
{
    private const string SampleMarkdown = """
        # Introduction
        
        This is the introduction to our article about testing.
        
        ## Section One
        
        Section one has some interesting content that we want to test. It covers the basics of unit testing and why it is important for software quality.
        
        ## Section Two
        
        Section two builds on the previous section and goes deeper into integration testing patterns and best practices for modern applications.
        
        ## Conclusion
        
        In conclusion, testing is crucial for building reliable software.
        """;

    [Fact]
    public void Chunk_WithLargeChunkSize_ProducesFewerChunks()
    {
        var chunks = HeadingAwareChunker.Chunk(SampleMarkdown, "https://example.com", 5000);

        chunks.Count.Should().BeGreaterThan(0);
        chunks.Count.Should().BeLessThanOrEqualTo(4);
    }

    [Fact]
    public void Chunk_WithSmallChunkSize_ProducesMoreChunks()
    {
        var chunks = HeadingAwareChunker.Chunk(SampleMarkdown, "https://example.com", 100);

        chunks.Count.Should().BeGreaterThan(2);
    }

    [Fact]
    public void Chunk_HasStableIds()
    {
        var chunks1 = HeadingAwareChunker.Chunk(SampleMarkdown, "https://example.com", 200);
        var chunks2 = HeadingAwareChunker.Chunk(SampleMarkdown, "https://example.com", 200);

        chunks1.Select(c => c.Id).Should().BeEquivalentTo(chunks2.Select(c => c.Id));
    }

    [Fact]
    public void Chunk_IdsAreDeterministic_DifferentUrlsProduceDifferentIds()
    {
        var chunks1 = HeadingAwareChunker.Chunk(SampleMarkdown, "https://example.com/a", 200);
        var chunks2 = HeadingAwareChunker.Chunk(SampleMarkdown, "https://example.com/b", 200);

        chunks1[0].Id.Should().NotBe(chunks2[0].Id);
    }

    [Fact]
    public void Chunk_IndicesAreSequential()
    {
        var chunks = HeadingAwareChunker.Chunk(SampleMarkdown, "https://example.com", 100);

        for (var i = 0; i < chunks.Count; i++)
        {
            chunks[i].Index.Should().Be(i);
        }
    }

    [Fact]
    public void Chunk_HasCharCount()
    {
        var chunks = HeadingAwareChunker.Chunk(SampleMarkdown, "https://example.com", 200);

        foreach (var chunk in chunks)
        {
            chunk.CharCount.Should().Be(chunk.Content.Length);
        }
    }

    [Fact]
    public void Chunk_WithMaxChunks_LimitsOutput()
    {
        var chunks = HeadingAwareChunker.Chunk(SampleMarkdown, "https://example.com", 100, maxChunks: 2);

        chunks.Count.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public void Chunk_ZeroChunkSize_ReturnsEmpty()
    {
        var chunks = HeadingAwareChunker.Chunk(SampleMarkdown, "https://example.com", 0);

        chunks.Should().BeEmpty();
    }

    [Fact]
    public void Chunk_EmptyInput_ReturnsEmpty()
    {
        var chunks = HeadingAwareChunker.Chunk("", "https://example.com", 200);

        chunks.Should().BeEmpty();
    }

    [Fact]
    public void Chunk_PreservesHeadings()
    {
        var chunks = HeadingAwareChunker.Chunk(SampleMarkdown, "https://example.com", 200);

        chunks.Any(c => c.Heading is not null).Should().BeTrue();
    }
}
