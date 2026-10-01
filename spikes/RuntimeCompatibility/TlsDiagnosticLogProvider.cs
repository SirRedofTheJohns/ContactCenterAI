using System.ComponentModel;
using Microsoft.Extensions.Logging;

// TLS startup/handshake diagnostics only. Never invoke a formatter or serialize exception messages.
internal sealed class TlsDiagnosticLogProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new TlsLogger(categoryName);
    public void Dispose() { }

    private sealed class TlsLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => category.StartsWith("Microsoft.AspNetCore.Server.Kestrel.Https", StringComparison.Ordinal);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel) || exception is null) return;
            Console.WriteLine($"TLS_SERVER Event={eventId.Id}");
            for (var current = exception; current is not null; current = current.InnerException)
            {
                Console.WriteLine($"TLS_SERVER Type={current.GetType().Name}; HResult=0x{current.HResult:X8}");
                if (current is Win32Exception native)
                    Console.WriteLine($"TLS_SERVER NativeCode=0x{native.NativeErrorCode:X8}");
            }
        }
    }
}
