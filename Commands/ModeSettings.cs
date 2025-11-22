using Spectre.Console.Cli;
using System.ComponentModel;

namespace SecondBrain.Commands;

public class ModeSettings : BaseSettings
{
    [CommandArgument(0, "<mode>")]
    [Description("Mode to set: 'local' (Ollama) or 'online' (CloudFlare)")]
    public string Mode { get; set; } = string.Empty;
}

