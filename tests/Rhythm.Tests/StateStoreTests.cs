using System;
using System.IO;
using System.Linq;
using Rhythm.State;
using Xunit;

namespace Rhythm.Tests;

public sealed class StateStoreTests : IDisposable
{
    private readonly string _tmpDir;
    private readonly string _path;

    public StateStoreTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), "Rhythm-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tmpDir);
        _path = Path.Combine(_tmpDir, "state.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tmpDir, recursive: true); } catch { }
    }

    [Fact]
    public void Save_Then_Load_RoundTripsAllFields()
    {
        var doc = new StateDocument
        {
            SchemaVersion = StateDocument.CurrentSchemaVersion,
            Items = new()
            {
                new RhythmItem(Guid.NewGuid(), "A"),
                new RhythmItem(Guid.NewGuid(), "B", RhythmItemKind.OneTime),
            },
            CompletedToday = new() { Guid.NewGuid() },
            LastResetDate = new DateOnly(2026, 5, 8),
            WindowPos = new WindowPos(10, 20, 300, 400, @"\\.\DISPLAY1"),
            EnableTransparency = false,
        };

        StateStore.Save(doc, _path);
        var loaded = StateStore.Load(_path);

        Assert.Equal(doc.SchemaVersion, loaded.SchemaVersion);
        Assert.Equal(doc.Items.Select(i => i.Text).ToArray(), loaded.Items.Select(i => i.Text).ToArray());
        Assert.Equal(doc.Items.Select(i => i.Kind).ToArray(), loaded.Items.Select(i => i.Kind).ToArray());
        Assert.Equal(doc.CompletedToday, loaded.CompletedToday);
        Assert.Equal(doc.LastResetDate, loaded.LastResetDate);
        Assert.Equal(doc.WindowPos, loaded.WindowPos);
        Assert.Equal(doc.EnableTransparency, loaded.EnableTransparency);
    }

    [Fact]
    public void Load_DefaultsLegacyItemsToDailyKind()
    {
        File.WriteAllText(_path, """
        {
          "schemaVersion": 1,
          "items": [
            {
              "id": "11111111-1111-1111-1111-111111111111",
              "text": "Legacy"
            }
          ],
          "completedToday": [],
          "lastResetDate": "2026-05-08",
          "enableTransparency": true
        }
        """);

        var loaded = StateStore.Load(_path);

        var item = Assert.Single(loaded.Items);
        Assert.Equal("Legacy", item.Text);
        Assert.Equal(RhythmItemKind.Daily, item.Kind);
    }

    [Fact]
    public void Load_ReturnsDefault_WhenFileMissing()
    {
        var loaded = StateStore.Load(_path);
        Assert.Equal(StateDocument.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Empty(loaded.Items);
        Assert.Empty(loaded.CompletedToday);
        Assert.True(loaded.EnableTransparency); // Default value
    }

    [Fact]
    public void Load_ReturnsDefault_WhenFileCorrupt()
    {
        File.WriteAllText(_path, "{ not valid JSON }");
        var loaded = StateStore.Load(_path);
        Assert.Equal(StateDocument.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Empty(loaded.Items);
    }

    [Fact]
    public void Save_DoesNotLeaveTempFile()
    {
        var doc = new StateDocument { LastResetDate = new DateOnly(2026, 5, 8) };
        StateStore.Save(doc, _path);
        Assert.False(File.Exists(_path + ".tmp"));
        Assert.True(File.Exists(_path));
    }
}
