using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Rhythm.State;

namespace Rhythm.UI;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly RhythmStateService _service;

    public ObservableCollection<ItemViewModel> Items { get; }
    public PomodoroViewModel Pomodoro { get; }

    public bool HasItems => Items.Count > 0;

    public string CurrentDate => DateTime.Now.ToString("M月d日 dddd", CultureInfo.GetCultureInfo("zh-CN"));

    public int CompletedCount => Items.Count(i => i.IsCompleted);

    public int TotalCount => Items.Count;

    public string ProgressText => TotalCount > 0 ? $"已完成 {CompletedCount}/{TotalCount}" : "暂无任务";

    public WindowPos? WindowPos => _service.State.WindowPos;

    public bool EnableTransparency => _service.State.EnableTransparency;

    public MainViewModel(RhythmStateService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
        Items = new ObservableCollection<ItemViewModel>(
            _service.State.Items.Select(i => new ItemViewModel(_service, i)));
        Pomodoro = new PomodoroViewModel(_service, Items);
        Items.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(CompletedCount));
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(ProgressText));
            Pomodoro.RefreshBindings();
        };

        // Subscribe to item completion changes
        foreach (var item in Items)
            item.PropertyChanged += OnItemPropertyChanged;
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ItemViewModel.IsCompleted))
        {
            OnPropertyChanged(nameof(CompletedCount));
            OnPropertyChanged(nameof(ProgressText));
        }
    }

    public void UpdateWindowPos(WindowPos pos) => _service.State.SetWindowPos(pos);

    public void PersistWindowPos() => _service.Persist();

    public void SetEnableTransparency(bool enabled)
    {
        _service.State.SetEnableTransparency(enabled);
        _service.Persist();
    }

    public ItemViewModel AddItem(string text, RhythmItemKind kind = RhythmItemKind.Daily)
    {
        var item = _service.State.AddItem(text, kind);
        var vm = new ItemViewModel(_service, item);
        vm.PropertyChanged += OnItemPropertyChanged;
        Items.Add(vm);
        _service.Persist();
        return vm;
    }

    public void RemoveItem(ItemViewModel itemVm)
    {
        Pomodoro.PrepareForItemRemoval(itemVm.Id);
        _service.State.RemoveItem(itemVm.Id);
        itemVm.PropertyChanged -= OnItemPropertyChanged;
        Items.Remove(itemVm);
        _service.Persist();
    }

    public void RenameItem(ItemViewModel itemVm, string newText)
    {
        _service.State.RenameItem(itemVm.Id, newText);
        var renamed = _service.State.Items.Single(i => i.Id == itemVm.Id);
        itemVm.UpdateText(renamed.Text);
        _service.Persist();
        Pomodoro.RefreshBindings();
    }

    public void MoveItemTo(ItemViewModel itemVm, int newIndex)
    {
        var idx = Items.IndexOf(itemVm);
        if (idx < 0) return;
        if (newIndex < 0 || newIndex >= Items.Count) return;
        if (newIndex == idx) return;
        _service.State.MoveItemTo(itemVm.Id, newIndex);
        Items.Move(idx, newIndex);
        _service.Persist();
    }

    public void ChangeKind(ItemViewModel itemVm, RhythmItemKind newKind)
    {
        _service.State.ChangeKind(itemVm.Id, newKind);
        itemVm.UpdateKind(newKind);
        _service.Persist();
    }

    public void RolloverAndRefresh()
    {
        var changed = _service.State.RolloverIfNeeded(DateOnly.FromDateTime(DateTime.Today));
        if (!changed) return;
        var snapshot = _service.State.Items.ToArray();

        foreach (var item in Items)
            item.PropertyChanged -= OnItemPropertyChanged;

        Items.Clear();
        foreach (var item in snapshot)
        {
            var vm = new ItemViewModel(_service, item);
            vm.PropertyChanged += OnItemPropertyChanged;
            Items.Add(vm);
        }

        Pomodoro.SynchronizeLinkedItemAvailability();
        _service.Persist();
        OnPropertyChanged(nameof(CurrentDate));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
