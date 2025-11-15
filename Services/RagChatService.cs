using Microsoft.KernelMemory;
using Microsoft.Extensions.Options;
using SecondBrain.Models;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace SecondBrain.Services;

public class RagChatService
{
    private readonly string _indexName;
    private readonly float _minRelevance;
    private readonly OllamaService _ollamaService;
    private readonly ILogger<RagChatService> _logger;
    private readonly RagResultService _ragResultService;

    public RagChatService(OllamaService ollamaService, IOptions<AppSettings> appSettings, ILogger<RagChatService> logger, RagResultService ragResultService)
    {
        var ragSettings = appSettings.Value.RAG;
        _indexName = ragSettings.IndexName;
        _minRelevance = ragSettings.MinRelevance;
        _ollamaService = ollamaService ?? throw new ArgumentNullException(nameof(ollamaService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ragResultService = ragResultService ?? throw new ArgumentNullException(nameof(ragResultService));
    }

    public async Task StartChatLoopAsync(IKernelMemory memory, string model, bool enableHistory = false, int contextSize = 5)
    {
        var conversationHistory = new List<ConversationMessage>();

        while (true)
        {
            // Show history status in prompt using Spectre.Console markup
            if (enableHistory)
            {
                AnsiConsole.Markup($"[green]H:on[/][dim]|{contextSize}[/] ");
            }
            else
            {
                AnsiConsole.Markup("[yellow]H:off[/] ");
            }
            AnsiConsole.Markup($"[cyan]RAG[/][dim]({model})[/]: [cyan]>[/] ");
            var userInput = Console.ReadLine(); // userInput is a placeholder - this will be replaced with the actual open ticket

            if (string.IsNullOrEmpty(userInput) || userInput.ToLower() == "exit")
            {
                _logger.LogInformation("Goodbye!");
                break;
            }

            try
            {
                // Add user message to history
                if (enableHistory)
                {
                    conversationHistory.Add(new ConversationMessage("user", userInput));
                }

                var (answer, searchResults) = await ProcessQueryAsync(memory, userInput, enableHistory ? conversationHistory : null, contextSize, model);

                // Store results for tree command
                _ragResultService.StoreLatestResults(searchResults);

                // Add assistant answer to history
                if (enableHistory && !string.IsNullOrEmpty(answer))
                {
                    conversationHistory.Add(new ConversationMessage("assistant", answer));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing query: {ex.Message}");
            }
        }
    }

    private async Task<(string answer, SearchResult searchResults)> ProcessQueryAsync(IKernelMemory memory, string userInput, List<ConversationMessage>? conversationHistory = null, int contextSize = 5, string? model = null)
    {
        // Show status while searching database
        var searchResults = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync("Searching knowledge base...", async ctx =>
            {
                ctx.Status("[cyan]Searching for relevant documents...[/]");
                return await memory.SearchAsync(
                    query: userInput,
                    index: _indexName,
                    limit: 5, // TODO: Increases context: Expand in future, filter by date and implement logic to choose best results
                    minRelevance: _minRelevance);
            });

        _logger.LogInformation($"Found: {searchResults.Results.Count()} relevant sources");

        // Build context from retrieved results
        var contextBuilder = new System.Text.StringBuilder();
        if (searchResults.Results.Any())
        {
            // TODO: Here we could filter further with date, and relevance score
            foreach (var result in searchResults.Results)
            {
                if (result.Partitions.Any())
                {
                    foreach (var partition in result.Partitions.Take(3)) // Limit to top 3 partitions per result TODO: Try expanding?
                    {
                        contextBuilder.AppendLine(partition.Text);
                        contextBuilder.AppendLine();
                    }
                }
            }
        }
        else
        {
            contextBuilder.AppendLine("No relevant information found in the knowledge base.");
        }
        // Send context to LLM
        var retrievedContext = contextBuilder.ToString();
        
        // Get conversation history for context (only last N messages, excluding current user input)
        List<ConversationMessage>? historyForPrompt = null;
        if (conversationHistory != null && conversationHistory.Count > 0)
        {
            // Get last N messages (excluding the current user input which was just added)
            var messagesToInclude = conversationHistory
                .TakeLast(contextSize)
                .Where(m => m.Role == "assistant" || (m.Role == "user" && m != conversationHistory.Last()))
                .ToList();
            
            if (messagesToInclude.Any())
            {
                historyForPrompt = messagesToInclude;
            }
        }
        
        var customPrompt = PromptTemplate.BuildPrompt(userInput, retrievedContext, historyForPrompt);
        
        // Show status while generating answer
        var answer = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("green"))
            .StartAsync("Generating answer...", async ctx =>
            {
                ctx.Status("[green]Processing with LLM...[/]");
                // Use provided model if specified, otherwise use the default from OllamaService
                return !string.IsNullOrWhiteSpace(model)
                    ? await _ollamaService.GenerateAnswerAsync(customPrompt, model)
                    : await _ollamaService.GenerateAnswerAsync(customPrompt);
            });

        // Display answer in a formatted Spectre.Console panel
        var answerPanel = new Panel(answer)
            .Header("[bold green]RAG Response[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Green1)
            .BorderStyle(Style.Parse("bold green"))
            .Padding(1, 1);
        AnsiConsole.Write(answerPanel);
        AnsiConsole.WriteLine();

        if (searchResults.Results.Any())
        {
            _logger.LogInformation($"Sources: {string.Join(", ", searchResults.Results.Select(r => r.DocumentId ?? "Unknown"))}");
        }

        if (!searchResults.Results.Any())
        {
            _logger.LogInformation("No relevant sources found. This might indicate:");
            _logger.LogInformation("- The embedding search didn't find matching content");
            _logger.LogInformation("- Try using different keywords or phrasing");
            _logger.LogInformation("- The knowledge base might not contain the requested information");
        }

        return (answer, searchResults);
    }
}

// - User Question
// 1. SearchAsync → Finds relevant documents (Qdrant)
// 2. Build Context → Extracts document text from results
// 3. Build Prompt → PromptTemplateService combines:
//    - Your custom prompt template
//    - Retrieved context
//    - User question
// 4. Call LLM Directly → OllamaService sends to Ollama
// 5. Display Answer