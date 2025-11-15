using Microsoft.KernelMemory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace SecondBrain.Services;

/// <summary>
/// Service to store the latest RAG search results for display in the tree command
/// Persists to file so results are available across separate command invocations
/// </summary>
public class RagResultService
{
    private readonly string _storageFilePath;
    private readonly ILogger<RagResultService> _logger;
    private readonly object _lock = new object();

    // Serializable data structure for storing results
    public class StoredResultData
    {
        public List<StoredCitation> Results { get; set; } = new();
    }

    public class StoredCitation
    {
        public string? DocumentId { get; set; }
        public List<StoredPartition> Partitions { get; set; } = new();
    }

    public class StoredPartition
    {
        public string Text { get; set; } = string.Empty;
        public float Relevance { get; set; }
    }

    public RagResultService(ILogger<RagResultService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // Store in the same directory as the executable
        var appDirectory = AppContext.BaseDirectory;
        _storageFilePath = Path.Combine(appDirectory, "rag-results.json");
    }

    /// <summary>
    /// Store the latest search results to file
    /// </summary>
    public void StoreLatestResults(SearchResult searchResults)
    {
        lock (_lock)
        {
            try
            {
                var storedData = new StoredResultData
                {
                    Results = searchResults.Results.Select(r => new StoredCitation
                    {
                        DocumentId = r.DocumentId,
                        Partitions = r.Partitions.Select(p => new StoredPartition
                        {
                            Text = p.Text,
                            Relevance = p.Relevance
                        }).ToList()
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(storedData, new JsonSerializerOptions 
                { 
                    WriteIndented = false 
                });

                File.WriteAllText(_storageFilePath, json);
                _logger.LogDebug("Stored RAG results to {FilePath}", _storageFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing RAG results to {FilePath}: {Message}", _storageFilePath, ex.Message);
            }
        }
    }

    /// <summary>
    /// Get the stored result data from file
    /// </summary>
    public StoredResultData? GetStoredData()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_storageFilePath))
                {
                    _logger.LogDebug("RAG results file not found: {FilePath}", _storageFilePath);
                    return null;
                }

                var json = File.ReadAllText(_storageFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                var storedData = JsonSerializer.Deserialize<StoredResultData>(json);
                _logger.LogDebug("Loaded RAG results from {FilePath}", _storageFilePath);
                return storedData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading RAG results from {FilePath}: {Message}", _storageFilePath, ex.Message);
                return null;
            }
        }
    }

    /// <summary>
    /// Get the latest search results from file (for backward compatibility)
    /// Returns null - use GetStoredData() instead
    /// </summary>
    [Obsolete("Use GetStoredData() instead")]
    public SearchResult? GetLatestResults()
    {
        return null;
    }

    /// <summary>
    /// Check if there are any stored results
    /// </summary>
    public bool HasResults()
    {
        lock (_lock)
        {
            var storedData = GetStoredData();
            return storedData != null && 
                   storedData.Results != null && 
                   storedData.Results.Any();
        }
    }
}

