using CommandLine;
using ServiceInstaller.Enum;

namespace ServiceInstaller.Model;
internal sealed class Arguments
{
    [Option('s', nameof(ServiceName), Required = false, HelpText = "The actual name (used by the system) for the service.")]
    public string ServiceName { get; set; } = string.Empty;

    [Option('n', nameof(DisplayName), Required = false, HelpText = "The human readable display-name for the service.")]
    public string DisplayName { get; set; } = string.Empty;

    [Option('f', nameof(FilePath), Required = false, HelpText = "The file path of the executable.")]
    public string FilePath { get; set; } = string.Empty;

    [Option('d', nameof(Description), Required = false, HelpText = "Information describing the service.")]
    public string Description { get; set; } = string.Empty;

    [Option('o', nameof(Operation), Required = false, HelpText = "If false, installs the service. If true uninstalls.")]
    public Operation Operation { get; set; } = Operation.None;
}
