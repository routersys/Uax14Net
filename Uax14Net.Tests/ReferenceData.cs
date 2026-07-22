using System;
using System.IO;

namespace Uax14Net.Tests;

internal static class ReferenceData
{
    public static string Directory { get; } = Locate();

    public static string Path(string name) => System.IO.Path.Combine(Directory, name);

    private static string Locate()
    {
        string? dir = AppContext.BaseDirectory;
        for (int i = 0; i < 12 && dir is not null; i++)
        {
            string candidate = System.IO.Path.Combine(dir, "reference", "data");
            if (File.Exists(System.IO.Path.Combine(candidate, "LineBreakTest.txt")))
            {
                return candidate;
            }
            dir = System.IO.Path.GetDirectoryName(dir);
        }
        throw new DirectoryNotFoundException(
            "reference/data with LineBreakTest.txt was not found. Run reference/build.sh or reference/build.bat first.");
    }
}
