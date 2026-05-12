using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Rhythm.State;

namespace Rhythm.UI;

public sealed class ItemViewModel : INotifyPropertyChanged
{
    private readonly RhythmStateService _service;
    private string _text;
    private RhythmItemKind _kind;
    private bool _isCompleted;
    private bool _isEditing;

    public Guid Id { get; }

    public string Text
    {
        get => _text;
        private set
        {
            if (_text == value) return;
            _text = value;
            OnPropertyChanged();
        }
    }

    public RhythmItemKind Kind
    {
        get => _kind;
        private set
        {
            if (_kind == value) return;
            _kind = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(KindLabel));
        }
    }

    public string KindLabel => Kind == RhythmItemKind.OneTime ? "一次性" : "每日";

    public bool IsCompleted
    {
        get => _isCompleted;
        set
        {
            if (_isCompleted == value) return;
            _isCompleted = value;
            _service.State.ToggleItem(Id);
            _service.Persist();
            OnPropertyChanged();
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (_isEditing == value) return;
            _isEditing = value;
            OnPropertyChanged();
        }
    }

    public ItemViewModel(RhythmStateService service, RhythmItem item)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(item);
        _service = service;
        Id = item.Id;
        _text = item.Text;
        _kind = item.Kind;
        _isCompleted = service.State.CompletedToday.Contains(item.Id);
    }

    internal void UpdateText(string text) => Text = text;
    internal void UpdateKind(RhythmItemKind kind) => Kind = kind;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
