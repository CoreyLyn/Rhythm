using System;

namespace Rhythm.State;

public sealed class PomodoroStateMachine
{
    public PomodoroStateMachine(PomodoroConfig config, PomodoroSessionSnapshot session)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        Session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public PomodoroConfig Config { get; }

    public PomodoroSessionSnapshot Session { get; private set; }

    public PomodoroSessionSnapshot Start(DateTimeOffset now, Guid? linkedItemId = null)
    {
        if (Session.Status != PomodoroStatus.Idle)
            return Session;

        Session = Session with
        {
            Status = PomodoroStatus.Running,
            PhaseType = PomodoroPhaseType.Focus,
            RemainingSeconds = GetPhaseDurationSeconds(PomodoroPhaseType.Focus),
            LinkedItemId = linkedItemId,
            PhaseStartedAt = now,
            LastUpdatedAt = now,
        };

        return Session;
    }

    public PomodoroSessionSnapshot Pause(DateTimeOffset now)
    {
        if (Session.Status != PomodoroStatus.Running)
            return Session;

        Session = AdvanceRunningSession(now) with
        {
            Status = PomodoroStatus.Paused,
            LastUpdatedAt = now,
        };

        return Session;
    }

    public PomodoroSessionSnapshot Resume(DateTimeOffset now)
    {
        if (Session.Status != PomodoroStatus.Paused)
            return Session;

        Session = Session with
        {
            Status = PomodoroStatus.Running,
            PhaseStartedAt = now,
            LastUpdatedAt = now,
        };

        return Session;
    }

    public PomodoroSessionSnapshot Reset()
    {
        Session = Session with
        {
            Status = PomodoroStatus.Idle,
            PhaseType = PomodoroPhaseType.Focus,
            RemainingSeconds = GetPhaseDurationSeconds(PomodoroPhaseType.Focus),
            CompletedFocusCountInCycle = 0,
            LinkedItemId = null,
            PhaseStartedAt = null,
            LastUpdatedAt = null,
        };

        return Session;
    }

    public PomodoroSessionSnapshot AdvanceTo(DateTimeOffset now)
    {
        if (Session.Status != PomodoroStatus.Running)
            return Session;

        Session = AdvanceRunningSession(now);
        return Session;
    }

    private PomodoroSessionSnapshot AdvanceRunningSession(DateTimeOffset now)
    {
        if (Session.PhaseStartedAt is null)
        {
            return Session with
            {
                LastUpdatedAt = now,
            };
        }

        var baseline = Session.LastUpdatedAt ?? Session.PhaseStartedAt.Value;
        var elapsedSeconds = (int)Math.Max(0, (now - baseline).TotalSeconds);

        if (elapsedSeconds <= 0)
            return Session;

        var remainingSeconds = Math.Max(0, Session.RemainingSeconds - elapsedSeconds);

        return Session with
        {
            RemainingSeconds = remainingSeconds,
            LastUpdatedAt = now,
        };
    }

    private int GetPhaseDurationSeconds(PomodoroPhaseType phaseType) => phaseType switch
    {
        PomodoroPhaseType.Focus => Config.FocusMinutes * 60,
        PomodoroPhaseType.ShortBreak => Config.ShortBreakMinutes * 60,
        PomodoroPhaseType.LongBreak => Config.LongBreakMinutes * 60,
        _ => throw new ArgumentOutOfRangeException(nameof(phaseType), phaseType, null),
    };
}
