using Spectre.Console;
using Spectre.Console.Cli;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using SecondBrain.Models;
using SecondBrain.Services;

namespace SecondBrain.Commands;

/// <summary>
/// Command to sync documents from folder to knowledge base
/// </summary>
public class SyncCommand : AsyncCommand<SyncSettings>
{
    private readonly IOptions<AppSettings> _appSettings;
    private readonly ILogger<SyncCommand> _logger;
    private readonly IKernelMemory _memory;
    private readonly DocumentEmbeddingService _documentEmbeddingService;
    private readonly SyncState _syncState;
    private readonly TimeProvider _timeProvider;

    public SyncCommand(
        IOptions<AppSettings> appSettings,
        ILogger<SyncCommand> logger,
        IKernelMemory memory,
        DocumentEmbeddingService documentEmbeddingService,
        SyncState syncState,
        TimeProvider timeProvider)
    {
        _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _documentEmbeddingService = documentEmbeddingService ?? throw new ArgumentNullException(nameof(documentEmbeddingService));
        _syncState = syncState ?? throw new ArgumentNullException(nameof(syncState));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, SyncSettings settings)
    {
        try
        {
            // Determine folder path (override or from config)
            var folderPath = settings.Folder ?? _appSettings.Value.RAG.DocumentFolderPath;
            
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] No folder path specified. Use --folder or configure DocumentFolderPath in appsettings.json");
                return 1;
            }

