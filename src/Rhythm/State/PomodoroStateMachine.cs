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

        Session = AdvanceRunningSession(Session, now) with
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

        Session = AdvanceRunningSession(Session, now);
        return Session;
    }

    public PomodoroSessionSnapshot SkipCurrentPhase(DateTimeOffset now)
    {
        if (Session.Status == PomodoroStatus.Idle)
            return Session;

        var session = Session;

        if (session.Status == PomodoroStatus.Running)
            session = AdvanceRunningSession(session, now);

        var shouldRunNextPhase = session.Status == PomodoroStatus.Running && Config.AutoStartNextPhase;
        Session = CreateNextPhaseSession(session, now, completedFocus: false, shouldRunNextPhase);
        return Session;
    }

    private PomodoroSessionSnapshot AdvanceRunningSession(
        PomodoroSessionSnapshot session,
        DateTimeOffset now)
    {
        if (session.PhaseStartedAt is null)
        {
            return session with
            {
                LastUpdatedAt = now,
            };
        }

        var baseline = session.LastUpdatedAt ?? session.PhaseStartedAt.Value;
        var elapsedSeconds = (int)Math.Max(0, (now - baseline).TotalSeconds);

        if (elapsedSeconds <= 0 && session.RemainingSeconds > 0)
            return session;

        var current = session;
        var remainingElapsedSeconds = elapsedSeconds;

        while (true)
        {
            var secondsToPhaseEnd = Math.Max(0, current.RemainingSeconds);

            if (secondsToPhaseEnd > remainingElapsedSeconds)
            {
                return current with
                {
                    RemainingSeconds = secondsToPhaseEnd - remainingElapsedSeconds,
                    LastUpdatedAt = now,
                };
            }

            remainingElapsedSeconds -= secondsToPhaseEnd;
            var transitionTime = baseline.AddSeconds(secondsToPhaseEnd);

            current = CreateNextPhaseSession(
                current,
                transitionTime,
                completedFocus: current.PhaseType == PomodoroPhaseType.Focus,
                shouldRunNextPhase: Config.AutoStartNextPhase);

            if (current.Status != PomodoroStatus.Running || remainingElapsedSeconds <= 0)
                return current;

            baseline = transitionTime;
        }
    }

    private int GetPhaseDurationSeconds(PomodoroPhaseType phaseType) => phaseType switch
    {
        PomodoroPhaseType.Focus => Config.FocusMinutes * 60,
        PomodoroPhaseType.ShortBreak => Config.ShortBreakMinutes * 60,
        PomodoroPhaseType.LongBreak => Config.LongBreakMinutes * 60,
        _ => throw new ArgumentOutOfRangeException(nameof(phaseType), phaseType, null),
    };

    private PomodoroSessionSnapshot CreateNextPhaseSession(
        PomodoroSessionSnapshot session,
        DateTimeOffset transitionTime,
        bool completedFocus,
        bool shouldRunNextPhase)
    {
        var nextPhaseType = GetNextPhaseType(session, completedFocus, out var completedFocusCountInCycle, out var completedFocusCountToday);

        return session with
        {
            Status = shouldRunNextPhase ? PomodoroStatus.Running : PomodoroStatus.Paused,
            PhaseType = nextPhaseType,
            RemainingSeconds = GetPhaseDurationSeconds(nextPhaseType),
            CompletedFocusCountInCycle = completedFocusCountInCycle,
            CompletedFocusCountToday = completedFocusCountToday,
            LinkedItemId = null,
            PhaseStartedAt = transitionTime,
            LastUpdatedAt = transitionTime,
        };
    }

    private PomodoroPhaseType GetNextPhaseType(
        PomodoroSessionSnapshot session,
        bool completedFocus,
        out int completedFocusCountInCycle,
        out int completedFocusCountToday)
    {
        completedFocusCountInCycle = session.CompletedFocusCountInCycle;
        completedFocusCountToday = session.CompletedFocusCountToday;

        if (session.PhaseType != PomodoroPhaseType.Focus)
            return PomodoroPhaseType.Focus;

        if (!completedFocus)
            return PomodoroPhaseType.ShortBreak;

        completedFocusCountToday++;
        completedFocusCountInCycle++;

        if (completedFocusCountInCycle >= Math.Max(1, Config.LongBreakEvery))
        {
            completedFocusCountInCycle = 0;
            return PomodoroPhaseType.LongBreak;
        }

        return PomodoroPhaseType.ShortBreak;
    }
}
