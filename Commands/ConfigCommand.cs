using Spectre.Console;
using Spectre.Console.Cli;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using SecondBrain.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SecondBrain.Commands;

/// <summary>
/// Command to manage configuration interactively
/// </summary>
public class ConfigCommand : Command<ConfigSettings>
{
    private readonly IOptions<AppSettings> _appSettings;
    private readonly ILogger<ConfigCommand> _logger;
    private readonly string _configFilePath;

    public ConfigCommand(
        IOptions<AppSettings> appSettings,
        ILogger<ConfigCommand> logger)
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

    public override int Execute(CommandContext context, ConfigSettings settings)
    {
        try
        {
            // Main interactive configuration menu
            while (true)
            {
                var category = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[bold cyan]Configuration Management[/]")
                        .PageSize(10)
                        .AddChoices(new[]
                        {
                            "• View Current Configuration",
                            "• Edit RAG Settings",
                            "• Edit CLI Settings",
                            "• Edit Chunking Settings",
                            "• Edit Online Mode Settings",
                            "x Exit"
                        }));

                if (category == "x Exit")
                {
                    break;
                }

                switch (category)
                {
                    case "• View Current Configuration":
                        DisplayCurrentConfiguration();
                        break;
                    case "• Edit RAG Settings":
                        EditRAGSettings();
                        break;
                    case "• Edit CLI Settings":
                        EditCLISettings();
                        break;
                    case "• Edit Chunking Settings":
                        EditChunkingSettings();
                        break;
                    case "• Edit Online Mode Settings":
                        EditOnlineModeSettings();
                        break;
                }

                AnsiConsole.WriteLine();
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error managing configuration: {Message}", ex.Message);
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private void DisplayCurrentConfiguration()
    {
        var ragSettings = _appSettings.Value.RAG;
        var cliSettings = _appSettings.Value.CLI;

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.LightGreen)
            .AddColumn("[bold cyan]Category[/]")
            .AddColumn("[bold cyan]Setting[/]")
            .AddColumn("[bold cyan]Value[/]")
            .Width(100);

        // RAG Settings
        table.AddRow("[bold magenta]RAG[/]", "[dim]Qdrant URL[/]", $"[white]{ragSettings.QdrantUrl}[/]");
        table.AddRow("[bold magenta]RAG[/]", "[dim]Ollama URL[/]", $"[white]{ragSettings.OllamaUrl}[/]");
        table.AddRow("[bold magenta]RAG[/]", "[dim]Index Name[/]", $"[white]{ragSettings.IndexName}[/]");
        table.AddRow("[bold magenta]RAG[/]", "[dim]Document Folder[/]", $"[white]{ragSettings.DocumentFolderPath}[/]");
        table.AddRow("[bold magenta]RAG[/]", "[dim]Text Model[/]", $"[cyan]{ragSettings.TextModel.Model}[/]");
        table.AddRow("[bold magenta]RAG[/]", "[dim]Embedding Model[/]", $"[cyan]{ragSettings.EmbeddingModel.Model}[/]");
        table.AddRow("[bold magenta]RAG[/]", "[dim]Min Relevance[/]", $"[white]{ragSettings.MinRelevance:F2}[/]");
        table.AddRow("[bold magenta]RAG[/]", "[dim]Answer Tokens[/]", $"[white]{ragSettings.AnswerTokens}[/]");
        table.AddRow("[bold magenta]RAG[/]", "[dim]Mode[/]", $"[cyan]{ragSettings.Mode ?? "local"}[/]");

        // CloudFlare Settings (if mode is online)
        if (ragSettings.Mode?.ToLower() == "online")
        {
            var cloudFlare = ragSettings.CloudFlare;
            table.AddRow("[bold green]CloudFlare[/]", "[dim]Account ID[/]", $"[white]{(string.IsNullOrEmpty(cloudFlare.AccountId) ? "Not set" : "***")}[/]");
            table.AddRow("[bold green]CloudFlare[/]", "[dim]API Token[/]", $"[white]{(string.IsNullOrEmpty(cloudFlare.ApiToken) ? "Not set" : "***")}[/]");
            table.AddRow("[bold green]CloudFlare[/]", "[dim]Generation Model[/]", $"[cyan]{cloudFlare.GenerationModel}[/]");
        }

        // Chunking Settings
        table.AddRow("[bold yellow]Chunking[/]", "[dim]Max Tokens Per Chunk[/]", $"[white]{ragSettings.Chunking.MaxTokensPerChunk}[/]");
        table.AddRow("[bold yellow]Chunking[/]", "[dim]Overlap Tokens[/]", $"[white]{ragSettings.Chunking.OverlapTokens}[/]");
        table.AddRow("[bold yellow]Chunking[/]", "[dim]Min Chars Per Sentence[/]", $"[white]{ragSettings.Chunking.MinCharactersPerSentence}[/]");
        table.AddRow("[bold yellow]Chunking[/]", "[dim]Min Sentences Per Chunk[/]", $"[white]{ragSettings.Chunking.MinSentencesPerChunk}[/]");

        // CLI Settings
        table.AddRow("[bold blue]CLI[/]", "[dim]Application Name[/]", $"[white]{cliSettings.ApplicationName}[/]");
        table.AddRow("[bold blue]CLI[/]", "[dim]Version[/]", $"[white]{cliSettings.ApplicationVersion}[/]");
        table.AddRow("[bold blue]CLI[/]", "[dim]Author[/]", $"[white]{cliSettings.Author ?? "N/A"}[/]");
        table.AddRow("[bold blue]CLI[/]", "[dim]LLM Model[/]", $"[green]{cliSettings.LlmModel}[/]");

        var panel = new Panel(table)
            .Header("[bold cyan]Current Configuration[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .BorderStyle(Style.Parse("bold cyan"))
            .Padding(1, 1);
        
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
        
        AnsiConsole.Prompt(
            new TextPrompt<string>("[dim]Press Enter to continue...[/]")
                .AllowEmpty());
    }

    private void EditRAGSettings()
    {
        var ragSettings = _appSettings.Value.RAG;
        var hasChanges = false;

        while (true)
        {
            var setting = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold magenta]Edit RAG Settings[/]")
                    .PageSize(10)
                    .AddChoices(new[]
                    {
                        $"Qdrant URL: [cyan]{ragSettings.QdrantUrl}[/]",
                        $"Ollama URL: [cyan]{ragSettings.OllamaUrl}[/]",
                        $"Index Name: [cyan]{ragSettings.IndexName}[/]",
                        $"Document Folder: [cyan]{ragSettings.DocumentFolderPath}[/]",
                        $"Text Model: [cyan]{ragSettings.TextModel.Model}[/]",
                        $"Embedding Model: [cyan]{ragSettings.EmbeddingModel.Model}[/]",
                        $"Min Relevance: [cyan]{ragSettings.MinRelevance:F2}[/]",
                        $"Answer Tokens: [cyan]{ragSettings.AnswerTokens}[/]",
                        "← Back"
                    }));

            if (setting == "← Back")
            {
                break;
            }

            if (setting.StartsWith("Qdrant URL:"))
            {
                var newValue = AnsiConsole.Prompt(
                    new TextPrompt<string>("[cyan]Qdrant URL:[/]")
                        .DefaultValue(ragSettings.QdrantUrl)
                        .Validate(url => Uri.TryCreate(url, UriKind.Absolute, out _) ? ValidationResult.Success() : ValidationResult.Error("[red]Invalid URL[/]")));
                ragSettings.QdrantUrl = newValue;
                hasChanges = true;
            }
            else if (setting.StartsWith("Ollama URL:"))
            {
                var newValue = AnsiConsole.Prompt(
                    new TextPrompt<string>("[cyan]Ollama URL:[/]")
                        .DefaultValue(ragSettings.OllamaUrl)
                        .Validate(url => Uri.TryCreate(url, UriKind.Absolute, out _) ? ValidationResult.Success() : ValidationResult.Error("[red]Invalid URL[/]")));
                ragSettings.OllamaUrl = newValue;
                hasChanges = true;
            }
            else if (setting.StartsWith("Index Name:"))
            {
                var newValue = AnsiConsole.Prompt(
                    new TextPrompt<string>("[cyan]Index Name:[/]")
                        .DefaultValue(ragSettings.IndexName)
                        .Validate(name => !string.IsNullOrWhiteSpace(name) ? ValidationResult.Success() : ValidationResult.Error("[red]Index name cannot be empty[/]")));
                ragSettings.IndexName = newValue;
                hasChanges = true;
            }
            else if (setting.StartsWith("Document Folder:"))
            {
                var newValue = AnsiConsole.Prompt(
                    new TextPrompt<string>("[cyan]Document Folder Path:[/]")
                        .DefaultValue(ragSettings.DocumentFolderPath)
                        .Validate(path => !string.IsNullOrWhiteSpace(path) ? ValidationResult.Success() : ValidationResult.Error("[red]Path cannot be empty[/]")));
                ragSettings.DocumentFolderPath = newValue;
                hasChanges = true;
            }
            else if (setting.StartsWith("Text Model:"))
            {
                var newValue = AnsiConsole.Prompt(
                    new TextPrompt<string>("[cyan]Text Model:[/]")
                        .DefaultValue(ragSettings.TextModel.Model)
                        .Validate(model => !string.IsNullOrWhiteSpace(model) ? ValidationResult.Success() : ValidationResult.Error("[red]Model name cannot be empty[/]")));
                ragSettings.TextModel.Model = newValue;
                hasChanges = true;
            }
            else if (setting.StartsWith("Embedding Model:"))
            {
                var newValue = AnsiConsole.Prompt(
                    new TextPrompt<string>("[cyan]Embedding Model:[/]")
                        .DefaultValue(ragSettings.EmbeddingModel.Model)
                        .Validate(model => !string.IsNullOrWhiteSpace(model) ? ValidationResult.Success() : ValidationResult.Error("[red]Model name cannot be empty[/]")));
                ragSettings.EmbeddingModel.Model = newValue;
                hasChanges = true;
            }
            else if (setting.StartsWith("Min Relevance:"))
            {
                var newValue = AnsiConsole.Prompt(
                    new TextPrompt<float>("[cyan]Min Relevance (0.0 - 1.0):[/]")
                        .DefaultValue(ragSettings.MinRelevance)
                        .Validate(value => value >= 0.0f && value <= 1.0f ? ValidationResult.Success() : ValidationResult.Error("[red]Value must be between 0.0 and 1.0[/]")));
                ragSettings.MinRelevance = newValue;
                hasChanges = true;
            }
            else if (setting.StartsWith("Answer Tokens:"))
            {
                var newValue = AnsiConsole.Prompt(
                    new TextPrompt<int>("[cyan]Answer Tokens:[/]")
                        .DefaultValue(ragSettings.AnswerTokens)
                        .Validate(value => value > 0 ? ValidationResult.Success() : ValidationResult.Error("[red]Value must be greater than 0[/]")));
                ragSettings.AnswerTokens = newValue;
                hasChanges = true;
            }

            AnsiConsole.MarkupLine($"[green]Success[/] Setting updated!");
            AnsiConsole.WriteLine();
        }

        if (hasChanges)
        {
            SaveConfiguration();
        }
    }

