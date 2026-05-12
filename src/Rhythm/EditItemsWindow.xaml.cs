using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using Rhythm.State;
using Rhythm.UI;

namespace Rhythm;

public partial class EditItemsWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly bool _enableTransparency;
    private Point? _dragStart;
    private ItemViewModel? _dragItem;
    private DropLineAdorner? _dropAdorner;

    public EditItemsWindow(MainViewModel vm, bool enableTransparency = true)
    {
        _vm = vm;
        _enableTransparency = enableTransparency;

        // Set window properties before InitializeComponent
        if (enableTransparency)
        {
            AllowsTransparency = true;
            Background = Brushes.Transparent;
        }
        else
        {
            AllowsTransparency = false;
            Background = (SolidColorBrush)Application.Current.FindResource("SolidWindowBackgroundBrush");

            // Use WindowChrome for rounded corners in non-transparent mode
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CornerRadius = new CornerRadius(12),
                GlassFrameThickness = new Thickness(0),
                ResizeBorderThickness = new Thickness(5),
                CaptionHeight = 0
            });
        }

        InitializeComponent();
        DataContext = _vm;
        ItemsList.ItemsSource = _vm.Items;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Set border background based on transparency mode
        if (!_enableTransparency)
        {
            SolidWindowChrome.Apply(this, RootBorder, 12);
        }
    }

    private void OnAdd(object sender, RoutedEventArgs e) => DoAdd();

    private void OnNewItemKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        DoAdd();
        e.Handled = true;
    }

    private void DoAdd()
    {
        var text = NewItemText.Text;
        if (string.IsNullOrWhiteSpace(text)) return;
        var kind = OneTimeItemRadio.IsChecked == true ? RhythmItemKind.OneTime : RhythmItemKind.Daily;
        var added = _vm.AddItem(text, kind);
        NewItemText.Clear();
        ItemsList.SelectedItem = added;
        ItemsList.ScrollIntoView(added);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;

        Close();
        e.Handled = true;
    }

    private void OnWindowChromeMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.OriginalSource is not DependencyObject source) return;
        if (FindAncestor<Button>(source) != null) return;
        if (FindAncestor<TextBox>(source) != null) return;
        if (FindAncestor<ListBoxItem>(source) != null) return;
        if (FindAncestor<ScrollBar>(source) != null) return;
        if (FindAncestor<ResizeGrip>(source) != null) return;

        DragMove();
    }

    private void OnKindBadgeClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ItemViewModel itemVm)
        {
            var newKind = itemVm.Kind == RhythmItemKind.Daily
                ? RhythmItemKind.OneTime
                : RhythmItemKind.Daily;
            _vm.ChangeKind(itemVm, newKind);
        }
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ItemViewModel itemVm)
        {
            _vm.RemoveItem(itemVm);
        }
    }

    private void OnItemMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;
        if (FindAncestor<Button>(source) != null) return;
        if (sender is not ListBoxItem listItem) return;
        if (listItem.DataContext is not ItemViewModel itemVm) return;
        if (itemVm.IsEditing) return;

        itemVm.IsEditing = true;
        e.Handled = true;
    }

    private void OnEditTextBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not ItemViewModel itemVm) return;
        if (!itemVm.IsEditing) return;
        tb.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
        {
            tb.Focus();
            tb.SelectAll();
        });
    }

    private void OnEditTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not ItemViewModel itemVm) return;

        if (e.Key == Key.Enter)
        {
            CommitInlineEdit(itemVm, tb);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            tb.Text = itemVm.Text;
            itemVm.IsEditing = false;
            e.Handled = true;
        }
    }

    private void OnEditTextBoxLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not ItemViewModel itemVm) return;
        if (!itemVm.IsEditing) return;
        CommitInlineEdit(itemVm, tb);
    }

    private void CommitInlineEdit(ItemViewModel itemVm, TextBox tb)
    {
        var newText = tb.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(newText) && newText != itemVm.Text)
        {
            _vm.RenameItem(itemVm, newText);
        }
        else
        {
            tb.Text = itemVm.Text;
        }
        itemVm.IsEditing = false;
    }

    private void OnDragHandlePreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not ItemViewModel itemVm) return;
        _dragStart = e.GetPosition(null);
        _dragItem = itemVm;
    }

    private void OnDragHandlePreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is not { } start || _dragItem is null) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _dragStart = null;
            _dragItem = null;
            return;
        }

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var item = _dragItem;
        var data = new DataObject(typeof(ItemViewModel), item);
        DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);

        _dragStart = null;
        _dragItem = null;
        _dropAdorner?.Hide();
    }

    private void OnListBoxDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(ItemViewModel)))
        {
            e.Effects = DragDropEffects.None;
            return;
        }
        e.Effects = DragDropEffects.Move;

        EnsureDropAdorner();
        var y = ComputeDropLineY(e);
        _dropAdorner?.Show(y);
        e.Handled = true;
    }

    private void OnListBoxDragLeave(object sender, DragEventArgs e)
    {
        _dropAdorner?.Hide();
    }

    private void OnListBoxDrop(object sender, DragEventArgs e)
    {
        _dropAdorner?.Hide();
        if (e.Data.GetData(typeof(ItemViewModel)) is not ItemViewModel dragged) return;
        var newIndex = ComputeDropIndex(e, dragged);
        if (newIndex < 0) return;
        _vm.MoveItemTo(dragged, newIndex);
        e.Handled = true;
    }

    private void EnsureDropAdorner()
    {
        if (_dropAdorner != null) return;
        var layer = AdornerLayer.GetAdornerLayer(ItemsList);
        if (layer == null) return;
        _dropAdorner = new DropLineAdorner(ItemsList, (Brush)FindResource("AccentBrush"));
        layer.Add(_dropAdorner);
    }

    private int ComputeDropIndex(DragEventArgs e, ItemViewModel dragged)
    {
        var target = FindListBoxItemUnderMouse(e);
        int draggedIdx = _vm.Items.IndexOf(dragged);
        if (draggedIdx < 0) return -1;

        if (target is null || target.DataContext is not ItemViewModel targetVm)
        {
            return _vm.Items.Count - 1;
        }

        int targetIdx = _vm.Items.IndexOf(targetVm);
        if (targetIdx < 0) return -1;

        var pos = e.GetPosition(target);
        bool insertAfter = pos.Y > target.ActualHeight / 2;
        int insertIdx = insertAfter ? targetIdx + 1 : targetIdx;
        if (draggedIdx < insertIdx) insertIdx--;
        if (insertIdx < 0) insertIdx = 0;
        if (insertIdx >= _vm.Items.Count) insertIdx = _vm.Items.Count - 1;
        return insertIdx;
    }

    private double ComputeDropLineY(DragEventArgs e)
    {
        var target = FindListBoxItemUnderMouse(e);
        if (target is null)
        {
            var lastIdx = _vm.Items.Count - 1;
            if (lastIdx < 0) return 0;
            if (ItemsList.ItemContainerGenerator.ContainerFromIndex(lastIdx) is not ListBoxItem last) return 0;
            return last.TranslatePoint(new Point(0, last.ActualHeight), ItemsList).Y;
        }
        var pos = e.GetPosition(target);
        bool insertAfter = pos.Y > target.ActualHeight / 2;
        return target.TranslatePoint(new Point(0, insertAfter ? target.ActualHeight : 0), ItemsList).Y;
    }

    private ListBoxItem? FindListBoxItemUnderMouse(DragEventArgs e)
    {
        if (ItemsList.InputHitTest(e.GetPosition(ItemsList)) is not DependencyObject hit) return null;
        return FindAncestor<ListBoxItem>(hit);
    }

    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T found) return found;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
