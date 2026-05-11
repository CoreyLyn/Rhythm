using System;
using System.Linq;
using Rhythm.State;
using Xunit;

namespace Rhythm.Tests;

public class RhythmStateTests
{
    private static RhythmState NewState() => new(new StateDocument
    {
        LastResetDate = new DateOnly(2026, 5, 8),
    });

    [Fact]
    public void RolloverIfNeeded_ClearsCompletedWhenDayAdvances()
    {
        var state = NewState();
        var a = state.AddItem("A");
        state.ToggleItem(a.Id);
        Assert.Single(state.CompletedToday);

        var changed = state.RolloverIfNeeded(new DateOnly(2026, 5, 9));

        Assert.True(changed);
        Assert.Empty(state.CompletedToday);
        Assert.Equal(new DateOnly(2026, 5, 9), state.LastResetDate);
    }

    [Fact]
    public void RolloverIfNeeded_KeepsCompletedDailyItems()
    {
        var state = NewState();
        var daily = state.AddItem("Daily", RhythmItemKind.Daily);
        state.ToggleItem(daily.Id);

        state.RolloverIfNeeded(new DateOnly(2026, 5, 9));

        Assert.Contains(state.Items, i => i.Id == daily.Id);
        Assert.Empty(state.CompletedToday);
    }

    [Fact]
    public void RolloverIfNeeded_RemovesCompletedOneTimeItems()
    {
        var state = NewState();
        var daily = state.AddItem("Daily", RhythmItemKind.Daily);
        var oneTime = state.AddItem("One time", RhythmItemKind.OneTime);
        state.ToggleItem(daily.Id);
        state.ToggleItem(oneTime.Id);

        state.RolloverIfNeeded(new DateOnly(2026, 5, 9));

        Assert.Equal(new[] { daily.Id }, state.Items.Select(i => i.Id).ToArray());
        Assert.Empty(state.CompletedToday);
    }

    [Fact]
    public void RolloverIfNeeded_KeepsIncompleteOneTimeItems()
    {
        var state = NewState();
        var oneTime = state.AddItem("One time", RhythmItemKind.OneTime);

        state.RolloverIfNeeded(new DateOnly(2026, 5, 9));

        Assert.Contains(state.Items, i => i.Id == oneTime.Id);
    }

    [Fact]
    public void RolloverIfNeeded_IsIdempotentOnSameDay()
    {
        var state = NewState();
        var a = state.AddItem("A");
        state.ToggleItem(a.Id);
        state.RolloverIfNeeded(new DateOnly(2026, 5, 8));
        var changed = state.RolloverIfNeeded(new DateOnly(2026, 5, 8));

        Assert.False(changed);
        Assert.Single(state.CompletedToday);
    }

    [Fact]
    public void RolloverIfNeeded_NoOpWhenTodayEqualsLastReset()
    {
        var state = NewState();
        var changed = state.RolloverIfNeeded(new DateOnly(2026, 5, 8));
        Assert.False(changed);
    }

    [Fact]
    public void RolloverIfNeeded_NoOpWhenTodayEarlierThanLastReset()
    {
        var state = NewState();
        var changed = state.RolloverIfNeeded(new DateOnly(2026, 5, 7));
        Assert.False(changed);
        Assert.Equal(new DateOnly(2026, 5, 8), state.LastResetDate);
    }

    [Fact]
    public void ToggleItem_FlipsBothWays()
    {
        var state = NewState();
        var a = state.AddItem("A");
        state.ToggleItem(a.Id);
        Assert.Contains(a.Id, state.CompletedToday);
        state.ToggleItem(a.Id);
        Assert.DoesNotContain(a.Id, state.CompletedToday);
    }

    [Fact]
    public void ToggleItem_ThrowsForUnknownId()
    {
        var state = NewState();
        Assert.Throws<ArgumentException>(() => state.ToggleItem(Guid.NewGuid()));
    }

