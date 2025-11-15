using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecondBrain.Models;
using Spectre.Console;

namespace SecondBrain.Services;

/// <summary>
/// Service for direct LLM chat without RAG context
/// </summary>
public class LlmChatService
{
    private readonly OllamaService _ollamaService;
    private readonly ILogger<LlmChatService> _logger;
    private readonly string _ollamaUrl;

    public LlmChatService(
        OllamaService ollamaService,
        IOptions<AppSettings> appSettings,
        ILogger<LlmChatService> logger)
    {
        _ollamaService = ollamaService ?? throw new ArgumentNullException(nameof(ollamaService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ollamaUrl = appSettings.Value.RAG.OllamaUrl;
    }

    public async Task StartChatLoopAsync(string model, bool enableHistory = false, int contextSize = 5)
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
            AnsiConsole.Markup($"[cyan]LLM[/][dim]({model}):[/] [cyan]>[/] ");
            var userInput = Console.ReadLine();

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

                var answer = await ProcessQueryAsync(userInput, model, enableHistory ? conversationHistory : null, contextSize);

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

    private async Task<string> ProcessQueryAsync(string userInput, string model, List<ConversationMessage>? conversationHistory = null, int contextSize = 5)
    {

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

        var prompt = BuildDirectPrompt(userInput, historyForPrompt);
        
        // Show status while generating answer
        var answer = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("green"))
            .StartAsync("Generating answer...", async ctx =>
            {
                ctx.Status("[cyan]Processing with LLM...[/]");
                return await _ollamaService.GenerateAnswerAsync(prompt, model);
            });

        // Display answer in a formatted Spectre.Console panel
        var answerPanel = new Panel(answer)
            .Header("[bold green]LLM Response[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Green1)
            .BorderStyle(Style.Parse("bold green"))
            .Padding(1, 1);
        AnsiConsole.Write(answerPanel);
        AnsiConsole.WriteLine();

        return answer;
    }

    private static string BuildDirectPrompt(string userInput, List<ConversationMessage>? conversationHistory = null)
    {
        var historySection = string.Empty;
        
        if (conversationHistory != null && conversationHistory.Any())
        {
            var historyBuilder = new System.Text.StringBuilder();
            historyBuilder.AppendLine("<conversation_history>");
            historyBuilder.AppendLine("PREVIOUS CONVERSATION:");
            foreach (var message in conversationHistory)
            {
                var roleLabel = message.Role == "user" ? "User" : "Assistant";
                historyBuilder.AppendLine($"{roleLabel}: {message.Content}");
            }
            historyBuilder.AppendLine("</conversation_history>");
            historyBuilder.AppendLine();
            historySection = historyBuilder.ToString();
        }

        return $@"<prompt>
<instruction>
You are a helpful AI assistant. Provide clear, concise, and accurate responses to user questions.
Your response will be displayed in a terminal/console, so format it appropriately for text-based output.
</instruction>

<formatting_guidelines>
• Use line breaks to separate paragraphs (double line break between paragraphs)
• Use bullet points (- or •) for lists when appropriate
• Keep lines to a reasonable length (around 80-100 characters when possible, but don't force line breaks mid-sentence)
• Use clear, natural language
• Structure longer responses with clear sections if needed
• Avoid markdown formatting (no **bold**, *italic*, etc.) unless specifically requested
• Use simple text formatting that works well in terminals
</formatting_guidelines>

{historySection}<user_input>
{userInput}
</user_input>

<response_guidelines>
• Answer directly and helpfully
• Be concise but complete
• If the question is unclear, ask for clarification
• If you don't know something, say so honestly
{(conversationHistory != null && conversationHistory.Any() ? "• Use the conversation history to provide context-aware answers and maintain continuity" : "")}
• Format your response for terminal display (use line breaks, paragraphs, and simple lists)
</response_guidelines>

Please provide your response:
</prompt>";
    }
}

