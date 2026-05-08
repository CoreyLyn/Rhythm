using System;
using Microsoft.Win32;

namespace Rhythm.Autostart;

public static class AutostartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "Rhythm";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(AppName) is string s && !string.IsNullOrWhiteSpace(s);
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (key is null)
            throw new InvalidOperationException($"Cannot open or create HKCU\\{RunKeyPath}");

        if (enabled)
        {
            var exePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("Environment.ProcessPath is null; cannot register autostart.");
            key.SetValue(AppName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
        }
    }
}
