using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Petaurum.Windows.Router;

/// <summary>One router page: its stored copy, shown straight away, and the state of its background refresh.</summary>
public sealed class RouterPageViewModel(string title, string path, bool needsAccessCode) : INotifyPropertyChanged
{
    private RouterPageData? _data;
    private DateTimeOffset? _readAt;
    private bool _isRefreshing;
    private string? _error;
    private bool _isWaitingForAccessCode;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title { get; } = title;

    /// <summary>The router path, such as "devices.ha".</summary>
    public string Path { get; } = path;

    /// <summary>Whether the router asks for the access code on this page.</summary>
    public bool NeedsAccessCode { get; } = needsAccessCode;

    public RouterPageData? Data
    {
        get => _data;
        set
        {
            if (Set(ref _data, value)) { OnPropertyChanged(nameof(Sections)); OnPropertyChanged(nameof(IsEmpty)); }
        }
    }

    public IReadOnlyList<RouterSection> Sections => Data?.Sections ?? [];

    public bool IsEmpty => Data is null;

    public DateTimeOffset? ReadAt
    {
        get => _readAt;
        set { if (Set(ref _readAt, value)) OnPropertyChanged(nameof(StatusText)); }
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set { if (Set(ref _isRefreshing, value)) OnPropertyChanged(nameof(StatusText)); }
    }

    /// <summary>Why the last refresh failed, or null.</summary>
    public string? Error
    {
        get => _error;
        set { if (Set(ref _error, value)) OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => Error is not null;

    /// <summary>The page can't be read until the user enters the access code.</summary>
    public bool IsWaitingForAccessCode
    {
        get => _isWaitingForAccessCode;
        set => Set(ref _isWaitingForAccessCode, value);
    }

    public string StatusText => (ReadAt, IsRefreshing) switch
    {
        (null, true) => "Reading from the router. Some pages take 30 seconds or more…",
        (null, false) => "Not read yet",
        ({ } at, true) => $"Read {Describe(at)} · refreshing…",
        ({ } at, false) => $"Read {Describe(at)}",
    };

    public bool IsStale(TimeSpan maxAge) => ReadAt is not { } at || DateTimeOffset.Now - at > maxAge;

    private static string Describe(DateTimeOffset time)
    {
        var local = time.ToLocalTime();
        return local.Date == DateTime.Today ? $"at {local:t}" : $"on {local:g}";
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
