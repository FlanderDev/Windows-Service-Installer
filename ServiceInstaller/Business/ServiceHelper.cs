using CliWrap;
using Serilog;
using CliWrap.Buffered;
using ServiceInstaller.Enum;

namespace ServiceInstaller.Business;
internal static class ServiceHelper
{
    internal static async Task InstallAsync(string serviceDisplayName, string serviceName, string serviceExecutable, string description, ServiceStartMode serviceStartMode)
    {
        Log.Verbose("Installing service...");

        var resultInstall = await Cli
        .Wrap("sc")
        .WithArguments([
            "create",
            serviceName,
            $"DisplayName={serviceDisplayName}",
            $"binPath={serviceExecutable}",
            $"start={serviceStartMode}"
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
            Log.Warning("Could not START service '{serviceDisplayName}'. SC-ExitCode: {code}", serviceDisplayName, resultStart.ExitCode);
            return resultStart.IsSuccess;
        }

        Log.Information("Service '{serviceDisplayName}' started.", serviceDisplayName);
        return resultStart.IsSuccess;
    }

    internal static async Task<bool> SetDescriptionAsync(string serviceName, string description)
    {
        var resultStart = await Cli
            .Wrap("sc")
            .WithArguments(["description ", serviceName])
            .ExecuteAsync();

        if (!resultStart.IsSuccess)
        {
            Log.Warning("Could not add DESCRIPTION to service '{serviceName}'. SC-ExitCode: {code}", serviceName, resultStart.ExitCode);
            return resultStart.IsSuccess;
        }

        Log.Information("Description added to service '{serviceName}'.", serviceName);
        return resultStart.IsSuccess;
    }

    internal static async Task<bool> UninstallAsync(string serviceDisplayName, string serviceName)
    {
        try
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
            } // TODO: Give more information on specific cases.


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
        catch (Exception ex)
        {
            Log.Error(ex, "Error uninstalling service.");
            return false;
        }
    }
}
