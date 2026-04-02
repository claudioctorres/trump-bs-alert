using Microsoft.UI.Xaml;

namespace TrumpBsAlert.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        this.InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => TrumpBsAlert.MauiProgram.CreateMauiApp();
}
