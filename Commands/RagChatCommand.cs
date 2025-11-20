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
    private readonly OllamaService _ollamaService;

    public RagChatCommand(
        ILogger<RagChatCommand> logger,
        IKernelMemory memory,
        RagChatService ragChatService,
        IOptions<AppSettings> appSettings,
        OllamaService ollamaService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _ragChatService = ragChatService ?? throw new ArgumentNullException(nameof(ragChatService));
        _appSettings = appSettings?.Value ?? throw new ArgumentNullException(nameof(appSettings));
        _ollamaService = ollamaService ?? throw new ArgumentNullException(nameof(ollamaService));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, RagChatSettings settings)
    {
        try
        {
            // Use model from command line if provided, otherwise use configured default
            var model = !string.IsNullOrWhiteSpace(settings.Model) 
                ? settings.Model 
                : _appSettings.RAG.TextModel.Model;

            var mode = _appSettings.RAG.Mode?.ToLower() ?? "local";
            var modeDisplay = mode == "online" ? "[green]online[/]" : "[yellow]local[/]";
            
            // Get the actual model that will be used (may differ in online mode)
            var actualModel = _ollamaService.GetActualModel(model);

            AnsiConsole.MarkupLine($"[cyan]Starting RAG knowledge base chat session with model: {actualModel}...[/]");
            AnsiConsole.MarkupLine($"[dim]Mode:[/] {modeDisplay}");
            
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

