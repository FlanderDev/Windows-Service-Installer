using Serilog;
using System.Diagnostics;
using System.Security.Principal;
using ServiceInstaller.Model;
using ServiceInstaller.Enum;
using System;

namespace ServiceInstaller.Business;

internal static class Helper
{
    internal static void LogArgumentValues(Arguments arg)
    {
        Log.Information("The following arguments have been provided:");
        Log.Information("'{name}' is set to '{value}'.", nameof(Arguments.ServiceName), arg.ServiceName);
        Log.Information("'{name}' is set to '{value}'.", nameof(Arguments.DisplayName), arg.DisplayName);
        Log.Information("'{name}' is set to '{value}'.", nameof(Arguments.FilePath), arg.FilePath);
        Log.Information("'{name}' is set to '{value}'.", nameof(Arguments.Description), arg.Description);
        Log.Information("'{name}' is set to '{value}'.", nameof(Arguments.Operation), arg.Operation);
    }

    internal static bool StartChildProcessAsAdmin(string arguments, out Process? process)
    {
        process = null;

        try
        {
            if (new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            {
                Log.Verbose("Program started with admin permissions.");
                return true;
            }

            Log.Verbose("The program was not stared with the required permissions. Do you want to elevate the process? y/N");
            if (Console.ReadLine()?.ToLower() is not "y")
                return false;

            var thisFile = Process.GetCurrentProcess().MainModule;
            if (thisFile == null)
            {
                Log.Warning("Could not find the file for this program.");
                return false;
            }

            process = Process.Start(new ProcessStartInfo
            {
                FileName = thisFile.FileName,
                UseShellExecute = true,
                Verb = "runas",
                Arguments = arguments
            });

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Process handling failed.");
            return false;
        }
    }

    /// <summary>
    /// Prompts the user for missing arguments, if necessary.
    /// </summary>
    /// <param name="arguments">The arguments instance, which will be changed.</param>
    /// <param name="exitCode">The error code this application will return, 0 if no error occured.</param>
    /// <returns>An indicator if acceptable data was inputted.</returns>
    internal static bool PromptUserForMissingArguments(Arguments arguments, out int exitCode)
    {
        if (arguments.Operation == Operation.None)
        {
            Log.Verbose("Do you want to install or uninstall a service?{newline}{selection}",
                Environment.NewLine,
                string.Join(Environment.NewLine, "(1) Install", "(2) Uninstall"));

            arguments.Operation = int.TryParse(Console.ReadLine(), out var num) ? (Operation)num : Operation.None;
            if (Operation.None == arguments.Operation)
            {
                exitCode = 400;
                return false;
            }
        }

        if (Operation.Install == arguments.Operation)
        {
            if (PromptForValue(arguments.FilePath, nameof(Arguments.FilePath)) is not { } filePath)
            {
                exitCode = 303;
                return false;
            }
            arguments.FilePath = filePath;
            var fileName = Path.GetFileNameWithoutExtension(arguments.FilePath);

            if (PromptForValue(arguments.DisplayName, nameof(Arguments.DisplayName), $"EMP.Service.{fileName}") is not { } displayName)
            {
                exitCode = 304;
                return false;
            }
            arguments.DisplayName = displayName;

            if (PromptForValue(arguments.ServiceName, nameof(Arguments.ServiceName), displayName) is not { } serviceName)
            {
                exitCode = 305;
                return false;
            }
            arguments.ServiceName = serviceName;

            if (PromptForValue(arguments.Description, nameof(Arguments.Description)) is not { } description)
            {
                exitCode = 306;
                return false;
            }
            arguments.Description = description;
        }

        if (Operation.Uninstall == arguments.Operation)
        {
            if (PromptForValue(arguments.ServiceName, nameof(Arguments.ServiceName)) is not { } serviceName)
            {
                exitCode = 307;
                return false;
            }
            arguments.ServiceName = serviceName;
        }

        exitCode = 0;
        return true;
    }

    private static string? PromptForValue(string value, string valueName, string? recommendation = null)
    {
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        Log.Verbose("Enter a value for '{name}'.", valueName);

        var input = (recommendation == null ? Console.ReadLine() ?? string.Empty : ReadLine(recommendation)).Trim('"').Trim();
        if (!string.IsNullOrWhiteSpace(input))
            return input;

        Log.Verbose("Invalid value for '{name}'.", valueName);
        return null;
    }

    private static string ReadLine(string preText)
    {
        Console.Write(preText);
        List<char> chars = string.IsNullOrEmpty(preText) ? [] : [.. preText];
        while (true)
        {
            var input = Console.ReadKey(true);
            if (!char.IsControl(input.KeyChar))
            {
                chars.Insert(Console.CursorLeft, input.KeyChar);
                PrintRight();
                Console.CursorLeft += 1;
            }

            switch (input.Key)
            {
                case ConsoleKey.RightArrow when chars.Count > Console.CursorLeft:
                    Console.CursorLeft += 1;
                    break;

                case ConsoleKey.LeftArrow when Console.CursorLeft > 0:
                    Console.CursorLeft -= 1;
                    break;

                case ConsoleKey.Backspace when Console.CursorLeft > 0:
                    Console.CursorLeft -= 1;
                    chars.RemoveAt(Console.CursorLeft);
                    PrintRight();
                    break;

                case ConsoleKey.Delete when chars.Count > Console.CursorLeft:
                    var pos = Console.CursorLeft;
                    var deleted = chars.ElementAtOrDefault(pos);
                    if (deleted == default)
                        continue;

                    chars.RemoveAt(pos);
                    PrintRight();
                    break;

                case ConsoleKey.Enter:
                    Console.WriteLine();
                    return new string(chars.ToArray());
            }
        }

        void PrintRight()
        {
            var pos = Console.CursorLeft;
            Console.Write([.. chars[pos..], ' ']);
            Console.CursorLeft = pos;
        }
    }
}
