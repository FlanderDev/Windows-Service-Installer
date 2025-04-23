using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceInstaller.Enum;

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