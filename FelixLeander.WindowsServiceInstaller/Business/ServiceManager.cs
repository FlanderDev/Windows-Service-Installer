using CliWrap;
using CliWrap.Buffered;
using FelixLeander.WindowsServiceInstaller.Enum;
using Serilog;

namespace FelixLeander.WindowsServiceInstaller.Business;

/// <summary>
/// A class managing interaction with a service.
/// </summary>
/// <param name="serviceName">The specific name of the service.</param>
/// <param name="serviceDisplayName">The (optinal) dispaly name of the service, if not defined uses the <paramref name="serviceName"/> instead.</param>
internal sealed class ServiceManager(string serviceName, string? serviceDisplayName = null)
{
    /// <summary>
    /// Installs the service.
    /// </summary>
    /// <param name="serviceExecutable">The path of the executable, which will be executed as the service.</param>
    /// <param name="serviceStartMode">The start mode of the service.</param>
    /// <returns>Indication if the installtion succeeded.</returns>
    internal async Task<bool> InstallAsync(string serviceExecutable, ServiceStartMode serviceStartMode)
    {
        try
        {
            Log.Verbose("Installing service...");

            var resultInstall = await Cli
            .Wrap("sc")
            .WithArguments([
                "create",
                serviceName,
                $"DisplayName={serviceDisplayName ?? serviceName}",
                $"binPath={serviceExecutable}",
                $"start={serviceStartMode}"
            ])
            .ExecuteAsync();

            if (!resultInstall.IsSuccess)
            {
                Log.Warning("Could not install service '{displayName}' ({serviceName}). SC-ExitCode: {code}", serviceDisplayName ?? serviceName, serviceName, resultInstall.ExitCode);
                return false;
            }

            Log.Information("Sucessfully installed service: '{displayName}'.", serviceDisplayName ?? serviceName);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error installing service.");
            return false;
        }
    }

    /// <summary>
    /// Starts the service.
    /// </summary>
    /// <returns>Indiacting sucess or failure.</returns>
    internal async Task<bool> StartAsync()
    {
        var resultStart = await Cli
            .Wrap("sc")
            .WithArguments(["start", serviceName])
            .ExecuteAsync();

        if (!resultStart.IsSuccess)
        {
            Log.Warning("Could not START service '{displayName}' ({serviceName}). SC-ExitCode: {code}", serviceDisplayName, serviceName, resultStart.ExitCode);
            return resultStart.IsSuccess;
        }

        Log.Information("Service '{serviceDisplayName}' started.", serviceDisplayName);
        return resultStart.IsSuccess;
    }

    /// <summary>
    /// Sets the description of the service to <paramref name="description"/>.
    /// </summary>
    /// <param name="description">The descirption for the service.</param>
    /// <returns>Indiacting sucess or failure.</returns>
    internal async Task<bool> SetDescriptionAsync(string description)
    {
        try
        {
            var resultStart = await Cli
            .Wrap("sc")
            .WithArguments(["description", serviceName, description])
            .ExecuteAsync();

            if (!resultStart.IsSuccess)
            {
                Log.Warning("Could not add DESCRIPTION to service '{serviceName}'. SC-ExitCode: {code}", serviceName, resultStart.ExitCode);
                return resultStart.IsSuccess;
            }

            Log.Information("Description added to service '{serviceName}'.", serviceName);
            return resultStart.IsSuccess;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting description.");
            return false;
        }
    }

    /// <summary>
    /// Uninstalls the service (stops it if it is running).
    /// </summary>
    /// <returns>Indiacting sucess or failure.</returns>
    internal async Task<bool> UninstallAsync()
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
            } // Might wanna add more information for specific cases.


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
