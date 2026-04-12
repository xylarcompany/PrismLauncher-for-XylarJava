using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.Installer.NeoForge;
using CmlLib.Core.Installer.NeoForge.Installers;
using CmlLib.Core.ProcessBuilder;

namespace XylarJavaLauncher;

public partial class MainWindow : Window
{
    private readonly MinecraftLauncher _launcher = null!;
    private readonly MinecraftPath _minecraftPath = null!;
    private readonly string _skinsFolder = string.Empty;
    private readonly string _skinSettingsFile = string.Empty;
    private readonly string _instancesFolder = string.Empty;
    private readonly HttpClient _httpClient = new();
    private CancellationTokenSource? _modpackLoadCts;
    private string _versionFilter = "Release";
    private List<ModpackItem> _modpacks = new();
    private readonly ObservableCollection<ModpackItem> _displayedModpacks = new();
    private readonly ObservableCollection<PlayProfile> _playProfiles = new();
    private PlayProfile _activePlayProfile = PlayProfile.Standard;
    private bool _suppressProfileReaction;
    private bool _isInitialized = false;

public MainWindow()
{
    InitializeComponent();

    try
    {
        _minecraftPath = new MinecraftPath();
        _launcher = new MinecraftLauncher(_minecraftPath);

        _skinsFolder = Path.Combine(AppContext.BaseDirectory, "skins");
        _skinSettingsFile = Path.Combine(_skinsFolder, "active_skin.json");
        _instancesFolder = Path.Combine(AppContext.BaseDirectory, "instances");

        Directory.CreateDirectory(_skinsFolder);
        Directory.CreateDirectory(_instancesFolder);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("XylarLauncher/0.2 (+https://modrinth.com)");

        ModpackList.ItemsSource = _displayedModpacks;
        ModpackList.SelectionChanged += ModpackList_SelectionChanged;
        UsernameBox.Text ??= "Player";

        if (ProfileTargetCombo != null)
            ProfileTargetCombo.ItemsSource = _playProfiles;

        _ = LoadVersionsAsync();
        _ = LoadModpacks();

        NavMain.IsChecked = true;
        MainSection.IsVisible = true;
        SkinSection.IsVisible = false;
        ModSection.IsVisible = false;
        CreditsSection.IsVisible = false;
        SetHeader("Home", "Profile, version, loader — then launch");

        _isInitialized = true;
        
        // Register event handlers AFTER initialization
        LoaderCombo.SelectionChanged += LoaderCombo_SelectionChanged;
        VersionCombo.SelectionChanged += VersionCombo_SelectionChanged;
        if (ProfileTargetCombo != null)
            ProfileTargetCombo.SelectionChanged += ProfileTargetCombo_SelectionChanged;
        UsernameBox.TextChanged += UsernameBox_TextChanged;

        // Initialize Liquid Glass Effect on Navbar
        ApplyLiquidGlassEffect();
        
        RefreshPlayProfiles();

        Opened += (_, _) => LoadEmbeddedBrandAssets();

        UpdateStatus("Ready to play.");
    }
    catch (Exception ex)
    {
        UpdateStatus($"Initialization failed: {ex.Message}");
    }
}

    private void ApplyLiquidGlassEffect()
    {
        if (NavGlassHost != null)
            LiquidGlassEffects.ApplySidebarLiquidGlass(NavGlassHost);
    }

    private void UpdateStatus(string message)
    {
        if (StatusText != null) StatusText.Text = message;
        if (HeroStatusText != null) HeroStatusText.Text = message;
    }

    private void UpdateSelectionSummary()
    {
        if (!_isInitialized || VersionCombo == null || LoaderCombo == null || UsernameBox == null) return;

        var player = string.IsNullOrWhiteSpace(UsernameBox.Text) ? "Player" : UsernameBox.Text.Trim();
        var version = VersionCombo.SelectedItem?.ToString();
        var versionText = string.IsNullOrWhiteSpace(version) ? "not selected" : version;
        var loader = GetSelectedLoader();

        if (HeroVersionText != null) HeroVersionText.Text = $"Version: {versionText}";
        if (HeroLoaderText != null) HeroLoaderText.Text = $"Loader: {loader}";
        if (CurrentVersionLabel != null) CurrentVersionLabel.Text = $"Version: {versionText}";
        if (CurrentLoaderLabel != null) CurrentLoaderLabel.Text = $"Loader: {loader}";
        if (CurrentPlayerLabel != null) CurrentPlayerLabel.Text = $"Player: {player}";
        if (SelectionSummaryText != null) SelectionSummaryText.Text = $"{loader} profile • {versionText} • {player}";
    }

