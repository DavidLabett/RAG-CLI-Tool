using Spectre.Console;
using Spectre.Console.Cli;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using SecondBrain.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SecondBrain.Commands;

/// Command to quickly switch between local and online modes
public class ModeCommand : Command<ModeSettings>
{
    private readonly IOptions<AppSettings> _appSettings;
    private readonly ILogger<ModeCommand> _logger;
    private readonly string _configFilePath;

    public ModeCommand(
        IOptions<AppSettings> appSettings,
        ILogger<ModeCommand> logger)
    {
        _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configFilePath = FindConfigFile();
    }

    private string FindConfigFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (directory.GetFiles("*.csproj").Length > 0)
            {
                var configPath = Path.Combine(directory.FullName, "appsettings.json");
                if (File.Exists(configPath))
                {
                    return configPath;
                }
            }
            directory = directory.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    public override int Execute(CommandContext context, ModeSettings settings)
    {
        try
        {
            // Validate mode
            var mode = settings.Mode?.ToLower().Trim();
            if (string.IsNullOrWhiteSpace(mode))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Mode is required");
                AnsiConsole.MarkupLine("Usage: [cyan]2b mode[/] <local|online>");
                return 1;
            }

            if (mode != "local" && mode != "online")
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid mode '{settings.Mode}'. Must be 'local' or 'online'");
                AnsiConsole.MarkupLine("Usage: [cyan]2b mode[/] <local|online>");
                return 1;
            }

            // Get current mode
            var currentMode = _appSettings.Value.RAG.Mode?.ToLower() ?? "local";

            if (currentMode == mode)
            {
                var modeDisplay = mode == "online" ? "[green]online[/]" : "[yellow]local[/]";
                AnsiConsole.MarkupLine($"[yellow]Mode is already set to {modeDisplay}[/]");
                return 0;
            }

            UpdateModeInConfig(mode);

            var newModeDisplay = mode == "online" ? "[green]online[/]" : "[yellow]local[/]";
            var oldModeDisplay = currentMode == "online" ? "[green]online[/]" : "[yellow]local[/]";

            AnsiConsole.MarkupLine($"[green]Success[/] Mode changed from {oldModeDisplay} to {newModeDisplay}");
            AnsiConsole.MarkupLine("[yellow]warning[/] [dim]Note: Restart the application for changes to take effect[/]");

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing mode: {Message}", ex.Message);
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private void UpdateModeInConfig(string mode)
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                throw new FileNotFoundException($"Configuration file not found: {_configFilePath}");
            }

            _logger.LogInformation("Updating mode to {Mode} in: {ConfigPath}", mode, _configFilePath);

            var jsonContent = File.ReadAllText(_configFilePath);
            var jsonNode = JsonNode.Parse(jsonContent);

            if (jsonNode == null)
            {
                throw new InvalidOperationException("Failed to parse appsettings.json");
            }

            var root = jsonNode.AsObject();

            if (root["AppSettings"] == null)
            {
                root["AppSettings"] = new JsonObject();
            }

            var appSettings = root["AppSettings"]!.AsObject();

            if (appSettings["RAG"] == null)
            {
                appSettings["RAG"] = new JsonObject();
            }

            var rag = appSettings["RAG"]!.AsObject();

            // Update Mode
            rag["Mode"] = mode;

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var finalJson = jsonNode.ToJsonString(options);

            var tempFile = _configFilePath + ".tmp";
            File.WriteAllText(tempFile, finalJson);

            if (!File.Exists(tempFile))
            {
                throw new IOException("Failed to write temporary configuration file");
            }

            File.Move(tempFile, _configFilePath, overwrite: true);

            _logger.LogInformation("Mode updated successfully to: {Mode}", mode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating mode in {ConfigPath}: {Message}", _configFilePath, ex.Message);
            throw;
        }
    }
}

