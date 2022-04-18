using Newtonsoft.Json;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;

namespace Blizzard.Installer;

// TODO: Add an uninstall feature

public class BlizzardInstaller
{
    /// <summary>
    /// The version of Blizzard currently installed on the machine
    /// </summary>
    private readonly Version CurrentBlizzardVersion;
    /// <summary>
    /// The target system architecture to install
    /// </summary>
    private readonly Architecture SystemArchitecture = RuntimeInformation.OSArchitecture;
    /// <summary>
    /// Is verbose output requested
    /// </summary>
    private readonly bool VerboseOutput;

    /// <summary>
    /// HttpClient for communicating with GitHub and installing Blizzard
    /// </summary>
    static readonly HttpClient httpClient = new();

    public BlizzardInstaller(Version currentBlizzardVersion, bool verbose)
    {
        CurrentBlizzardVersion = currentBlizzardVersion;
        VerboseOutput = verbose;

        // Make sure the current platform is targeted for automatic install. Prompt for manual download otherwise.
        if (!(SystemArchitecture is Architecture.X86 or Architecture.X64 or Architecture.Arm or Architecture.Arm64) // Only targets x86, x64, Arm, Arm64
            || (OSPlatformIdentifier == "osx" && SystemArchitecture != Architecture.X64) // OS X only targets x64
            || (OSPlatformIdentifier == "linux" && !(SystemArchitecture is Architecture.X64 or Architecture.Arm))) // Linux only targets x64 and Arm
        {
            Console.WriteLine("We cannot determine which Blizzard Runtime to install. Please visit https://github.com/BlizzardLang/Blizzard/releases/latest and install the correct runtime.");
            Console.WriteLine("We appoligize for the inconvenience and request you submit an issue at https://github.com/BlizzardLang/Blizzard/issues alerting us about the error.");

            if (verbose)
            {
                throw new PlatformNotSupportedException();
            }

            Environment.Exit(0);
        }

        // Define http headers to correctly interact with the GitHub API
        httpClient.DefaultRequestHeaders.Add("User-Agent", "BlizzardLang/Blizzard Command Line Update Tool");
        httpClient.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/vnd.github.v3+json"));
        httpClient.DefaultRequestHeaders.Add("Accept", "application/octet-stream");
    }

    /// <summary>
    /// Gets the platform identifier of the current operating system
    /// </summary>
    /// <returns><c>win</c> or <c>osx</c> or <c>linux</c></returns>
    /// <exception cref="PlatformNotSupportedException">Thrown when the operating system is not Windows, OS X, or Linux</exception>
    private static string OSPlatformIdentifier
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" :
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" :
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "liunx" :
                throw new PlatformNotSupportedException("Operating System could not be detected as Windows, OS X, or Linux");

    /// <summary>
    /// Get the Blizzard Runtime Identifier for the operating system and architecture
    /// </summary>
    /// <returns>OS Platform Identifier-Architecture Type</returns>
    private string BlizzardRuntimeIdentifier => OSPlatformIdentifier + "-" + SystemArchitecture switch
    {
        Architecture.X64 => "x64",
        Architecture.X86 => "x86",
        Architecture.Arm64 => "arm-x64",
        Architecture.Arm => "arm",

        // Should never occur due to validation in constructor
        _ => throw new PlatformNotSupportedException(),
    };

    /// <summary>
    /// Installs the latest version of the Blizzard Runtime
    /// </summary>
    /// <exception cref="Exception"></exception>
    public void InstallLatest()
    {
        // Get the latest release version of Blizzard
        var res = httpClient.GetStringAsync("https://api.github.com/repos/BlizzardLang/Blizzard/releases/latest").Result;
        // Parse the release info into a C# object
        dynamic latestReleaseInfo = JsonConvert.DeserializeObject(res) ?? throw new HttpRequestException("Could not fetch latest version info. Try running this command specifying the target version.");
        // Get the latest version by splitting off the v from vX.X.X
        var latestVersion = Version.Parse($"{latestReleaseInfo.name}"[1..]);

        // Compare current and latest versions to determine if an update is needed
        switch (latestVersion.CompareTo(CurrentBlizzardVersion))
        {
            case > 0:
                InstallVersion(latestVersion);
                break;

            default:
                Console.WriteLine("No updates available. You are already using the latest release.");
                break;
        }
    }

    /// <summary>
    /// Installs the target version of the Blizzard Runtime
    /// </summary>
    /// <param name="targetVersion">The target runtime version</param>
    public void InstallVersion(Version targetVersion)
    {
        if (VerboseOutput)
        {
            Console.WriteLine($"Current Version: {CurrentBlizzardVersion.Major}.{CurrentBlizzardVersion.Minor}.{CurrentBlizzardVersion.Build}");
            Console.WriteLine($"Target Version:  {targetVersion}");
        }

        // File names for downloaded assets
        var targetZipFile = $"Blizzard_{targetVersion}.zip";

        var blizzardTargetRuntimeURI = $"https://github.com/BlizzardLang/Blizzard/releases/download/v{targetVersion}/Blizzard_{BlizzardRuntimeIdentifier}.zip";

        Console.WriteLine("\nDownloading Runtime" + (VerboseOutput ? $" from {blizzardTargetRuntimeURI}..." : "..."));

        // Download the release bundle by copying the download stream to a file stream
        using (var downloadStream = httpClient.GetStreamAsync(blizzardTargetRuntimeURI).Result)
        {
            using var fileStream = new FileStream(targetZipFile, FileMode.Create, FileAccess.Write);
            downloadStream.CopyTo(fileStream);
        }

        // Unzip the release bundle once it has finished downloading
        Console.WriteLine("\nUnzipping Runtime" + (VerboseOutput ? $" from {targetZipFile}..." : "..."));
        using (var archive = ZipFile.OpenRead(targetZipFile))
        {
            try
            {
                // Extract all the contents to the current directory
                archive.ExtractToDirectory(".", true);
                if (VerboseOutput)
                {
                    foreach (var entry in archive.Entries)
                    {
                        Console.WriteLine($"Extracted {entry.FullName}");
                    }
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine(ex.ToString());
                Console.WriteLine(ex.StackTrace);
#else
                if (VerboseOutput)
                {
                    Console.WriteLine(ex.ToString());
                    Console.WriteLine(ex.StackTrace);
                }
                else
                {
                    Console.WriteLine($"There was an error extracting file {archive}\nTry running the command with verbose mode enabled.");
                }
#endif
            }
        }

        // Delete the bundled runtime
        Console.WriteLine("\nRemoving Update Artifacts...");
        File.Delete(targetZipFile);
        if (VerboseOutput)
        {
            Console.WriteLine($"Deleted file {targetZipFile}");
        }
    }
}