    private void EditCLISettings()
    {
        var cliSettings = _appSettings.Value.CLI;
        var hasChanges = false;

        while (true)
        {
            var setting = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold blue]Edit CLI Settings[/]")
                    .PageSize(10)
                    .AddChoices(new[]
                    {
                        $"Application Name: [cyan]{cliSettings.ApplicationName}[/]",
                        $"Version: [cyan]{cliSettings.ApplicationVersion}[/]",
                        $"Author: [cyan]{cliSettings.Author ?? "N/A"}[/]",
                        $"LLM Model: [cyan]{cliSettings.LlmModel}[/]",
                        "← Back"
                    }));

            if (setting == "← Back")
            {
                break;
            }

            if (setting.StartsWith("Application Name:"))
            {
                var newValue = AnsiConsole.Prompt(
                    new TextPrompt<string>("[cyan]Application Name:[/]")
                        .DefaultValue(cliSettings.ApplicationName)
                        .Validate(name => !string.IsNullOrWhiteSpace(name) ? ValidationResult.Success() : ValidationResult.Error("[red]Application name cannot be empty[/]")));
                cliSettings.ApplicationName = newValue;
                hasChanges = true;
            }
            else if (setting.StartsWith("Version:"))
            {
                var newValue = AnsiConsole.Prompt(
                    new TextPrompt<string>("[cyan]Version:[/]")
                        .DefaultValue(cliSettings.ApplicationVersion)
                        .Validate(version => !string.IsNullOrWhiteSpace(version) ? ValidationResult.Success() : ValidationResult.Error("[red]Version cannot be empty[/]")));
                cliSettings.ApplicationVersion = newValue;
                hasChanges = true;
            }
            else if (setting.StartsWith("Author:"))
            {
                var newValue = AnsiConsole.Prompt(
                    new TextPrompt<string>("[cyan]Author (press Enter to clear):[/]")
                        .DefaultValue(cliSettings.Author ?? "")
                        .AllowEmpty());
                cliSettings.Author = string.IsNullOrWhiteSpace(newValue) ? null : newValue;
                hasChanges = true;
            }
            else if (setting.StartsWith("LLM Model:"))
            {
                var newValue = AnsiConsole.Prompt(
                    new TextPrompt<string>("[cyan]LLM Model:[/]")
                        .DefaultValue(cliSettings.LlmModel)
                        .Validate(model => !string.IsNullOrWhiteSpace(model) ? ValidationResult.Success() : ValidationResult.Error("[red]Model name cannot be empty[/]")));
                cliSettings.LlmModel = newValue;
                hasChanges = true;
            }

