using Microsoft.KernelMemory;
using Microsoft.Extensions.Options;
using SecondBrain.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace SecondBrain.Services;

// Service for embedding local documents (PDF, etc.) into the knowledge base
public class DocumentEmbeddingService
{
    private readonly string _indexName;
    private readonly ILogger<DocumentEmbeddingService> _logger;

    public DocumentEmbeddingService(
        IOptions<AppSettings> appSettings,
        ILogger<DocumentEmbeddingService> logger)
    {
        _indexName = appSettings.Value.RAG.IndexName;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Sanitizes document ID to only contain allowed characters (A-Z, a-z, 0-9, '.', '_', '-')
    private string SanitizeDocumentId(string documentId)
    {
        var sanitized = Regex.Replace(documentId, @"[^A-Za-z0-9._-]", "_");

        sanitized = Regex.Replace(sanitized, @"_+", "_");

        sanitized = sanitized.Trim('_');

        if (string.IsNullOrEmpty(sanitized))
        {
            sanitized = "document";
        }

        return sanitized;
    }

    // Imports a document file and embeds it into the knowledge base
    public async Task ImportDocumentAsync(IKernelMemory memory, string filePath, int currentIndex = 0, int totalCount = 0)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("File not found: {FilePath}", filePath);
                return;
            }

            var fileName = Path.GetFileName(filePath);
            var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();
            var documentId = SanitizeDocumentId(Path.GetFileNameWithoutExtension(filePath));

            var fileInfo = new FileInfo(filePath);
            var fileSizeMB = fileInfo.Length / (1024.0 * 1024.0);

            if (totalCount > 0)
            {
                _logger.LogInformation("[{Current}/{Total}] Starting import: {FileName} ({Size:F2} MB, {Type})",
                    currentIndex, totalCount, fileName, fileSizeMB, fileExtension.ToUpperInvariant());
            }
            else
            {
                _logger.LogInformation("Starting import: {FileName} ({Size:F2} MB, {Type})",
                    fileName, fileSizeMB, fileExtension.ToUpperInvariant());
            }

            _logger.LogInformation("  → Extracting text and generating embeddings...");

            // Use KernelMemory's ImportDocumentAsync for PDF, DOCX and other supported formats
            if (fileExtension == ".pdf" || fileExtension == ".docx")
            {
                await memory.ImportDocumentAsync(
                    filePath: filePath,
                    documentId: documentId,
                    index: _indexName,
                    tags: new TagCollection
                    {
                        {"filename", fileName},
                        {"filepath", filePath},
                        {"extension", fileExtension},
                        {"imported", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")}
                    });
            }
            else
            {
                _logger.LogInformation("  → Reading text content...");
                var content = await File.ReadAllTextAsync(filePath);
                var contentLength = content.Length;
                _logger.LogInformation("  → Read {Length:N0} characters, generating embeddings...", contentLength);

                await memory.ImportTextAsync(
                    text: content,
                    documentId: documentId,
                    index: _indexName,
                    tags: new TagCollection
                    {
                        {"filename", fileName},
                        {"filepath", filePath},
                        {"extension", fileExtension},
                        {"imported", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")}
                    });
            }

            var duration = (DateTime.UtcNow - startTime).TotalSeconds;
            _logger.LogInformation("\nSuccess: Successfully imported {FileName} in {Duration:F1}s", fileName, duration);
        }
        catch (Exception ex)
        {
            var duration = (DateTime.UtcNow - startTime).TotalSeconds;
            _logger.LogError(ex, "\nError: Error importing document {FilePath} after {Duration:F1}s: {Message}", filePath, duration, ex.Message);
            throw;
        }
    }

    public async Task<int> ImportDocumentsFromFolderAsync(IKernelMemory memory, string folderPath)
    {
        return await ImportDocumentsFromFolderAsync(memory, folderPath, null);
    }

    // Imports documents from a directory, optionally filtered by last modified time
    public async Task<int> ImportDocumentsFromFolderAsync(IKernelMemory memory, string folderPath, DateTime? sinceDateTime, Action<int, int, string>? progressCallback = null)
    {
        try
        {
            if (!Directory.Exists(folderPath))
            {
                _logger.LogWarning("Folder not found: {FolderPath}", folderPath);
                return 0;
            }

            _logger.LogInformation("Scanning folder for documents: {FolderPath}", folderPath);

            // Get all PDF files
            var pdfFiles = Directory.GetFiles(folderPath, "*.pdf", SearchOption.TopDirectoryOnly);

            // Get all text files (other extensuions)
            var textFiles = Directory.GetFiles(folderPath, "*.txt", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(folderPath, "*.md", SearchOption.TopDirectoryOnly))
                .Concat(Directory.GetFiles(folderPath, "*.docx", SearchOption.TopDirectoryOnly));

            var allFiles = pdfFiles.Concat(textFiles).ToList();

            if (sinceDateTime.HasValue)
            {
                var originalCount = allFiles.Count;
                var sinceUtc = sinceDateTime.Value.Kind == DateTimeKind.Utc ? sinceDateTime.Value : sinceDateTime.Value.ToUniversalTime();

                allFiles = allFiles.Where(file =>
                {
                    var fileInfo = new FileInfo(file);
                    // Use the more recent of the two timestamps
                    var mostRecentTime = fileInfo.LastWriteTimeUtc > fileInfo.CreationTimeUtc
                        ? fileInfo.LastWriteTimeUtc
                        : fileInfo.CreationTimeUtc;

                    return mostRecentTime >= sinceUtc;
                }).ToList();

                _logger.LogInformation("Filtered {FilteredCount} file(s) modified since {SinceDateTime} from {TotalCount} total file(s)",
                    allFiles.Count, sinceDateTime.Value, originalCount);
            }

            if (allFiles.Count == 0)
            {
                _logger.LogInformation("No documents found to import.");
                return 0;
            }

            _logger.LogInformation("Found {Count} document(s) to import", allFiles.Count);
            _logger.LogInformation("═══════════════════════════════════════════════════════════════════════════════");

            var overallStartTime = DateTime.UtcNow;
            int successCount = 0;
            int failureCount = 0;

            for (int i = 0; i < allFiles.Count; i++)
            {
                var file = allFiles[i];
                var fileName = Path.GetFileName(file);

                progressCallback?.Invoke(i, allFiles.Count, fileName);

                try
                {
                    await ImportDocumentAsync(memory, file, i + 1, allFiles.Count);
                    successCount++;

                    progressCallback?.Invoke(i + 1, allFiles.Count, fileName);
                }
                catch (Exception ex)
                {
                    failureCount++;
                    _logger.LogError(ex, "Failed to import document {Index}/{Total}: {FileName}",
                        i + 1, allFiles.Count, fileName);
                    progressCallback?.Invoke(i + 1, allFiles.Count, fileName);
                }

                if (i < allFiles.Count - 1)
                {
                    _logger.LogInformation("───────────────────────────────────────────────────────────────────────────────");
                }
            }

            var overallDuration = (DateTime.UtcNow - overallStartTime).TotalSeconds;
            _logger.LogInformation("═══════════════════════════════════════════════════════════════════════════════");
            _logger.LogInformation("Import Summary: {Success} succeeded, {Failed} failed, Total time: {Duration:F1}s",
                successCount, failureCount, overallDuration);
            _logger.LogInformation("═══════════════════════════════════════════════════════════════════════════════\n");

            return successCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing documents from folder {FolderPath}: {Message}", folderPath, ex.Message);
            throw;
        }
    }
}

