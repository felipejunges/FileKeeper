using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace FileKeeper.Core.Logging;

public class AnsiConsoleLoggerOptions
{
    // Overall minimum level if no category-level override matches
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    // Format string supports tokens: {time} {level} {category} {message}
    // Example: "[{time} {level}] {category}: {message}"
    public string Format { get; set; } = "[{time} {level}] {category}: {message}";

    // Escape message/category/time before passing to Markup to avoid Spectre interpreting user content
    public bool EscapeMessage { get; set; } = true;

    // Optional per-category minimum levels. Key is category prefix (e.g. "FileKeeper.Core.Services"),
    // value is the minimum LogLevel for that category. The longest matching prefix is used.
    public IDictionary<string, LogLevel> CategoryMinimums { get; set; } = new Dictionary<string, LogLevel>();
}

public class AnsiConsoleLoggerProvider : ILoggerProvider
{
    private readonly AnsiConsoleLoggerOptions _options;

    public AnsiConsoleLoggerProvider() : this(new AnsiConsoleLoggerOptions()) { }

    public AnsiConsoleLoggerProvider(AnsiConsoleLoggerOptions options)
    {
        _options = options;
    }

    public ILogger CreateLogger(string categoryName)
    {
        var min = GetMinimumLevelForCategory(categoryName);
        return new AnsiConsoleLogger(categoryName, min, _options);
    }

    public void Dispose() { }

    private LogLevel GetMinimumLevelForCategory(string category)
    {
        if (_options.CategoryMinimums.Count == 0)
            return _options.MinimumLevel;

        // Find the longest matching prefix key
        string? bestKey = null;
        foreach (var key in _options.CategoryMinimums.Keys)
        {
            if (category.StartsWith(key, StringComparison.OrdinalIgnoreCase))
            {
                if (bestKey == null || key.Length > bestKey.Length)
                    bestKey = key;
            }
        }

        if (bestKey != null && _options.CategoryMinimums.TryGetValue(bestKey, out var level))
            return level;

        return _options.MinimumLevel;
    }

    private class AnsiConsoleLogger : ILogger
    {
        private readonly string _category;
        private readonly LogLevel _minLevel;
        private readonly AnsiConsoleLoggerOptions _options;

        public AnsiConsoleLogger(string category, LogLevel minLevel, AnsiConsoleLoggerOptions options)
        {
            _category = category;
            _minLevel = minLevel;
            _options = options;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string>? formatter)
        {
            if (!IsEnabled(logLevel)) return;
            if (formatter == null) return;

            var message = formatter(state!, exception);
            var time = DateTime.Now.ToString("HH:mm:ss");

            string formatted;

            if (_options.EscapeMessage)
            {
                var escTime = Markup.Escape(time);
                var escLevel = Markup.Escape(logLevel.ToString());
                var escCategory = Markup.Escape(_category);
                var escMessage = Markup.Escape(message);
                var escException = exception is not null ? Markup.Escape(exception.Message) : null;

                formatted = _options.Format
                    .Replace("{time}", escTime)
                    .Replace("{level}", escLevel)
                    .Replace("{category}", escCategory)
                    .Replace("{message}", escMessage);

                if (escException != null)
                    formatted = formatted + " - " + escException;

                // Escape the whole formatted string to ensure literal format characters (like []) are not treated as markup
                formatted = Markup.Escape(formatted);
            }
            else
            {
                formatted = _options.Format
                    .Replace("{time}", time)
                    .Replace("{level}", logLevel.ToString())
                    .Replace("{category}", _category)
                    .Replace("{message}", message);

                if (exception is not null)
                    formatted = formatted + " - " + exception.Message;

                // If EscapeMessage is false we still need to guard the wrapper, but assume the formatted string is intentional.
            }

            // Render using Text + Style to avoid any markup parsing of the message content.
            var styleColor = logLevel switch
            {
                LogLevel.Critical => Color.Red,
                LogLevel.Error => Color.Red,
                LogLevel.Warning => Color.Yellow,
                LogLevel.Information => Color.Green,
                LogLevel.Debug => Color.Grey,
                LogLevel.Trace => Color.Grey,
                _ => Color.White
            };

            var text = new Text(formatted, new Style(foreground: styleColor));
            AnsiConsole.Write(text);
            AnsiConsole.WriteLine();
        }

        // lightweight NullScope to satisfy BeginScope
        private class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new NullScope();
            public void Dispose() { }
        }
    }
}
