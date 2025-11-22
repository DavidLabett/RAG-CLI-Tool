using SecondBrain.Models;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace SecondBrain.Services
{
    public class SyncState
    {
        private readonly AppSettings _appSettings;
        private readonly ILogger<SyncState> _logger;
        private readonly TimeProvider _timeProvider;

        public SyncState(AppSettings appSettings, ILogger<SyncState> logger, TimeProvider timeProvider)
        {
            _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        public DateTime GetLastRun()
        {
            return GetLastRunFromFile(
                _appSettings.RAG.StoredLastRun ??
                throw new InvalidOperationException("StoredLastRun is not configured"),
                _appSettings.RAG.DefaultLastRun ??
                throw new InvalidOperationException("DefaultLastRun is not configured"),
                "GMT Standard Time");
        }

        public void SetLastRun(DateTime dateTime)
        {
            var filePath = _appSettings.RAG.StoredLastRun ??
                          throw new InvalidOperationException("StoredLastRun not configured");
            try
            {
                // Ensure we store as UTC
                var utcDateTime = dateTime.Kind == DateTimeKind.Utc ? dateTime : dateTime.ToUniversalTime();
                File.WriteAllText(filePath,
                    utcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

                _logger.LogInformation("Successfully wrote last run date to {FilePath}: {DateTime} (UTC)", filePath, utcDateTime);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied when writing to {filePath}: {Message}. SyncState not updated", filePath, ex.Message);
                throw;
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "I/O error when writing to {filePath}: {Message}. SyncState not updated, but continuing", filePath, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error when writing to {filePath}: {Message}. SyncState not updated", filePath, ex.Message);
                throw;
            }
        }

        // Helper to read last run from file (or default)
        private DateTime GetLastRunFromFile(string filePath, string defaultDate, string timezoneId)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            }

            if (string.IsNullOrEmpty(defaultDate))
            {
                throw new ArgumentException("Default date cannot be null or empty", nameof(defaultDate));
            }

            string content = string.Empty;

            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    content = System.IO.File.ReadAllText(filePath);
                    _logger.LogDebug("Successfully read last run date from file {filePath}: {Content}", filePath, content);
                }
            }
            catch (FileNotFoundException)
            {
                // File doesn't exist yet - this is expected on first run, will use default
                _logger.LogDebug("File {filePath} does not exist yet. Will use default: {defaultDate}", filePath, defaultDate);
            }
            catch (IOException ex)
            {
                _logger.LogWarning("Could not read file {filePath}: {Message}. Will use default: {defaultDate}", filePath, ex.Message, defaultDate);
            }
            catch (Exception ex) // Either exception we will use default
            {
                _logger.LogWarning("Unexpected error when trying to read file {filePath}: {Message}. Will use default: {defaultDate}", filePath, ex.Message, defaultDate);
            }

            // Parse the date from file content or use default
            // Always treat as UTC to avoid timezone issues
            if (!string.IsNullOrWhiteSpace(content))
            {
                if (DateTime.TryParse(content, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedDate))
                {
                    // Ensure it's UTC
                    return parsedDate.Kind == DateTimeKind.Utc ? parsedDate : parsedDate.ToUniversalTime();
                }
                _logger.LogWarning("Could not parse date from file content: {Content}. Will use default: {defaultDate}", content, defaultDate);
            }

            // Use default date - treat as UTC
            if (DateTime.TryParse(defaultDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var defaultParsedDate))
            {
                // Ensure it's UTC
                return defaultParsedDate.Kind == DateTimeKind.Utc ? defaultParsedDate : defaultParsedDate.ToUniversalTime();
            }

            throw new InvalidOperationException($"Could not parse default date: {defaultDate}");
        }
    }
}