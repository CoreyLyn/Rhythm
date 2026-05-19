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
                2,
                5,
                null,
                null,
                null),
            reset);
        Assert.Equal(reset, machine.Session);
    }
}
