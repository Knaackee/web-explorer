using BenchmarkDotNet.Attributes;
using Ndggr.Parsing;

namespace Ndggr.Benchmarks;

[MemoryDiagnoser]
[SimpleJob]
public class SearchParsingBenchmarks
{
    private string _basicHtml = null!;
    private string _instantAnswerHtml = null!;
    private string _paginationHtml = null!;
    private HtmlResultParser _parser = null!;

    [GlobalSetup]
    public void Setup()
    {
        _basicHtml = File.ReadAllText(Path.Combine("Fixtures", "search_results_basic.html"));
        _instantAnswerHtml = File.ReadAllText(Path.Combine("Fixtures", "search_results_with_instant_answer.html"));
        _paginationHtml = File.ReadAllText(Path.Combine("Fixtures", "search_results_with_pagination.html"));
        _parser = new HtmlResultParser();
    }

    [Benchmark(Description = "Parse basic search results")]
    public object ParseBasicResults() => _parser.Parse(_basicHtml);

    [Benchmark(Description = "Parse results with instant answer")]
    public object ParseInstantAnswer() => _parser.Parse(_instantAnswerHtml);

    [Benchmark(Description = "Parse results with pagination")]
    public object ParsePagination() => _parser.Parse(_paginationHtml);
}
