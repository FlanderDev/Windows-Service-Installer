using System.Diagnostics;
using System.Security.Principal;
using CliWrap;
using CliWrap.Buffered;
using CliWrap.Exceptions;
using Serilog;

try
{
    const string template = "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}";
    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console(outputTemplate: template)
        .WriteTo.File("log/log_.log", rollingInterval: RollingInterval.Day, outputTemplate: template)
        .CreateLogger();

    if (!OperatingSystem.IsWindows())
    {
        Log.Warning("This program can only run on a windows operating system.");
        return 901;
    }

    if (args.Length > 0 && !File.Exists(args[0]))
    {
        Log.Warning($"The provided file dosen't exist. '{args[0]}'. Aborting.");
        return 910;
    }

    string? userInput;
    if (args.Length < 1)
    {
        Log.Verbose("Enter/drop the path of the .exe service and press enter...");
        userInput = Console.ReadLine() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userInput))
        {
            Log.Warning("Empty input for file. Aborting.");
            return 920;
        }
    }
    else
        userInput = args[0];


    var serviceExecutable = new FileInfo(userInput.Trim('"'));
    if (!serviceExecutable.Exists)
    {
        Log.Warning($"File with path '{serviceExecutable}' does not exist. Aborting.");
        return 930;
    }

    if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
    {
        Log.Verbose("The program was not stared with the required permissions. Do you want to elevate the process? y/n");
        if (args.Length < 2 && Console.ReadLine() is not "y")
            return 931;

        var thisFile = Process.GetCurrentProcess().MainModule;
        if (thisFile == null)
        {
            Log.Warning("Could not find the file for this program.");
            return 932;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = thisFile.FileName,
            UseShellExecute = true,
            Verb = "runas",
            ArgumentList = { serviceExecutable.FullName, args.Length > 1 ? args[1] : string.Empty }
        });
        return 933;
    }

    var serviceDisplayName = Path.GetFileNameWithoutExtension(serviceExecutable.Name);

    var serviceName = Path
        .GetInvalidPathChars() //Why not GetInvalidFileChars? Because we get the name from the file already.
        .Aggregate(serviceDisplayName, (current, invalidChar) => current.Replace(invalidChar.ToString(), string.Empty));

    PrintAsciiTable([
        new KeyValuePair<string, string>("Service Display Name", serviceDisplayName),
        new KeyValuePair<string, string>("Service Name", serviceName),

    ]);


    string? userInputSelection;
    if (args.Length < 2)
    {
        Log.Verbose(
            $"Type either 'install' or 'uninstall' and press enter, to do the selected action.{Environment.NewLine}Entering anything else will abort the process.");
        userInputSelection = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(userInputSelection))
        {
            Log.Warning("Invalid selection. Aborting");
            return 940;
        }
    }
    else
        userInputSelection = args[1];


    switch (userInputSelection)
    {
        case "install":
            Log.Verbose("Installing service...");
            var resultInstall = await Cli.Wrap("sc")
                .WithArguments([
                    "create", serviceName, $"binPath={serviceExecutable}", $"DisplayName={serviceDisplayName}",
                    "start=auto"
                ])
                .ExecuteAsync();

            if (!resultInstall.IsSuccess)
            {
                Log.Warning($"Could not install service '{serviceDisplayName}'. Aborting.");
                return 801;
            }

            Log.Information($"Service '{serviceDisplayName}' has been installed.{Environment.NewLine}Starting service...");
            var resultStart =
                await Cli.Wrap("sc")
                    .WithArguments(["start", serviceName])
                    .ExecuteAsync();

            if (!resultStart.IsSuccess)
            {
                Log.Warning($"Could not start service '{serviceDisplayName}'. Aborting.");
                return 802;
            }

            Log.Information($"Service '{serviceDisplayName}' started.");
            break;

        case "uninstall":
            Log.Information($"Uninstalling service '{serviceDisplayName}'...");

            var resultStatus = await Cli.Wrap("sc")
                .WithArguments(["query", serviceName])
                .ExecuteBufferedAsync();
            if (!resultStatus.IsSuccess)
            {
                Log.Warning($"Could not QUERY service '{serviceDisplayName}'. Aborting.");
                return 850;
            }

            var parsedState = resultStatus.StandardOutput
                    .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault(f => f.StartsWith("STATE"))?
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .LastOrDefault();

            if (parsedState == null)
            {
                Log.Error($"Could not parse the QUERY for service '{serviceDisplayName}'. Aborting.");
                return 851;
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
                    return 852;
                }
                Log.Information($"Service '{serviceDisplayName}' stopped.");
            }
            else if (!parsedState.Equals("STOPPED", StringComparison.OrdinalIgnoreCase)) // handles all but previous and stopped.
            {
                Log.Warning($"Service {serviceDisplayName} STATE is invalid. Cannot stop/uninstall it.");
                return 853;
            } // TODO: Handle other cases.


            var resultDelete =
                await Cli.Wrap("sc")
                    .WithArguments(["delete", serviceName])
                    .ExecuteAsync();
            if (!resultDelete.IsSuccess)
            {
                Log.Warning($"Could not DELETE service '{serviceDisplayName}'. Aborting.");
                return 851;
            }

            Log.Information($"Service '{serviceDisplayName}' uninstalled.");
            break;

        default:
            Log.Warning("Invalid selection. Aborting.");
            return 890;
    }
}
catch (CommandExecutionException ex)
{
    const string link = "https://learn.microsoft.com/en-us/windows/win32/debug/system-error-codes#system-error-codes";
    var errorText = ex.ExitCode switch
    {
        5 => "Invalid permissions. Try executing this program as privileged user. Aborting.",
        1073 => "The specified service already exists. Aborting.",
        _ => $"Unexpected command execution error: '{ex.Message}'{Environment.NewLine}Find your exitCode '{ex.ExitCode}' here:{Environment.NewLine}{link}."
    };

    Log.Error(errorText);
    return 101;
}
catch (Exception ex)
{
    Log.Fatal($"Unexpected error: '{ex.Message}'. Aborting.");
    return 100;
}

return 0;

static void PrintAsciiTable(List<KeyValuePair<string, string>> keyValuePairs)
{
    if (keyValuePairs.Count == 0)
    {
        Log.Warning("No data to display in ASCII-table.");
        return;
    }

    var keyWidth = keyValuePairs.Max(kvp => kvp.Key.Length);
    var valueWidth = keyValuePairs.Max(kvp => kvp.Value.Length);
    var separator = $"+{new string('-', keyWidth + 2)}+{new string('-', valueWidth + 2)}+";

    Log.Verbose(separator);
    Log.Verbose($"| {"Property".PadRight(keyWidth)} | {"Value".PadRight(valueWidth)} |");
    Log.Verbose(separator);

    foreach (var kvp in keyValuePairs)
        Log.Verbose($"| {kvp.Key.PadRight(keyWidth)} | {kvp.Value.PadRight(valueWidth)} |");

    Log.Verbose(separator);
}
