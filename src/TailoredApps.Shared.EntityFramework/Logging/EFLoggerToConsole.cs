using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System;

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

        private class EFConsoleLogger : ILogger
        {
            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                Console.WriteLine(formatter(state, exception));
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return null;
            }
        }

        private class NullLogger : ILogger
        {
            public bool IsEnabled(LogLevel logLevel)
            {
                return false;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                Console.WriteLine(formatter(state, exception));

            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return null;
            }
        }
    }
}
