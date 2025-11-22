using Spectre.Console.Cli;
using System.ComponentModel;

namespace SecondBrain.Commands;

// Base settings class for global options that apply to all commands
public class BaseSettings : CommandSettings
{
    [CommandOption("-c|--config")]
    [Description("Path to configuration file")]
    public string? ConfigPath { get; set; }
}

