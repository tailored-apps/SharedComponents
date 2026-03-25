using System;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace TailoredApps.Shared.EntityFramework.Logging
{
    /// <summary>
    /// An <see cref="ILoggerProvider"/> that routes Entity Framework Core relational command
    /// log entries to the console. All other categories are silenced via a no-op logger.
    /// </summary>
    public class EFLoggerToConsole : ILoggerProvider
    {
        /// <summary>
        /// Creates an <see cref="ILogger"/> for the given category.
        /// Returns a console logger for EF Core relational commands and a no-op logger for everything else.
        /// </summary>
        /// <param name="categoryName">The logger category name.</param>
        /// <returns>An <see cref="ILogger"/> instance appropriate for the category.</returns>
        public ILogger CreateLogger(string categoryName)
        {
            // NOTE: This sample uses EF Core 1.1. If using EF Core 1.0, then use 
            //       Microsoft.EntityFrameworkCore.Storage.Internal.RelationalCommandBuilderFactory
            //       rather than IRelationalCommandBuilderFactory

            if (categoryName == typeof(RelationalCommandBuilderFactory).FullName)
            {
                return new EFConsoleLogger();
            }

            return new NullLogger();
        }

        /// <summary>
        /// Releases all resources used by this provider.
        /// </summary>
        public void Dispose()
        { }

        /// <summary>
        /// An <see cref="ILogger"/> implementation that writes all log entries to the console.
        /// Used for EF Core relational command logging.
        /// </summary>
        private class EFConsoleLogger : ILogger
        {
            /// <summary>
            /// Always returns <c>true</c>; all log levels are enabled.
            /// </summary>
            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            /// <summary>
            /// Formats the log entry using <paramref name="formatter"/> and writes it to the console.
            /// </summary>
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                Console.WriteLine(formatter(state, exception));
            }

            /// <summary>
            /// Begins a logical operation scope. Returns <c>null</c> (no-op).
            /// </summary>
            public IDisposable BeginScope<TState>(TState state)
            {
                return null;
            }
        }

        /// <summary>
        /// A no-op <see cref="ILogger"/> implementation that discards all log entries.
        /// Used for all categories other than EF Core relational commands.
        /// </summary>
        private class NullLogger : ILogger
        {
            /// <summary>
            /// Always returns <c>false</c>; all log levels are disabled.
            /// </summary>
            public bool IsEnabled(LogLevel logLevel)
            {
                return false;
            }

            /// <summary>
            /// No-op log method. Writes the formatted message to the console even though
            /// <see cref="IsEnabled"/> returns <c>false</c> — kept for legacy compatibility.
            /// </summary>
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                Console.WriteLine(formatter(state, exception));

            }

            /// <summary>
            /// Begins a logical operation scope. Returns <c>null</c> (no-op).
            /// </summary>
            public IDisposable BeginScope<TState>(TState state)
            {
                return null;
            }
        }
    }
}
