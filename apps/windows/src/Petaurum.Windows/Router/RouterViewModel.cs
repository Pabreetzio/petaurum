using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using Petaurum.Windows.Discovery;
using Petaurum.Windows.Storage;

namespace Petaurum.Windows.Router;

/// <summary>
/// Finds the router, shows its stored pages straight away and refreshes them in the background, one request at a
/// time. See specs/features/router.md.
/// </summary>
public sealed class RouterViewModel : INotifyPropertyChanged
{
    /// <summary>A page opened after this long gets refreshed.</summary>
    private static readonly TimeSpan MaxAgeWhenOpened = TimeSpan.FromMinutes(2);

    private BgwRouterClient? _client;
    private string? _routerId;
    private bool _isAvailable;
    private bool _hasAccessCode;
    private RouterPageViewModel? _current;

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<RouterPageViewModel> Pages { get; } =
    [
        new("Status", "home.ha", false),
        new("Device List", "devices.ha", false),
        new("Home Network", "lanstatistics.ha", false),
        new("Broadband", "broadbandstatistics.ha", false),
        new("Fiber", "fiberstat.ha", false),
        new("System Information", "sysinfo.ha", false),
        new("Firewall", "firewall.ha", false),
        new("Voice", "voice.ha", false),
        new("Logs", "logs.ha", false),
        new("IP Allocation", "ipalloc.ha", true),
        new("NAT Table", "nattable.ha", true),
        new("Event Log", "events.ha", true),
    ];

    /// <summary>Whether a supported router was found.</summary>
    public bool IsAvailable
    {
        get => _isAvailable;
        private set => Set(ref _isAvailable, value);
    }

    public bool HasAccessCode
    {
        get => _hasAccessCode;
        private set => Set(ref _hasAccessCode, value);
    }

    /// <summary>
    /// Finds the default gateway, and if it's a supported router, loads its stored pages and refreshes them all in
    /// the background. Must be called on the UI thread.
    /// </summary>
    public async Task StartAsync()
    {
        var gateway = LocalSubnet.Discover().Select(s => s.Gateway).OfType<IPAddress>().FirstOrDefault();
        if (gateway is null || !await BgwRouterClient.IsBgwAsync(gateway, CancellationToken.None)) return;

        _client = new BgwRouterClient(gateway);
        _routerId = RouterId(gateway);
        HasAccessCode = AccessCodeStore.Get(_routerId) is not null;
        foreach (var page in Pages)
            if (LocalStore.Read<StoredRouterPage>(StorePath(page)) is { } stored)
            {
                page.Data = stored.Data;
                page.ReadAt = stored.ReadAt;
            }
        IsAvailable = true;

        // The page on screen goes first; the rest follow one at a time.
        if (_current is not null) await RefreshAsync(_current);
        foreach (var page in Pages)
            if (page != _current && (!page.NeedsAccessCode || HasAccessCode))
                await RefreshAsync(page);
    }

    /// <summary>Called when a page comes on screen: refreshes it if its stored copy is old.</summary>
    public void Open(RouterPageViewModel page)
    {
        _current = page;
        if (IsAvailable && page.IsStale(MaxAgeWhenOpened)) _ = RefreshAsync(page);
    }

    /// <summary>Reads a page from the router and stores it. The stored copy stays on screen until this finishes.</summary>
    public async Task RefreshAsync(RouterPageViewModel page)
    {
        if (_client is null || page.IsRefreshing) return;
        page.IsRefreshing = true;
        try
        {
            var data = await _client.ReadAsync(page.Path, () => _routerId is null ? null : AccessCodeStore.Get(_routerId),
                CancellationToken.None);
            page.Data = data;
            page.ReadAt = DateTimeOffset.Now;
            page.Error = null;
            page.IsWaitingForAccessCode = false;
            LocalStore.Write(StorePath(page), new StoredRouterPage(page.Path, page.ReadAt.Value, data));
        }
        catch (AccessCodeRequiredException)
        {
            page.IsWaitingForAccessCode = true;
            page.Error = null;
        }
        catch (AccessCodeRejectedException e)
        {
            ForgetAccessCode();
            page.IsWaitingForAccessCode = true;
            page.Error = e.Message;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException)
        {
            page.Error = e is TaskCanceledException ? "The router took too long to answer." : $"Couldn't refresh: {e.Message}";
        }
        finally
        {
            page.IsRefreshing = false;
        }
    }

    /// <summary>Stores the access code and reads the pages that were waiting for it.</summary>
    public async Task SetAccessCodeAsync(string code)
    {
        if (_routerId is null) return;
        AccessCodeStore.Set(_routerId, code);
        HasAccessCode = true;
        foreach (var page in Pages.Where(p => p.IsWaitingForAccessCode || p.NeedsAccessCode && p.IsEmpty).ToList())
            await RefreshAsync(page);
    }

    public void ForgetAccessCode()
    {
        if (_routerId is not null) AccessCodeStore.Remove(_routerId);
        HasAccessCode = false;
    }

    /// <summary>The router's own web page for this page.</summary>
    public string PageUri(RouterPageViewModel page) => $"http://{_client?.Address}/cgi-bin/{page.Path}";

    /// <summary>Identifies the router by its MAC, so another network's router at the same IP keeps its own data.</summary>
    private static string RouterId(IPAddress gateway)
    {
        var mac = ArpTable.Read().FirstOrDefault(e => e.IpAddress.Equals(gateway))?.MacAddress;
        return mac is null
            ? gateway.ToString()
            : string.Join("-", mac.GetAddressBytes().Select(b => b.ToString("X2")));
    }

    private string StorePath(RouterPageViewModel page) =>
        System.IO.Path.Combine("router", _routerId ?? "unknown", System.IO.Path.ChangeExtension(page.Path, ".json"));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
