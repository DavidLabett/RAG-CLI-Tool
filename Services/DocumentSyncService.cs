using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;

namespace SecondBrain.Services;

public class DocumentSyncService
{
    private readonly ILogger<DocumentSyncService> _logger;
    private readonly DocumentEmbeddingService _documentEmbeddingService;
    private readonly SyncState _syncState;
    private readonly TimeProvider _timeProvider;
    private readonly string _folderPath;
    
    public DocumentSyncService(
        ILogger<DocumentSyncService> logger, 
        DocumentEmbeddingService documentEmbeddingService, 
        SyncState syncState, 
        TimeProvider timeProvider,
        string folderPath)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _documentEmbeddingService = documentEmbeddingService ?? throw new ArgumentNullException(nameof(documentEmbeddingService));
        _syncState = syncState ?? throw new ArgumentNullException(nameof(syncState));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _folderPath = folderPath ?? throw new ArgumentNullException(nameof(folderPath));
    }

    public async Task SyncDocuments(IKernelMemory memory)
    {
        try
        {
            DateTime lastRunTime = _syncState.GetLastRun();
            DateTime currentRunTime = _timeProvider.GetUtcNow().DateTime;

            _logger.LogInformation("Starting sync of documents from folder since last run at {lastRunTime}.", lastRunTime);

            // Import all documents from the folder
            var filesSynced = await _documentEmbeddingService.ImportDocumentsFromFolderAsync(memory, _folderPath);
            
            if (filesSynced > 0)
            {
                _syncState.SetLastRun(currentRunTime);
                _logger.LogInformation("Document sync completed successfully. Updated last run time to {currentRunTime}.", currentRunTime);
            }
            else
            {
                _logger.LogInformation("No documents to sync - all files are up to date.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing documents: {Message}", ex.Message);
            throw;
        }
    }
}

