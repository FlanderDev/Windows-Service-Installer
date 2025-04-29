using CommandLine;
using FelixLeander.WindowsServiceInstaller.Business;
using FelixLeander.WindowsServiceInstaller.Enum;
using FelixLeander.WindowsServiceInstaller.Model;
using Serilog;
using Serilog.Events;

try
{
    #region Setup
    const string template = "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}";
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Verbose()
        .WriteTo.Console(outputTemplate: template)
        .WriteTo.File("log/log_.log", LogEventLevel.Information, rollingInterval: RollingInterval.Day, outputTemplate: template)
        .CreateLogger();

    if (!OperatingSystem.IsWindows())
    {
        Log.Warning("This program can only run on a windows operating systems.");
        return 301;
    }

    var result = Parser.Default.ParseArguments<Arguments>(args);
    if (result.Value is not { } arg || result.Errors.Any())
    {
        Log.Warning("Could not parse arguments:");
        var text = string.Join(Environment.NewLine, result.Errors.Select(s => s.Tag).ToArray());
        Log.Warning(text);
        return 302;
    }

    Helper.LogArgumentValues(arg);
    Console.WriteLine();
    #endregion

    if (!Helper.PromptUserForMissingArguments(arg, out var exitCode))
    {
        Log.Warning("Invalid input, ending application.");
        return exitCode;
    }

    if (!Helper.StartChildProcessAsAdmin(Parser.Default.FormatCommandLine(arg), out var process))
    {
        Log.Warning("Could not start child process as admin, ending application.");
        return 305;
    }

    if (process != null)
    {
        process.OutputDataReceived += (s, e) => Log.Verbose(e.Data ?? "[OUTPUT_STREAM]");
        process.ErrorDataReceived += (s, e) => Log.Verbose(e.Data ?? "[ERROR_STREAM]");

        Log.Verbose("Waiting for child process to finish...");
        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
            Log.Information("Child process exited successfully.");
        else
            Log.Warning("Child process exited with an error: {exitCode}", process.ExitCode);

        Log.Information("Ending parent application.");
        return 200;
    }
    // Starting here the application has admin rights!

    if (Operation.Install == arg.Operation)
    {
        await ServiceHelper.InstallAsync(arg.DisplayName, arg.ServiceName, arg.FilePath, arg.Description, ServiceStartMode.Auto);
        await ServiceHelper.SetDescriptionAsync(arg.ServiceName, arg.Description);
        await ServiceHelper.StartAsync(arg.DisplayName, arg.ServiceName);
    }
    else if (Operation.Uninstall == arg.Operation)
        await ServiceHelper.UninstallAsync(arg.DisplayName, arg.ServiceName);
    else
        Log.Verbose("Unsupported operation.");

    Log.Verbose("End of application.");
    await Task.Delay(3000);
    return 0;
}
catch (Exception ex)
{
    Log.Error(ex, "Unhandled exception.");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
