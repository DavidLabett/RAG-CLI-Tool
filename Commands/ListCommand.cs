using Spectre.Console;
using Spectre.Console.Cli;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using SecondBrain.Models;
using SecondBrain.Services;

namespace SecondBrain.Commands;

/// <summary>
/// Command to list all documents and show sync status
/// </summary>
public class ListCommand : AsyncCommand<ListSettings>
{
    private readonly IOptions<AppSettings> _appSettings;
    private readonly ILogger<ListCommand> _logger;
    private readonly SyncState _syncState;

    public ListCommand(
        IOptions<AppSettings> appSettings,
        ILogger<ListCommand> logger,
        SyncState syncState)
    {
        _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _syncState = syncState ?? throw new ArgumentNullException(nameof(syncState));
    }

    public override Task<int> ExecuteAsync(CommandContext context, ListSettings settings)
    {
        try
        {
            // Determine folder path (override or from config)
            var folderPath = settings.Folder ?? _appSettings.Value.RAG.DocumentFolderPath;
            
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] No folder path specified. Use --folder or configure DocumentFolderPath in appsettings.json");
                return Task.FromResult(1);
            }

            if (!Directory.Exists(folderPath))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Folder not found: [yellow]{folderPath}[/]");
                return Task.FromResult(1);
            }

            AnsiConsole.MarkupLine($"[cyan]Scanning folder:[/] [yellow]{folderPath}[/]\n");

            // Get all files from folder
            var pdfFiles = Directory.GetFiles(folderPath, "*.pdf", SearchOption.TopDirectoryOnly);
            var textFiles = Directory.GetFiles(folderPath, "*.txt", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(folderPath, "*.md", SearchOption.TopDirectoryOnly))
                .Concat(Directory.GetFiles(folderPath, "*.docx", SearchOption.TopDirectoryOnly));
            var allFiles = pdfFiles.Concat(textFiles).ToList();

            if (allFiles.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No documents found in folder[/]");
                return Task.FromResult(0);
            }

            // Get last sync time to determine which files are synced
            // Use the same logic as SyncCommand: a file is synced if it was modified/created before the last sync
            DateTime lastSyncTime;
            try
            {
                lastSyncTime = _syncState.GetLastRun();
            }
            catch
            {
                // If we can't get last sync time, assume no files are synced
                lastSyncTime = DateTime.MinValue;
            }

            // Check sync status for each file using timestamp comparison (same as SyncCommand)
            var documentInfo = new List<DocumentInfo>();
            
            AnsiConsole.MarkupLine($"[dim]Last sync:[/] {lastSyncTime:yyyy-MM-dd HH:mm:ss} UTC\n");
            
            foreach (var file in allFiles)
            {
                var fileName = Path.GetFileName(file);
                var fileInfo = new FileInfo(file);
                var fileSize = fileInfo.Length;
                
                // Use the same logic as SyncCommand: check both LastWriteTime and CreationTime
                // Use the more recent of the two timestamps
                var mostRecentTime = fileInfo.LastWriteTimeUtc > fileInfo.CreationTimeUtc 
                    ? fileInfo.LastWriteTimeUtc 
                    : fileInfo.CreationTimeUtc;
                
                // File is synced if it was last modified/created before the last sync time
                // (i.e., it wouldn't be included in a sync operation)
                var isSynced = mostRecentTime < lastSyncTime;
                
                documentInfo.Add(new DocumentInfo
                {
                    FileName = fileName,
                    FilePath = file,
                    Size = fileSize,
                    IsSynced = isSynced
                });
            }

            // Display results
            DisplayDocumentList(documentInfo);

            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing documents: {Message}", ex.Message);
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return Task.FromResult(1);
        }
    }

    private void DisplayDocumentList(List<DocumentInfo> documents)
    {
        // Sort by size descending for better visualization
        var sortedDocs = documents.OrderByDescending(d => d.Size).ToList();
        
        // Calculate statistics
        var totalSize = documents.Sum(d => d.Size);
        var syncedCount = documents.Count(d => d.IsSynced);
        var notSyncedCount = documents.Count(d => !d.IsSynced);
        var syncedPercentage = documents.Count > 0 ? (syncedCount * 100.0 / documents.Count) : 0;
        
        // Group documents by file type
        var fileTypeGroups = documents
            .GroupBy(d => Path.GetExtension(d.FileName).ToUpperInvariant())
            .OrderByDescending(g => g.Count())
            .ToList();

        // Color mapping for file types
        var typeColors = new Dictionary<string, Color>
        {
            { ".PDF", Color.Red1 },
            { ".TXT", Color.Blue1 },
            { ".MD", Color.Green1 },
            { ".DOCX", Color.Yellow1 }
        };

        // Create tree structure
        var tree = new Tree("[bold cyan]List[/]");

        // Add statistics node
        var statsNode = tree.AddNode($"[bold yellow]Statistics[/]");
        statsNode.AddNode($"[white]Total: {documents.Count} | Size: {FormatSize(totalSize)}[/]");
        statsNode.AddNode($"[green]Synced: {syncedCount} ({syncedPercentage:F1}%)[/]");
        statsNode.AddNode($"[red]Not Synced: {notSyncedCount} ({100 - syncedPercentage:F1}%)[/]");

        // Add file types node
        var fileTypesNode = tree.AddNode("[bold magenta]File Types[/]");
        foreach (var group in fileTypeGroups)
        {
            var ext = string.IsNullOrEmpty(group.Key) ? "Unknown" : group.Key;
            var color = typeColors.TryGetValue(ext, out var c) 
                ? (c == Color.Red1 ? "red" : c == Color.Blue1 ? "blue" : c == Color.Green1 ? "green" : c == Color.Yellow1 ? "yellow" : "white")
                : "white";
            fileTypesNode.AddNode($"[{color}]{ext}[/]: {group.Count()}");
        }

        // Add documents grouped by file type
        var documentsNode = tree.AddNode("[bold cyan]Documents[/]");
        foreach (var group in fileTypeGroups)
        {
            var ext = string.IsNullOrEmpty(group.Key) ? "Unknown" : group.Key;
            var color = typeColors.TryGetValue(ext, out var c) 
                ? (c == Color.Red1 ? "red" : c == Color.Blue1 ? "blue" : c == Color.Green1 ? "green" : c == Color.Yellow1 ? "yellow" : "white")
                : "white";
            
            var typeNode = documentsNode.AddNode($"[{color}]{ext}[/] ({group.Count()})");
            
            foreach (var doc in group.OrderByDescending(d => d.Size))
            {
                var status = doc.IsSynced 
                    ? "[green]Synced[/]" 
                    : "[red]Not Synced[/]";
                
                typeNode.AddNode($"[white]{doc.FileName}[/] [cyan]({FormatSize(doc.Size)})[/] {status}");
            }
        }

        var treePanel = new Panel(tree)
            .Header($"[bold cyan]Documents[/] [dim]({documents.Count} total)[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .Padding(0, 0);

        AnsiConsole.Write(treePanel);
        AnsiConsole.WriteLine();
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private class DocumentInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long Size { get; set; }
        public bool IsSynced { get; set; }
    }
}