    private string GetSelectedLoader()
    {
        if (!_isInitialized || LoaderCombo == null) return "Fabric";
        return (LoaderCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Fabric";
    }

    private void UpdateLoaderMessage()
    {
        if (!_isInitialized || LoaderWarning == null) return;
        var loader = GetSelectedLoader();
        LoaderWarning.Text = string.Equals(loader, "Forge", StringComparison.OrdinalIgnoreCase)
            ? "Forge: CmlLib installs the matching Forge profile for your Minecraft version."
            : "Fabric: the official Fabric installer creates a fabric-loader profile under versions.";
        UpdateSelectionSummary();
    }

    private async Task LoadVersionsAsync()
    {
        try
        {
            if (StatusText == null) return;

            UpdateStatus("Loading versions...");
            var versions = await _launcher.GetAllVersionsAsync();
            if (versions == null || !versions.Any())
            {
                UpdateStatus("No versions were found yet.");
                return;
            }

            var filtered = versions.Where(v => _versionFilter switch
            {
                "Release"   => v.Type == "release",
                "Installed" => Directory.Exists(Path.Combine(_minecraftPath.BasePath, "versions", v.Name)),
                _           => true,
            }).Select(v => v.Name).ToList();

            if (VersionCombo != null)
            {
                VersionCombo.ItemsSource = filtered;
                if (filtered.Any())
                    VersionCombo.SelectedItem = filtered[0];
            }

            UpdateSelectionSummary();
            UpdateStatus(filtered.Any() ? $"Loaded {filtered.Count} versions." : "No versions matched this filter.");
        }
        catch (Exception ex)
        {
            if (StatusText != null)
                UpdateStatus($"Version loading failed: {ex.Message}");
        }
    }

    private void VersionFilterButton_Click(object? sender, RoutedEventArgs e)
    {
        ReleaseFilter.IsChecked = sender == ReleaseFilter;
        InstalledFilter.IsChecked = sender == InstalledFilter;
        AllFilter.IsChecked = sender == AllFilter;

        _versionFilter = ReleaseFilter.IsChecked == true ? "Release"
            : InstalledFilter.IsChecked == true ? "Installed"
            : "All";

        _ = LoadVersionsAsync();
    }

    private void RefreshVersionsButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = LoadVersionsAsync();
    }

