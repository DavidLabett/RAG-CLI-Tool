using Spectre.Console.Cli;
using System.ComponentModel;

namespace SecondBrain.Commands;

/// <summary>
/// Settings for the mode command
/// </summary>
public class ModeSettings : BaseSettings
{
    [CommandArgument(0, "<mode>")]
    [Description("Mode to set: 'local' (Ollama) or 'online' (CloudFlare)")]
    public string Mode { get; set; } = string.Empty;
}

