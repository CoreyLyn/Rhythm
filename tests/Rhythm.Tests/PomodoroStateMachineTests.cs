using System;
using Rhythm.State;
using Xunit;

namespace Rhythm.Tests;

public sealed class PomodoroStateMachineTests
{
    private static readonly DateTimeOffset StartTime = new(2026, 5, 19, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Start_UsesConfiguredFocusDurationAndOptionalLinkedItem()
    {
        var machine = new PomodoroStateMachine(
            new PomodoroConfig(FocusMinutes: 30),
            new PomodoroSessionSnapshot());
        var linkedItemId = Guid.NewGuid();

        var session = machine.Start(StartTime, linkedItemId);

        Assert.Equal(
            new PomodoroSessionSnapshot(
                PomodoroStatus.Running,
                PomodoroPhaseType.Focus,
                1800,
                0,
                0,
                linkedItemId,
                StartTime,
                StartTime),
            session);
        Assert.Equal(session, machine.Session);
    }

    [Fact]
    public void Start_NoOpWhenSessionIsNotIdle()
    {
        var existing = new PomodoroSessionSnapshot(
            PomodoroStatus.Paused,
            PomodoroPhaseType.Focus,
            1234,
            1,
            2,
            Guid.NewGuid(),
            StartTime,
            StartTime.AddMinutes(4));
        var machine = new PomodoroStateMachine(
            new PomodoroConfig(FocusMinutes: 30),
            existing);

        var session = machine.Start(StartTime.AddMinutes(10), Guid.NewGuid());

        Assert.Equal(existing, session);
        Assert.Equal(existing, machine.Session);
    }

    [Fact]
    public void Pause_FreezesRemainingSeconds()
    {
        var machine = new PomodoroStateMachine(
            new PomodoroConfig(FocusMinutes: 25),
            new PomodoroSessionSnapshot());

        machine.Start(StartTime);
        machine.AdvanceTo(StartTime.AddMinutes(5));

        var paused = machine.Pause(StartTime.AddMinutes(5));
        var afterAdvance = machine.AdvanceTo(StartTime.AddMinutes(8));

        Assert.Equal(PomodoroStatus.Paused, paused.Status);
        Assert.Equal(1200, paused.RemainingSeconds);
        Assert.Equal(StartTime, paused.PhaseStartedAt);
        Assert.Equal(StartTime.AddMinutes(5), paused.LastUpdatedAt);
        Assert.Equal(paused, afterAdvance);
        Assert.Equal(paused, machine.Session);
    }

    [Fact]
    public void Resume_RestartsCurrentPhaseFromPausedRemainingSeconds()
    {
        var machine = new PomodoroStateMachine(
            new PomodoroConfig(FocusMinutes: 25),
            new PomodoroSessionSnapshot());

        machine.Start(StartTime);
        machine.AdvanceTo(StartTime.AddMinutes(5));
        machine.Pause(StartTime.AddMinutes(5));

        var resumed = machine.Resume(StartTime.AddMinutes(7));
        var afterAdvance = machine.AdvanceTo(StartTime.AddMinutes(10));

        Assert.Equal(PomodoroStatus.Running, resumed.Status);
        Assert.Equal(PomodoroPhaseType.Focus, resumed.PhaseType);
        Assert.Equal(1200, resumed.RemainingSeconds);
        Assert.Equal(StartTime.AddMinutes(7), resumed.PhaseStartedAt);
        Assert.Equal(StartTime.AddMinutes(7), resumed.LastUpdatedAt);
        Assert.Equal(1020, afterAdvance.RemainingSeconds);
        Assert.Equal(StartTime.AddMinutes(7), afterAdvance.PhaseStartedAt);
        Assert.Equal(StartTime.AddMinutes(10), afterAdvance.LastUpdatedAt);
    }

    [Fact]
    public void AdvanceTo_AccumulatesSubSecondTicksUntilAWholeSecondElapses()
    {
        var machine = new PomodoroStateMachine(
            new PomodoroConfig(FocusMinutes: 25),
            new PomodoroSessionSnapshot());

        machine.Start(StartTime);

        var after400ms = machine.AdvanceTo(StartTime.AddMilliseconds(400));
        var after800ms = machine.AdvanceTo(StartTime.AddMilliseconds(800));
        var after1200ms = machine.AdvanceTo(StartTime.AddMilliseconds(1200));

        Assert.Equal(1500, after400ms.RemainingSeconds);
        Assert.Equal(StartTime, after400ms.LastUpdatedAt);
        Assert.Equal(1500, after800ms.RemainingSeconds);
        Assert.Equal(StartTime, after800ms.LastUpdatedAt);
        Assert.Equal(1499, after1200ms.RemainingSeconds);
        Assert.Equal(StartTime.AddMilliseconds(1200), after1200ms.LastUpdatedAt);
    }

    [Fact]
    public void AdvanceTo_FocusCompletionStartsShortBreakAndIncrementsCounts()
    {
        var linkedItemId = Guid.NewGuid();
        var machine = new PomodoroStateMachine(
            new PomodoroConfig(FocusMinutes: 25, ShortBreakMinutes: 5),
            new PomodoroSessionSnapshot());

        machine.Start(StartTime, linkedItemId);

        var session = machine.AdvanceTo(StartTime.AddMinutes(25));

        Assert.Equal(
            new PomodoroSessionSnapshot(
                PomodoroStatus.Running,
                PomodoroPhaseType.ShortBreak,
                300,
                1,
                1,
                null,
                StartTime.AddMinutes(25),
                StartTime.AddMinutes(25)),
            session);
        Assert.Equal(session, machine.Session);
    }

    [Fact]
    public void AdvanceTo_FourthFocusCompletionStartsLongBreak()
    {
        var machine = new PomodoroStateMachine(
            new PomodoroConfig(FocusMinutes: 25, ShortBreakMinutes: 5, LongBreakMinutes: 15, LongBreakEvery: 4),
            new PomodoroSessionSnapshot(
                PomodoroStatus.Running,
                PomodoroPhaseType.Focus,
                1500,
                3,
                7,
                Guid.NewGuid(),
                StartTime,
                StartTime));

        var session = machine.AdvanceTo(StartTime.AddMinutes(25));

        Assert.Equal(
            new PomodoroSessionSnapshot(
                PomodoroStatus.Running,
                PomodoroPhaseType.LongBreak,
                900,
                0,
                8,
                null,
                StartTime.AddMinutes(25),
                StartTime.AddMinutes(25)),
            session);
        Assert.Equal(session, machine.Session);
    }

    [Fact]
    public void SkipCurrentPhase_DoesNotCountSkippedFocusAsCompleted()
    {
        var linkedItemId = Guid.NewGuid();
        var machine = new PomodoroStateMachine(
            new PomodoroConfig(FocusMinutes: 25, ShortBreakMinutes: 5),
            new PomodoroSessionSnapshot());

        machine.Start(StartTime, linkedItemId);
        machine.AdvanceTo(StartTime.AddMinutes(10));

        var session = machine.SkipCurrentPhase(StartTime.AddMinutes(10));

        Assert.Equal(
            new PomodoroSessionSnapshot(
                PomodoroStatus.Running,
                PomodoroPhaseType.ShortBreak,
                300,
                0,
                0,
                null,
                StartTime.AddMinutes(10),
                StartTime.AddMinutes(10)),
            session);
        Assert.Equal(session, machine.Session);
    }

    [Fact]
    public void CompleteCurrentPhase_CountsFocusAsCompleted()
    {
        var linkedItemId = Guid.NewGuid();
        var machine = new PomodoroStateMachine(
            new PomodoroConfig(FocusMinutes: 25, ShortBreakMinutes: 5),
            new PomodoroSessionSnapshot());

        machine.Start(StartTime, linkedItemId);
        machine.AdvanceTo(StartTime.AddMinutes(10));

        var session = machine.CompleteCurrentPhase(StartTime.AddMinutes(10));

        Assert.Equal(
            new PomodoroSessionSnapshot(
                PomodoroStatus.Running,
                PomodoroPhaseType.ShortBreak,
                300,
                1,
                1,
                null,
                StartTime.AddMinutes(10),
                StartTime.AddMinutes(10)),
            session);
        Assert.Equal(session, machine.Session);
    }

    [Fact]
    public void CompleteCurrentPhase_StartsLongBreakWhenCycleCompletes()
    {
        var machine = new PomodoroStateMachine(
            new PomodoroConfig(FocusMinutes: 25, ShortBreakMinutes: 5, LongBreakMinutes: 15, LongBreakEvery: 4),
            new PomodoroSessionSnapshot(
                PomodoroStatus.Running,
                PomodoroPhaseType.Focus,
                900,
                3,
                7,
                Guid.NewGuid(),
                StartTime,
                StartTime.AddMinutes(10)));

        var session = machine.CompleteCurrentPhase(StartTime.AddMinutes(10));

        Assert.Equal(
            new PomodoroSessionSnapshot(
                PomodoroStatus.Running,
                PomodoroPhaseType.LongBreak,
                900,
                0,
                8,
                null,
                StartTime.AddMinutes(10),
                StartTime.AddMinutes(10)),
            session);
        Assert.Equal(session, machine.Session);
    }

    [Fact]
    public void AdvanceTo_AutoStartDisabledStopsAtNextPhasePaused()
    {
        var machine = new PomodoroStateMachine(
            new PomodoroConfig(FocusMinutes: 25, ShortBreakMinutes: 5, AutoStartNextPhase: false),
            new PomodoroSessionSnapshot());

        machine.Start(StartTime, Guid.NewGuid());

        var session = machine.AdvanceTo(StartTime.AddMinutes(25));

        Assert.Equal(
            new PomodoroSessionSnapshot(
                PomodoroStatus.Paused,
                PomodoroPhaseType.ShortBreak,
                300,
                1,
                1,
                null,
                StartTime.AddMinutes(25),
                StartTime.AddMinutes(25)),
            session);
        Assert.Equal(session, machine.Session);
    }

    [Fact]
    public void AdvanceTo_RestoresAcrossMultiplePhasesUsingElapsedTime()
    {
        var machine = new PomodoroStateMachine(
            new PomodoroConfig(FocusMinutes: 25, ShortBreakMinutes: 5),
            new PomodoroSessionSnapshot());

        machine.Start(StartTime, Guid.NewGuid());

        var session = machine.AdvanceTo(StartTime.AddMinutes(61));

        Assert.Equal(
            new PomodoroSessionSnapshot(
                PomodoroStatus.Running,
                PomodoroPhaseType.Focus,
                1440,
                2,
                2,
                null,
                StartTime.AddMinutes(60),
                StartTime.AddMinutes(61)),
            session);
        Assert.Equal(session, machine.Session);
    }

    [Fact]
    public void Reset_ReturnsToIdleFocusAndClearsLinkedItem()
    {
        var machine = new PomodoroStateMachine(
            new PomodoroConfig(FocusMinutes: 25),
            new PomodoroSessionSnapshot(
                PomodoroStatus.Paused,
                PomodoroPhaseType.ShortBreak,
                90,
                2,
                5,
                Guid.NewGuid(),
                StartTime,
                StartTime.AddMinutes(3)));

        var reset = machine.Reset();

        Assert.Equal(
            new PomodoroSessionSnapshot(
                PomodoroStatus.Idle,
                PomodoroPhaseType.Focus,
                1500,
                0,
                5,
                null,
                null,
                null),
            reset);
        Assert.Equal(reset, machine.Session);
    }
}
