using SecondBrain.Models;

namespace SecondBrain.Services;

public static class PromptTemplate
{
    public static string BuildPrompt(string userQuestion, string retrievedContext, List<ConversationMessage>? conversationHistory = null)
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
You are a helpful AI assistant that answers questions based on a personal knowledge base. 
Base your answer solely on the information provided in the context below. Do not add external assumptions or information not found in the context.
{(conversationHistory != null && conversationHistory.Any() ? " Use the conversation history to provide context-aware answers and maintain continuity in the conversation." : "")}
</instruction>
<instruction>
• Provide clear, concise, and accurate answers based on the retrieved context.
• If the context contains relevant information, synthesize it into a coherent answer.
• If multiple relevant pieces of information exist, organize them logically.
• If the context does not contain sufficient information to answer the question, politely state that the information is not available in the knowledge base.
• Use a natural, conversational tone while remaining factual.
• Cite specific details from the context when relevant.
{(conversationHistory != null && conversationHistory.Any() ? "• Reference previous conversation when relevant to provide continuity." : "")}
</instruction>

{historySection}<context>
KNOWLEDGE BASE CONTENT:
{retrievedContext}
</context>

<input>
User Question: {userQuestion}
</input>

<answer>
Provide your answer here based on the context above.
</answer>
</prompt>";
    }
}

