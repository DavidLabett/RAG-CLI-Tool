using Spectre.Console;
using Spectre.Console.Cli;
using Microsoft.Extensions.Options;
using SecondBrain.Models;

namespace SecondBrain.Commands;

/// <summary>
/// Simple version command to test the CLI setup
/// </summary>
public class VersionCommand : Command<BaseSettings>
{
    private readonly IOptions<AppSettings> _appSettings;

    public VersionCommand(IOptions<AppSettings> appSettings)
    {
        _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
    }

    public override int Execute(CommandContext context, BaseSettings settings)
    {
        var cliSettings = _appSettings.Value.CLI;
        var ragSettings = _appSettings.Value.RAG;

        // ASCII Art Banner
        AnsiConsole.WriteLine(@"
        ▄█████ ▄▄▄▄▄  ▄▄▄▄  ▄▄▄  ▄▄  ▄▄ ▄▄▄▄    █████▄ ▄▄▄▄   ▄▄▄  ▄▄ ▄▄  ▄▄ 
        ▀▀▀▄▄▄ ██▄▄  ██▀▀▀ ██▀██ ███▄██ ██▀██   ██▄▄██ ██▄█▄ ██▀██ ██ ███▄██ 
        █████▀ ██▄▄▄ ▀████ ▀███▀ ██ ▀██ ████▀   ██▄▄█▀ ██ ██ ██▀██ ██ ██ ▀██ 
        ");

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.LightGreen)
            .AddColumn("[bold cyan]Category[/]")
            .AddColumn("[bold cyan]Value[/]")
            .Width(80);

        table.AddRow("[bold cyan]Application[/]", $"[white]{cliSettings.ApplicationName} v{cliSettings.ApplicationVersion}[/]");
        table.AddRow("[bold cyan]Author[/]", $"[white]{cliSettings.Author ?? "David Labett"}[/]");
        table.AddRow("[bold magenta]RAG System[/]", "[white]KernelMemory, Qdrant & Ollama[/]");
        table.AddRow("[bold magenta]Embedding Model[/]", $"[cyan]{ragSettings.EmbeddingModel.Model}[/]");
        table.AddRow("[bold magenta]Text Model[/]", $"[cyan]{ragSettings.TextModel.Model}[/]");
        table.AddRow("[bold yellow]LLM Model[/]", $"[green]{cliSettings.LlmModel}[/]");
        table.AddRow("[bold magenta]Vector Database[/]", "[white]Qdrant[/]");

        var panel = new Panel(table)
            .Header("[bold cyan]Version Information[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .BorderStyle(Style.Parse("bold cyan"))
            .Padding(1, 1);
        
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        return 0;
    }
}

