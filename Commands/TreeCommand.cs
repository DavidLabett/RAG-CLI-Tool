using Spectre.Console;
using Spectre.Console.Cli;
using Microsoft.Extensions.Logging;
using SecondBrain.Services;

namespace SecondBrain.Commands;

/// Command to display the latest RAG sources and chunks as a tree structure
public class TreeCommand : Command<TreeSettings>
{
    private readonly ILogger<TreeCommand> _logger;
    private readonly RagResultService _ragResultService;

    public TreeCommand(
        ILogger<TreeCommand> logger,
        RagResultService ragResultService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ragResultService = ragResultService ?? throw new ArgumentNullException(nameof(ragResultService));
    }

    public override int Execute(CommandContext context, TreeSettings settings)
    {
        try
        {
            var storedData = _ragResultService.GetStoredData();

            if (storedData == null)
            {
                var panel = new Panel("[yellow]No RAG results available.[/]\n\n" +
                    "[dim]Run a rag command first to generate results.[/]")
                    .Header("[bold yellow]No Results[/]")
                    .Border(BoxBorder.Rounded)
                    .BorderColor(Color.Yellow1)
                    .BorderStyle(Style.Parse("bold yellow"))
                    .Padding(1, 1);
                AnsiConsole.Write(panel);
                AnsiConsole.WriteLine();
                return 0;
            }

            if (storedData.Results == null || !storedData.Results.Any())
            {
                var panel = new Panel("[yellow]No RAG results available.[/]\n\n" +
                    "[dim]The last query returned no results.[/]")
                    .Header("[bold yellow]No Results[/]")
                    .Border(BoxBorder.Rounded)
                    .BorderColor(Color.Yellow1)
                    .BorderStyle(Style.Parse("bold yellow"))
                    .Padding(1, 1);
                AnsiConsole.Write(panel);
                AnsiConsole.WriteLine();
                return 0;
            }

            _logger.LogDebug("Tree command: Found {Count} results", storedData.Results.Count);

            DisplaySourcesTree(storedData);

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error displaying tree: {Message}", ex.Message);
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private void DisplaySourcesTree(RagResultService.StoredResultData storedData)
    {
        if (storedData.Results == null || !storedData.Results.Any())
        {
            return;
        }

        var tree = new Tree("[bold cyan]Source Tree[/]");

        foreach (var result in storedData.Results)
        {
            var documentId = result.DocumentId ?? "Unknown";
            var documentNode = tree.AddNode($"[bold yellow]{documentId}[/]");

            if (result.Partitions != null && result.Partitions.Any())
            {
                var partitionIndex = 0;
                foreach (var partition in result.Partitions)
                {
                    partitionIndex++;
                    var relevanceColor = partition.Relevance >= 0.7 ? "green" : partition.Relevance >= 0.5 ? "yellow" : "red";
                    var chunkPreview = partition.Text.Length > 80
                        ? partition.Text.Substring(0, 80) + "..."
                        : partition.Text;

                    var chunkNode = documentNode.AddNode(
                        $"[{relevanceColor}]Chunk {partitionIndex}[/] [dim](relevance: {partition.Relevance:F3})[/]");
                    chunkNode.AddNode($"[dim]{chunkPreview}[/]");
                }
            }
            else
            {
                documentNode.AddNode("[dim]No chunks found[/]");
            }
        }

        var treePanel = new Panel(tree)
            .Header("[bold cyan]Sources & Chunks[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .BorderStyle(Style.Parse("bold cyan"))
            .Padding(1, 1);

        AnsiConsole.Write(treePanel);
        AnsiConsole.WriteLine();
    }
}

