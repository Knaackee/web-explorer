using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Ndggr.Content.Models;
using Xunit;

namespace Ndggr.Tests.Unit.Content;

/// <summary>
/// JSON schema v1 compatibility tests: ensures the schema is additive-only
/// and round-trip stable. Verifies that existing consumers won't break.
/// </summary>
public class JsonSchemaCompatibilityTests
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    // ── Required fields always present ──

    [Fact]
    public void Schema_AlwaysHasSchemaVersion()
    {
        var doc = CreateMinimalDocument();
        var json = JsonSerializer.Serialize(doc, WriteOptions);
        var node = JsonNode.Parse(json)!;

        node["schemaVersion"].Should().NotBeNull();
        node["schemaVersion"]!.GetValue<int>().Should().Be(1);
    }

    [Fact]
    public void Schema_AlwaysHasUrl()
    {
        var doc = CreateMinimalDocument();
        var json = JsonSerializer.Serialize(doc, WriteOptions);
        var node = JsonNode.Parse(json)!;

        node["url"].Should().NotBeNull();
        node["url"]!.GetValue<string>().Should().Be("https://example.com");
    }

    [Fact]
    public void Schema_AlwaysHasWordCountAndFetchedAt()
    {
        var doc = CreateMinimalDocument();
        var json = JsonSerializer.Serialize(doc, WriteOptions);
        var node = JsonNode.Parse(json)!;

        node["wordCount"].Should().NotBeNull();
        node["fetchedAt"].Should().NotBeNull();
    }

    // ── Null fields are omitted with WhenWritingNull ──

    [Fact]
    public void Schema_OmitsNullOptionalFields()
    {
        var doc = CreateMinimalDocument();
        var json = JsonSerializer.Serialize(doc, WriteOptions);

        json.Should().NotContain("\"title\"");
        json.Should().NotContain("\"author\"");
        json.Should().NotContain("\"publishedDate\"");
        json.Should().NotContain("\"language\"");
        json.Should().NotContain("\"siteName\"");
        json.Should().NotContain("\"excerpt\"");
        json.Should().NotContain("\"chunks\"");
        json.Should().NotContain("\"links\"");
        json.Should().NotContain("\"error\"");
    }

    // ── Full document round-trip ──

    [Fact]
    public void Schema_FullDocument_RoundTrips()
    {
        var doc = CreateFullDocument();
        var json = JsonSerializer.Serialize(doc);
        var deserialized = JsonSerializer.Deserialize<ContentDocument>(json);

        deserialized.Should().NotBeNull();
        deserialized!.SchemaVersion.Should().Be(1);
        deserialized.Url.Should().Be("https://example.com/article");
        deserialized.Title.Should().Be("Test Title");
        deserialized.Author.Should().Be("Test Author");
        deserialized.PublishedDate.Should().Be("2025-01-15");
        deserialized.Language.Should().Be("en");
        deserialized.SiteName.Should().Be("Example");
        deserialized.Excerpt.Should().Be("A test article.");
        deserialized.Markdown.Should().Be("# Test\n\nContent");
        deserialized.TextContent.Should().Be("Test Content");
        deserialized.WordCount.Should().Be(2);
        deserialized.Chunks.Should().HaveCount(1);
        deserialized.Chunks![0].Id.Should().Be("abc123def456");
        deserialized.Links.Should().HaveCount(1);
        deserialized.Links![0].Href.Should().Be("https://example.com/link");
    }

    // ── Forward compatibility: unknown fields are ignored on deserialization ──

    [Fact]
    public void Schema_UnknownFields_IgnoredOnDeserialization()
    {
        var json = """
            {
                "schemaVersion": 1,
                "url": "https://example.com",
                "wordCount": 5,
                "fetchedAt": "2025-01-01T00:00:00+00:00",
                "newFieldInV2": "value",
                "anotherNewField": 42
            }
            """;

        var doc = JsonSerializer.Deserialize<ContentDocument>(json);

        doc.Should().NotBeNull();
        doc!.Url.Should().Be("https://example.com");
        doc.SchemaVersion.Should().Be(1);
    }

    // ── Property names are camelCase ──

    [Fact]
    public void Schema_PropertyNames_AreCamelCase()
    {
        var doc = CreateFullDocument();
        var json = JsonSerializer.Serialize(doc);
        var node = JsonNode.Parse(json)!.AsObject();

        var keys = node.Select(p => p.Key).ToList();

        keys.Should().Contain("schemaVersion");
        keys.Should().Contain("url");
        keys.Should().Contain("title");
        keys.Should().Contain("wordCount");
        keys.Should().Contain("fetchedAt");
        keys.Should().Contain("textContent");
        keys.Should().Contain("publishedDate");
        keys.Should().Contain("siteName");
        keys.Should().Contain("charCount".Replace("charCount", "chunks")); // verify nested names
    }

    [Fact]
    public void Schema_ChunkProperties_AreCamelCase()
    {
        var chunk = new ContentChunk
        {
            Id = "abc123",
            Index = 0,
            Heading = "Test",
            Content = "Body",
            CharCount = 4
        };
        var json = JsonSerializer.Serialize(chunk);
        var node = JsonNode.Parse(json)!.AsObject();

        node.Select(p => p.Key).Should().Contain("id");
        node.Select(p => p.Key).Should().Contain("index");
        node.Select(p => p.Key).Should().Contain("heading");
        node.Select(p => p.Key).Should().Contain("content");
        node.Select(p => p.Key).Should().Contain("charCount");
    }

    [Fact]
    public void Schema_LinkProperties_AreCamelCase()
    {
        var link = new ExtractedLink
        {
            Text = "Example",
            Href = "https://example.com"
        };
        var json = JsonSerializer.Serialize(link);
        var node = JsonNode.Parse(json)!.AsObject();

        node.Select(p => p.Key).Should().Contain("text");
        node.Select(p => p.Key).Should().Contain("href");
    }

    // ── SchemaVersion default is 1 ──

    [Fact]
    public void Schema_DefaultVersion_Is1()
    {
        var doc = new ContentDocument
        {
            Url = "https://example.com",
            FetchedAt = DateTimeOffset.UtcNow
        };

        doc.SchemaVersion.Should().Be(1);
    }

    // ── Additive-only: all v1 fields exist and are typed correctly ──

    [Fact]
    public void Schema_V1Fields_TypeCheck()
    {
        var doc = CreateFullDocument();
        var json = JsonSerializer.Serialize(doc);
        var node = JsonNode.Parse(json)!;

        node["schemaVersion"]!.GetValue<int>().Should().Be(1);
        node["url"]!.GetValue<string>().Should().NotBeNullOrEmpty();
        node["title"]!.GetValue<string>().Should().NotBeNullOrEmpty();
        node["author"]!.GetValue<string>().Should().NotBeNullOrEmpty();
        node["publishedDate"]!.GetValue<string>().Should().NotBeNullOrEmpty();
        node["language"]!.GetValue<string>().Should().NotBeNullOrEmpty();
        node["siteName"]!.GetValue<string>().Should().NotBeNullOrEmpty();
        node["excerpt"]!.GetValue<string>().Should().NotBeNullOrEmpty();
        node["markdown"]!.GetValue<string>().Should().NotBeNullOrEmpty();
        node["textContent"]!.GetValue<string>().Should().NotBeNullOrEmpty();
        node["wordCount"]!.GetValue<int>().Should().BeGreaterThan(0);
        node["chunks"]!.AsArray().Should().HaveCount(1);
        node["links"]!.AsArray().Should().HaveCount(1);
        node["fetchedAt"]!.GetValue<string>().Should().NotBeNullOrEmpty();
    }

    // ── Helpers ──

    private static ContentDocument CreateMinimalDocument() => new()
    {
        Url = "https://example.com",
        FetchedAt = DateTimeOffset.Parse("2025-01-01T00:00:00Z")
    };

    private static ContentDocument CreateFullDocument() => new()
    {
        SchemaVersion = 1,
        Url = "https://example.com/article",
        Title = "Test Title",
        Author = "Test Author",
        PublishedDate = "2025-01-15",
        Language = "en",
        SiteName = "Example",
        Excerpt = "A test article.",
        Markdown = "# Test\n\nContent",
        TextContent = "Test Content",
        WordCount = 2,
        Chunks =
        [
            new ContentChunk
            {
                Id = "abc123def456",
                Index = 0,
                Heading = "Test",
                Content = "Content",
                CharCount = 7
            }
        ],
        Links =
        [
            new ExtractedLink
            {
                Text = "Link",
                Href = "https://example.com/link"
            }
        ],
        FetchedAt = DateTimeOffset.Parse("2025-01-15T12:00:00Z")
    };
}