    [Fact]
    public void AddItem_RejectsEmptyOrWhitespace()
    {
        var state = NewState();
        Assert.Throws<ArgumentException>(() => state.AddItem(""));
        Assert.Throws<ArgumentException>(() => state.AddItem("   "));
        Assert.Throws<ArgumentException>(() => state.AddItem("\t\n"));
    }

    [Fact]
    public void AddItem_TrimsAndAssignsUniqueGuid()
    {
        var state = NewState();
        var a = state.AddItem("  hello ");
        var b = state.AddItem("world", RhythmItemKind.OneTime);
        Assert.Equal("hello", a.Text);
        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(RhythmItemKind.Daily, a.Kind);
        Assert.Equal(RhythmItemKind.OneTime, b.Kind);
        Assert.Equal(2, state.Items.Count);
    }

    [Fact]
    public void AddItem_RejectsUnknownKind()
    {
        var state = NewState();
        Assert.Throws<ArgumentException>(() => state.AddItem("A", (RhythmItemKind)999));
    }

    [Fact]
    public void RemoveItem_RemovesFromItemsAndCompleted()
    {
        var state = NewState();
        var a = state.AddItem("A");
        state.ToggleItem(a.Id);
        state.RemoveItem(a.Id);
        Assert.Empty(state.Items);
        Assert.Empty(state.CompletedToday);
    }

    [Fact]
    public void RemoveItem_ThrowsForUnknownId()
    {
        var state = NewState();
        Assert.Throws<ArgumentException>(() => state.RemoveItem(Guid.NewGuid()));
    }

    [Fact]
    public void RenameItem_UpdatesText()
    {
        var state = NewState();
        var a = state.AddItem("old");
        state.RenameItem(a.Id, " new ");
        Assert.Equal("new", state.Items.Single(i => i.Id == a.Id).Text);
    }

    [Fact]
    public void RenameItem_RejectsEmpty()
    {
        var state = NewState();
        var a = state.AddItem("A");
        Assert.Throws<ArgumentException>(() => state.RenameItem(a.Id, ""));
        Assert.Throws<ArgumentException>(() => state.RenameItem(a.Id, "   "));
    }

    [Fact]
    public void MoveItem_UpAndDownReorders()
    {
        var state = NewState();
        var a = state.AddItem("A");
        var b = state.AddItem("B");
        var c = state.AddItem("C");

        state.MoveItem(b.Id, -1);
        Assert.Equal(new[] { b.Id, a.Id, c.Id }, state.Items.Select(i => i.Id).ToArray());

        state.MoveItem(b.Id, 1);
        Assert.Equal(new[] { a.Id, b.Id, c.Id }, state.Items.Select(i => i.Id).ToArray());
    }

    [Fact]
    public void MoveItem_ClampsAtEdges()
    {
        var state = NewState();
        var a = state.AddItem("A");
        var b = state.AddItem("B");

        state.MoveItem(a.Id, -1);
        Assert.Equal(new[] { a.Id, b.Id }, state.Items.Select(i => i.Id).ToArray());

        state.MoveItem(b.Id, 1);
        Assert.Equal(new[] { a.Id, b.Id }, state.Items.Select(i => i.Id).ToArray());
    }

    [Fact]
    public void ToDocument_RoundtripsMutations()
    {
        var state = NewState();
        var a = state.AddItem("A");
        state.ToggleItem(a.Id);
        state.SetWindowPos(new WindowPos(10, 20, 300, 400, "DISPLAY1"));
        state.SetEnableTransparency(false);
        var doc = state.ToDocument();

        Assert.Equal(StateDocument.CurrentSchemaVersion, doc.SchemaVersion);
        Assert.Single(doc.Items);
        Assert.Equal(RhythmItemKind.Daily, doc.Items[0].Kind);
        Assert.Single(doc.CompletedToday);
        Assert.Equal("DISPLAY1", doc.WindowPos!.ScreenDeviceName);
        Assert.False(doc.EnableTransparency);
    }
}
