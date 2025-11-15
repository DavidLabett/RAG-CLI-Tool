using Spectre.Console;
using Spectre.Console.Cli;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecondBrain.Models;
using SecondBrain.Services;

namespace SecondBrain.Commands;

/// <summary>
/// Command to start an interactive chat session with a direct LLM (without RAG)
/// </summary>
public class LlmCommand : AsyncCommand<LlmSettings>
{
    private readonly ILogger<LlmCommand> _logger;
    private readonly LlmChatService _llmChatService;
    private readonly AppSettings _appSettings;

    public LlmCommand(
        ILogger<LlmCommand> logger,
        LlmChatService llmChatService,
        IOptions<AppSettings> appSettings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _llmChatService = llmChatService ?? throw new ArgumentNullException(nameof(llmChatService));
        _appSettings = appSettings?.Value ?? throw new ArgumentNullException(nameof(appSettings));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, LlmSettings settings)
    {
        try
        {
            // Use model from command line if provided, otherwise use configured default
            var model = !string.IsNullOrWhiteSpace(settings.Model) 
                ? settings.Model 
                : _appSettings.CLI.LlmModel;

            AnsiConsole.MarkupLine($"[cyan]Starting direct LLM chat session with model: {model}...[/]");
            
            // Show history status indicator
            var historyStatus = settings.History 
                ? $"[green]History:on[/] ([dim]context: {settings.Context} messages[/])"
                : "[yellow]History:off[/]";
            AnsiConsole.MarkupLine($"[dim]Status:[/] {historyStatus}");
            
            AnsiConsole.MarkupLine("[dim]Type 'exit' or press 'CTRL + C' to end the chat.[/]\n");

            // Start the chat loop with history support
            await _llmChatService.StartChatLoopAsync(model, settings.History, settings.Context);

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LLM chat session: {Message}", ex.Message);
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}

