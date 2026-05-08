using System;
using System.IO;
using System.Text.Json;

namespace Rhythm.State;

public static class StateStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static StateDocument Load(string? path = null)
    {
        var actualPath = path ?? AppPaths.StateFilePath;
        if (path is null)
            AppPaths.EnsureAppDataDir();
        if (!File.Exists(actualPath))
            return CreateDefault();

        try
        {
            using var stream = File.OpenRead(actualPath);
            var doc = JsonSerializer.Deserialize<StateDocument>(stream, Options);
            if (doc is null)
            {
                Console.Error.WriteLine("[Rhythm] state.json deserialized to null; using defaults.");
                return CreateDefault();
            }
            return doc;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"[Rhythm] state.json corrupt ({ex.Message}); using defaults.");
            return CreateDefault();
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"[Rhythm] state.json read failed ({ex.Message}); using defaults.");
            return CreateDefault();
        }
    }

    public static void Save(StateDocument doc, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(doc);
        var actualPath = path ?? AppPaths.StateFilePath;
        if (path is null)
            AppPaths.EnsureAppDataDir();
        var tmpPath = actualPath + ".tmp";

        var json = JsonSerializer.Serialize(doc, Options);
        File.WriteAllText(tmpPath, json);
        File.Move(tmpPath, actualPath, overwrite: true);
    }

    private static StateDocument CreateDefault() => new()
    {
        SchemaVersion = StateDocument.CurrentSchemaVersion,
        LastResetDate = DateOnly.FromDateTime(DateTime.Today),
    };
}
