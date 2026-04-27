using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Globalization;


namespace FileMonitor;

public sealed class FileMonitorService : BackgroundService, IDisposable
{
    private readonly ILogger<FileMonitorService> _logger;
    private readonly IOptions<MonitorSettings> _options;

    private readonly ConcurrentDictionary<string, FileSnapshot> _previousSnapshot = new(StringComparer.OrdinalIgnoreCase);

    private bool _disposed;
    private PeriodicTimer? _timer;

    public FileMonitorService(ILogger<FileMonitorService> logger, IOptions<MonitorSettings> options)
    {
        _logger = logger;
        _options = options;
    }

    public override void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _timer?.Dispose();
        _timer = null;

        base.Dispose();
    }

    /// <summary>
    /// Рабочий цикл: периодические снимки до отмены
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = _options.Value;

        if (string.IsNullOrWhiteSpace(settings.DirectoryPath))
        {
            _logger.LogError("Пустая DirectoryPath в секции {Section}) файла appsettings.", MonitorSettings.SectionName);
            return;
        }

        var seconds = Math.Max(1, settings.PollingIntervalSeconds);

        _logger.LogInformation("Мониторинг: {Path}, период {Interval}", settings.DirectoryPath, seconds);

        _timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));

        try
        {
            do
            {
                // Запускаем проверку файлов в папке
                await FileCheckAsync(_options.Value, stoppingToken);
            }
            while (await _timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("FileMonitorService: остановка по CancellationToken.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "FileMonitorService: необработанное исключение в цикле ExecuteAsync.");
            throw;
        }
    }

    private Task FileCheckAsync(MonitorSettings options, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.DirectoryPath))
        {
            _logger.LogWarning("Нет настройки DirectoryPath");
            return Task.CompletedTask;
        }

        var path = Path.GetFullPath(options.DirectoryPath);

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            _logger.LogWarning("Нет папки {Path}, создали", path);
        }

        IReadOnlyDictionary<string, FileSnapshot> curSnapShots;

        try
        {
            if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);

            curSnapShots = BuildCurrentSnapshot(path, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Проблемы с папкой {Directory}. Сервис продолжит опросы.", options.DirectoryPath);
            return Task.CompletedTask;
        }

        foreach (var (fullPath, _) in curSnapShots)
        {
            // Чтобы не сохранять одно и тоже
            if (_previousSnapshot.ContainsKey(fullPath))
            {
                continue;
            }
            var name = Path.GetFileName(fullPath) ?? fullPath;

            // Логируем текущий файл
            _logger.LogInformation(
                "[{Time}] Поступил файл: {FileName}",
                DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture),
                name);
        }
        ReplacePreviousSnapshotWith(curSnapShots);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Смотрит в файлы только верхнего уровня
    /// </summary>
    /// <exception cref="OperationCanceledException"/>
    private static Dictionary<string, FileSnapshot> BuildCurrentSnapshot(
        string directory, CancellationToken cancellationToken)
    {
        var list = new Dictionary<string, FileSnapshot>(StringComparer.OrdinalIgnoreCase);
        var paths = Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly);

        foreach (var fullPath in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Если нет файла
            var fileInfo = new FileInfo(fullPath);

            if (!fileInfo.Exists)
            {
                continue;
            }

            list[fullPath] = new FileSnapshot(fileInfo.LastWriteTimeUtc, fileInfo.Length);
        }
        return list;
    }

    private void ReplacePreviousSnapshotWith(IReadOnlyDictionary<string, FileSnapshot> current)
    {
        _previousSnapshot.Clear();
        foreach (var (key, value) in current)
        {
            _previousSnapshot[key] = value;
        }
    }

    private readonly struct FileSnapshot(DateTime lastWriteTimeUtc, long length)
    {
        public DateTime LastWriteTimeUtc { get; } = lastWriteTimeUtc;
        public long Length { get; } = length;
    }
}