            if (!Directory.Exists(folderPath))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Folder not found: [yellow]{folderPath}[/]");
                return 1;
            }

            // Handle --force option
            if (settings.Force)
            {
                AnsiConsole.MarkupLine("[yellow]Force mode:[/] Will sync all documents regardless of last run time");
            }

            // Handle --dry-run option
            if (settings.DryRun)
            {
                return await ExecuteDryRunAsync(folderPath, settings);
            }

            // Show sync information
            AnsiConsole.MarkupLine($"[cyan]Syncing documents from:[/] [yellow]{folderPath}[/]");
            
            if (!settings.Force)
            {
                var lastRun = _syncState.GetLastRun();
                AnsiConsole.MarkupLine($"[dim]Last sync:[/] {lastRun:yyyy-MM-dd HH:mm:ss}");
            }
            AnsiConsole.WriteLine();

            // Perform the sync
            var filesSynced = await ExecuteSyncAsync(folderPath, settings);

            if (filesSynced > 0)
            {
                AnsiConsole.MarkupLine($"\n[green]Success[/] Sync completed [green]successfully[/] - {filesSynced} document(s) synced");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]info[/] No documents to sync - [yellow]all files are up to date[/] \n");
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sync: {Message}", ex.Message);
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private async Task<int> ExecuteDryRunAsync(string folderPath, SyncSettings settings)
    {
        AnsiConsole.MarkupLine("[yellow]DRY RUN MODE[/] - No changes will be made\n");
        AnsiConsole.MarkupLine($"[cyan]Scanning folder:[/] [yellow]{folderPath}[/]\n");

        // Get files that would be synced
        var pdfFiles = Directory.GetFiles(folderPath, "*.pdf", SearchOption.TopDirectoryOnly);
        var textFiles = Directory.GetFiles(folderPath, "*.txt", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(folderPath, "*.md", SearchOption.TopDirectoryOnly))
            .Concat(Directory.GetFiles(folderPath, "*.docx", SearchOption.TopDirectoryOnly));

        var allFiles = pdfFiles.Concat(textFiles).ToList();

        // Filter by last run time if not in force mode
        if (!settings.Force)
        {
            var lastRun = _syncState.GetLastRun();
            var originalCount = allFiles.Count;
            
            var lastRunUtc = lastRun.Kind == DateTimeKind.Utc ? lastRun : lastRun.ToUniversalTime();
            
            allFiles = allFiles.Where(file =>
            {
                var fileInfo = new FileInfo(file);
                // Check both LastWriteTime and CreationTime - if file was copied, CreationTime will be recent
                // Use the more recent of the two timestamps
                var mostRecentTime = fileInfo.LastWriteTimeUtc > fileInfo.CreationTimeUtc 
                    ? fileInfo.LastWriteTimeUtc 
                    : fileInfo.CreationTimeUtc;
                
                return mostRecentTime >= lastRunUtc;
            }).ToList();
            
            AnsiConsole.MarkupLine($"[dim]Last sync:[/] {lastRun:yyyy-MM-dd HH:mm:ss}");
            AnsiConsole.MarkupLine($"[dim]Filtering files modified since last sync...[/]");
            AnsiConsole.MarkupLine($"[dim]Found {allFiles.Count} file(s) to sync (from {originalCount} total)[/]\n");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Force mode:[/] All files would be synced\n");
        }

        if (allFiles.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No documents found to sync[/]");
            return 0;
        }

        // Create a table to show what would be synced
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.LightGreen)
            .AddColumn("[bold cyan]File Name[/]")
            .AddColumn("[bold cyan]Size[/]")
            .AddColumn("[bold cyan]Type[/]")
            .Width(100);

        foreach (var file in allFiles)
        {
            var fileInfo = new FileInfo(file);
            var sizeMB = fileInfo.Length / (1024.0 * 1024.0);
            var extension = Path.GetExtension(file).ToUpperInvariant();

            table.AddRow(
                $"[white]{Path.GetFileName(file)}[/]",
                $"[cyan]{sizeMB:F2} MB[/]",
                $"[dim]{extension}[/]"
            );
        }

        var panel = new Panel(table)
            .Header("[bold cyan]Files to Sync (Dry Run)[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .BorderStyle(Style.Parse("bold cyan"))
            .Padding(1, 1);
        
        AnsiConsole.Write(panel);
        AnsiConsole.MarkupLine($"\n[bold cyan]Total:[/] [white]{allFiles.Count}[/] document(s) would be synced");

        return await Task.FromResult(0);
    }

    private async Task<int> ExecuteSyncAsync(string folderPath, SyncSettings settings)
    {
        // Determine the cutoff time for filtering files
        DateTime? sinceDateTime = null;
        if (!settings.Force)
        {
            sinceDateTime = _syncState.GetLastRun();
        }

        // Check if there are any files to sync first
        var pdfFiles = Directory.GetFiles(folderPath, "*.pdf", SearchOption.TopDirectoryOnly);
        var textFiles = Directory.GetFiles(folderPath, "*.txt", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(folderPath, "*.md", SearchOption.TopDirectoryOnly))
            .Concat(Directory.GetFiles(folderPath, "*.docx", SearchOption.TopDirectoryOnly));
        var allFiles = pdfFiles.Concat(textFiles).ToList();

        // Filter by last run time if not in force mode
        if (sinceDateTime.HasValue)
        {
            var sinceUtc = sinceDateTime.Value.Kind == DateTimeKind.Utc ? sinceDateTime.Value : sinceDateTime.Value.ToUniversalTime();
            
            allFiles = allFiles.Where(file =>
            {
                var fileInfo = new FileInfo(file);
                // Check both LastWriteTime and CreationTime - if file was copied, CreationTime will be recent
                // Use the more recent of the two timestamps
                var mostRecentTime = fileInfo.LastWriteTimeUtc > fileInfo.CreationTimeUtc 
                    ? fileInfo.LastWriteTimeUtc 
                    : fileInfo.CreationTimeUtc;
                
                return mostRecentTime >= sinceUtc;
            }).ToList();
        }

        // If no files to sync, return early without showing progress bar
        if (allFiles.Count == 0)
        {
            return 0;
        }

        // Use Spectre.Console Progress API for better UX
        int filesSynced = 0;
        await AnsiConsole.Progress()
            .AutoRefresh(true)
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn()
            })
            .StartAsync(async ctx =>
            {
                // Create a task for the sync operation with the total number of files
                var task = ctx.AddTask("[green]Syncing documents[/]", maxValue: allFiles.Count);

                // Progress callback to update the progress bar
                Action<int, int, string> progressCallback = (current, total, fileName) =>
                {
                    // Update task description with current file name
                    task.Description = $"[green]Processing:[/] [cyan]{fileName}[/] [dim]({current}/{total})[/]";
                    
                    // Update progress
                    task.Value = current;
                };

                // Start the sync
                try
                {
                    filesSynced = await _documentEmbeddingService.ImportDocumentsFromFolderAsync(_memory, folderPath, sinceDateTime, progressCallback);
                    
                    // Update last run time if not in force mode and files were synced
                    if (!settings.Force && filesSynced > 0)
                    {
                        // Use UtcDateTime to ensure we have a proper UTC DateTime
                        var currentRunTime = _timeProvider.GetUtcNow().UtcDateTime;
                        _syncState.SetLastRun(currentRunTime);
                    }
                    
                    // Mark task as complete
                    task.Value = task.MaxValue;
                    task.Description = "[green]Sync completed[/]";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during document import: {Message}", ex.Message);
                    task.Description = $"[red]Error:[/] {ex.Message}";
                    throw;
                }
            });

        return filesSynced;
    }
}

