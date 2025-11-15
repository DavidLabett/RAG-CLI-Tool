using Spectre.Console;
using Spectre.Console.Cli;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using SecondBrain.Services;
using SecondBrain.Models;
using Microsoft.Extensions.Options;
namespace SecondBrain.Commands;

/// <summary>
/// Command to start an interactive chat session with the knowledge base
/// </summary>
public class RagChatCommand : AsyncCommand<RagChatSettings>
{
    private readonly ILogger<RagChatCommand> _logger;
    private readonly IKernelMemory _memory;
    private readonly RagChatService _ragChatService;
    private readonly AppSettings _appSettings;

    public RagChatCommand(
        ILogger<RagChatCommand> logger,
        IKernelMemory memory,
        RagChatService ragChatService,
        IOptions<AppSettings> appSettings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _ragChatService = ragChatService ?? throw new ArgumentNullException(nameof(ragChatService));
        _appSettings = appSettings?.Value ?? throw new ArgumentNullException(nameof(appSettings));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, RagChatSettings settings)
    {
        try
        {
            // Use model from command line if provided, otherwise use configured default
            var model = !string.IsNullOrWhiteSpace(settings.Model) 
                ? settings.Model 
                : _appSettings.RAG.TextModel.Model;

            AnsiConsole.MarkupLine($"[cyan]Starting RAG knowledge base chat session with model: {model}...[/]");
            
            // Show history status indicator
            var historyStatus = settings.History 
                ? $"[green]History:on[/] ([dim]context: {settings.Context} messages[/])"
                : "[yellow]History:off[/]";
            AnsiConsole.MarkupLine($"[dim]Status:[/] {historyStatus}");
            
            AnsiConsole.MarkupLine("[dim]Type 'exit' or press 'CTRL + C' to end the chat.[/]\n");

            // Start the chat loop with history support
            await _ragChatService.StartChatLoopAsync(_memory, model, settings.History, settings.Context);

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chat session: {Message}", ex.Message);
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}

