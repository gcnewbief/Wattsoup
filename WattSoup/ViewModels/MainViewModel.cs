using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using WattSoup.Models;
using WattSoup.Services;

namespace WattSoup.ViewModels;

public sealed partial class MainViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    private readonly IBatteryService _batteryService;
    private readonly CancellationTokenSource _cts = new();
    private Timer? _timer;

    /// <summary>One entry per physical battery. Bound to a TabControl in the UI.</summary>
    public ObservableCollection<BatteryViewModel> Batteries { get; } = new();

    /// <summary>False when no battery is present (e.g. desktop) — UI shows a message.</summary>
    [ObservableProperty] private bool _hasBattery = true;

    /// <summary>True when more than one battery is present (drives tab-header visibility).</summary>
    [ObservableProperty] private bool _hasMultipleBatteries;

    public MainViewModel(IBatteryService batteryService)
    {
        _batteryService = batteryService;

        // Kick an immediate refresh, then poll on an interval.
        _ = RefreshAsync();
        _timer = new Timer(_ => _ = RefreshAsync(), null, PollInterval, PollInterval);
    }

    private async Task RefreshAsync()
    {
        if (_cts.IsCancellationRequested)
            return;

        IReadOnlyList<BatteryInfo> infos;
        try
        {
            infos = await _batteryService.GetBatteriesAsync(_cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // CRITICAL: marshal all property writes back onto the UI thread.
        await Dispatcher.UIThread.InvokeAsync(() => Apply(infos));
    }

    private void Apply(IReadOnlyList<BatteryInfo> infos)
    {
        HasBattery = infos.Count > 0;
        HasMultipleBatteries = infos.Count > 1;

        // Update existing VMs in place (preserves selected tab); add/remove as needed.
        var seen = new HashSet<string>();
        foreach (var info in infos)
        {
            seen.Add(info.Name);
            var existing = Batteries.FirstOrDefault(b => b.Key == info.Name);
            if (existing is null)
                Batteries.Add(new BatteryViewModel(info));
            else
                existing.Update(info);
        }

        // Remove batteries that disappeared (e.g. hot-swappable bay removed).
        for (int i = Batteries.Count - 1; i >= 0; i--)
        {
            if (!seen.Contains(Batteries[i].Key))
                Batteries.RemoveAt(i);
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
        if (!_cts.IsCancellationRequested)
            _cts.Cancel();
        _cts.Dispose();
    }
}
