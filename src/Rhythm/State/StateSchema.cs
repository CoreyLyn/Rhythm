using System;
using System.Collections.Generic;

namespace Rhythm.State;

public enum RhythmItemKind
{
    Daily = 0,
    OneTime = 1,
}

public sealed record RhythmItem(Guid Id, string Text, RhythmItemKind Kind = RhythmItemKind.Daily);

public sealed record WindowPos(double X, double Y, double Width, double Height, string ScreenDeviceName);

public sealed class StateDocument
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public List<RhythmItem> Items { get; init; } = new();
    public List<Guid> CompletedToday { get; init; } = new();
    public DateOnly LastResetDate { get; init; }
    public WindowPos? WindowPos { get; init; }
    public bool EnableTransparency { get; init; } = true;
    public PomodoroConfig PomodoroConfig { get; init; } = new();
    public PomodoroSessionSnapshot PomodoroSession { get; init; } = new();
}
