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
        Items.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(CompletedCount));
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(ProgressText));
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
        _service.State.RemoveItem(itemVm.Id);
        Items.Remove(itemVm);
        _service.Persist();
    }

    public void RenameItem(ItemViewModel itemVm, string newText)
    {
        var idx = Items.IndexOf(itemVm);
        if (idx < 0) return;
        _service.State.RenameItem(itemVm.Id, newText);
        var renamed = _service.State.Items[idx];
        itemVm.PropertyChanged -= OnItemPropertyChanged;
        var newVm = new ItemViewModel(_service, renamed);
        newVm.PropertyChanged += OnItemPropertyChanged;
        Items[idx] = newVm;
        _service.Persist();
    }

    public void RolloverAndRefresh()
    {
        var changed = _service.State.RolloverIfNeeded(DateOnly.FromDateTime(DateTime.Today));
        if (!changed) return;

        _service.Persist();
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

        OnPropertyChanged(nameof(CurrentDate));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
