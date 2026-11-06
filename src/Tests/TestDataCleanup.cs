using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Horse.Messaging.Tests;

/// <summary>
/// Linked into every test project by Tests/Directory.Build.props.
/// Test servers create a new data folder for each run (data-*, pt-data-*, bench-data-*) and never delete them.
/// The module initializer runs once when the test assembly is loaded, before any test creates a folder,
/// so it removes only the folders left by previous runs.
/// </summary>
internal static class TestDataCleanup
{
    //{prefix}data-{Environment.TickCount}-{random}. TickCount wraps to negative values on long uptimes.
    private static readonly Regex DataFolderPattern = new(@"^(pt-|bench-)?data--?\d+-\d+$", RegexOptions.Compiled);

    [ModuleInitializer]
    internal static void DeleteLeftoverDataFolders()
    {
        try
        {
            foreach (string directory in Directory.GetDirectories(Directory.GetCurrentDirectory()))
            {
                if (!DataFolderPattern.IsMatch(Path.GetFileName(directory)))
                    continue;

                try
                {
                    Directory.Delete(directory, true);
                }
                catch (Exception)
                {
                    //a locked folder must not fail the test run, it is tried again in the next run
                }
            }
        }
        catch (Exception)
        {
        }
    }
}
