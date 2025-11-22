using Spectre.Console.Cli;
using System.ComponentModel;

namespace SecondBrain.Commands;

public class ListSettings : BaseSettings
{
    [CommandOption("--folder")]
    [Description("Override document folder path")]
    public string? Folder { get; set; }
}

