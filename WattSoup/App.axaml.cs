using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WattSoup.Services;
using WattSoup.ViewModels;
using WattSoup.Views;

namespace WattSoup;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Simple manual composition root. Swap for a DI container later if needed.
            IBatteryService batteryService = new LinuxBatteryService();
            var mainViewModel = new MainViewModel(batteryService);

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel,
            };

            desktop.ShutdownRequested += (_, _) => mainViewModel.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
