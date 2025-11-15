using Spectre.Console.Cli;
using System.ComponentModel;

namespace SecondBrain.Commands;

/// <summary>
/// Settings for the sync command
/// </summary>
public class SyncSettings : BaseSettings
{
    [CommandOption("--folder")]
    [Description("Override document folder path")]
    public string? Folder { get; set; }

    [CommandOption("--force")]
    [Description("Force re-embedding of all documents (ignores last run time)")]
    public bool Force { get; set; }

    [CommandOption("--dry-run")]
    [Description("Show what would be synced without doing it")]
    public bool DryRun { get; set; }
}

