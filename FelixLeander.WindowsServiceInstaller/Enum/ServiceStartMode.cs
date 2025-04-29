namespace FelixLeander.WindowsServiceInstaller.Enum;

/// <summary>
/// The values for the start mode for the service.
/// </summary>
public enum ServiceStartMode
{
    Boot = 0,
    System = 1,
    Auto = 2,
    Demand = 3,
    Disabled = 4,
}