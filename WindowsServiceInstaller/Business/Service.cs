using System.ServiceProcess;
using CliWrap;
using CliWrap.Buffered;
using Serilog;

namespace WindowsServiceInstaller.Business;

/// <summary>
/// Handles interaction with the windows service management.
/// </summary>
internal static class Service
{
    internal static async Task InstallAsync(string serviceDisplayName, string serviceName, string serviceExecutable, ServiceStartMode serviceStartMode = ServiceStartMode.Automatic)
    {
        Log.Verbose("Installing service...");

        var resultInstall = await Cli
        .Wrap("sc")
        .WithArguments([
            "create", serviceName, $"binPath={serviceExecutable}", $"DisplayName={serviceDisplayName}",
                "start=auto"
        ])
        .ExecuteAsync();

        if (!resultInstall.IsSuccess)
        {
            Log.Warning("Could not install service '{serviceDisplayName}'. SC-ExitCode: {code}", serviceDisplayName, resultInstall.ExitCode);
            throw new ApplicationException($"Unknown error while installing service '{serviceDisplayName}'. SC-ExitCode: {resultInstall.ExitCode}");
        }

        Log.Information("");
    }

    internal static async Task<bool> StartAsync(string serviceDisplayName, string serviceName)
    {
        var resultStart = await Cli
            .Wrap("sc")
            .WithArguments(["start", serviceName])
            .ExecuteAsync();

        if (!resultStart.IsSuccess)
        {
            Log.Warning("Could not start service '{serviceDisplayName}'. SC-ExitCode: {code}", serviceDisplayName, resultStart.ExitCode);
            return resultStart.IsSuccess;
        }

        Log.Information("Service '{serviceDisplayName}' started.", serviceDisplayName);
        return resultStart.IsSuccess;
    }

    internal static async Task<bool> UninstallAsync(string serviceDisplayName, string serviceName)
    {
        Log.Information($"Uninstalling service '{serviceDisplayName}'...");

        var resultStatus = await Cli.Wrap("sc")
            .WithArguments(["query", serviceName])
            .ExecuteBufferedAsync();
        if (!resultStatus.IsSuccess)
        {
            Log.Warning($"Could not QUERY service '{serviceDisplayName}'. Aborting.");
            return false;
        }

        var parsedState = resultStatus.StandardOutput
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(f => f.StartsWith("STATE"))?
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();

        if (parsedState == null)
        {
            Log.Error($"Could not parse the QUERY for service '{serviceDisplayName}'. Aborting.");
            return false;
        }

        if (parsedState.Equals("RUNNING", StringComparison.OrdinalIgnoreCase)) //stop if running
        {
            var resultStop =
                await Cli.Wrap("sc")
                    .WithArguments(["stop", serviceName])
                    .ExecuteAsync();
            if (!resultStop.IsSuccess)
            {
                Log.Warning($"Could not STOP service '{serviceDisplayName}'. Aborting.");
                return false;
            }
            Log.Information($"Service '{serviceDisplayName}' stopped.");
        }
        else if (!parsedState.Equals("STOPPED", StringComparison.OrdinalIgnoreCase)) // handles all but previous and stopped.
        {
            Log.Warning($"Service {serviceDisplayName} STATE is invalid. Cannot stop/uninstall it.");
            return false;
        } // TODO: Handle other cases.


        var resultDelete =
            await Cli.Wrap("sc")
                .WithArguments(["delete", serviceName])
                .ExecuteAsync();
        if (!resultDelete.IsSuccess)
        {
            Log.Warning($"Could not DELETE service '{serviceDisplayName}'. Aborting.");
            return false;
        }

        Log.Information($"Service '{serviceDisplayName}' uninstalled.");
        return true;
    }
}
