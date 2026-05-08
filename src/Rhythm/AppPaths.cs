using System;
using System.IO;

namespace Rhythm;

public static class AppPaths
{
    public static string AppDataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Rhythm");

    public static string StateFilePath { get; } = Path.Combine(AppDataDir, "state.json");

    public static void EnsureAppDataDir() => Directory.CreateDirectory(AppDataDir);
}
