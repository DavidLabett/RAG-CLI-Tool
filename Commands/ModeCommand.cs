using Spectre.Console;
using Spectre.Console.Cli;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using SecondBrain.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SecondBrain.Commands;

/// <summary>
/// Command to quickly switch between local and online modes
/// </summary>
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
        // Walk up from base directory to find project root (where .csproj file is)
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
        
        // Fallback to base directory
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
            
            // Check if mode is already set
            if (currentMode == mode)
            {
                var modeDisplay = mode == "online" ? "[green]online[/]" : "[yellow]local[/]";
                AnsiConsole.MarkupLine($"[yellow]Mode is already set to {modeDisplay}[/]");
                return 0;
            }

            // Update configuration
            UpdateModeInConfig(mode);

            // Display success message
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
            // Verify the file exists
            if (!File.Exists(_configFilePath))
            {
                throw new FileNotFoundException($"Configuration file not found: {_configFilePath}");
            }

            _logger.LogInformation("Updating mode to {Mode} in: {ConfigPath}", mode, _configFilePath);

            // Read the entire JSON file to preserve other sections
            var jsonContent = File.ReadAllText(_configFilePath);
            var jsonNode = JsonNode.Parse(jsonContent);
            
            if (jsonNode == null)
            {
                throw new InvalidOperationException("Failed to parse appsettings.json");
            }

            var root = jsonNode.AsObject();

            // Ensure AppSettings section exists
            if (root["AppSettings"] == null)
            {
                root["AppSettings"] = new JsonObject();
            }

            var appSettings = root["AppSettings"]!.AsObject();
            
            // Ensure RAG section exists
            if (appSettings["RAG"] == null)
            {
                appSettings["RAG"] = new JsonObject();
            }

            var rag = appSettings["RAG"]!.AsObject();

            // Update Mode
            rag["Mode"] = mode;

            // Write back with pretty formatting
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var finalJson = jsonNode.ToJsonString(options);
            
            // Write to a temporary file first, then replace (atomic operation)
            var tempFile = _configFilePath + ".tmp";
            File.WriteAllText(tempFile, finalJson);
            
            // Verify the temp file was written correctly
            if (!File.Exists(tempFile))
            {
                throw new IOException("Failed to write temporary configuration file");
            }
            
            // Replace the original file
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

