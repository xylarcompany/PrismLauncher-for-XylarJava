using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace XylarJavaLauncher;

public sealed class ModpackItem : INotifyPropertyChanged
{
    private Bitmap? _icon;
    private bool _iconLoadStarted;
    private bool _isInstalling;
    private bool _isInstalled;
    private string? _instancePath;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public int Downloads { get; set; }
    public int Follows { get; set; }
    public bool IsEnabled { get; set; }

    public Bitmap? Icon
    {
        get => _icon;
        private set
        {
            if (ReferenceEquals(_icon, value)) return;
            _icon = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasIcon));
        }
    }

    public bool HasIcon => Icon != null;

    public bool IsInstalling
    {
        get => _isInstalling;
        private set
        {
            if (_isInstalling == value) return;
            _isInstalling = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(InstallActionText));
            OnPropertyChanged(nameof(CanClickInstall));
        }
    }

    public bool IsInstalled
    {
        get => _isInstalled;
        private set
        {
            if (_isInstalled == value) return;
            _isInstalled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(InstallActionText));
            OnPropertyChanged(nameof(ShowOpenFolder));
            OnPropertyChanged(nameof(CanOpenFolder));
            OnPropertyChanged(nameof(StateLabel));
        }
    }

    public string? InstancePath
    {
        get => _instancePath;
        private set
        {
            if (_instancePath == value) return;
            _instancePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowOpenFolder));
            OnPropertyChanged(nameof(CanOpenFolder));
        }
    }

    public string StateLabel => IsInstalled ? "Installed" : "Modrinth";

    public string MetaLine => $"{Downloads:N0} downloads · {Follows:N0} follows";

    public string InstallActionText => IsInstalling ? "Installing…" : IsInstalled ? "Reinstall" : "Install";

    public bool CanClickInstall =>
        !IsInstalling && (!string.IsNullOrWhiteSpace(Slug) || !string.IsNullOrWhiteSpace(ProjectId));

    public bool CanOpenWeb =>
        !string.IsNullOrWhiteSpace(Slug) || !string.IsNullOrWhiteSpace(ProjectId);

    public bool ShowOpenFolder => IsInstalled && !string.IsNullOrWhiteSpace(InstancePath);

    public bool CanOpenFolder => ShowOpenFolder && Directory.Exists(InstancePath);

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshInstallState(string instancesRoot)
    {
        var key = PickFolderKey();
        if (string.IsNullOrWhiteSpace(key))
        {
            IsInstalled = false;
            InstancePath = null;
            return;
        }

        var safe = SanitizeFolderName(key);
        var path = Path.Combine(instancesRoot, safe);
        var root = ModrinthModpackInstaller.FindPackRoot(path);
        if (root != null)
        {
            IsInstalled = true;
            InstancePath = root;
        }
        else
        {
            IsInstalled = false;
            InstancePath = null;
        }
    }

    public void MarkInstalled(string extractDirectory)
    {
        var root = ModrinthModpackInstaller.FindPackRoot(extractDirectory);
        if (root != null)
        {
            InstancePath = root;
            IsInstalled = true;
        }
        else
        {
            InstancePath = extractDirectory;
            IsInstalled = false;
        }
    }

    public void SetInstalling(bool value) => IsInstalling = value;

    public string PickFolderKey()
    {
        if (!string.IsNullOrWhiteSpace(Slug)) return Slug;
        if (!string.IsNullOrWhiteSpace(ProjectId)) return ProjectId;
        return string.Empty;
    }

    public static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0)
                chars[i] = '_';
        }

        var s = new string(chars).Trim();
        return string.IsNullOrEmpty(s) ? "pack" : s;
    }

    public async Task TryLoadIconAsync(HttpClient http, CancellationToken ct = default)
    {
        if (_iconLoadStarted || string.IsNullOrWhiteSpace(IconUrl)) return;
        _iconLoadStarted = true;

        try
        {
            await using var stream = await http.GetStreamAsync(IconUrl, ct).ConfigureAwait(false);
            await using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
            ms.Position = 0;
            var bmp = new Bitmap(ms);
            await Dispatcher.UIThread.InvokeAsync(() => Icon = bmp);
        }
        catch
        {
            _iconLoadStarted = false;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
