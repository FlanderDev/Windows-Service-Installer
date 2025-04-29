using CommandLine;
using FelixLeander.WindowsServiceInstaller.Enum;

namespace FelixLeander.WindowsServiceInstaller.Model;

/// <summary>
/// The argumetns for this program.
/// Based on the <see cref="Operation"/> the program will ask for the required arguments interactively.
/// </summary>
/// <remarks>If any of them is missing, they shall be autofilled interactively.</remarks>
internal sealed class Arguments
{
    /// <summary>
    /// The name of the service.
    /// </summary>
    /// <remarks>Fallback for <see cref="DisplayName"/>.</remarks>
    [Option('s', nameof(ServiceName), Required = false, HelpText = "The actual name (used by the system) for the service.")]
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// The human readable display-name for the service.
    /// </summary>
    /// <remarks>Can be replaced by the <see cref="DisplayName"/>.</remarks>
    [Option('n', nameof(DisplayName), Required = false, HelpText = "The human readable display-name for the service.")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The file path of the executable.
    /// </summary>
    [Option('f', nameof(FilePath), Required = false, HelpText = "The file path of the executable.")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// The decripiton of the service.
    /// </summary>
    [Option('d', nameof(Description), Required = false, HelpText = "Information describing the service.")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The operation which this program will perform.
    /// </summary>
    [Option('o', nameof(Operation), Required = false, HelpText = "If false, installs the service. If true uninstalls.")]
    public Operation Operation { get; set; } = Operation.None;
}