            AnsiConsole.MarkupLine($"[green]Success[/] Setting updated!");
            AnsiConsole.WriteLine();
        }

        if (hasChanges)
        {
            SaveConfiguration();
        }
    }

    private void EditChunkingSettings()
    {
        var chunking = _appSettings.Value.RAG.Chunking;
        var hasChanges = false;

        while (true)
        {
            var setting = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold yellow]Edit Chunking Settings[/]")
                    .PageSize(10)
                    .AddChoices(new[]
                    {
                        $"Max Tokens Per Chunk: [cyan]{chunking.MaxTokensPerChunk}[/]",
                        $"Overlap Tokens: [cyan]{chunking.OverlapTokens}[/]",
                        $"Min Chars Per Sentence: [cyan]{chunking.MinCharactersPerSentence}[/]",
                        $"Min Sentences Per Chunk: [cyan]{chunking.MinSentencesPerChunk}[/]",
                        "← Back"
                    }));

            if (setting == "← Back")
            {
                break;
            }

            if (setting.StartsWith("Max Tokens Per Chunk:"))
            {
                var newValue = AnsiConsole.Prompt(
                    new TextPrompt<int>("[cyan]Max Tokens Per Chunk (1-8192):[/]")
                        .DefaultValue(chunking.MaxTokensPerChunk)
                        .Validate(value => value >= 1 && value <= 8192 ? ValidationResult.Success() : ValidationResult.Error("[red]Value must be between 1 and 8192[/]")));
                chunking.MaxTokensPerChunk = newValue;
                hasChanges = true;
            }
            else if (setting.StartsWith("Overlap Tokens:"))
            {
                var newValue = AnsiConsole.Prompt(
                    new TextPrompt<int>("[cyan]Overlap Tokens (0-1024):[/]")
                        .DefaultValue(chunking.OverlapTokens)
                        .Validate(value => value >= 0 && value <= 1024 ? ValidationResult.Success() : ValidationResult.Error("[red]Value must be between 0 and 1024[/]")));
                chunking.OverlapTokens = newValue;
                hasChanges = true;
            }
            else if (setting.StartsWith("Min Chars Per Sentence:"))
            {
                var newValue = AnsiConsole.Prompt(
                    new TextPrompt<int>("[cyan]Min Characters Per Sentence (1-1000):[/]")
                        .DefaultValue(chunking.MinCharactersPerSentence)
                        .Validate(value => value >= 1 && value <= 1000 ? ValidationResult.Success() : ValidationResult.Error("[red]Value must be between 1 and 1000[/]")));
                chunking.MinCharactersPerSentence = newValue;
                hasChanges = true;
            }
            else if (setting.StartsWith("Min Sentences Per Chunk:"))
            {
                var newValue = AnsiConsole.Prompt(
                    new TextPrompt<int>("[cyan]Min Sentences Per Chunk (1-100):[/]")
                        .DefaultValue(chunking.MinSentencesPerChunk)
                        .Validate(value => value >= 1 && value <= 100 ? ValidationResult.Success() : ValidationResult.Error("[red]Value must be between 1 and 100[/]")));
                chunking.MinSentencesPerChunk = newValue;
                hasChanges = true;
            }

            AnsiConsole.MarkupLine($"[green]Success[/] Setting updated!");
            AnsiConsole.WriteLine();
        }

        if (hasChanges)
        {
            SaveConfiguration();
        }
    }

    private void EditOnlineModeSettings()
    {
        var ragSettings = _appSettings.Value.RAG;
        var cloudFlare = ragSettings.CloudFlare;
        var hasChanges = false;

        while (true)
        {
            var mode = ragSettings.Mode ?? "local";
            var setting = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold green]Edit Online Mode Settings[/]")
                    .PageSize(10)
                    .AddChoices(new[]
                    {
                        $"Mode: [cyan]{mode}[/]",
                        $"CloudFlare Account ID: [cyan]{(string.IsNullOrEmpty(cloudFlare.AccountId) ? "Not set" : "***")}[/]",
                        $"CloudFlare API Token: [cyan]{(string.IsNullOrEmpty(cloudFlare.ApiToken) ? "Not set" : "***")}[/]",
                        $"CloudFlare Generation Model: [cyan]{cloudFlare.GenerationModel}[/]",
                        "← Back"
                    }));

            if (setting == "← Back")
            {
                break;
            }

            if (setting.StartsWith("Mode:"))
            {
                var choices = new[] { "local", "online" };
                var defaultIndex = Array.IndexOf(choices, mode);
                var selectionPrompt = new SelectionPrompt<string>()
                    .Title("[cyan]Select Mode:[/]")
                    .AddChoices(choices);
                if (defaultIndex >= 0)
                {
                    selectionPrompt = selectionPrompt.PageSize(10);
                }
                var newValue = AnsiConsole.Prompt(selectionPrompt);
                ragSettings.Mode = newValue;
                hasChanges = true;
            }
            else if (setting.StartsWith("CloudFlare Account ID:"))
            {
                var newValue = AnsiConsole.Prompt(
                    new TextPrompt<string>("[cyan]CloudFlare Account ID (leave empty to keep current):[/]")
                        .Secret()
                        .AllowEmpty());
                if (!string.IsNullOrEmpty(newValue))
                {
                    cloudFlare.AccountId = newValue;
                    hasChanges = true;
                }
            }
            else if (setting.StartsWith("CloudFlare API Token:"))
            {
                var newValue = AnsiConsole.Prompt(
                    new TextPrompt<string>("[cyan]CloudFlare API Token (leave empty to keep current):[/]")
                        .Secret()
                        .AllowEmpty());
                if (!string.IsNullOrEmpty(newValue))
                {
                    cloudFlare.ApiToken = newValue;
                    hasChanges = true;
                }
            }
            else if (setting.StartsWith("CloudFlare Generation Model:"))
            {
                var newValue = AnsiConsole.Prompt(
                    new TextPrompt<string>("[cyan]CloudFlare Generation Model:[/]")
                        .DefaultValue(cloudFlare.GenerationModel)
                        .Validate(model => !string.IsNullOrWhiteSpace(model) ? ValidationResult.Success() : ValidationResult.Error("[red]Model name cannot be empty[/]")));
                cloudFlare.GenerationModel = newValue;
                hasChanges = true;
            }

            AnsiConsole.MarkupLine($"[green]Success[/] Setting updated!");
            AnsiConsole.WriteLine();
        }

        if (hasChanges)
        {
            SaveConfiguration();
        }
    }

    private void SaveConfiguration()
    {
        try
        {
            // Verify the file exists
            if (!File.Exists(_configFilePath))
            {
                throw new FileNotFoundException($"Configuration file not found: {_configFilePath}");
            }

            _logger.LogInformation("Saving configuration to: {ConfigPath}", _configFilePath);

            // Read the entire JSON file to preserve other sections (Logging, Quartz)
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
            
            // Ensure RAG and CLI sections exist
            if (appSettings["RAG"] == null)
            {
                appSettings["RAG"] = new JsonObject();
            }
            if (appSettings["CLI"] == null)
            {
                appSettings["CLI"] = new JsonObject();
            }

            var rag = appSettings["RAG"]!.AsObject();
            var cli = appSettings["CLI"]!.AsObject();

            // Update RAG settings
            rag["QdrantUrl"] = _appSettings.Value.RAG.QdrantUrl;
            rag["OllamaUrl"] = _appSettings.Value.RAG.OllamaUrl;
            rag["IndexName"] = _appSettings.Value.RAG.IndexName;
            rag["DocumentFolderPath"] = _appSettings.Value.RAG.DocumentFolderPath;
            rag["AnswerTokens"] = _appSettings.Value.RAG.AnswerTokens;
            rag["MinRelevance"] = _appSettings.Value.RAG.MinRelevance;
            
            if (!string.IsNullOrEmpty(_appSettings.Value.RAG.DefaultLastRun))
                rag["DefaultLastRun"] = _appSettings.Value.RAG.DefaultLastRun;
            if (!string.IsNullOrEmpty(_appSettings.Value.RAG.StoredLastRun))
                rag["StoredLastRun"] = _appSettings.Value.RAG.StoredLastRun;

            // Update TextModel
            if (rag["TextModel"] == null)
            {
                rag["TextModel"] = new JsonObject();
            }
            var textModel = rag["TextModel"]!.AsObject();
            textModel["Model"] = _appSettings.Value.RAG.TextModel.Model;
            textModel["MaxTokenTotal"] = _appSettings.Value.RAG.TextModel.MaxTokenTotal;
            if (_appSettings.Value.RAG.TextModel.Seed.HasValue)
                textModel["Seed"] = _appSettings.Value.RAG.TextModel.Seed!.Value;
            else
                textModel.Remove("Seed");

            // Update EmbeddingModel
            if (rag["EmbeddingModel"] == null)
            {
                rag["EmbeddingModel"] = new JsonObject();
            }
            var embeddingModel = rag["EmbeddingModel"]!.AsObject();
            embeddingModel["Model"] = _appSettings.Value.RAG.EmbeddingModel.Model;
            embeddingModel["MaxTokenTotal"] = _appSettings.Value.RAG.EmbeddingModel.MaxTokenTotal;

            // Update Chunking
            if (rag["Chunking"] == null)
            {
                rag["Chunking"] = new JsonObject();
            }
            var chunking = rag["Chunking"]!.AsObject();
            chunking["MaxTokensPerChunk"] = _appSettings.Value.RAG.Chunking.MaxTokensPerChunk;
            chunking["OverlapTokens"] = _appSettings.Value.RAG.Chunking.OverlapTokens;
            chunking["MinCharactersPerSentence"] = _appSettings.Value.RAG.Chunking.MinCharactersPerSentence;
            chunking["MinSentencesPerChunk"] = _appSettings.Value.RAG.Chunking.MinSentencesPerChunk;

            // Update Mode
            if (!string.IsNullOrEmpty(_appSettings.Value.RAG.Mode))
                rag["Mode"] = _appSettings.Value.RAG.Mode;
            else if (rag.ContainsKey("Mode"))
                rag.Remove("Mode");

            // Update CloudFlare settings
            if (rag["CloudFlare"] == null)
            {
                rag["CloudFlare"] = new JsonObject();
            }
            var cloudFlare = rag["CloudFlare"]!.AsObject();
            cloudFlare["AccountId"] = _appSettings.Value.RAG.CloudFlare.AccountId ?? "";
            cloudFlare["ApiToken"] = _appSettings.Value.RAG.CloudFlare.ApiToken ?? "";
            cloudFlare["EmbeddingModel"] = _appSettings.Value.RAG.CloudFlare.EmbeddingModel;
            cloudFlare["GenerationModel"] = _appSettings.Value.RAG.CloudFlare.GenerationModel;
            if (!string.IsNullOrEmpty(_appSettings.Value.RAG.CloudFlare.ReRankingModel))
                cloudFlare["ReRankingModel"] = _appSettings.Value.RAG.CloudFlare.ReRankingModel;
            else if (cloudFlare.ContainsKey("ReRankingModel"))
                cloudFlare.Remove("ReRankingModel");

            // Update CLI settings
            cli["ApplicationName"] = _appSettings.Value.CLI.ApplicationName;
            cli["ApplicationVersion"] = _appSettings.Value.CLI.ApplicationVersion;
            cli["LlmModel"] = _appSettings.Value.CLI.LlmModel;
            
            if (!string.IsNullOrEmpty(_appSettings.Value.CLI.Author))
                cli["Author"] = _appSettings.Value.CLI.Author;
            else if (cli.ContainsKey("Author"))
                cli.Remove("Author");
                
            if (!string.IsNullOrEmpty(_appSettings.Value.CLI.Description))
                cli["Description"] = _appSettings.Value.CLI.Description;
            else if (cli.ContainsKey("Description"))
                cli.Remove("Description");

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

            _logger.LogInformation("Configuration saved successfully to: {ConfigPath}", _configFilePath);
            AnsiConsole.MarkupLine($"[green]Success[/] Configuration saved to [cyan]{_configFilePath}[/]");
            AnsiConsole.MarkupLine("[yellow]warning[/] [dim]Note: Restart the application for changes to take effect[/]");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration to {ConfigPath}: {Message}", _configFilePath, ex.Message);
            AnsiConsole.MarkupLine($"[red]Error[/] Error saving configuration: [red]{ex.Message}[/]");
            AnsiConsole.MarkupLine($"[dim]Target file: {_configFilePath}[/]");
            
            // Clean up temp file if it exists
            var tempFile = _configFilePath + ".tmp";
            if (File.Exists(tempFile))
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch { }
            }
        }
    }
}

