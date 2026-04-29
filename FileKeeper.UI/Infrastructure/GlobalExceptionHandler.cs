using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace FileKeeper.UI.Infrastructure;

public sealed class GlobalExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public void Register()
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        Dispatcher.UIThread.UnhandledException += OnUIThreadUnhandledException;
    }

    public void Unregister()
    {
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        Dispatcher.UIThread.UnhandledException -= OnUIThreadUnhandledException;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;

        if (e.IsTerminating)
            _logger.LogCritical(exception, "Fatal unhandled exception. Application is terminating.");
        else
            _logger.LogError(exception, "Unhandled exception on AppDomain.");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Unobserved task exception.");
        e.SetObserved();
    }

    private void OnUIThreadUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Unhandled exception on UI thread.");
        e.Handled = true;
    }
}

