using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using H.NotifyIcon;
using Rhythm.Autostart;
using Rhythm.State;
using Rhythm.UI;

namespace Rhythm;

public partial class App : Application
{
    private TaskbarIcon? _trayIcon;
    private MainViewModel? _viewModel;
    private MainWindow? _mainWindow;
    private DispatcherTimer? _midnightTimer;

    public bool IsShuttingDown { get; private set; }

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
        _mainWindow = new MainWindow { DataContext = _viewModel };
        _mainWindow.Show();

        BuildTrayIcon();
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
        var miShow = new MenuItem { Header = "显示窗口" };
        miShow.Click += (_, _) => ShowMainWindow();
        var miHide = new MenuItem { Header = "隐藏窗口" };
        miHide.Click += (_, _) => HideMainWindow();
        var miEdit = new MenuItem { Header = "编辑事项..." };
        miEdit.Click += (_, _) => OpenEditItems();
        var miAutostart = new MenuItem { Header = "开机启动", IsCheckable = true, IsChecked = AutostartManager.IsEnabled() };
        miAutostart.Click += (_, _) =>
        {
            try { AutostartManager.Set(miAutostart.IsChecked); }
            catch (Exception ex)
            {
                MessageBox.Show($"无法修改自启动设置：\n{ex.Message}", "Rhythm", MessageBoxButton.OK, MessageBoxImage.Warning);
                miAutostart.IsChecked = AutostartManager.IsEnabled();
            }
        };
        var miAbout = new MenuItem { Header = "关于 Rhythm" };
        miAbout.Click += (_, _) => MessageBox.Show(
            "Rhythm v1\n\n本地桌面便签\n勾选完成，每日自动重置。",
            "关于 Rhythm",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        var miExit = new MenuItem { Header = "退出" };
        miExit.Click += (_, _) => ShutdownApp();

        menu.Items.Add(miShow);
        menu.Items.Add(miHide);
        menu.Items.Add(miEdit);
        menu.Items.Add(new Separator());
        menu.Items.Add(miAutostart);
        menu.Items.Add(new Separator());
        menu.Items.Add(miAbout);
        menu.Items.Add(miExit);

        _trayIcon.ContextMenu = menu;
        _trayIcon.TrayLeftMouseDoubleClick += (_, _) => ShowMainWindow();
        _trayIcon.ForceCreate();
    }

    private void ShowMainWindow()
    {
        if (_mainWindow == null) return;
        if (!_mainWindow.IsVisible) _mainWindow.Show();
        _mainWindow.Activate();
    }

    private void HideMainWindow() => _mainWindow?.Hide();

    public void ShutdownFromUi() => ShutdownApp();

    public void OpenEditItems()
    {
        if (_viewModel == null) return;
        var w = new EditItemsWindow(_viewModel)
        {
            Owner = _mainWindow?.IsVisible == true ? _mainWindow : null,
        };
        w.ShowDialog();
    }

    private void ShutdownApp()
    {
        IsShuttingDown = true;
        _midnightTimer?.Stop();
        _trayIcon?.Dispose();
        Shutdown();
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
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
