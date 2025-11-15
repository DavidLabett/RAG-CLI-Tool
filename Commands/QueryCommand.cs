using Spectre.Console;
using Spectre.Console.Cli;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using SecondBrain.Models;
using SecondBrain.Services;

namespace SecondBrain.Commands;

/// <summary>
/// Command to query the knowledge base
/// </summary>
public class QueryCommand : AsyncCommand<QuerySettings>
{
    private readonly IOptions<AppSettings> _appSettings;
    private readonly ILogger<QueryCommand> _logger;
    private readonly IKernelMemory _memory;
    private readonly OllamaService _ollamaService;
    private readonly RagResultService _ragResultService;

    public QueryCommand(
        IOptions<AppSettings> appSettings,
        ILogger<QueryCommand> logger,
        IKernelMemory memory,
        OllamaService ollamaService,
        RagResultService ragResultService)
    {
        _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _ollamaService = ollamaService ?? throw new ArgumentNullException(nameof(ollamaService));
        _ragResultService = ragResultService ?? throw new ArgumentNullException(nameof(ragResultService));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, QuerySettings settings)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(settings.Question))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Question is required");
                AnsiConsole.MarkupLine("Usage: [cyan]2b query[/] <question> [options]");
                return 1;
            }

            var ragSettings = _appSettings.Value.RAG;
            var indexName = ragSettings.IndexName;
            var minRelevance = ragSettings.MinRelevance;

            AnsiConsole.MarkupLine($"[cyan]Query:[/] [yellow]{settings.Question}[/]\n");

            // Search the knowledge base with status spinner
            var searchResults = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync("Searching knowledge base...", async ctx =>
                {
                    ctx.Status("[cyan]Searching for relevant documents...[/]");
                    return await _memory.SearchAsync(
                        query: settings.Question,
                        index: indexName,
                        limit: settings.Limit,
                        minRelevance: minRelevance);
                });

            var resultCount = searchResults.Results.Count();

            // Store results for tree command (even if empty, so tree command knows a query was run)
            _ragResultService.StoreLatestResults(searchResults);

            // If no results, show message and exit
            if (resultCount == 0)
            {
                var panel = new Panel("[yellow]No relevant sources found in the knowledge base.[/]\n\n" +
                    "[dim]This might indicate:[/]\n" +
                    "• The embedding search didn't find matching content\n" +
                    "• Try using different keywords or phrasing\n" +
                    "• The knowledge base might not contain the requested information")
                    .Header("[bold yellow]No Results[/]")
                    .Border(BoxBorder.Rounded)
                    .BorderColor(Color.Yellow1)
                    .BorderStyle(Style.Parse("bold yellow"))
                    .Padding(1, 1);
                AnsiConsole.Write(panel);
                AnsiConsole.WriteLine();
                return 0;
            }

            // Build context from retrieved results
            var contextBuilder = new System.Text.StringBuilder();
            foreach (var result in searchResults.Results)
            {
                if (result.Partitions.Any())
                {
                    foreach (var partition in result.Partitions.Take(3)) // Limit to top 3 partitions per result
                    {
                        contextBuilder.AppendLine(partition.Text);
                        contextBuilder.AppendLine();
                    }
                }
            }
            var retrievedContext = contextBuilder.ToString();

            // Show search results if --no-llm or --sources
            if (settings.NoLlm || settings.Sources)
            {
                DisplaySearchResultsAsync(searchResults, settings);
            }

            // Generate answer if not --no-llm
            if (!settings.NoLlm)
            {
                var customPrompt = PromptTemplate.BuildPrompt(settings.Question, retrievedContext);

                // Show status while generating answer
                var answer = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("green"))
                    .StartAsync("Generating answer...", async ctx =>
                    {
                        ctx.Status("[green]Processing with LLM...[/]");
                        return await _ollamaService.GenerateAnswerAsync(customPrompt);
                    });

                var answerPanel = new Panel(answer)
                    .Header("[bold green]Answer[/]")
                    .Border(BoxBorder.Rounded)
                    .BorderColor(Color.Green1)
                    .BorderStyle(Style.Parse("bold green"))
                    .Padding(1, 1);
                AnsiConsole.Write(answerPanel);
                AnsiConsole.WriteLine();
            }

            // Show sources if requested
            if (settings.Sources)
            {
                DisplaySourcesAsync(searchResults);
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing query: {Message}", ex.Message);
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private void DisplaySearchResultsAsync(SearchResult searchResults, QuerySettings settings)
    {
        // Create a table for search results
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.LightGreen)
            .AddColumn("[bold cyan]Document[/]")
            .AddColumn("[bold cyan]Relevance[/]")
            .AddColumn("[bold cyan]Text Preview[/]")
            .Width(100);

        foreach (var result in searchResults.Results)
        {
            foreach (var partition in result.Partitions.Take(3))
            {
                var preview = partition.Text.Length > 100 
                    ? partition.Text.Substring(0, 100) + "..." 
                    : partition.Text;
                
                table.AddRow(
                    $"[white]{result.DocumentId ?? "Unknown"}[/]",
                    $"[cyan]{partition.Relevance:F3}[/]",
                    $"[dim]{preview}[/]"
                );
            }
        }

        var panel = new Panel(table)
            .Header("[bold cyan]Search Results[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .BorderStyle(Style.Parse("bold cyan"))
            .Padding(1, 1);
        
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private void DisplaySourcesAsync(SearchResult searchResults)
    {
        var sources = searchResults.Results
            .Select(r => r.DocumentId ?? "Unknown")
            .Distinct()
            .ToList();

        if (sources.Any())
        {
            var sourcesPanel = new Panel(string.Join("\n", sources.Select(s => $"• {s}")))
                .Header("[bold cyan]Sources[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Cyan1)
                .BorderStyle(Style.Parse("bold cyan"))
                .Padding(1, 1);
            AnsiConsole.Write(sourcesPanel);
            AnsiConsole.WriteLine();
        }
    }

}

