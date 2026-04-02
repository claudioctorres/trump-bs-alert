using Microsoft.Extensions.Logging;
#if WINDOWS
using System.Windows.Input;
using H.NotifyIcon;
#endif

namespace TrumpBsAlert.Services;

public partial class TrayService : ITrayService
{
    private readonly ILogger<TrayService> _logger;
#if WINDOWS
    private TaskbarIcon? _taskbarIcon;
#endif

    public TrayService(ILogger<TrayService> logger)
    {
        _logger = logger;
    }

    public void Initialize()
    {
#if WINDOWS
        var showCommand = new SimpleCommand(() => ShowWindowRequested?.Invoke(this, EventArgs.Empty));

        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = "trump-bs-alert",
            LeftClickCommand = showCommand,
            DoubleClickCommand = showCommand,
            NoLeftClickDelay = true,
        };

        var iconPath = Path.Combine(AppContext.BaseDirectory, "tray_icon.scale-100.ico");
        if (!File.Exists(iconPath))
            iconPath = Path.Combine(AppContext.BaseDirectory, "appicon.ico");
        if (File.Exists(iconPath))
            _taskbarIcon.UpdateIcon(new System.Drawing.Icon(iconPath));

        FlyoutBase.SetContextFlyout(_taskbarIcon, new MenuFlyout
        {
            new MenuFlyoutItem
            {
                Text = "Sair",
                Command = new SimpleCommand(() => Application.Current?.Quit()),
            }
        });

        _taskbarIcon.ForceCreate();
#endif
        _logger.LogInformation("Tray service initialized");
    }

    public void UpdateTooltip(string text)
    {
#if WINDOWS
        if (_taskbarIcon is not null)
            _taskbarIcon.ToolTipText = text;
#endif
    }

    public event EventHandler? ShowWindowRequested;

#if WINDOWS
    private sealed class SimpleCommand(Action action) : ICommand
    {
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => action();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
#endif
}
