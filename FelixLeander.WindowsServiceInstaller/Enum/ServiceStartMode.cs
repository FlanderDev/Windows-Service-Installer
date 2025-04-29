namespace FelixLeander.WindowsServiceInstaller.Enum;

/// <summary>
/// The values for the start mode for the service.
/// </summary>
public enum ServiceStartMode
{
    Boot,
    System,
    Auto,
    Demand,
    Disabled
}