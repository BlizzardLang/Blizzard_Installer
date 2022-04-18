using System;
using System.CommandLine;
using System.Runtime.InteropServices;

namespace Blizzard.Installer;

internal static class CommandLineHandler
{
    /// <summary>
    /// Handles the parsing of the command line flags and correct execution of the installer
    /// </summary>
    /// <param name="args"></param>
    public static void Handle(string[] args)
    {
        // Add all the command options
        rootCommand.Add(currentVersion);
        rootCommand.Add(targetVersion);
        rootCommand.Add(verbose);

        // Set the handler for the command line
        rootCommand.SetHandler((string currentVersion, string targetVersion, Architecture architecture, bool verbose) =>
        {
            var installer = new BlizzardInstaller(Version.Parse(currentVersion), verbose);
            if (targetVersion == "latest")
            {
                installer.InstallLatest();
            }
            else
            {
                installer.InstallVersion(Version.Parse(targetVersion));
            }

            Console.WriteLine("\n\n\nDone. Press any key to continue...");
            Console.ReadKey();
        }, currentVersion, targetVersion, verbose);

        // Invoke the handler with the given command line arguments
        rootCommand.Invoke(args);
    }

    private static readonly RootCommand rootCommand = new(
        description: "The command line installer for the Blizzard Programming Language."
    );

    private static readonly Option<string> currentVersion = new(
        aliases: new[] { "--current" },
        description: "The current Blizzard version."
    );

    private static readonly Option<string> targetVersion = new(
        aliases: new[] { "--target" },
        description: "The target version to install. Either 'latest' or a version 'x.x.x'.",
        getDefaultValue: () => "latest"
    );

    private static readonly Option<bool> verbose = new(
        aliases: new[] { "-v" },
        description: "Show verbose output."
    );
}
