using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using H.NotifyIcon;
using Rhythm.Autostart;
using Rhythm.State;
using Rhythm.UI;

namespace Rhythm;

public partial class App : Application
{
    private static readonly FieldInfo? PomodoroMachineField = typeof(PomodoroViewModel).GetField("_machine", BindingFlags.Instance | BindingFlags.NonPublic);

    private TaskbarIcon? _trayIcon;
    private ContextMenu? _trayMenu;
    private MenuItem? _pomodoroActionMenuItem;
    private MenuItem? _pomodoroSkipMenuItem;
    private MenuItem? _pomodoroResetMenuItem;
    private MainViewModel? _viewModel;
    private MainWindow? _mainWindow;
    private DispatcherTimer? _midnightTimer;
    private DispatcherTimer? _pomodoroTimer;
    private bool _isRecreatingWindow;
    private PomodoroPhaseType? _lastPomodoroPhase;
    private PomodoroStatus? _lastPomodoroStatus;
    private int? _lastCompletedFocusCountToday;

    public bool IsShuttingDown { get; private set; }

    public bool IsRecreatingWindow => _isRecreatingWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppPaths.EnsureAppDataDir();
        var firstLaunch = !File.Exists(AppPaths.StateFilePath);

        var doc = StateStore.Load();
        var state = new RhythmState(doc);
        state.RolloverIfNeeded(DateOnly.FromDateTime(DateTime.Today));

        if (firstLaunch)
        {
            state.AddItem("早起冥想 5 分钟");
            state.AddItem("运动 20 分钟");
            state.AddItem("读书 30 分钟");
        }

        StateStore.Save(state.ToDocument());

        var service = new RhythmStateService(state);
        _viewModel = new MainViewModel(service);
        _mainWindow = new MainWindow(state.EnableTransparency) { DataContext = _viewModel };
        _mainWindow.Show();

