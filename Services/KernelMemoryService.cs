using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI.Ollama;
using Microsoft.Extensions.Options;
using SecondBrain.Models;
using Microsoft.KernelMemory.Configuration;

namespace SecondBrain.Services;

public class KernelMemoryService
{
    private readonly AppSettings.RAGSettings _ragSettings;

    public KernelMemoryService(IOptions<AppSettings> appSettings)
    {
        _ragSettings = appSettings.Value.RAG;
    }

    public IKernelMemory Build()
    {
        var ollamaConfig = new OllamaConfig()
        {
            TextModel = new OllamaModelConfig(_ragSettings.TextModel.Model)
            {
                MaxTokenTotal = _ragSettings.TextModel.MaxTokenTotal,
                Seed = _ragSettings.TextModel.Seed
            },
            EmbeddingModel = new OllamaModelConfig(_ragSettings.EmbeddingModel.Model)
            {
                MaxTokenTotal = _ragSettings.EmbeddingModel.MaxTokenTotal
            },
            Endpoint = _ragSettings.OllamaUrl
        };

        var memoryBuilder = new KernelMemoryBuilder()
            .WithOllamaTextGeneration(ollamaConfig)
            .WithOllamaTextEmbeddingGeneration(ollamaConfig)
            .WithQdrantMemoryDb(_ragSettings.QdrantUrl)
            .WithSearchClientConfig(new SearchClientConfig() { AnswerTokens = _ragSettings.AnswerTokens })
            .WithCustomTextPartitioningOptions(
                new TextPartitioningOptions
                {
                    MaxTokensPerParagraph = _ragSettings.Chunking.MaxTokensPerChunk,
                    OverlappingTokens = _ragSettings.Chunking.OverlapTokens,
                }
            );
        
        return memoryBuilder.Build(new KernelMemoryBuilderBuildOptions
        {
            AllowMixingVolatileAndPersistentData = true
        });
    }
}

