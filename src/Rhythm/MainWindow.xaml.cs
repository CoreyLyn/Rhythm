using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Rhythm.Interop;
using Rhythm.State;
using Rhythm.UI;

namespace Rhythm;

public partial class MainWindow : Window
{
    private bool _isReady;
    private DispatcherTimer? _saveDebounce;
    private readonly bool _enableTransparency;

    public MainWindow(bool enableTransparency = true)
    {
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
        }

        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        LocationChanged += OnLocationOrSizeChanged;
        SizeChanged += OnLocationOrSizeChanged;
        Closing += OnClosing;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;

        var ex = Win32.GetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE).ToInt64();
        Win32.SetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE,
            new IntPtr(ex | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE));

        Win32.SetWindowPos(hwnd, Win32.HWND_BOTTOM, 0, 0, 0, 0,
            Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);

        var src = HwndSource.FromHwnd(hwnd);
        src?.AddHook(WndProc);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Set border background based on transparency mode
        if (!_enableTransparency)
        {
            SolidWindowChrome.Apply(this, RootBorder, 10);

            // Set context menu background
            if (RootBorder.ContextMenu != null)
            {
                RootBorder.ContextMenu.Background = Background;
            }
        }

        if (DataContext is not MainViewModel vm) return;

        if (vm.WindowPos is { } pos && IsOnScreen(pos))
        {
            Left = pos.X;
            Top = pos.Y;
            if (pos.Width > 0) Width = pos.Width;
        }
        else
        {
            Left = SystemParameters.WorkArea.Width - Width - 30;
            Top = 80;
        }

        _isReady = true;

        vm.UpdateWindowPos(new WindowPos(Left, Top, Width, ActualHeight, "Primary"));
        vm.PersistWindowPos();
    }

    private static bool IsOnScreen(WindowPos pos)
    {
        var vsLeft = SystemParameters.VirtualScreenLeft;
        var vsTop = SystemParameters.VirtualScreenTop;
        var vsRight = vsLeft + SystemParameters.VirtualScreenWidth;
        var vsBottom = vsTop + SystemParameters.VirtualScreenHeight;
        var w = pos.Width > 0 ? pos.Width : 320;
        return pos.X + 32 > vsLeft
            && pos.X + w - 32 < vsRight
            && pos.Y + 32 > vsTop
            && pos.Y + 80 < vsBottom;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case Win32.WM_WINDOWPOSCHANGING:
            {
                var wp = Marshal.PtrToStructure<Win32.WINDOWPOS>(lParam);
                wp.hwndInsertAfter = Win32.HWND_BOTTOM;
                Marshal.StructureToPtr(wp, lParam, fDeleteOld: false);
                break;
            }
            case Win32.WM_WINDOWPOSCHANGED:
            {
                Win32.SetWindowPos(hwnd, Win32.HWND_BOTTOM, 0, 0, 0, 0,
                    Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
                break;
            }
            case Win32.WM_MOUSEACTIVATE:
            {
                handled = true;
                return new IntPtr(Win32.MA_NOACTIVATE);
            }
        }
        return IntPtr.Zero;
    }

    private void OnLocationOrSizeChanged(object? sender, EventArgs e)
    {
        if (!_isReady) return;
        if (DataContext is not MainViewModel vm) return;

        vm.UpdateWindowPos(new WindowPos(Left, Top, Width, ActualHeight, "Primary"));

        _saveDebounce ??= CreateSaveDebounce();
        _saveDebounce.Stop();
        _saveDebounce.Start();
    }

    private DispatcherTimer CreateSaveDebounce()
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        t.Tick += (_, _) =>
        {
            t.Stop();
            if (DataContext is MainViewModel vm)
                vm.PersistWindowPos();
        };
        return t;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.PersistWindowPos();

        if (Application.Current is App app && !app.IsShuttingDown && !app.IsRecreatingWindow)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void OnHideClick(object sender, RoutedEventArgs e) => Hide();

    private void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app)
            app.OpenEditItems();
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app)
            app.ShutdownFromUi();
    }

    private void OnPomodoroPrimaryActionClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel { Pomodoro.CanExecutePrimaryAction: true } vm)
            vm.Pomodoro.ExecutePrimaryAction();
    }

    private void OnPomodoroStartOrResumeClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel { Pomodoro.CanStartOrResume: true } vm)
            vm.Pomodoro.StartOrResume();
    }

    private void OnPomodoroPauseClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel { Pomodoro.CanPause: true } vm)
            vm.Pomodoro.Pause();
    }

    private void OnPomodoroSkipClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel { Pomodoro.CanSkip: true } vm)
            vm.Pomodoro.SkipCurrentPhase();
    }

    private void OnPomodoroResetClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel { Pomodoro.CanReset: true } vm)
            vm.Pomodoro.Reset();
    }

    private void OnPomodoroSettingsClick(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app)
            app.OpenPomodoroSettings();
    }

    private void OnPomodoroClearLinkedItemClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel { Pomodoro.IsIdle: true } vm)
            vm.Pomodoro.SelectedLinkedItemId = null;
    }

    private void OnBorderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