    private async void LaunchButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var username = UsernameBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(username))
                username = "Player";

            var mcVersion = VersionCombo.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(mcVersion))
            {
                UpdateStatus("Choose a Minecraft version first.");
                return;
            }

            var loader = GetSelectedLoader();
            string? gameDir = null;
            string? pinnedLoaderVersion = null;

            if (_activePlayProfile is { IsStandard: false } pack)
            {
                if (!string.IsNullOrWhiteSpace(pack.MinecraftVersion))
                    mcVersion = pack.MinecraftVersion;
                loader = pack.Loader;
                pinnedLoaderVersion = pack.LoaderVersion;
                gameDir = pack.GameDirectory;
            }

            UpdateSelectionSummary();
            UpdateStatus($"Preparing {loader}…");
            DownloadProgress.Value = 0;

            var launchVersionId = mcVersion;
            var forgeOpts = new ForgeInstallOptions { SkipIfAlreadyInstalled = true };
            var neoOpts = new NeoForgeInstallOptions { SkipIfAlreadyInstalled = true };

            if (!string.Equals(loader, "Vanilla", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(loader, "Forge", StringComparison.OrdinalIgnoreCase))
                {
                    var forgeInst = new ForgeInstaller(_launcher, _httpClient);
                    launchVersionId = string.IsNullOrWhiteSpace(pinnedLoaderVersion)
                        ? await forgeInst.Install(mcVersion, forgeOpts)
                        : await forgeInst.Install(mcVersion, pinnedLoaderVersion, forgeOpts);
                    if (LoaderWarning != null) LoaderWarning.Text = "Forge ready.";
                }
                else if (string.Equals(loader, "NeoForge", StringComparison.OrdinalIgnoreCase))
                {
                    var neoInst = new NeoForgeInstaller(_launcher);
                    launchVersionId = string.IsNullOrWhiteSpace(pinnedLoaderVersion)
                        ? await neoInst.Install(mcVersion, neoOpts)
                        : await neoInst.Install(mcVersion, pinnedLoaderVersion, neoOpts);
                    if (LoaderWarning != null) LoaderWarning.Text = "NeoForge ready.";
                }
                else if (string.Equals(loader, "Fabric", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(loader, "Quilt", StringComparison.OrdinalIgnoreCase))
                {
                    var ok = await RunFabricQuiltInstallerAsync(mcVersion, loader, pinnedLoaderVersion);
                    if (!ok)
                    {
                        UpdateStatus($"{loader} install failed — check Java in PATH and Loader message below.");
                        return;
                    }

                    launchVersionId = DiscoverFabricQuiltVersionId(loader, mcVersion);
                    if (string.IsNullOrWhiteSpace(launchVersionId))
                    {
                        UpdateStatus(
                            $"{loader} installed but no matching version folder was found under versions\\. Try launching again after install finishes.");
                        return;
                    }

                    if (LoaderWarning != null) LoaderWarning.Text = $"{loader} profile: {launchVersionId}";
                }
                else
                {
                    UpdateStatus($"Loader \"{loader}\" is not supported for launch yet.");
                    return;
                }
            }

            UpdateStatus("Installing / verifying game files…");
            await _launcher.InstallAsync(launchVersionId);

            var option = CreateLaunchOption(username, gameDir);
            UpdateStatus("Launching…");
            var process = await _launcher.BuildProcessAsync(launchVersionId, option);
            process.Start();

            UpdateStatus("Game launched.");
            DownloadProgress.Value = 100;
        }
        catch (Exception ex)
        {
            UpdateStatus($"Launch failed: {ex.Message}");
        }
    }

    private MLaunchOption CreateLaunchOption(string username, string? gameDirectory)
    {
        var session = MSession.CreateOfflineSession(username);
        var option = new MLaunchOption { Session = session };
        if (!string.IsNullOrWhiteSpace(gameDirectory))
        {
            option.ExtraGameArguments = new[]
            {
                new MArgument("--gameDir"),
                new MArgument(gameDirectory!)
            };
        }

        // OfflineSkinLaunchHelper.ApplySkinToLaunchOptions(
        //     _minecraftPath,
        //     username,
        //     _skinSettingsFile,
        //     _skinsFolder,
        //     option);
        return option;
    }

    private string? DiscoverFabricQuiltVersionId(string loader, string mcVersion)
    {
        var versionsDir = Path.Combine(_minecraftPath.BasePath, "versions");
        try
        {
            if (!Directory.Exists(versionsDir))
                return null;

            var prefix = string.Equals(loader, "Fabric", StringComparison.OrdinalIgnoreCase)
                ? "fabric-loader-"
                : "quilt-loader-";

            return Directory.GetDirectories(versionsDir)
                .Select(static d => Path.GetFileName(d))
                .Where(n => !string.IsNullOrEmpty(n))
                .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                            n.Contains(mcVersion, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(static n => n!.Length)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private async Task<bool> RunFabricQuiltInstallerAsync(string version, string loader, string? loaderVersion)
    {
        try
        {
            var loaderKey = loader.ToLowerInvariant();
            var loaderUrl = loaderKey switch
            {
                "fabric" => await GetFabricInstallerUrlAsync(version),
                "quilt"  => await GetQuiltInstallerUrlAsync(version),
                _        => null
            };

            if (string.IsNullOrWhiteSpace(loaderUrl))
            {
                if (LoaderWarning != null) LoaderWarning.Text = $"{loader} is not available for this version yet.";
                return false;
            }

            var installerPath = Path.Combine(AppContext.BaseDirectory, $"{loaderKey}-installer.jar");

            UpdateStatus($"Downloading {loader}…");
            using (var response = await _httpClient.GetAsync(loaderUrl))
            {
                if (!response.IsSuccessStatusCode)
                {
                    if (LoaderWarning != null) LoaderWarning.Text = $"{loader} download failed: {response.StatusCode}";
                    return false;
                }

                await using var contentStream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = File.Create(installerPath);
                await contentStream.CopyToAsync(fileStream);
            }

            UpdateStatus($"Installing {loader}…");

            var loaderArg = string.IsNullOrWhiteSpace(loaderVersion) ? "" : $" -loader {loaderVersion}";
            var args = $"-jar \"{installerPath}\" client -mcversion {version} -downloadMinecraft{loaderArg}";

            var processInfo = new ProcessStartInfo
            {
                FileName               = "java",
                Arguments              = args,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = false,
                WorkingDirectory       = _minecraftPath.BasePath
            };

            using var proc = Process.Start(processInfo);
            if (proc == null)
                return false;

            await Task.Run(() => proc.WaitForExit());
            var ok = proc.ExitCode == 0;
            if (LoaderWarning != null)
            {
                LoaderWarning.Text = ok
                    ? $"{loader} installed successfully."
                    : $"{loader} installer exit {proc.ExitCode}: {proc.StandardError.ReadToEnd()}";
            }

            try
            {
                File.Delete(installerPath);
            }
            catch
            {
                // ignore
            }

            return ok;
        }
        catch (Exception ex)
        {
            if (LoaderWarning != null) LoaderWarning.Text = $"{loader} install error: {ex.Message}";
            return false;
        }
    }

    private async Task<string?> GetFabricInstallerUrlAsync(string version)
    {
        try
        {
            using var response = await _httpClient.GetAsync("https://meta.fabricmc.net/v2/versions/installer");
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var latestVersion = doc.RootElement.EnumerateArray().FirstOrDefault().GetProperty("version").GetString();

            if (!string.IsNullOrWhiteSpace(latestVersion))
                return $"https://maven.fabricmc.net/net/fabricmc/fabric-installer/{latestVersion}/fabric-installer-{latestVersion}.jar";
        }
        catch { }
        return null;
    }

    private async Task<string?> GetQuiltInstallerUrlAsync(string version)
    {
        try
        {
            using var response = await _httpClient.GetAsync("https://meta.quiltmc.org/v3/versions/installer");
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var latestVersion = doc.RootElement.GetProperty("latest").GetProperty("release").GetString();

            if (!string.IsNullOrWhiteSpace(latestVersion))
                return $"https://maven.quiltmc.org/repository/release/org/quiltmc/quilt-installer/{latestVersion}/quilt-installer-{latestVersion}.jar";
        }
        catch { }
        return null;
    }

    private void RefreshPlayProfiles()
    {
        if (ProfileTargetCombo == null)
            return;

        var keepId = _activePlayProfile.Id;
        _playProfiles.Clear();
        _playProfiles.Add(PlayProfile.Standard);

        if (Directory.Exists(_instancesFolder))
        {
            foreach (var sub in Directory.GetDirectories(_instancesFolder))
            {
                var root = ModrinthModpackInstaller.FindPackRoot(sub);
                if (string.IsNullOrEmpty(root))
                    continue;

                var info = ModrinthPackIndexReader.TryRead(root);
                if (info == null)
                    continue;

                _playProfiles.Add(new PlayProfile
                {
                    Id                = root,
                    Title             = $"{info.Name} (installed)",
                    GameDirectory     = root,
                    MinecraftVersion  = info.MinecraftVersion,
                    Loader            = info.Loader,
                    LoaderVersion     = info.LoaderVersion
                });
            }
        }

        _suppressProfileReaction = true;
        var pick = _playProfiles.FirstOrDefault(p => p.Id == keepId) ?? PlayProfile.Standard;
        ProfileTargetCombo.SelectedItem = pick;
        _suppressProfileReaction = false;
        OnProfileSelectionApplied(pick);
    }

    private void ProfileTargetCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized || _suppressProfileReaction)
            return;
        var p = ProfileTargetCombo?.SelectedItem as PlayProfile ?? PlayProfile.Standard;
        OnProfileSelectionApplied(p);
    }

    private void OnProfileSelectionApplied(PlayProfile p)
    {
        _activePlayProfile = p;
        UpdateProfileUiLocks();
        UpdateActiveTargetFromProfile();
        UpdateSelectionSummary();
        if (!p.IsStandard)
            _ = ApplyModpackSelectionAsync(p);
    }

    private void UpdateProfileUiLocks()
    {
        var std = _activePlayProfile.IsStandard;
        if (VersionCombo != null)
            VersionCombo.IsEnabled = std;
        if (LoaderCombo != null)
            LoaderCombo.IsEnabled = std;
        if (ReleaseFilter != null)
            ReleaseFilter.IsEnabled = std;
        if (InstalledFilter != null)
            InstalledFilter.IsEnabled = std;
        if (AllFilter != null)
            AllFilter.IsEnabled = std;
    }

    private void UpdateActiveTargetFromProfile()
    {
        var p = _activePlayProfile;
        if (ActiveTargetTitle != null)
            ActiveTargetTitle.Text = p.Title;
        if (ActiveTargetSubtitle != null)
        {
            ActiveTargetSubtitle.Text = p.IsStandard
                ? "Default game directory (.minecraft)."
                : $"{p.MinecraftVersion} · {p.Loader}" +
                  (string.IsNullOrWhiteSpace(p.LoaderVersion) ? "" : " " + p.LoaderVersion) +
                  " · mods from instance folder";
        }

        if (ActiveTargetStatus != null)
            ActiveTargetStatus.Text = p.IsStandard ? string.Empty : p.GameDirectory ?? string.Empty;
    }

    private async Task ApplyModpackSelectionAsync(PlayProfile p)
    {
        if (p.IsStandard || VersionCombo == null || LoaderCombo == null)
            return;

        try
        {
            AllFilter.IsChecked = true;
            ReleaseFilter.IsChecked = false;
            InstalledFilter.IsChecked = false;
            _versionFilter = "All";
            await LoadVersionsAsync();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!string.IsNullOrWhiteSpace(p.MinecraftVersion))
                    VersionCombo.SelectedItem = p.MinecraftVersion;
                SelectLoaderComboItem(p.Loader);
                UpdateSelectionSummary();
                UpdateLoaderMessage();
                UpdateActiveTargetFromProfile();
            });
        }
        catch
        {
            // ignore UI sync failures
        }
    }

    private void SelectLoaderComboItem(string loader)
    {
        if (LoaderCombo == null)
            return;
        var display = NormalizeLoaderForCombo(loader);
        foreach (var o in LoaderCombo.Items)
        {
            if (o is ComboBoxItem item &&
                string.Equals(item.Content?.ToString(), display, StringComparison.OrdinalIgnoreCase))
            {
                LoaderCombo.SelectedItem = item;
                return;
            }
        }

        foreach (var o in LoaderCombo.Items)
        {
            if (o is ComboBoxItem first)
            {
                LoaderCombo.SelectedItem = first;
                return;
            }
        }
    }

    private static string NormalizeLoaderForCombo(string loader)
    {
        if (string.IsNullOrWhiteSpace(loader))
            return "Fabric";
        if (loader.Equals("NeoForge", StringComparison.OrdinalIgnoreCase) ||
            loader.Equals("Forge", StringComparison.OrdinalIgnoreCase))
            return "Forge";
        return "Fabric";
    }

    private async void RefreshModpacksButton_Click(object? sender, RoutedEventArgs e)
    {
        await LoadModpacks();
    }

    private async Task LoadModpacks()
    {
        _modpackLoadCts?.Cancel();
        _modpackLoadCts = new CancellationTokenSource();
        var token = _modpackLoadCts.Token;

        try
        {
            UpdateStatus("Downloading Modrinth catalog...");
            _modpacks = new List<ModpackItem>();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _displayedModpacks.Clear();
                if (ModpackLoadProgressText != null)
                    ModpackLoadProgressText.Text = "Starting catalog sync…";
            });

            const int limit = 100;
            var offset = 0;
            int? totalHits = null;
            var facets = Uri.EscapeDataString("[[\"project_type:modpack\"]]");

            while (!token.IsCancellationRequested)
            {
                var url = $"https://api.modrinth.com/v2/search?facets={facets}&limit={limit}&offset={offset}";
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(token);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: token);

                if (!totalHits.HasValue && doc.RootElement.TryGetProperty("total_hits", out var th))
                    totalHits = th.GetInt32();

                var hits = doc.RootElement.GetProperty("hits");
                var batch = new List<ModpackItem>();
                foreach (var item in hits.EnumerateArray())
                {
                    if (token.IsCancellationRequested) break;

                    var title = item.TryGetProperty("title", out var te) ? te.GetString() ?? "Untitled" : "Untitled";
                    var slug = item.TryGetProperty("slug", out var se) ? se.GetString() ?? string.Empty : string.Empty;
                    var desc = item.TryGetProperty("description", out var de) ? de.GetString() ?? string.Empty : string.Empty;
                    var pid = item.TryGetProperty("project_id", out var pe) ? pe.GetString() ?? string.Empty : string.Empty;
                    var icon = item.TryGetProperty("icon_url", out var ie) && ie.ValueKind != JsonValueKind.Null
                        ? ie.GetString() ?? string.Empty
                        : string.Empty;
                    var downloads = item.TryGetProperty("downloads", out var dwn) ? dwn.GetInt32() : 0;
                    var follows = item.TryGetProperty("follows", out var fl) ? fl.GetInt32() : 0;

                    batch.Add(new ModpackItem
                    {
                        Title       = title,
                        Description = desc,
                        Slug        = slug,
                        ProjectId   = pid,
                        IconUrl     = icon,
                        Downloads   = downloads,
                        Follows     = follows,
                        IsEnabled   = false
                    });
                }

                if (batch.Count == 0)
                    break;

                _modpacks.AddRange(batch);
                offset += batch.Count;

                var loaded = _modpacks.Count;
                var totalText = totalHits.HasValue ? totalHits.Value.ToString("N0") : "…";
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    AppendModpacksToView(batch);
                    if (ModpackLoadProgressText != null)
                        ModpackLoadProgressText.Text = $"Loaded {loaded:N0} modpacks (Modrinth reports {totalText} total).";
                });

                if (totalHits.HasValue && offset >= totalHits.Value)
                    break;
                if (batch.Count < limit)
                    break;
            }

            if (!token.IsCancellationRequested)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RefreshModpackInstallStates();
                    FilterModpacks();
                    UpdateStatus($"Catalog ready — {_modpacks.Count:N0} modpacks.");
                    if (ModpackLoadProgressText != null)
                        ModpackLoadProgressText.Text = $"Complete: {_modpacks.Count:N0} modpacks indexed locally.";
                    RefreshPlayProfiles();
                });
            }
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                RefreshModpackInstallStates();
                FilterModpacks();
                UpdateStatus("Catalog load stopped.");
                if (ModpackLoadProgressText != null)
                    ModpackLoadProgressText.Text = $"Stopped at {_modpacks.Count:N0} modpacks.";
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() => UpdateStatus($"Modpack catalog failed: {ex.Message}"));
        }
    }

    private void AppendModpacksToView(IEnumerable<ModpackItem> batch)
    {
        var term = ModSearchBox?.Text?.Trim().ToLowerInvariant() ?? "";
        if (string.IsNullOrEmpty(term))
        {
            foreach (var item in batch)
            {
                item.RefreshInstallState(_instancesFolder);
                _displayedModpacks.Add(item);
            }
        }
        else
            FilterModpacks();
    }

    private void RefreshModpackInstallStates()
    {
        foreach (var m in _modpacks)
            m.RefreshInstallState(_instancesFolder);
    }

    private void StopModpackLoad_Click(object? sender, RoutedEventArgs e)
    {
        _modpackLoadCts?.Cancel();
    }

    private async void ModpackRow_Loaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: ModpackItem item })
            return;
        try
        {
            await item.TryLoadIconAsync(_httpClient);
        }
        catch
        {
            // ignore icon failures
        }
    }

    private async void ModpackInstall_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ModpackItem pack })
            return;
        if (!pack.CanClickInstall)
            return;

        var projectRef = !string.IsNullOrWhiteSpace(pack.ProjectId) ? pack.ProjectId : pack.Slug;
        if (string.IsNullOrWhiteSpace(projectRef))
            return;

        pack.SetInstalling(true);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(45));
            var token = cts.Token;
            var destFolder = Path.Combine(_instancesFolder, ModpackItem.SanitizeFolderName(pack.PickFolderKey()));

            UpdateStatus($"Looking up {pack.Title}…");
            var fileUrl = await ModrinthModpackInstaller.TryGetLatestMrpackUrlAsync(_httpClient, projectRef, token);
            if (string.IsNullOrWhiteSpace(fileUrl))
            {
                UpdateStatus($"No .mrpack file listed for {pack.Title}.");
                return;
            }

            var temp = Path.Combine(Path.GetTempPath(), $"xylar-{Guid.NewGuid():N}.mrpack");
            try
            {
                UpdateStatus($"Downloading {pack.Title}…");
                await ModrinthModpackInstaller.DownloadFileAsync(_httpClient, fileUrl, temp, token);
                UpdateStatus($"Extracting {pack.Title}…");
                ModrinthModpackInstaller.ExtractMrpackZip(temp, destFolder);
                var packRoot = ModrinthModpackInstaller.FindPackRoot(destFolder);
                if (packRoot != null)
                {
                    UpdateStatus($"Downloading mods for {pack.Title} (Modrinth index)…");
                    var progress = new Progress<(int done, int total, string relativePath)>(p =>
                    {
                        _ = Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            if (DownloadProgress != null && p.total > 0)
                                DownloadProgress.Value = 100.0 * p.done / Math.Max(1, p.total);
                            UpdateStatus(p.done == 0
                                ? $"Preparing files… ({p.total} entries)"
                                : $"Mods {p.done}/{p.total}: {p.relativePath}");
                        });
                    });
                    await ModrinthModpackInstaller.DownloadPackFilesFromIndexAsync(_httpClient, packRoot, progress, token);
                }

                pack.MarkInstalled(destFolder);
                RefreshPlayProfiles();
                UpdateStatus($"Installed {pack.Title} (mods + index) under instances\\{Path.GetFileName(destFolder)}");
            }
            finally
            {
                try
                {
                    if (File.Exists(temp))
                        File.Delete(temp);
                }
                catch
                {
                    // ignore
                }
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"Install failed: {ex.Message}");
        }
        finally
        {
            pack.SetInstalling(false);
        }
    }

    private void ModpackWeb_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ModpackItem pack })
            return;

        var slug = string.IsNullOrWhiteSpace(pack.Slug) ? pack.ProjectId : pack.Slug;
        if (string.IsNullOrWhiteSpace(slug))
            return;

        var url = $"https://modrinth.com/modpack/{slug}";
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        UpdateStatus($"Opened {pack.Title} in browser.");
    }

    private void ModpackOpenFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ModpackItem pack })
            return;
        if (string.IsNullOrWhiteSpace(pack.InstancePath) || !Directory.Exists(pack.InstancePath))
            return;

        Process.Start(new ProcessStartInfo { FileName = pack.InstancePath, UseShellExecute = true });
        UpdateStatus($"Opened folder for {pack.Title}.");
    }

    private void ModpackList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ModpackList.SelectedItem is not ModpackItem pack)
        {
            if (ModpackTargetTitle != null) ModpackTargetTitle.Text = "Pick a row";
            if (ModpackTargetSubtitle != null) ModpackTargetSubtitle.Text = string.Empty;
            return;
        }

        if (ModpackTargetTitle != null) ModpackTargetTitle.Text = pack.Title;
        if (ModpackTargetSubtitle != null)
            ModpackTargetSubtitle.Text = string.IsNullOrWhiteSpace(pack.Description) ? pack.MetaLine : pack.Description;
        if (ModpackTargetStatus != null)
        {
            var pathNote = pack.ShowOpenFolder && !string.IsNullOrWhiteSpace(pack.InstancePath)
                ? $"\n{pack.InstancePath}"
                : string.Empty;
            ModpackTargetStatus.Text = $"Slug: {pack.Slug} · {pack.MetaLine}{pathNote}";
        }
    }

    private void ModpackToggle_Checked(object? sender, RoutedEventArgs e)
        => HandleModpackToggle(sender, true);

    private void ModpackToggle_Unchecked(object? sender, RoutedEventArgs e)
        => HandleModpackToggle(sender, false);

    private void HandleModpackToggle(object? sender, bool enabled)
    {
        if (sender is ToggleSwitch ts && ts.DataContext is ModpackItem item)
        {
            item.IsEnabled = enabled;
            UpdateStatus(enabled ? $"Marked modpack: {item.Title}" : $"Unmarked modpack: {item.Title}");
        }
    }

    private void OpenOfflineSkinsModPage_Click(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://modrinth.com/mod/offlineskins?version=1.21.11",
            UseShellExecute = true
        });
        UpdateStatus("Opened OfflineSkins on Modrinth.");
    }

    private void DiscordXylarInc_Click(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo { FileName = "https://discord.gg/CQ6Et3Kws7", UseShellExecute = true });
        UpdateStatus("Opened Xylar Inc. Discord.");
    }

    private void DiscordXylarSupport_Click(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo { FileName = "https://discord.gg/KEHccUvvaX", UseShellExecute = true });
        UpdateStatus("Opened Xylar Support Discord.");
    }

    private void LoadEmbeddedBrandAssets()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetName().Name ?? "XylarJavaLauncher";

        try
        {
            if (SidebarBrandImage != null)
                SidebarBrandImage.Source = OpenBitmapAsset(name, "Assets/minecraft.png");
        }
        catch { /* optional asset */ }

        try
        {
            if (AboutLogoXylarInc != null)
                AboutLogoXylarInc.Source = OpenBitmapAsset(name, "Resources/logos/xylarinc.png");
        }
        catch { /* optional asset */ }

        try
        {
            if (AboutLogoXylarSupport != null)
                AboutLogoXylarSupport.Source = OpenBitmapAsset(name, "Resources/logos/xylarsupport.png");
        }
        catch { /* optional asset */ }

        try
        {
            var uri = new Uri($"avares://{name}/Assets/minecraft.png");
            using var s = AssetLoader.Open(uri);
            Icon = new WindowIcon(s);
        }
        catch { /* window icon optional */ }
    }

    private static Bitmap OpenBitmapAsset(string assemblyName, string relativePath)
    {
        var uri = new Uri($"avares://{assemblyName}/{relativePath}");
        using var input = AssetLoader.Open(uri);
        using var ms = new MemoryStream();
        input.CopyTo(ms);
        ms.Position = 0;
        return new Bitmap(ms);
    }

    private void ModSearchBox_KeyUp(object? sender, Avalonia.Input.KeyEventArgs e)
        => FilterModpacks();

    private void FilterModpacks()
    {
        var searchTerm = ModSearchBox?.Text?.ToLower() ?? "";
        var filtered = string.IsNullOrWhiteSpace(searchTerm)
            ? _modpacks
            : _modpacks.Where(m =>
                m.Title.ToLower().Contains(searchTerm) ||
                m.Description.ToLower().Contains(searchTerm)).ToList();

        _displayedModpacks.Clear();
        foreach (var item in filtered)
        {
            item.RefreshInstallState(_instancesFolder);
            _displayedModpacks.Add(item);
        }
    }

    private async void NavButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton btn) return;

        string content = btn.Content?.ToString() ?? "";
        Border? target = content switch
        {
            "Home"     => MainSection,
            "Skins"    => SkinSection,
            "Modpacks" => ModSection,
            "About"    => CreditsSection,
            _          => null
        };

        switch (content)
        {
            case "Home":
                SetHeader("Home", "Profile, version, loader — then launch");
                RefreshPlayProfiles();
                break;
            case "Skins":
                SetHeader("Offline skins", "Fabric mod: OfflineSkins — setup guide");
                break;
            case "Modpacks":
                SetHeader("Modpacks", "Full Modrinth catalog");
                RefreshModpackInstallStates();
                break;
            case "About":
                SetHeader("About", "Credits and legal");
                break;
        }

        NavMain.IsChecked    = content == "Home";
        NavSkins.IsChecked   = content == "Skins";
        NavMods.IsChecked    = content == "Modpacks";
        NavCredits.IsChecked = content == "About";

        await AnimateSectionSwitchAsync(target);
    }

    private async Task AnimateSectionSwitchAsync(Border? target)
    {
        var sections = new[] { MainSection, SkinSection, ModSection, CreditsSection };
        foreach (var s in sections)
        {
            if (s == null) continue;
            if (s != target)
            {
                s.IsVisible = false;
                s.Opacity   = 1;
            }
        }

        if (target == null) return;

        target.Opacity   = 0;
        target.IsVisible = true;
        await Task.Delay(16);
        await Dispatcher.UIThread.InvokeAsync(() => { target.Opacity = 1; }, DispatcherPriority.Render);
    }

    private void SetHeader(string title, string subtitle)
    {
        if (HeaderTitleText != null) HeaderTitleText.Text = title;
        if (HeaderSubtitleText != null) HeaderSubtitleText.Text = subtitle;
    }

    private void LoaderCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        UpdateLoaderMessage();
        UpdateActiveTargetFromProfile();
    }

    private void VersionCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        UpdateSelectionSummary();
        UpdateActiveTargetFromProfile();
    }

    private void UsernameBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_isInitialized) return;
        UpdateSelectionSummary();
    }

}

public class SkinSettings
{
    public string Path  { get; set; } = string.Empty;
    public string Model { get; set; } = "classic";
}