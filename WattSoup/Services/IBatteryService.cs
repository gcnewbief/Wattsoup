using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WattSoup.Models;

namespace WattSoup.Services;

public interface IBatteryService
{
    /// <summary>
    /// Reads a fresh snapshot of every battery present on the machine
    /// (laptops such as ThinkPads can have two). Returns an empty list when no
    /// battery can be found (e.g. desktop machine). Implementations must NOT
    /// throw for missing individual metric files.
    /// </summary>
    Task<IReadOnlyList<BatteryInfo>> GetBatteriesAsync(CancellationToken cancellationToken = default);
}
