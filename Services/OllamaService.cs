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
    private readonly AppSettings _appSettings;
    private readonly HttpClient? _cloudFlareHttpClient;

    public OllamaService(HttpClient httpClient, IOptions<AppSettings> appSettings, ILogger<OllamaService> logger, IServiceProvider? serviceProvider = null)
    {
        _appSettings = appSettings.Value;
        var ragSettings = _appSettings.RAG;
        _ollamaUrl = ragSettings.OllamaUrl;
        _model = ragSettings.TextModel.Model;
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(_ollamaUrl);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Create separate HttpClient for CloudFlare if in online mode
        if (ragSettings.Mode?.ToLower() == "online")
        {
            var cloudFlareSettings = ragSettings.CloudFlare;
            if (string.IsNullOrEmpty(cloudFlareSettings.ApiToken))
            {
                _logger.LogWarning("CloudFlare mode is enabled but ApiToken is not set. Text generation will fail.");
            }
            if (string.IsNullOrEmpty(cloudFlareSettings.AccountId))
            {
                _logger.LogWarning("CloudFlare mode is enabled but AccountId is not set. Text generation will fail.");
            }

            // Create HttpClient for CloudFlare API
            // Try to get IHttpClientFactory from DI, otherwise create a new HttpClient
            var httpClientFactory = serviceProvider?.GetService(typeof(IHttpClientFactory)) as IHttpClientFactory;
            _cloudFlareHttpClient = httpClientFactory?.CreateClient() ?? new HttpClient();
            // Don't set BaseAddress - we'll construct full URLs manually
            
            _cloudFlareHttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {cloudFlareSettings.ApiToken}");
            _cloudFlareHttpClient.Timeout = TimeSpan.FromSeconds(60);
        }
    }

    /// <summary>
    /// Gets the actual model that will be used for generation, based on the current mode.
    /// In online mode, returns CloudFlare GenerationModel; in local mode, returns the provided model or default.
    /// </summary>
    public string GetActualModel(string? model = null)
    {
        var mode = _appSettings.RAG.Mode?.ToLower() ?? "local";
        
        if (mode == "online")
        {
            return _appSettings.RAG.CloudFlare.GenerationModel;
        }
        else
        {
            return !string.IsNullOrWhiteSpace(model) ? model : _model;
        }
    }

    public async Task<string> GenerateAnswerAsync(string prompt)
    {
        var model = _appSettings.RAG.Mode?.ToLower() == "online" 
            ? _appSettings.RAG.CloudFlare.GenerationModel 
            : _model;
        return await GenerateAnswerAsync(prompt, model);
    }

    public async Task<string> GenerateAnswerAsync(string prompt, string model)
    {
        var mode = _appSettings.RAG.Mode?.ToLower() ?? "local";
        
        if (mode == "online")
        {
            // In online mode, always use CloudFlare GenerationModel, ignore the passed model
            var cloudFlareModel = _appSettings.RAG.CloudFlare.GenerationModel;
            return await GenerateAnswerWithCloudFlareAsync(prompt, cloudFlareModel);
        }
        else
        {
            return await GenerateAnswerWithOllamaAsync(prompt, model);
        }
    }

    private async Task<string> GenerateAnswerWithOllamaAsync(string prompt, string model)
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
                "Ollama generation metrics - Tokens: {EvalCount}, Time: {ElapsedMs}ms, Speed: {TokensPerSec:F2} tokens/sec",
                result.EvalCount, 
                stopwatch.ElapsedMilliseconds, 
                tokensPerSecond);
        }

        return result?.Response ?? "Unable to generate answer.";
    }

    private async Task<string> GenerateAnswerWithCloudFlareAsync(string prompt, string model)
    {
        if (_cloudFlareHttpClient == null)
        {
            throw new InvalidOperationException("CloudFlare HttpClient is not initialized. Check that ApiToken and AccountId are configured.");
        }

        var stopwatch = Stopwatch.StartNew();
        var cloudFlareSettings = _appSettings.RAG.CloudFlare;

        try
        {
            // CloudFlare Workers AI endpoint: https://api.cloudflare.com/client/v4/accounts/{accountId}/ai/run/{model}
            // Use Uri class to properly construct the URL with path segments
            var baseUri = new Uri($"https://api.cloudflare.com/client/v4/accounts/{cloudFlareSettings.AccountId}/ai/run/");
            var fullUrl = new Uri(baseUri, model).ToString();

            _logger.LogDebug("Calling CloudFlare Workers AI: URL={Url}, model={Model}", fullUrl, model);

            var requestBody = new
            {
                prompt = prompt
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _cloudFlareHttpClient.PostAsync(fullUrl, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("CloudFlare Workers AI API error: {StatusCode} - {Error}. URL: {Url}", response.StatusCode, errorContent, fullUrl);
                response.EnsureSuccessStatusCode();
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("CloudFlare API response: {Response}", responseJson);

            var result = JsonSerializer.Deserialize<CloudFlareResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            stopwatch.Stop();
            _logger.LogInformation(
                "CloudFlare generation completed - Time: {ElapsedMs}ms",
                stopwatch.ElapsedMilliseconds);

            if (result == null)
            {
                _logger.LogWarning("CloudFlare response deserialization returned null. Raw response: {Response}", responseJson);
                return "Unable to generate answer - response parsing failed.";
            }

            if (string.IsNullOrEmpty(result.Response))
            {
                _logger.LogWarning("CloudFlare response has empty Response field. Full response: {Response}", responseJson);
                return "Unable to generate answer - empty response from API.";
            }

            return result.Response;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error calling CloudFlare Workers AI API");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling CloudFlare Workers AI API");
            throw;
        }
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

    // CloudFlareResponse class to deserialize response from CloudFlare Workers AI
    // CloudFlare Workers AI returns: {"result": {"response": "..."}, "success": true}
    private class CloudFlareResponse
    {
        [JsonPropertyName("result")]
        public CloudFlareResult? Result { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        // Helper property to get the response text
        public string? Response => Result?.Response;
    }

    private class CloudFlareResult
    {
        [JsonPropertyName("response")]
        public string? Response { get; set; }
    }
}