        BuildTrayIcon();
        InitializePomodoroTimer();
        ScheduleNextMidnight();
    }

    private void BuildTrayIcon()
    {
        var iconUri = new Uri("pack://application:,,,/Resources/icon.ico");
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Rhythm",
            IconSource = new BitmapImage(iconUri),
        };

        var menu = new ContextMenu();
        _trayMenu = menu;
        var enableTransparency = _viewModel?.EnableTransparency ?? true;
        UpdateMenuBackground(menu, enableTransparency);
        var miShow = new MenuItem { Header = "显示窗口" };
        miShow.Click += (_, _) => ShowMainWindow();
        var miHide = new MenuItem { Header = "隐藏窗口" };
        miHide.Click += (_, _) => HideMainWindow();
        var miEdit = new MenuItem { Header = "编辑事项..." };
        miEdit.Click += (_, _) => OpenEditItems();
        _pomodoroActionMenuItem = new MenuItem();
        _pomodoroSkipMenuItem = new MenuItem { Header = "跳过当前阶段" };
        _pomodoroResetMenuItem = new MenuItem { Header = "重置番茄钟" };
        var miPomodoroSettings = new MenuItem { Header = "番茄钟设置..." };
        var miAutostart = new MenuItem { IsCheckable = true, StaysOpenOnClick = true };
        RefreshAutostartMenuItem(miAutostart);
        menu.Opened += (_, _) =>
        {
            RefreshPomodoroMenuItems();
            RefreshAutostartMenuItem(miAutostart);
        };
        _pomodoroActionMenuItem.Click += (_, _) => ExecutePomodoroPrimaryAction();
        _pomodoroSkipMenuItem.Click += (_, _) => SkipPomodoroPhase();
        _pomodoroResetMenuItem.Click += (_, _) => ResetPomodoro();
        miPomodoroSettings.Click += (_, _) => OpenPomodoroSettings();
        miAutostart.Click += (_, _) =>
        {
            try
            {
                AutostartManager.Set(miAutostart.IsChecked);
                RefreshAutostartMenuItem(miAutostart);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法修改自启动设置：\n{ex.Message}", "Rhythm", MessageBoxButton.OK, MessageBoxImage.Warning);
                RefreshAutostartMenuItem(miAutostart);
            }
        };

        var miTransparency = new MenuItem { IsCheckable = true, StaysOpenOnClick = true };
        RefreshTransparencyMenuItem(miTransparency);
        menu.Opened += (_, _) => RefreshTransparencyMenuItem(miTransparency);
        miTransparency.Click += (_, _) =>
        {
            if (_viewModel == null) return;
            try
            {
                var newValue = miTransparency.IsChecked;
                _viewModel.SetEnableTransparency(newValue);
                RecreateMainWindow(newValue);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法修改透明设置：\n{ex.Message}", "Rhythm", MessageBoxButton.OK, MessageBoxImage.Warning);
                RefreshTransparencyMenuItem(miTransparency);
            }
        };

        var miAbout = new MenuItem { Header = "关于 Rhythm" };
        miAbout.Click += (_, _) => ShowAboutDialogAfterMenuCloses();
        var miExit = new MenuItem { Header = "退出" };
        miExit.Click += (_, _) => ShutdownApp();

        menu.Items.Add(miShow);
        menu.Items.Add(miHide);
        menu.Items.Add(miEdit);
        menu.Items.Add(new Separator());
        menu.Items.Add(_pomodoroActionMenuItem);
        menu.Items.Add(_pomodoroSkipMenuItem);
        menu.Items.Add(_pomodoroResetMenuItem);
        menu.Items.Add(miPomodoroSettings);
        menu.Items.Add(new Separator());
        menu.Items.Add(miAutostart);
        menu.Items.Add(miTransparency);
        menu.Items.Add(new Separator());
        menu.Items.Add(miAbout);
        menu.Items.Add(miExit);

        _trayIcon.ContextMenu = menu;
        _trayIcon.TrayLeftMouseDoubleClick += (_, _) => ShowMainWindow();
        _trayIcon.ForceCreate();
    }

    private void RefreshPomodoroMenuItems()
    {
        if (_pomodoroActionMenuItem == null || _pomodoroSkipMenuItem == null || _pomodoroResetMenuItem == null)
            return;

        if (_viewModel == null)
        {
            _pomodoroActionMenuItem.Header = "开始专注";
            _pomodoroActionMenuItem.IsEnabled = false;
            _pomodoroSkipMenuItem.IsEnabled = false;
            _pomodoroResetMenuItem.IsEnabled = false;
            return;
        }

        var pomodoro = _viewModel.Pomodoro;
        if (pomodoro.IsRunning)
        {
            _pomodoroActionMenuItem.Header = "暂停番茄钟";
            _pomodoroActionMenuItem.IsEnabled = pomodoro.CanPause;
        }
        else if (pomodoro.IsPaused)
        {
            _pomodoroActionMenuItem.Header = "继续番茄钟";
            _pomodoroActionMenuItem.IsEnabled = pomodoro.CanStartOrResume;
        }
        else
        {
            _pomodoroActionMenuItem.Header = "开始专注";
            _pomodoroActionMenuItem.IsEnabled = pomodoro.CanStartOrResume;
        }

        _pomodoroSkipMenuItem.IsEnabled = pomodoro.CanSkip;
        _pomodoroResetMenuItem.IsEnabled = pomodoro.CanReset;
    }

    private void RefreshPomodoroMenuItemsIfOpen()
    {
        if (_trayMenu?.IsOpen == true)
            RefreshPomodoroMenuItems();
    }

    private static void RefreshAutostartMenuItem(MenuItem menuItem)
    {
        try
        {
            var isEnabled = AutostartManager.IsEnabled();
            menuItem.IsChecked = isEnabled;
            menuItem.Header = "开机启动";
        }
        catch (Exception ex)
        {
            menuItem.IsChecked = false;
            menuItem.Header = "开机启动（状态未知）";
            MessageBox.Show($"无法读取自启动设置：\n{ex.Message}", "Rhythm", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RefreshTransparencyMenuItem(MenuItem menuItem)
    {
        if (_viewModel == null)
        {
            menuItem.IsChecked = false;
            menuItem.Header = "透明效果（状态未知）";
            return;
        }
        menuItem.IsChecked = _viewModel.EnableTransparency;
        menuItem.Header = "透明效果";
    }

    private void RecreateMainWindow(bool enableTransparency)
    {
        if (_mainWindow == null || _viewModel == null) return;

        // Save current window position and size
        var wasVisible = _mainWindow.IsVisible;
        var oldLeft = _mainWindow.Left;
        var oldTop = _mainWindow.Top;
        var oldWidth = _mainWindow.Width;
        var oldHeight = _mainWindow.ActualHeight;

        // Set flag to allow window to close for recreation
        _isRecreatingWindow = true;

        // Close old window
        _mainWindow.Close();

        // Reset flag
        _isRecreatingWindow = false;

        // Create new window with updated transparency setting
        _mainWindow = new MainWindow(enableTransparency) { DataContext = _viewModel };
        _mainWindow.Left = oldLeft;
        _mainWindow.Top = oldTop;
        _mainWindow.Width = oldWidth;
        // Height is controlled by SizeToContent, so we restore it after load
        _mainWindow.Loaded += (_, _) =>
        {
            // Restore height if it differs from saved height (e.g., after manual resize)
            if (Math.Abs(_mainWindow.ActualHeight - oldHeight) > 1 && oldHeight > 0)
                _mainWindow.Height = oldHeight;
        };
        if (wasVisible)
            _mainWindow.Show();

        // Update tray menu background
        if (_trayIcon?.ContextMenu != null)
            UpdateMenuBackground(_trayIcon.ContextMenu, enableTransparency);
    }

    private void UpdateMenuBackground(ContextMenu menu, bool enableTransparency)
    {
        if (enableTransparency)
        {
            // Use transparent brush (let the style take over)
            menu.ClearValue(ContextMenu.BackgroundProperty);
        }
        else
        {
            // Use solid background
            menu.Background = (SolidColorBrush)Current.FindResource("SolidWindowBackgroundBrush");
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow == null) return;
        if (!_mainWindow.IsVisible) _mainWindow.Show();
    }

    private void HideMainWindow() => _mainWindow?.Hide();

    private void ShowAboutDialogAfterMenuCloses()
    {
        Dispatcher.BeginInvoke(new Action(ShowAboutDialog), DispatcherPriority.ContextIdle);
    }

    private void ShowAboutDialog()
    {
        var enableTransparency = _viewModel?.EnableTransparency ?? true;
        var aboutWindow = new AboutWindow(enableTransparency);
        aboutWindow.ShowDialog();
    }

    public void ShutdownFromUi() => ShutdownApp();

    public void OpenEditItems()
    {
        if (_viewModel == null) return;
        var enableTransparency = _viewModel.EnableTransparency;
        var w = new EditItemsWindow(_viewModel, enableTransparency);
        w.ShowDialog();
    }

    public void OpenPomodoroSettings()
    {
        if (_viewModel == null) return;

        var enableTransparency = _viewModel.EnableTransparency;
        var window = new PomodoroSettingsWindow(_viewModel.Pomodoro.Config, enableTransparency);

        if (window.ShowDialog() == true)
        {
            _viewModel.Pomodoro.ApplyConfig(window.ResultConfig);
            SyncPomodoroState(showNotification: false);
        }
    }

    private void ShutdownApp()
    {
        IsShuttingDown = true;
        _midnightTimer?.Stop();
        StopPomodoroTimer();
        _trayIcon?.Dispose();
        Shutdown();
    }

    private void InitializePomodoroTimer()
    {
        StopPomodoroTimer();
        SyncPomodoroState(showNotification: false);

        _pomodoroTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _pomodoroTimer.Tick += (_, _) =>
        {
            _viewModel?.Pomodoro.AdvanceToNow();
            SyncPomodoroState(showNotification: true);
        };
        _pomodoroTimer.Start();
    }

    private void StopPomodoroTimer()
    {
        _pomodoroTimer?.Stop();
        _pomodoroTimer = null;
    }

    private void ExecutePomodoroPrimaryAction()
    {
        if (_viewModel == null) return;

        var pomodoro = _viewModel.Pomodoro;
        if (pomodoro.IsRunning)
            pomodoro.Pause();
        else
            pomodoro.StartOrResume();

        SyncPomodoroState(showNotification: true);
    }

    private void SkipPomodoroPhase()
    {
        if (_viewModel == null || !_viewModel.Pomodoro.CanSkip) return;

        _viewModel.Pomodoro.SkipCurrentPhase();
        SyncPomodoroState(showNotification: true);
    }

    private void ResetPomodoro()
    {
        if (_viewModel == null || !_viewModel.Pomodoro.CanReset) return;

        _viewModel.Pomodoro.Reset();
        SyncPomodoroState(showNotification: false);
    }

    private void SyncPomodoroState(bool showNotification)
    {
        if (_viewModel == null) return;

        var pomodoro = _viewModel.Pomodoro;
        var phase = pomodoro.PhaseType;
        var status = pomodoro.Status;
        var completedFocusCountToday = GetCompletedFocusCountToday();
        var shouldNotify = showNotification && ShouldNotifyPomodoroPhaseChange(
            phase,
            status,
            _lastPomodoroPhase,
            _lastPomodoroStatus,
            completedFocusCountToday,
            _lastCompletedFocusCountToday);

        _lastPomodoroPhase = phase;
        _lastPomodoroStatus = status;
        _lastCompletedFocusCountToday = completedFocusCountToday;

        RefreshPomodoroMenuItemsIfOpen();

        if (shouldNotify)
            ShowPomodoroNotification(phase);
    }

    private static bool ShouldNotifyPomodoroPhaseChange(
        PomodoroPhaseType phase,
        PomodoroStatus status,
        PomodoroPhaseType? previousPhase,
        PomodoroStatus? previousStatus,
        int? completedFocusCountToday,
        int? previousCompletedFocusCountToday)
    {
        if (status == PomodoroStatus.Idle || previousPhase is null || previousStatus is null)
            return false;

        if (previousPhase != phase)
            return true;

        return completedFocusCountToday is { } currentCount
            && previousCompletedFocusCountToday is { } previousCount
            && currentCount > previousCount;
    }

    private int? GetCompletedFocusCountToday()
    {
        if (_viewModel?.Pomodoro == null || PomodoroMachineField?.GetValue(_viewModel.Pomodoro) is not PomodoroStateMachine machine)
            return null;

        return machine.Session.CompletedFocusCountToday;
    }

    private void ShowPomodoroNotification(PomodoroPhaseType phase)
    {
        if (_trayIcon == null)
            return;

        var message = phase switch
        {
            PomodoroPhaseType.Focus => "进入专注阶段。",
            PomodoroPhaseType.ShortBreak => "进入短休息阶段。",
            PomodoroPhaseType.LongBreak => "进入长休息阶段。",
            _ => "番茄钟阶段已切换。",
        };

        _trayIcon.ShowNotification(
            "Rhythm 番茄钟",
            message,
            default,
            null,
            false,
            false,
            true,
            true,
            TimeSpan.FromSeconds(3));
    }

    private void ScheduleNextMidnight()
    {
        var now = DateTime.Now;
        var nextMidnight = now.Date.AddDays(1);
        var delta = nextMidnight - now;
        if (delta.TotalSeconds < 1) delta = TimeSpan.FromSeconds(1);

        _midnightTimer?.Stop();
        _midnightTimer = new DispatcherTimer { Interval = delta };
        _midnightTimer.Tick += (_, _) =>
        {
            _midnightTimer!.Stop();
            _viewModel?.RolloverAndRefresh();
            ScheduleNextMidnight();
        };
        _midnightTimer.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _midnightTimer?.Stop();
        StopPomodoroTimer();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
