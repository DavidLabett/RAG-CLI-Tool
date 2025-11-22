using System.ComponentModel.DataAnnotations;
using System;

namespace SecondBrain.Models;

public class AppSettings
{
    [Required]
    public RAGSettings RAG { get; set; } = new();
    [Required]
    public QuartzSettings Quartz { get; set; } = new();
    public CLISettings CLI { get; set; } = new();

    public class RAGSettings
    {
        [Required, Url]
        public string QdrantUrl { get; set; } = string.Empty;
        [Required, Url]
        public string OllamaUrl { get; set; } = string.Empty;
        [Required]
        public TextModelSettings TextModel { get; set; } = new();
        [Required]
        public EmbeddingModelSettings EmbeddingModel { get; set; } = new();
        [Required, MinLength(1)]
        public string IndexName { get; set; } = string.Empty;

        [Required]
        public string DocumentFolderPath { get; set; } = string.Empty;
        public string? DefaultLastRun { get; set; }
        public string? StoredLastRun { get; set; }
        public int AnswerTokens { get; set; } = 4096;
        public float MinRelevance { get; set; } = 0.3f;
        public ChunkingSettings Chunking { get; set; } = new();
        public string Mode { get; set; } = "local";
        public CloudFlareSettings CloudFlare { get; set; } = new();
    }

    public class ChunkingSettings
    {
        [Range(1, 8192)]
        public int MaxTokensPerChunk { get; set; } = 1000;
        [Range(0, 1024)]
        public int OverlapTokens { get; set; } = 200;
        [Range(1, 1000)]
        public int MinCharactersPerSentence { get; set; } = 10;
        [Range(1, 100)]
        public int MinSentencesPerChunk { get; set; } = 1;
    }

    public class TextModelSettings
    {
        [Required, MinLength(1)]
        public string Model { get; set; } = string.Empty;
        [Range(1, int.MaxValue)]
        public int MaxTokenTotal { get; set; } = 125000;
        public int? Seed { get; set; } = 42;
    }

    public class EmbeddingModelSettings
    {
        [Required, MinLength(1)]
        public string Model { get; set; } = string.Empty;
        [Range(1, int.MaxValue)]
        public int MaxTokenTotal { get; set; } = 2048;
    }

    public class CloudFlareSettings
    {
        public string ApiToken { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public string EmbeddingModel { get; set; } = "@cf/baai/bge-base-en-v1.5";
        public string GenerationModel { get; set; } = "@cf/meta/llama-3-8b-instruct";
        public string? ReRankingModel { get; set; }
    }

    public class QuartzSettings
    {
        public SchedulerSettings Scheduler { get; set; } = new();
        public ThreadPoolSettings ThreadPool { get; set; } = new();
        public JobStoreSettings JobStore { get; set; } = new();
        public PluginSettings Plugin { get; set; } = new();
    }
    public class SchedulerSettings
    {
        public string InstanceName { get; set; } = "RAGKnowledgeBaseSync";
        public string InstanceId { get; set; } = "AUTO";
    }
    public class ThreadPoolSettings
    {
        public string Type { get; set; } = "Quartz.Simpl.SimpleThreadPool, Quartz";
        public int ThreadCount { get; set; } = 50;
        public int ThreadPriority { get; set; } = 2;
    }

    public class JobStoreSettings
    {
        public int MisfireThreshold { get; set; } = 60000;
        public string Type { get; set; } = "Quartz.Simpl.RAMJobStore, Quartz";
    }

    public class PluginSettings
    {
        public XmlPluginSettings Xml { get; set; } = new();
    }

    public class XmlPluginSettings
    {
        public string Type { get; set; } = "Quartz.Plugin.Xml.XMLSchedulingDataProcessorPlugin, Quartz.Plugins";
        public string FileNames { get; set; } = "~/quartz_jobs.xml";
    }

    public class CLISettings
    {
        [Required, MinLength(1)]
        public string ApplicationName { get; set; } = "2b";

        [Required, MinLength(1)]
        public string ApplicationVersion { get; set; } = "1.0.0";
        public string? Author { get; set; }
        public string? Description { get; set; }
        [MinLength(1)]
        public string LlmModel { get; set; } = "gemma3:4b";
    }
}
