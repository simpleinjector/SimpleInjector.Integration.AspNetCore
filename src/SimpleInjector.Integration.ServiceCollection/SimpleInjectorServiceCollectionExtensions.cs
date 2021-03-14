﻿// Copyright (c) Simple Injector Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace SimpleInjector
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Localization;
    using Microsoft.Extensions.Logging;
    using SimpleInjector.Diagnostics;
    using SimpleInjector.Integration.ServiceCollection;
    using SimpleInjector.Lifestyles;

    /// <summary>
    /// Extensions to configure Simple Injector on top of <see cref="IServiceCollection"/>.
    /// </summary>
    public static class SimpleInjectorServiceCollectionExtensions
    {
        private static readonly object AddOptionsKey = new();
        private static readonly object AddLoggingKey = new();
        private static readonly object AddLocalizationKey = new();

        /// <summary>
        /// Sets up the basic configuration that allows Simple Injector to be used in frameworks that require
        /// the use of <see cref="IServiceCollection"/> for registration of framework components.
        /// In case of the absense of a
        /// <see cref="ContainerOptions.DefaultScopedLifestyle">DefaultScopedLifestyle</see>, this method
        /// will configure <see cref="AsyncScopedLifestyle"/> as the default scoped lifestyle.
        /// In case a <paramref name="setupAction"/> is supplied, that delegate will be called that allow
        /// further configuring the container.
        /// </summary>
        /// <param name="services">The framework's <see cref="IServiceCollection"/> instance.</param>
        /// <param name="container">The application's <see cref="Container"/> instance.</param>
        /// <param name="setupAction">An optional setup action.</param>
        /// <returns>The supplied <paramref name="services"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or
        /// <paramref name="container"/> are null references.</exception>
        public static IServiceCollection AddSimpleInjector(
            this IServiceCollection services,
            Container container,
            Action<SimpleInjectorAddOptions>? setupAction = null)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (container is null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            var options = new SimpleInjectorAddOptions(
                services,
                container,
                new DefaultServiceProviderAccessor(container));

            // Add the container; this simplifies registration of types that depend on the container, but need
            // to be constructed by MS.DI (such as generic activators). Those registrations don't need to add
            // the container themselves.
            services.TryAddSingleton(container);

            // This stores the options, which includes the IServiceCollection. IServiceCollection is required
            // when calling UseSimpleInjector to enable auto cross wiring.
            AddSimpleInjectorOptions(container, options);

            // Set lifestyle before calling setupAction. Code in the delegate might depend on that.
            TrySetDefaultScopedLifestyle(container);

            // #17: We must register the service scope before calling the setupAction. This allows setup code to
            // replace it.
            RegisterServiceScope(options);

            setupAction?.Invoke(options);

            // #15: Unfortunately, the addition of the IHostedService breaks Azure Functions. Azure Functions do no
            // support IHostedService. See: https://stackoverflow.com/questions/59947132/. This is why we had to make
            // this conditional. 
            if (options.EnableHostedServiceResolution)
            {
                HookAspNetCoreHostHostedServiceServiceProviderInitialization(options);
            }

            if (options.AutoCrossWireFrameworkComponents)
            {
                AddAutoCrossWiring(options);
            }

            if (options.DisposeContainerWithServiceProvider)
            {
                AddContainerDisposalOnShutdown(services, options);
            }

            return services;
        }

        /// <summary>
        /// Finalizes the configuration of Simple Injector on top of <see cref="IServiceCollection"/>. Will
        /// ensure framework components can be injected into Simple Injector-resolved components, unless
        /// <see cref="SimpleInjectorAddOptions.AutoCrossWireFrameworkComponents"/> is set to <c>false</c>.
        /// </summary>
        /// <param name="provider">The application's <see cref="IServiceProvider"/>.</param>
        /// <param name="container">The application's <see cref="Container"/> instance.</param>
        /// <returns>The supplied <paramref name="provider"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> or
        /// <paramref name="container"/> are null references.</exception>
        public static IServiceProvider UseSimpleInjector(this IServiceProvider provider, Container container)
        {
            if (provider is null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            if (container is null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            SimpleInjectorAddOptions addOptions = GetOptions(container);

            addOptions.SetServiceProviderIfNull(provider);

            return provider;
        }

        /// <summary>
        /// Finalizes the configuration of Simple Injector on top of <see cref="IServiceCollection"/>. Will
        /// ensure framework components can be injected into Simple Injector-resolved components, unless
        /// <see cref="SimpleInjectorUseOptions.AutoCrossWireFrameworkComponents"/> is set to <c>false</c>
        /// using the <paramref name="setupAction"/>.
        /// </summary>
        /// <param name="provider">The application's <see cref="IServiceProvider"/>.</param>
        /// <param name="container">The application's <see cref="Container"/> instance.</param>
        /// <param name="setupAction">An optional setup action.</param>
        /// <returns>The supplied <paramref name="provider"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> or
        /// <paramref name="container"/> are null references.</exception>
        //// I wanted to add this obsolete message in 4.8, but it was confusing considering the obsolete
        //// messages for everything on top of SimpleInjectorUseOptions. When those obsolete messages are
        //// resolved by the user, there is no harm in calling this method any longer. So it will get
        //// obsoleted in a later release.
        ////[Obsolete(
        ////    "You are supplying a setup action, but due breaking changes in ASP.NET Core 3, the Simple " +
        ////    "Injector container can get locked at an earlier stage, making it impossible to further setup " +
        ////    "the container at this stage. Please call the UseSimpleInjector(IServiceProvider, Container) " +
        ////    "overload instead. Take a look at the compiler warnings on the individual methods you are " +
        ////    "calling inside your setupAction delegate to understand how to migrate them. " +
        ////    " For more information, see: https://simpleinjector.org/aspnetcore. " +
        ////    "Will be treated as an error from version 4.10. Will be removed in version 5.0.",
        ////    error: false)]
        public static IServiceProvider UseSimpleInjector(
            this IServiceProvider provider,
            Container container,
            Action<SimpleInjectorUseOptions>? setupAction)
        {
            if (provider is null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            if (container is null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            SimpleInjectorAddOptions addOptions = GetOptions(container);

            addOptions.SetServiceProviderIfNull(provider);

            var useOptions = new SimpleInjectorUseOptions(addOptions, provider);

            setupAction?.Invoke(useOptions);

            return provider;
        }

        /// <summary>
        /// Cross wires an ASP.NET Core or third-party service to the container, to allow the service to be
        /// injected into components that are built by Simple Injector.
        /// </summary>
        /// <typeparam name="TService">The type of service object to cross-wire.</typeparam>
        /// <param name="options">The options.</param>
        /// <returns>The supplied <paramref name="options"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parameter is a null reference.
        /// </exception>
        public static SimpleInjectorAddOptions CrossWire<TService>(this SimpleInjectorAddOptions options)
            where TService : class
        {
            return CrossWire(options, typeof(TService));
        }

        /// <summary>
        /// Cross wires an ASP.NET Core or third-party service to the container, to allow the service to be
        /// injected into components that are built by Simple Injector.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="serviceType">The type of service object to ross-wire.</param>
        /// <returns>The supplied <paramref name="options"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when one of the parameters is a null reference.
        /// </exception>
        public static SimpleInjectorAddOptions CrossWire(
            this SimpleInjectorAddOptions options, Type serviceType)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (serviceType is null)
            {
                throw new ArgumentNullException(nameof(serviceType));
            }

            // At this point there is no IServiceProvider (ApplicationServices) yet, which is why we need to
            // postpone the registration of the cross-wired service. When the container gets locked, we will
            // (hopefully) have the IServiceProvider available.
            options.Container.Options.ContainerLocking += (s, e) =>
            {
                Registration registration = CreateCrossWireRegistration(
                    options,
                    options.ApplicationServices,
                    serviceType,
                    DetermineLifestyle(serviceType, options.Services));

                options.Container.AddRegistration(serviceType, registration);
            };

            return options;
        }

        /// <summary>
        /// Allows components that are built by Simple Injector to depend on the (non-generic)
        /// <see cref="ILogger">Microsoft.Extensions.Logging.ILogger</see> abstraction. Components are
        /// injected with an contextual implementation. Using this method, application components can simply
        /// depend on <b>ILogger</b> instead of its generic counter part, <b>ILogger&lt;T&gt;</b>, which
        /// simplifies development.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>The supplied <paramref name="options"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="options"/> is a null reference.</exception>
        /// <exception cref="InvalidOperationException">Thrown when no <see cref="ILoggerFactory"/> entry
        /// can be found in the framework's list of services defined by <see cref="IServiceCollection"/>.
        /// </exception>
        public static SimpleInjectorAddOptions AddLogging(this SimpleInjectorAddOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            EnsureMethodOnlyCalledOnce(options, nameof(AddLogging), AddLoggingKey);

            // Both RootLogger and Logger<T> depend on ILoggerFactory
            VerifyLoggerFactoryAvailable(options.Services);

            // Cross-wire ILoggerFactory explicitly, because auto cross-wiring might be disabled by the user.
            options.Container.RegisterSingleton(() => options.GetRequiredFrameworkService<ILoggerFactory>());

            Type genericLoggerType = GetGenericLoggerType();

            options.Container.RegisterConditional(
                typeof(ILogger),
                c => c.Consumer is null
                    ? typeof(RootLogger)
                    : genericLoggerType.MakeGenericType(c.Consumer.ImplementationType),
                Lifestyle.Singleton,
                _ => true);

            return options;
        }

        /// <summary>
        /// Allows components that are built by Simple Injector to depend on the (non-generic)
        /// <see cref="IStringLocalizer">Microsoft.Extensions.Localization.IStringLocalizer</see> abstraction.
        /// Components are injected with an contextual implementation. Using this method, application 
        /// components can simply depend on <b>IStringLocalizer</b> instead of its generic counter part,
        /// <b>IStringLocalizer&lt;T&gt;</b>, which simplifies development.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>The supplied <paramref name="options"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="options"/> is a null reference.</exception>
        /// <exception cref="InvalidOperationException">Thrown when no <see cref="IStringLocalizerFactory"/>
        /// entry can be found in the framework's list of services defined by <see cref="IServiceCollection"/>.
        /// </exception>
        /// <exception cref="ActivationException">Thrown when an <see cref="IStringLocalizer"/> is directly 
        /// resolved from the container. Instead use <see cref="IStringLocalizer"/> within a constructor 
        /// dependency.</exception>
        public static SimpleInjectorAddOptions AddLocalization(this SimpleInjectorAddOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            EnsureMethodOnlyCalledOnce(options, nameof(AddLocalization), AddLocalizationKey);

            VerifyStringLocalizerFactoryAvailable(options.Services);

            // Cross-wire IStringLocalizerFactory explicitly, because auto cross-wiring might be disabled.
            options.Container.RegisterSingleton(
                () => options.GetRequiredFrameworkService<IStringLocalizerFactory>());

            Type genericLocalizerType = GetGenericLocalizerType();

            options.Container.RegisterConditional(
                typeof(IStringLocalizer),
                c => c.Consumer is null
                    ? throw new ActivationException(
                        "IStringLocalizer is being resolved directly from the container, but this is not " +
                        "supported as string localizers need to be related to a consuming type. Instead, " +
                        "make IStringLocalizer a constructor dependency of the type it is used in.")
                    : genericLocalizerType.MakeGenericType(c.Consumer.ImplementationType),
                Lifestyle.Singleton,
                _ => true);

            return options;
        }

        private static void VerifyLoggerFactoryAvailable(IServiceCollection services)
        {
            var descriptor = FindServiceDescriptor(services, typeof(ILoggerFactory));

            if (descriptor is null)
            {
                throw new InvalidOperationException(
                    $"A registration for the {typeof(ILoggerFactory).FullName} is missing from the ASP.NET " +
                    "Core configuration system. This is most likely caused by a missing call to services" +
                    ".AddLogging() as part of the ConfigureServices(IServiceCollection) method of the " +
                    "Startup class. The .AddLogging() extension method is part of the LoggingService" +
                    "CollectionExtensions class of the Microsoft.Extensions.Logging assembly.");
            }
            else if (descriptor.Lifetime != ServiceLifetime.Singleton)
            {
                // By default, the LoggerFactory implementation is registered using auto-wiring (so not with
                // ImplementationInstance) which means we have to support that as well.
                throw new InvalidOperationException(
                    $"Although a registration for {typeof(ILoggerFactory).FullName} exists in the ASP.NET " +
                    $"Core configuration system, the registration is not added as Singleton. Instead the " +
                    $"registration exists as {descriptor.Lifetime}. This might be caused by a third-party " +
                    "library that replaced .NET Core's default ILoggerFactory. Make sure that you use one " +
                    "of the AddSingleton overloads to register ILoggerFactory. Simple Injector does not " +
                    "support ILoggerFactory to be registered with anything other than Singleton.");
            }
        }

        private static void VerifyStringLocalizerFactoryAvailable(IServiceCollection services)
        {
            var descriptor = FindServiceDescriptor(services, typeof(IStringLocalizerFactory));

            if (descriptor is null)
            {
                throw new InvalidOperationException(
                    $"A registration for the {typeof(IStringLocalizerFactory).FullName} is missing from " +
                    "the ASP.NET Core configuration system. This is most likely caused by a missing call " +
                    "to services.AddLocalization() as part of the ConfigureServices(IServiceCollection) " +
                    "method of the Startup class. The .AddLocalization() extension method is part of the " +
                    "LocalizationServiceCollectionExtensions class of the Microsoft.Extensions.Localization" +
                    " assembly.");
            }
            else if (descriptor.Lifetime != ServiceLifetime.Singleton)
            {
                // By default, the IStringLocalizerFactory implementation is registered using auto-wiring 
                // (so not with ImplementationInstance) which means we have to support that as well.
                throw new InvalidOperationException(
                    $"Although a registration for {typeof(IStringLocalizerFactory).FullName} exists in the " +
                    "ASP.NET Core configuration system, the registration is not added as Singleton. " +
                    $"Instead the registration exists as {descriptor.Lifetime}. This might be caused by a " +
                    "third-party library that replaced .NET Core's default IStringLocalizerFactory. Make " +
                    "sure that you use one of the AddSingleton overloads to register " +
                    "IStringLocalizerFactory. Simple Injector does not support IStringLocalizerFactory to " +
                    "be registered with anything other than Singleton.");
            }
        }

        private static void RegisterServiceScope(SimpleInjectorAddOptions options)
        {
            var container = options.Container;

            container.Register<ServiceScopeProvider>(Lifestyle.Scoped);

            var registration = Lifestyle.Scoped.CreateRegistration(
                new ServiceScopeRetriever(options).GetServiceScope,
                container);

            // Suppress disposal, because an externally supplied IServiceScope (through
            // ServiceScopeProvider.ServiceScope) should not be disposed. An internally created scope is registered
            // for disposal.
            registration.SuppressDisposal = true;

            container.AddRegistration<IServiceScope>(registration);
        }

        private static SimpleInjectorAddOptions GetOptions(Container container)
        {
            var options =
                (SimpleInjectorAddOptions?)container.ContainerScope.GetItem(AddOptionsKey);

            if (options is null)
            {
                throw new InvalidOperationException(
                    "Please ensure the " +
                    $"{nameof(SimpleInjectorServiceCollectionExtensions.AddSimpleInjector)} extension " +
                    "method is called on the IServiceCollection instance before using this method.");
            }

            return options;
        }

        private static void AddAutoCrossWiring(SimpleInjectorAddOptions options)
        {
            // By using ContainerLocking, we ensure that this ResolveUnregisteredType registration is made 
            // after all possible ResolveUnregisteredType registrations the users did themselves.
            options.Container.Options.ContainerLocking += (_, __) =>
            {
                // If there's no IServiceProvider, the property will throw, which is something we want to do
                // at this point, not later on, when an unregistered type is resolved.
                IServiceProvider provider = options.ApplicationServices;

                options.Container.ResolveUnregisteredType += (_, e) =>
                {
                    if (!e.Handled)
                    {
                        Type serviceType = e.UnregisteredServiceType;

                        ServiceDescriptor? descriptor = FindServiceDescriptor(options.Services, serviceType);

                        if (descriptor != null)
                        {
                            Registration registration =
                                CreateCrossWireRegistration(
                                    options,
                                    provider,
                                    serviceType,
                                    ToLifestyle(descriptor.Lifetime));

                            e.Register(registration);
                        }
                    }
                };
            };
        }

        // Note about implementation: We could have used the ASP.NET Core IApplicationLifetime or
        // IHostApplicationLifetime for this as well, but there are a few downsides to this:
        // * IApplicationLifetime is obsolete
        // * IHostApplicationLifetime is only available for ASP.NET Core >= 3.0
        // * This requires ASP.NET Core, so this is less generic and couldn't be implemented in this library.
        // * It required much more research and testing to get right.
        private static void AddContainerDisposalOnShutdown(
            IServiceCollection services, SimpleInjectorAddOptions options)
        {
            // This wrapper implements disposable and allows the container to be disposed of when
            // IServiceProvider is disposed of. Just like Simple Injector, however, MS.DI will only
            // dispose of instances that are registered using this overload (not using AddSingleton<T>(T)).
            services.AddSingleton<ContainerDisposeWrapper>();

            options.Container.Options.ContainerLocking += (_, __) =>
            {
                // If there's no IServiceProvider, the property will throw, which is something we want to do
                // at this point, not later on, when an unregistered type is resolved.
                IServiceProvider provider = options.ApplicationServices;

                // In order for the wrapper to get disposed of, it needs to be resolved once.
                var wrapper = provider.GetRequiredService<ContainerDisposeWrapper>();
                wrapper.FrameworkProviderType = provider.GetType();
            };
        }

        private static Lifestyle DetermineLifestyle(Type serviceType, IServiceCollection services)
        {
            var descriptor = FindServiceDescriptor(services, serviceType);

            // In case the service type is an IEnumerable, a registration can't be found, but collections are
            // in Core always registered as Transient, so it's safe to fall back to the transient lifestyle.
            return ToLifestyle(descriptor?.Lifetime ?? ServiceLifetime.Transient);
        }

        private static Registration CreateCrossWireRegistration(
            SimpleInjectorAddOptions options,
            IServiceProvider provider,
            Type serviceType,
            Lifestyle lifestyle)
        {
            var registration = lifestyle.CreateRegistration(
                serviceType,
                lifestyle == Lifestyle.Singleton
                    ? BuildSingletonInstanceCreator(serviceType, provider)
                    : BuildScopedInstanceCreator(serviceType, options.ServiceProviderAccessor),
                options.Container);

            // This registration is managed and disposed by IServiceProvider and should, therefore, not be
            // disposed (again) by Simple Injector.
            registration.SuppressDisposal = true;

            if (lifestyle == Lifestyle.Transient && typeof(IDisposable).IsAssignableFrom(serviceType))
            {
                registration.SuppressDiagnosticWarning(
                    DiagnosticType.DisposableTransientComponent,
                    justification: "This is a cross-wired service. It will be disposed by IServiceScope.");
            }

            return registration;
        }

        private static Func<object> BuildSingletonInstanceCreator(Type serviceType, IServiceProvider rootProvider)
        {
            return () => GetRequiredService(rootProvider, serviceType);
        }

        private static Func<object> BuildScopedInstanceCreator(
            Type serviceType, IServiceProviderAccessor accessor)
        {
            // The ServiceProviderAccessor allows access to a request-specific IServiceProvider. This
            // allows Scoped and Transient instances to be resolved from a scope instead of the root
            // container—resolving them from the root container will cause memory leaks. Specific
            // framework integration (such as Simple Injector's ASP.NET Core integration) can override
            // this accessor with one that allows retrieving the IServiceProvider from a web request.
            return () =>
            {
                IServiceProvider current;

                try
                {
                    current = accessor.Current;
                }
                catch (ActivationException ex)
                {
                    // The DefaultServiceProviderAccessor will throw an ActivationException in case the
                    // IServiceProvider (or in fact the underlying IServiceScope) is requested outside the
                    // context of an active scope. Here we enrich that exception message with information
                    // of the actual requested cross-wired service.
                    throw new ActivationException(
                        $"Error resolving the cross-wired {serviceType.ToFriendlyName()}. {ex.Message}", ex);
                }

                return GetRequiredService(current, serviceType);
            };
        }

        private static object GetRequiredService(IServiceProvider provider, Type serviceType)
        {
            try
            {
                return provider.GetRequiredService(serviceType);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to resolve {serviceType.ToFriendlyName()}. {serviceType.ToFriendlyName()} is a " +
                    $"cross-wired service, meaning that Simple Injector forwarded the request the framework's " +
                    $"IServiceProvider in order to get an instance. The used {provider.GetType().FullName}, " +
                    $"however, failed with the following message: \"{ex.Message}\". This error might indicate a " +
                    $"misconfiguration of services in the framework's IServiceCollection.",
                    ex);
            }
        }

        private static ServiceDescriptor? FindServiceDescriptor(IServiceCollection services, Type serviceType)
        {
            // In case there are multiple descriptors for a given type, .NET Core will use the last
            // descriptor when one instance is resolved. We will have to get this last one as well.
            ServiceDescriptor? descriptor = services.LastOrDefault(d => d.ServiceType == serviceType);

            if (descriptor is null && serviceType.GetTypeInfo().IsGenericType)
            {
                // In case the registration is made as open-generic type, the previous query will return
                // null, and we need to go find the last open generic registration for the service type.
                var serviceTypeDefinition = serviceType.GetTypeInfo().GetGenericTypeDefinition();
                descriptor = services.LastOrDefault(d => d.ServiceType == serviceTypeDefinition);
            }

            return descriptor;
        }

        private static Lifestyle ToLifestyle(ServiceLifetime lifetime)
        {
            switch (lifetime)
            {
                case ServiceLifetime.Singleton: return Lifestyle.Singleton;
                case ServiceLifetime.Scoped: return Lifestyle.Scoped;
                default: return Lifestyle.Transient;
            }
        }

        private static void AddSimpleInjectorOptions(Container container, SimpleInjectorAddOptions builder)
        {
            var current = container.ContainerScope.GetItem(AddOptionsKey);

            if (current is null)
            {
                container.ContainerScope.SetItem(AddOptionsKey, builder);
            }
            else
            {
                throw new InvalidOperationException(
                    $"The {nameof(AddSimpleInjector)} extension method can only be called once.");
            }
        }

        private static void TrySetDefaultScopedLifestyle(Container container)
        {
            if (container.Options.DefaultScopedLifestyle is null)
            {
                container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            }
        }

        private static void HookAspNetCoreHostHostedServiceServiceProviderInitialization(
            SimpleInjectorAddOptions options)
        {
            // ASP.NET Core 3's new Host class resolves hosted services much earlier in the pipeline. This registration
            // ensures that the IServiceProvider is assigned before such resolve takes place, to ensure that that
            // hosted service can be injected with cross-wired dependencies.
            // We must ensure that this hosted service gets resolved by ASP.NET before any other hosted service is
            // resolve; that's why we do the Insert(0).
            options.Services.Insert(0, ServiceDescriptor.Singleton<IHostedService>(provider =>
            {
                options.SetServiceProviderIfNull(provider);

                // We can't return null here, so we return an empty implementation.
                return new NullSimpleInjectorHostedService();
            }));
        }

        private static void EnsureMethodOnlyCalledOnce(
            SimpleInjectorAddOptions options, string methodName, object key)
        {
            if (options.Container.ContainerScope.GetItem(key) != null)
            {
                throw new InvalidOperationException(
                    $"The {methodName} extension method can only be called once on a Container instance.");
            }
            else
            {
                options.Container.ContainerScope.SetItem(key, new object());
            }
        }

        // We prefer using the Microsoft.Logger<T> type directly, but this is only something that can be done
        // when that type has exactly one public constructor and that constructor only has a single parameter
        // of type ILoggerFactory. These requirements are met in .NET Core 2 and 3, but MS might choose to add
        // an extra constructor any time, in which case this integration would fail. To make the library
        // forward compatible, we check whether the type still has one single constructor. If not, we fall
        // back to Simple Injector's internally defined Logger<T> derivative. That type is guaranteed to have
        // a single constructor.
        private static Type GetGenericLoggerType() =>
            typeof(Microsoft.Extensions.Logging.Logger<>).GetConstructors().Length == 1
                ? typeof(Microsoft.Extensions.Logging.Logger<>)
                : typeof(Integration.ServiceCollection.Logger<>);

        // We do exactly the same thing for StringLocalizer<T>.
        private static Type GetGenericLocalizerType() =>
            typeof(Microsoft.Extensions.Localization.StringLocalizer<>).GetConstructors().Length == 1
                ? typeof(Microsoft.Extensions.Localization.StringLocalizer<>)
                : typeof(Integration.ServiceCollection.StringLocalizer<>);

        private sealed class NullSimpleInjectorHostedService : IHostedService
        {
            public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private sealed class ContainerDisposeWrapper : IDisposable
        {
            private readonly Container container;

            public ContainerDisposeWrapper(Container container) => this.container = container;

            public Type FrameworkProviderType { get; internal set; } = typeof(IServiceProvider);

            public void Dispose()
            {
                try
                {
                    // NOTE: We can't call container.DisposeContainerAsync().GetAwaiter().GetResult(), because we don't
                    // know the context in which this library is running. If it's ASP.NET Core, it would be okay to
                    // call GetResult(), but in case we're running in a UI framework, GetResult() might result in a
                    // deadlock.
                    this.container.Dispose();
                }
                catch (InvalidOperationException ex)
                    when (ex.Message.Contains("IAsyncDisposable") && ex.Message.Contains("IDisposable"))
                {
                    // When we get here, Dispose complained that there was a Singleton registration that implements
                    // IAsyncDisposable, while this is a synchronous Dispose call.
                    throw new InvalidOperationException(
                        $"Simple Injector was configured to be disposed of together with the application's " +
                        $"{this.FrameworkProviderType.ToFriendlyName()}. This configuration is not suited for " +
                        $"asynchronous disposal. The Simple Injector Container, however, contains a Singleton that " +
                        $"implements IAsyncDisposable, which cannot be disposed of synchronously. To fix this " +
                        $"problem, configure Simple Injector by setting {nameof(SimpleInjectorAddOptions)}." +
                        $"{nameof(SimpleInjectorAddOptions.DisposeContainerWithServiceProvider)} to false and " +
                        $"manually call 'await Container.{nameof(Container.DisposeContainerAsync)}()' at " +
                        $"application shutdown. e.g.:\n" +
                        $"services.{nameof(SimpleInjectorServiceCollectionExtensions.AddSimpleInjector)}(container, " +
                        $"options => {{ " +
                           $"options.{nameof(SimpleInjectorAddOptions.DisposeContainerWithServiceProvider)} = false;" +
                        $" }}). {ex.Message}",
                        ex);
                }
            }
        }

        private sealed class ServiceScopeRetriever
        {
            private readonly Container container;
            private readonly IServiceScopeFactory factory;

            // Closure for a cached ServiceScopeProvider producer. IP.GetIntance is faster than Container.GetInstance.
            private InstanceProducer? providerProducer = null;

            public ServiceScopeRetriever(SimpleInjectorAddOptions options)
            {
                this.container = options.Container;
                this.factory = options.ServiceScopeFactory;
            }

            public IServiceScope GetServiceScope() =>
                this.GetExternallyProvidedServiceScope()
                ?? this.CreateAndTrackServiceScope();

            private IServiceScope? GetExternallyProvidedServiceScope()
            {
                this.providerProducer ??= this.container.GetRegistration<ServiceScopeProvider>(throwOnFailure: true)!;
                var provider = (ServiceScopeProvider)providerProducer.GetInstance();
                return provider.ServiceScope;
            }

            private IServiceScope CreateAndTrackServiceScope()
            {
                var serviceScope = this.factory.CreateScope();

                // Ensure the created scope is disposed by Simple Injector
                Lifestyle.Scoped.GetCurrentScope(this.container)!.RegisterForDisposal(serviceScope);

                return serviceScope;
            }
        }
    }
}