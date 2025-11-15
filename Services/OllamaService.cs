using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SecondBrain.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;
using System.Diagnostics;

namespace SecondBrain.Services;

public class OllamaService
{
    private readonly string _ollamaUrl;
    private readonly string _model;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaService> _logger;

    public OllamaService(HttpClient httpClient, IOptions<AppSettings> appSettings, ILogger<OllamaService> logger)
    {
        var ragSettings = appSettings.Value.RAG;
        _ollamaUrl = ragSettings.OllamaUrl;
        _model = ragSettings.TextModel.Model;
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(_ollamaUrl);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> GenerateAnswerAsync(string prompt)
    {
        return await GenerateAnswerAsync(prompt, _model);
    }

    public async Task<string> GenerateAnswerAsync(string prompt, string model)
    {
        var stopwatch = Stopwatch.StartNew();

        var requestBody = new
        {
            model = model,
            prompt = prompt,
            stream = false
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/api/generate", content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<OllamaResponse>(responseJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        stopwatch.Stop();
         // Calculate and log tokens per second
        if (result != null && result.EvalCount > 0)
        {
            var tokensPerSecond = result.EvalCount / (stopwatch.ElapsedMilliseconds / 1000.0);
            _logger.LogInformation(
                "Generation metrics - Tokens: {EvalCount}, Time: {ElapsedMs}ms, Speed: {TokensPerSec:F2} tokens/sec",
                result.EvalCount, 
                stopwatch.ElapsedMilliseconds, 
                tokensPerSecond);
        }

        return result?.Response ?? "Unable to generate answer.";
    }

    // OllamaResponse class to deserialize response from Ollama
    private class OllamaResponse
    {
        [JsonPropertyName("response")]
        public string Response { get; set; } = "";

        [JsonPropertyName("eval_count")]
        public int EvalCount { get; set; } // Output tokens generated

        [JsonPropertyName("prompt_eval_count")]
        public int PromptEvalCount { get; set; } // Input tokens processed

        [JsonPropertyName("total_duration")]
        public long TotalDuration { get; set; } // Total time in nanoseconds

        [JsonPropertyName("eval_duration")]
        public long EvalDuration { get; set; } // Generation time in nanoseconds

        [JsonPropertyName("prompt_eval_duration")]
        public long PromptEvalDuration { get; set; } // Prompt processing time in nanoseconds
    }
}

