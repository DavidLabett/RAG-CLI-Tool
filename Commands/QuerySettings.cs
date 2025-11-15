using Spectre.Console.Cli;
using System.ComponentModel;

namespace SecondBrain.Commands;

/// <summary>
/// Settings for the query command
/// </summary>
public class QuerySettings : BaseSettings
{
    [CommandArgument(0, "<question>")]
    [Description("The question to query the knowledge base")]
    public string Question { get; set; } = string.Empty;

    [CommandOption("--limit")]
    [Description("Number of results to return (default: 5)")]
    public int Limit { get; set; } = 5;

    [CommandOption("--sources")]
    [Description("Include source document information")]
    public bool Sources { get; set; }

    [CommandOption("--no-llm")]
    [Description("Only return search results, don't generate answer")]
    public bool NoLlm { get; set; }
}

