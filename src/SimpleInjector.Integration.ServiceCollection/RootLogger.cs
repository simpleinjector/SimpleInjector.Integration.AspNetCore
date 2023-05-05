// Copyright (c) Simple Injector Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace SimpleInjector.Integration.ServiceCollection
{
    using System;
    using Microsoft.Extensions.Logging;

    internal sealed class RootLogger : ILogger
    {
        private readonly ILogger logger;

        // This constructor needs to be public for Simple Injector to create this type.
        public RootLogger(ILoggerFactory factory) => this.logger = factory.CreateLogger(string.Empty);

#if NETSTANDARD2_0
        public IDisposable BeginScope<TState>(TState state) => this.logger.BeginScope(state);
#endif

#if NET6_0
        public IDisposable BeginScope<TState>(TState state) => this.logger.BeginScope(state);
#endif

#if NET7_0_OR_GREATER
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => this.logger.BeginScope(state);
#endif

        public bool IsEnabled(LogLevel logLevel) => this.logger.IsEnabled(logLevel);

#if NETSTANDARD2_0
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter) =>
            this.logger.Log(logLevel, eventId, state, exception, formatter);
#endif

#if NET6_0_OR_GREATER
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            this.logger.Log(logLevel, eventId, state, exception, formatter);
#endif
    }
}