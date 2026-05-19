using System;

namespace Rhythm.State;

public enum PomodoroPhaseType
{
    Focus = 0,
    ShortBreak = 1,
    LongBreak = 2,
}

public enum PomodoroStatus
{
    Idle = 0,
    Running = 1,
    Paused = 2,
}

public sealed record PomodoroConfig(
    int FocusMinutes = 25,
    int ShortBreakMinutes = 5,
    int LongBreakMinutes = 15,
    int LongBreakEvery = 4,
    bool AutoStartNextPhase = true);

public sealed record PomodoroSessionState(
    PomodoroStatus Status = PomodoroStatus.Idle,
    PomodoroPhaseType PhaseType = PomodoroPhaseType.Focus,
    int RemainingSeconds = 0,
    int CompletedFocusCountInCycle = 0,
    int CompletedFocusCountToday = 0,
    Guid? LinkedItemId = null,
    DateTimeOffset? PhaseStartedAt = null,
    DateTimeOffset? LastUpdatedAt = null);
