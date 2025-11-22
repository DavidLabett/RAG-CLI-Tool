using Spectre.Console;
using Spectre.Console.Cli;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using SecondBrain.Models;
using SecondBrain.Services;

namespace SecondBrain.Commands;

/// Command to check system status and health
public class StatusCommand : AsyncCommand<StatusSettings>
{
    private readonly IOptions<AppSettings> _appSettings;
    private readonly ILogger<StatusCommand> _logger;
    private readonly IKernelMemory _memory;
    private readonly SyncState _syncState;

    public StatusCommand(
        IOptions<AppSettings> appSettings,
        ILogger<StatusCommand> logger,
        IKernelMemory memory,
        SyncState syncState)
    {
        _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _syncState = syncState ?? throw new ArgumentNullException(nameof(syncState));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, StatusSettings settings)
    {
        try
        {
            var ragSettings = _appSettings.Value.RAG;
            var cliSettings = _appSettings.Value.CLI;

            // Single compact table with all information
            await DisplayCompactStatusTable(ragSettings, cliSettings);

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking status: {Message}", ex.Message);
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private async Task DisplayCompactStatusTable(AppSettings.RAGSettings ragSettings, AppSettings.CLISettings cliSettings)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.LightGreen)
            .AddColumn("[bold cyan]Category[/]")
            .AddColumn("[bold cyan]Setting[/]")
            .AddColumn("[bold cyan]Value[/]")
            .Width(100);

        // Knowledge Base
        table.AddRow("[bold cyan]Knowledge Base[/]", "[dim]Index Name[/]", $"[white]{ragSettings.IndexName}[/]");

        // Check index accessibility
        try
        {
            var testSearch = await _memory.SearchAsync(
                query: "test",
                index: ragSettings.IndexName,
                limit: 1,
                minRelevance: 0.0f);
            table.AddRow("[bold cyan]Knowledge Base[/]", "[dim]Status[/]", "[green]Accessible[/]");
        }
        catch (Exception ex)
        {
            table.AddRow("[bold cyan]Knowledge Base[/]", "[dim]Status[/]", $"[red]Error: {ex.Message}[/]");
        }

        // Sync Information
        try
        {
            var lastRun = _syncState.GetLastRun();
            var hasStored = File.Exists(_appSettings.Value.RAG.StoredLastRun ?? "");

            table.AddRow("[bold blue]Sync[/]", "[dim]Last Sync[/]", $"[white]{lastRun:yyyy-MM-dd HH:mm:ss}[/]");
            table.AddRow("[bold blue]Sync[/]", "[dim]State File[/]", hasStored ? "[green]Exists[/]" : "[yellow]warning: Not found[/]");
            table.AddRow("[bold blue]Sync[/]", "[dim]Document Folder[/]", $"[white]{ragSettings.DocumentFolderPath}[/]");
        }
        catch (Exception ex)
        {
            table.AddRow("[bold blue]Sync[/]", "[dim]Last Sync[/]", $"[red]Error: {ex.Message}[/]");
        }

        // RAG Configuration
        var mode = ragSettings.Mode?.ToLower() ?? "local";
        var modeDisplay = mode == "online" ? "[green]online[/]" : "[yellow]local[/]";
        table.AddRow("[bold magenta]RAG[/]", "[dim]Mode[/]", modeDisplay);
        table.AddRow("[bold magenta]RAG[/]", "[dim]Qdrant URL[/]", $"[white]{ragSettings.QdrantUrl}[/]");
        table.AddRow("[bold magenta]RAG[/]", "[dim]Ollama URL[/]", $"[white]{ragSettings.OllamaUrl}[/]");
        table.AddRow("[bold magenta]RAG[/]", "[dim]Text Model (chat)[/]", $"[cyan]{ragSettings.TextModel.Model}[/]");
        table.AddRow("[bold magenta]RAG[/]", "[dim]Embedding Model[/]", $"[cyan]{ragSettings.EmbeddingModel.Model}[/]");
        table.AddRow("[bold magenta]RAG[/]", "[dim]Min Relevance[/]", $"[white]{ragSettings.MinRelevance:F2}[/]");

        // CloudFlare Configuration (if online mode)
        if (mode == "online")
        {
            var cloudFlare = ragSettings.CloudFlare;
            table.AddRow("[bold green]CloudFlare[/]", "[dim]Generation Model[/]", $"[cyan]{cloudFlare.GenerationModel}[/]");
            table.AddRow("[bold green]CloudFlare[/]", "[dim]Account ID[/]", $"[white]{(string.IsNullOrEmpty(cloudFlare.AccountId) ? "Not set" : "***")}[/]");
            table.AddRow("[bold green]CloudFlare[/]", "[dim]API Token[/]", $"[white]{(string.IsNullOrEmpty(cloudFlare.ApiToken) ? "Not set" : "***")}[/]");
        }

        // CLI Configuration
        table.AddRow("[bold yellow]CLI[/]", "[dim]Application Name[/]", $"[white]{cliSettings.ApplicationName}[/]");
        table.AddRow("[bold yellow]CLI[/]", "[dim]Version[/]", $"[white]{cliSettings.ApplicationVersion}[/]");
        table.AddRow("[bold yellow]CLI[/]", "[dim]LLM Model (llm)[/]", $"[green]{cliSettings.LlmModel}[/]");
        table.AddRow("[bold yellow]CLI[/]", "[dim]Author[/]", $"[white]{cliSettings.Author ?? "David Labett"}[/]");

        var panel = new Panel(table)
            .Header("[bold cyan]System Status[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .BorderStyle(Style.Parse("bold cyan"))
            .Padding(1, 1);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

}

