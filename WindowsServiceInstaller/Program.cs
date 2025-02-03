using System.Diagnostics;
using System.Security.Principal;
using CliWrap;
using CliWrap.Buffered;
using CliWrap.Exceptions;

try
{
    if (!OperatingSystem.IsWindows())
    {
        Console.WriteLine("This program can only run on a windows operating system.");
        return 901;
    }

    if (args.Length > 0 && !File.Exists(args[0]))
    {
        Console.WriteLine($"The provided file dosen't exist. '{args[0]}'. Aborting.");
        return 910;
    }

    string? userInput;
    if (args.Length < 1)
    {
        Console.WriteLine("Enter/drop the path of the .exe service and press enter...");
        userInput = Console.ReadLine() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userInput))
        {
            Console.WriteLine("Empty input for file. Aborting.");
            return 920;
        }
    }
    else
        userInput = args[0];


    var serviceExecutable = new FileInfo(userInput.Trim('"'));
    if (!serviceExecutable.Exists)
    {
        Console.WriteLine($"File with path '{serviceExecutable}' does not exist. Aborting.");
        return 930;
    }

    if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
    {
        Console.WriteLine(
            "The program was not stared with the required permissions. Do you want to elevate the process? y/n");
        if (args.Length < 2 && Console.ReadLine() is not "y")
            return 931;

        var thisFile = Process.GetCurrentProcess().MainModule;
        if (thisFile == null)
        {
            Console.WriteLine("Could not find the file for this program.");
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
        Console.WriteLine(
            $"Type either 'install' or 'uninstall' and press enter, to do the selected action.{Environment.NewLine}Entering anything else will abort the process.");
        userInputSelection = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(userInputSelection))
        {
            Console.WriteLine($"Invalid selection. Aborting");
            return 940;
        }
    }
    else
        userInputSelection = args[1];


    switch (userInputSelection)
    {
        case "install":
            Console.WriteLine("Installing service...");
            var resultInstall = await Cli.Wrap("sc")
                .WithArguments([
                    "create", serviceName, $"binPath={serviceExecutable}", $"DisplayName={serviceDisplayName}",
                    "start=auto"
                ])
                .ExecuteAsync();

            if (!resultInstall.IsSuccess)
            {
                Console.WriteLine($"Could not install service '{serviceDisplayName}'. Aborting.");
                return 801;
            }

            Console.WriteLine(
                $"Service '{serviceDisplayName}' has been installed.{Environment.NewLine}Starting service...");
            var resultStart =
                await Cli.Wrap("sc")
                    .WithArguments(["start", serviceName])
                    .ExecuteAsync();

            if (!resultStart.IsSuccess)
            {
                Console.WriteLine($"Could not start service '{serviceDisplayName}'. Aborting.");
                return 802;
            }

            Console.WriteLine($"Service '{serviceDisplayName}' started.");
            break;

        case "uninstall":
            Console.WriteLine($"Uninstalling service '{serviceDisplayName}'...");

            var resultStatus = await Cli.Wrap("sc")
                .WithArguments(["query", serviceName])
                .ExecuteBufferedAsync();
            if (!resultStatus.IsSuccess)
            {
                Console.WriteLine($"Could not QUERY service '{serviceDisplayName}'. Aborting.");
                return 850;
            }

            var parsedState = resultStatus.StandardOutput
                    .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault(f => f.StartsWith("STATE"))?
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .LastOrDefault();

            if (parsedState == null)
            {
                Console.WriteLine($"Could not parse the QUERY for service '{serviceDisplayName}'. Aborting.");
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
                    Console.WriteLine($"Could not STOP service '{serviceDisplayName}'. Aborting.");
                    return 852;
                }
                Console.WriteLine($"Service '{serviceDisplayName}' stopped.");
            }
            else if (!parsedState.Equals("STOPPED", StringComparison.OrdinalIgnoreCase)) // handles all but previous and stopped.
            {
                Console.WriteLine($"Service {serviceDisplayName} STATE is invalid. Cannot stop/uninstall it.");
                return 853;
            } // TODO: Handle other cases.


            var resultDelete =
                await Cli.Wrap("sc")
                    .WithArguments(["delete", serviceName])
                    .ExecuteAsync();
            if (!resultDelete.IsSuccess)
            {
                Console.WriteLine($"Could not DELETE service '{serviceDisplayName}'. Aborting.");
                return 851;
            }

            Console.WriteLine($"Service '{serviceDisplayName}' uninstalled.");
            break;

        default:
            Console.WriteLine("Invalid selection. Aborting.");
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

    Console.WriteLine(errorText);
    return 101;
}
catch (Exception ex)
{
    Console.WriteLine($"Unexpected error: '{ex.Message}'. Aborting.");
    return 100;
}

return 0;

static void PrintAsciiTable(List<KeyValuePair<string, string>> keyValuePairs)
{
    if (keyValuePairs.Count == 0)
        return;

    var keyWidth = keyValuePairs.Max(kvp => kvp.Key.Length);
    var valueWidth = keyValuePairs.Max(kvp => kvp.Value.Length);
    var separator = $"+{new string('-', keyWidth + 2)}+{new string('-', valueWidth + 2)}+";

    Console.WriteLine(separator);
    Console.WriteLine($"| {"Property".PadRight(keyWidth)} | {"Value".PadRight(valueWidth)} |");
    Console.WriteLine(separator);

    foreach (var kvp in keyValuePairs)
        Console.WriteLine($"| {kvp.Key.PadRight(keyWidth)} | {kvp.Value.PadRight(valueWidth)} |");

    Console.WriteLine(separator);
}
