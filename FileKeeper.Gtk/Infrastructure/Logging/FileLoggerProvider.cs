using FileKeeper.Core.Application;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace FileKeeper.Gtk.Infrastructure.Logging;

/// <summary>
/// Custom logger provider that writes logs to files with daily rotation.
/// </summary>
public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logsDirectory;
    private readonly LogLevel _minimumLevel;
    private readonly ConcurrentDictionary<string, FileLoggerInstance> _loggers;
    private readonly Lock _lockObject = new Lock();
    private StreamWriter? _currentWriter;
    private DateTime _currentLogDate;
    private const long MaxFileSizeBytes = 104857600; // 100 MB

    public FileLoggerProvider(string applicationName = "FileKeeper", LogLevel minimumLevel = LogLevel.Information)
    {
        _minimumLevel = minimumLevel;
        _loggers = new ConcurrentDictionary<string, FileLoggerInstance>();
        
        var paths = new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FileKeeper",
            "logs"
        };

        if (ApplicationInfo.IsDebug)
            paths.Insert(2, "debug");

        _logsDirectory = Path.Combine(paths.ToArray());

        if (!Directory.Exists(_logsDirectory))
        {
            Directory.CreateDirectory(_logsDirectory);
        }

        _currentLogDate = DateTime.Now.Date;
        InitializeWriter();
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new FileLoggerInstance(this, name, _minimumLevel));
    }

    /// <summary>
    /// Internal method called by FileLoggerInstance to write logs.
    /// </summary>
    internal void WriteLog(string categoryName, LogLevel logLevel, EventId eventId, string message, Exception? exception)
    {
        lock (_lockObject)
        {
            try
            {
                CheckAndRotateIfNeeded();

                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var levelString = logLevel.ToString().PadRight(5);
                var logMessage = $"[{timestamp}] [{levelString}] [{categoryName}] {message}";

                _currentWriter?.WriteLine(logMessage);

                if (exception != null)
                {
                    _currentWriter?.WriteLine($"Exception: {exception.GetType().Name} - {exception.Message}");
                    _currentWriter?.WriteLine(exception.StackTrace);
                }

                _currentWriter?.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to log file: {ex.Message}");
            }
        }
    }

    private void CheckAndRotateIfNeeded()
    {
        var today = DateTime.Now.Date;
        if (today != _currentLogDate)
        {
            _currentWriter?.Dispose();
            _currentLogDate = today;
            InitializeWriter();
        }
    }

    private void InitializeWriter()
    {
        try
        {
            var logFileName = $"FileKeeper-{_currentLogDate:yyyy-MM-dd}.log";
            var logFilePath = Path.Combine(_logsDirectory, logFileName);

            // Check if file exists and is large, create a backup if needed
            if (File.Exists(logFilePath))
            {
                var fileInfo = new FileInfo(logFilePath);
                if (fileInfo.Length > MaxFileSizeBytes)
                {
                    var backupFileName = $"FileKeeper-{_currentLogDate:yyyy-MM-dd}-{DateTime.Now:HHmmss}.log";
                    var backupFilePath = Path.Combine(_logsDirectory, backupFileName);
                    File.Move(logFilePath, backupFilePath, overwrite: false);
                }
            }

            _currentWriter = new StreamWriter(logFilePath, append: true)
            {
                AutoFlush = true
            };

            _currentWriter.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INFO] ========== Application Logging Started ==========");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing log writer: {ex.Message}");
        }
    }

    /// <summary>
    /// Cleans up old log files (keeps only the last 30 days).
    /// </summary>
    public void CleanupOldLogs(int daysToKeep = 30)
    {
        try
        {
            var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
            var logFiles = Directory.GetFiles(_logsDirectory, "*.log");

            foreach (var file in logFiles)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTime < cutoffDate)
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error cleaning up old logs: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the logs directory path.
    /// </summary>
    public string GetLogsDirectory() => _logsDirectory;

    public void Dispose()
    {
        lock (_lockObject)
        {
            try
            {
                _currentWriter?.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INFO] ========== Application Logging Closed ==========");
                _currentWriter?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing logger: {ex.Message}");
            }
        }
    }
}

/// <summary>
/// Individual logger instance that delegates to the provider.
/// </summary>
public class FileLoggerInstance : ILogger
{
    private readonly FileLoggerProvider _provider;
    private readonly string _categoryName;
    private readonly LogLevel _minimumLevel;

    public FileLoggerInstance(FileLoggerProvider provider, string categoryName, LogLevel minimumLevel)
    {
        _provider = provider;
        _categoryName = categoryName;
        _minimumLevel = minimumLevel;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= _minimumLevel;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        _provider.WriteLog(_categoryName, logLevel, eventId, message, exception);
    }
}

/// <summary>
/// Extension method to add file logging to the host builder.
/// </summary>
public static class FileLoggerExtensions
{
    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string applicationName, LogLevel minimumLevel)
    {
        builder.AddProvider(new FileLoggerProvider(applicationName, minimumLevel));
        return builder;
    }
}

