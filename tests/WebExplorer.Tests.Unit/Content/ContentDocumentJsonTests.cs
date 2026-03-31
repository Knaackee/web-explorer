using System.Text.Json;
using FluentAssertions;
using WebExplorer.Content;
using WebExplorer.Content.Models;
using Xunit;

namespace WebExplorer.Tests.Unit.Content;

public class ContentDocumentJsonTests
{
    [Fact]
    public void ContentDocument_SerializesToJson()
    {
        var doc = new ContentDocument
        {
            Url = "https://example.com",
            Title = "Test",
            Markdown = "# Test",
            TextContent = "Test content",
            WordCount = 2,
            FetchedAt = DateTimeOffset.UtcNow
        };

        var json = JsonSerializer.Serialize(doc);

        json.Should().Contain("\"schemaVersion\":1");
        json.Should().Contain("\"url\":\"https://example.com\"");
        json.Should().Contain("\"title\":\"Test\"");
    }

    [Fact]
    public void ContentDocument_OmitsNullFields()
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        var doc = new ContentDocument
        {
            Url = "https://example.com",
            FetchedAt = DateTimeOffset.UtcNow
        };

        var json = JsonSerializer.Serialize(doc, options);

        json.Should().NotContain("\"author\"");
        json.Should().NotContain("\"chunks\"");
        json.Should().NotContain("\"links\"");
    }

    [Fact]
    public void ContentDocument_RoundTrips()
    {
        var doc = new ContentDocument
        {
            SchemaVersion = 1,
            Url = "https://example.com",
            Title = "Round Trip Test",
            Markdown = "# Hello",
            TextContent = "Hello",
            WordCount = 1,
            FetchedAt = DateTimeOffset.Parse("2025-01-01T00:00:00Z")
        };

        var json = JsonSerializer.Serialize(doc);
        var deserialized = JsonSerializer.Deserialize<ContentDocument>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Url.Should().Be("https://example.com");
        deserialized.Title.Should().Be("Round Trip Test");
        deserialized.SchemaVersion.Should().Be(1);
    }

    [Fact]
    public void ContentChunk_SerializesCorrectly()
    {
        var chunk = new ContentChunk
        {
            Id = "abc123def456",
            Index = 0,
            Heading = "Test Section",
            Content = "Some content here",
            CharCount = 17
        };

        var json = JsonSerializer.Serialize(chunk);

        json.Should().Contain("\"id\":\"abc123def456\"");
        json.Should().Contain("\"heading\":\"Test Section\"");
    }

    [Fact]
    public void ExtractedLink_SerializesCorrectly()
    {
        var link = new ExtractedLink
        {
            Text = "Example",
            Href = "https://example.com"
        };

        var json = JsonSerializer.Serialize(link);

        json.Should().Contain("\"text\":\"Example\"");
        json.Should().Contain("\"href\":\"https://example.com\"");
    }
}
