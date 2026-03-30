using FluentAssertions;
using Ndggr.Content;
using Ndggr.Content.Chunking;
using Ndggr.Content.Models;
using Xunit;

namespace Ndggr.Tests.Unit.Content;

/// <summary>
/// Tests verifying chunking determinism: same input → identical output (IDs, order, content).
/// </summary>
public class ChunkingDeterminismTests
{
    private const string LongMarkdown = """
        # Introduction

        This is an introduction to a comprehensive article about software engineering practices.
        Modern software development requires a deep understanding of multiple disciplines.

        ## Architecture Patterns

        Software architecture patterns provide reusable solutions to common problems in software design.
        The choice of architecture pattern significantly impacts the maintainability and scalability of a system.

        ### Microservices

        Microservices architecture structures an application as a collection of loosely coupled services.
        Each service implements a specific business capability and can be deployed independently.
        This approach enables teams to work autonomously and choose appropriate technologies.

        ### Event-Driven Architecture

        Event-driven architecture is a software design pattern in which decoupled services communicate through events.
        An event is a change in state, or an update, that is significant to the system.
        This pattern is well-suited for real-time processing and asynchronous communication.

        ## Testing Strategies

        Testing is a critical part of the software development lifecycle. A comprehensive testing strategy
        includes unit tests, integration tests, and end-to-end tests. Each type serves a different purpose
        and operates at a different level of abstraction.

        ### Unit Testing

        Unit tests verify individual components in isolation. They are fast, reliable, and provide
        quick feedback during development. A good unit test suite gives developers confidence to refactor.

        ### Integration Testing

        Integration tests verify that different components work together correctly. They test the
        boundaries between modules and external dependencies like databases and APIs.

        ## Conclusion

        Building high-quality software requires a thoughtful approach to architecture and testing.
        The investment in good practices pays dividends in reduced maintenance costs and faster delivery.
        """;

    [Fact]
    public void Chunk_SameInput_ProducesIdenticalOutput()
    {
        var chunks1 = HeadingAwareChunker.Chunk(LongMarkdown, "https://example.com/article", 300);
        var chunks2 = HeadingAwareChunker.Chunk(LongMarkdown, "https://example.com/article", 300);

        chunks1.Should().HaveCount(chunks2.Count);

        for (var i = 0; i < chunks1.Count; i++)
        {
            chunks1[i].Id.Should().Be(chunks2[i].Id, $"chunk {i} ID must be stable");
            chunks1[i].Index.Should().Be(chunks2[i].Index, $"chunk {i} index must match");
            chunks1[i].Heading.Should().Be(chunks2[i].Heading, $"chunk {i} heading must match");
            chunks1[i].Content.Should().Be(chunks2[i].Content, $"chunk {i} content must be identical");
            chunks1[i].CharCount.Should().Be(chunks2[i].CharCount, $"chunk {i} charCount must match");
        }
    }

    [Fact]
    public void Chunk_RunMultipleTimes_AllIdentical()
    {
        var baseline = HeadingAwareChunker.Chunk(LongMarkdown, "https://example.com/stable", 400);

        for (var run = 0; run < 10; run++)
        {
            var current = HeadingAwareChunker.Chunk(LongMarkdown, "https://example.com/stable", 400);
            current.Select(c => c.Id).Should().BeEquivalentTo(baseline.Select(c => c.Id),
                $"run {run} must produce identical IDs");
        }
    }

    [Fact]
    public void Chunk_DifferentUrls_DifferentIds_SameContent()
    {
        var chunksA = HeadingAwareChunker.Chunk(LongMarkdown, "https://example.com/page-a", 300);
        var chunksB = HeadingAwareChunker.Chunk(LongMarkdown, "https://example.com/page-b", 300);

        chunksA.Should().HaveCount(chunksB.Count, "same content should produce same number of chunks");

        for (var i = 0; i < chunksA.Count; i++)
        {
            chunksA[i].Id.Should().NotBe(chunksB[i].Id, $"chunk {i} ID should differ for different URLs");
            chunksA[i].Content.Should().Be(chunksB[i].Content, $"chunk {i} content should be identical");
        }
    }

    [Fact]
    public void Chunk_IdsAre12CharHex()
    {
        var chunks = HeadingAwareChunker.Chunk(LongMarkdown, "https://example.com", 300);

        foreach (var chunk in chunks)
        {
            chunk.Id.Should().HaveLength(12);
            chunk.Id.Should().MatchRegex("^[0-9a-f]{12}$", "ID must be lowercase hex");
        }
    }

    [Fact]
    public void Chunk_CharCountMatchesContentLength()
    {
        var chunks = HeadingAwareChunker.Chunk(LongMarkdown, "https://example.com", 300);

        foreach (var chunk in chunks)
        {
            chunk.CharCount.Should().Be(chunk.Content.Length,
                $"CharCount must equal Content.Length for chunk {chunk.Index}");
        }
    }

    [Fact]
    public void Chunk_IndicesAreContiguousFromZero()
    {
        var chunks = HeadingAwareChunker.Chunk(LongMarkdown, "https://example.com", 200);

        chunks.Should().HaveCountGreaterThan(1);
        for (var i = 0; i < chunks.Count; i++)
        {
            chunks[i].Index.Should().Be(i);
        }
    }

    [Fact]
    public void Chunk_IdsAreUnique()
    {
        var chunks = HeadingAwareChunker.Chunk(LongMarkdown, "https://example.com", 200);

        chunks.Select(c => c.Id).Should().OnlyHaveUniqueItems("each chunk must have a unique ID");
    }

    [Fact]
    public void Pipeline_Chunking_IsDeterministic()
    {
        var html = File.ReadAllText(Path.Combine("Fixtures", "content_article.html"));
        using var pipeline = new ContentPipeline();
        var options = new ContentExtractionOptions { ChunkSize = 300 };

        var doc1 = pipeline.ExtractFromHtml(html, "https://example.com/test", options);
        var doc2 = pipeline.ExtractFromHtml(html, "https://example.com/test", options);

        doc1.Chunks.Should().NotBeNull();
        doc2.Chunks.Should().NotBeNull();
        doc1.Chunks!.Count.Should().Be(doc2.Chunks!.Count);
        doc1.Chunks.Select(c => c.Id).Should().BeEquivalentTo(doc2.Chunks.Select(c => c.Id));
    }

    [Fact]
    public void Chunk_MaxChunks_StillDeterministic()
    {
        var chunks1 = HeadingAwareChunker.Chunk(LongMarkdown, "https://example.com", 200, maxChunks: 3);
        var chunks2 = HeadingAwareChunker.Chunk(LongMarkdown, "https://example.com", 200, maxChunks: 3);

        chunks1.Should().HaveCountLessThanOrEqualTo(3);
        chunks1.Select(c => c.Id).Should().BeEquivalentTo(chunks2.Select(c => c.Id));
    }
}
