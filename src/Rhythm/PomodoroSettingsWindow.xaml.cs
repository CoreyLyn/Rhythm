using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using Rhythm.State;
using Rhythm.UI;

namespace Rhythm;

public partial class PomodoroSettingsWindow : Window
{
    private readonly bool _enableTransparency;

    public PomodoroSettingsWindow(PomodoroConfig config, bool enableTransparency = true)
    {
        _enableTransparency = enableTransparency;

        if (enableTransparency)
        {
            AllowsTransparency = true;
            Background = Brushes.Transparent;
        }
        else
        {
            AllowsTransparency = false;
            Background = (SolidColorBrush)Application.Current.FindResource("SolidWindowBackgroundBrush");
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CornerRadius = new CornerRadius(12),
                GlassFrameThickness = new Thickness(0),
                ResizeBorderThickness = new Thickness(0),
                CaptionHeight = 0
            });
        }

        InitializeComponent();
        ResultConfig = config;
        FocusMinutesTextBox.Text = config.FocusMinutes.ToString();
        ShortBreakMinutesTextBox.Text = config.ShortBreakMinutes.ToString();
        LongBreakMinutesTextBox.Text = config.LongBreakMinutes.ToString();
        LongBreakEveryTextBox.Text = config.LongBreakEvery.ToString();
        AutoStartNextPhaseCheckBox.IsChecked = config.AutoStartNextPhase;
        Loaded += OnLoaded;
    }

    public PomodoroConfig ResultConfig { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_enableTransparency)
        {
            SolidWindowChrome.Apply(this, RootBorder, 12);
        }

        FocusMinutesTextBox.Focus();
        FocusMinutesTextBox.SelectAll();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!TryParsePositiveInteger(FocusMinutesTextBox.Text, "专注时长", out var focusMinutes) ||
            !TryParsePositiveInteger(ShortBreakMinutesTextBox.Text, "短休息时长", out var shortBreakMinutes) ||
            !TryParsePositiveInteger(LongBreakMinutesTextBox.Text, "长休息时长", out var longBreakMinutes) ||
            !TryParsePositiveInteger(LongBreakEveryTextBox.Text, "长休息轮次", out var longBreakEvery))
        {
            return;
        }

        ResultConfig = new PomodoroConfig(
            FocusMinutes: focusMinutes,
            ShortBreakMinutes: shortBreakMinutes,
            LongBreakMinutes: longBreakMinutes,
            LongBreakEvery: longBreakEvery,
            AutoStartNextPhase: AutoStartNextPhaseCheckBox.IsChecked == true);

        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;

        DialogResult = false;
        e.Handled = true;
    }

    private void OnWindowChromeMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.OriginalSource is not DependencyObject source) return;
        if (FindAncestor<Button>(source) != null) return;
        if (FindAncestor<TextBox>(source) != null) return;
        if (FindAncestor<CheckBox>(source) != null) return;
        if (FindAncestor<ScrollBar>(source) != null) return;
        if (FindAncestor<ResizeGrip>(source) != null) return;

        DragMove();
    }

    private bool TryParsePositiveInteger(string? text, string fieldName, out int value)
    {
        if (int.TryParse(text?.Trim(), out value) && value > 0)
        {
            return true;
        }

        MessageBox.Show($"{fieldName}必须是大于 0 的整数。", "Rhythm", MessageBoxButton.OK, MessageBoxImage.Warning);
        value = 0;
        return false;
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
