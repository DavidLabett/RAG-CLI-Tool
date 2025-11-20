using Spectre.Console.Cli;
using System.ComponentModel;

namespace SecondBrain.Commands;

/// <summary>
/// Settings for the rag command
/// </summary>
public class RagChatSettings : BaseSettings
{
    [CommandOption("--history")]
    [Description("Enable conversation history to maintain context across messages")]
    public bool History { get; set; }

    [CommandOption("--context")]
    [Description("Number of previous messages to include in context (default: 5)")]
    [DefaultValue(5)]
    public int Context { get; set; } = 5;

    [CommandOption("--model")]
    [Description("LLM model to use (overrides appsettings.json)")]
    public string? Model { get; set; }
}

