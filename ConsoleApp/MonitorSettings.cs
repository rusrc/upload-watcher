namespace FileMonitor;

/// <summary>
/// Настройки опроса каталога, секция "MonitorSettings" в appsettings.json
/// </summary>
public sealed class MonitorSettings
{
    public const string SectionName = "MonitorSettings";

    /// <summary>
    /// Путь к отслеживаемой папке (абсолютный или относительно рабочей директории)
    /// </summary>
    public string DirectoryPath { get; set; } = string.Empty;

    /// <summary>
    /// Интервал между снимками, секунд
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Маска файлов (см. <see cref="Directory.EnumerateFiles(string, string)"/>)
    /// </summary>
    public string FileFilter { get; set; } = "*.*";
}
