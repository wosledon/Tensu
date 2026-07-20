using System.IO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Net.Http;

namespace Tensu.Api.Services;

/// <summary>
/// Watches appsettings.json for changes and triggers a settings reload via the admin API.
/// This provides configuration hot-reload for file-based changes.
/// </summary>
public class ConfigFileWatcherService : BackgroundService
{
    private readonly ILogger<ConfigFileWatcherService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _configPath;
    private FileSystemWatcher? _watcher;
    private DateTime _lastReload = DateTime.MinValue;
    private const int ReloadThrottleMs = 1000;

    public ConfigFileWatcherService(ILogger<ConfigFileWatcherService> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configPath = FindConfigPath(configuration) ?? "appsettings.json";
    }

    private static string? FindConfigPath(IConfiguration configuration)
    {
        var configPath = configuration["Config:FilePath"];
        if (string.IsNullOrEmpty(configPath)) return null;

        if (Path.IsPathFullyQualified(configPath)) return configPath;

        var baseDir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(baseDir, configPath));
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!File.Exists(_configPath))
        {
            _logger.LogInformation("Config file {Path} not found, skipping file watcher", _configPath);
            return Task.CompletedTask;
        }

        _watcher = new FileSystemWatcher(Path.GetDirectoryName(_configPath)!)
        {
            Filter = Path.GetFileName(_configPath),
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
        };

        _watcher.Changed += OnConfigChanged;
        _watcher.EnableRaisingEvents = true;

        _logger.LogInformation("Watching config file {Path} for changes", _configPath);
        return Task.CompletedTask;
    }

    private async void OnConfigChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Changed) return;

        var now = DateTime.UtcNow;
        if ((now - _lastReload).TotalMilliseconds < ReloadThrottleMs) return;

        _lastReload = now;
        _logger.LogInformation("Config file changed, triggering settings reload");

        try
        {
            using var client = _httpClientFactory.CreateClient();
            var token = Environment.GetEnvironmentVariable("TENSU_ADMIN_TOKEN");
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            var response = await client.PostAsync("http://localhost:5000/api/admin/settings/reload", null);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Settings reload triggered successfully");
            }
            else
            {
                _logger.LogWarning("Settings reload failed with status {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger settings reload");
        }
    }

    public override void Dispose()
    {
        _watcher?.Dispose();
        base.Dispose();
    }
}
