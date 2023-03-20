using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System;

namespace TailoredApps.Shared.EntityFramework.Logging
{

    public class EFLoggerToConsole : ILoggerProvider
    {
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
