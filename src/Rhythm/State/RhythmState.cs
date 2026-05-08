using System;
using System.Collections.Generic;
using System.Linq;

namespace Rhythm.State;

public sealed class RhythmState
{
    private readonly List<RhythmItem> _items;
    private readonly HashSet<Guid> _completed;

    public RhythmState(StateDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        _items = doc.Items.ToList();
        _completed = doc.CompletedToday.ToHashSet();
        LastResetDate = doc.LastResetDate;
        WindowPos = doc.WindowPos;
    }

    public IReadOnlyList<RhythmItem> Items => _items;
    public IReadOnlySet<Guid> CompletedToday => _completed;
    public DateOnly LastResetDate { get; private set; }
    public WindowPos? WindowPos { get; private set; }

    public bool RolloverIfNeeded(DateOnly today)
    {
        if (today <= LastResetDate)
            return false;
        _completed.Clear();
        LastResetDate = today;
        return true;
    }

    public void ToggleItem(Guid id)
    {
        if (!_items.Any(i => i.Id == id))
            throw new ArgumentException($"Unknown item id: {id}", nameof(id));
        if (!_completed.Add(id))
            _completed.Remove(id);
    }

    public RhythmItem AddItem(string text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ArgumentException("Item text cannot be empty or whitespace.", nameof(text));
        var item = new RhythmItem(Guid.NewGuid(), trimmed);
        _items.Add(item);
        return item;
    }

    public void RemoveItem(Guid id)
    {
        var idx = _items.FindIndex(i => i.Id == id);
        if (idx < 0)
            throw new ArgumentException($"Unknown item id: {id}", nameof(id));
        _items.RemoveAt(idx);
        _completed.Remove(id);
    }

    public void RenameItem(Guid id, string newText)
    {
        var trimmed = (newText ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ArgumentException("Item text cannot be empty or whitespace.", nameof(newText));
        var idx = _items.FindIndex(i => i.Id == id);
        if (idx < 0)
            throw new ArgumentException($"Unknown item id: {id}", nameof(id));
        _items[idx] = _items[idx] with { Text = trimmed };
    }

    public void MoveItem(Guid id, int delta)
    {
        if (delta != -1 && delta != 1)
            throw new ArgumentException("Delta must be -1 (up) or +1 (down).", nameof(delta));
        var idx = _items.FindIndex(i => i.Id == id);
        if (idx < 0)
            throw new ArgumentException($"Unknown item id: {id}", nameof(id));
        var newIdx = idx + delta;
        if (newIdx < 0 || newIdx >= _items.Count)
            return;
        (_items[idx], _items[newIdx]) = (_items[newIdx], _items[idx]);
    }

    public void SetWindowPos(WindowPos? pos) => WindowPos = pos;

    public StateDocument ToDocument() => new()
    {
        SchemaVersion = StateDocument.CurrentSchemaVersion,
        Items = _items.ToList(),
        CompletedToday = _completed.ToList(),
        LastResetDate = LastResetDate,
        WindowPos = WindowPos,
    };
}
