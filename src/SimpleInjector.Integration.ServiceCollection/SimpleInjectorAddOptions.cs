// Copyright (c) Simple Injector Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace SimpleInjector.Integration.ServiceCollection
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using SimpleInjector.Advanced;

    /// <summary>
    /// Provides programmatic configuration for the Simple Injector on top of <see cref="IServiceCollection"/>.
    /// </summary>
    public sealed class SimpleInjectorAddOptions : ApiObject
    {
        private IServiceProviderAccessor serviceProviderAccessor;
        private IServiceScopeFactory? serviceScopeFactory;
        private IServiceProvider? applicationServices;

        internal SimpleInjectorAddOptions(
            IServiceCollection services, Container container, IServiceProviderAccessor accessor)
        {
            this.Services = services;
            this.Container = container;
            this.serviceProviderAccessor = accessor;
        }

        /// <summary>
        /// Gets the <see cref="IServiceCollection"/> that contains the collection of framework components.
        /// </summary>
        /// <value>The <see cref="IServiceCollection"/> instance.</value>
        public IServiceCollection Services { get; }

        /// <summary>
        /// Gets the <see cref="Container"/> instance used by the application.
        /// </summary>
        /// <value>The <see cref="Container"/> instance.</value>
        public Container Container { get; }

        /// <summary>
        /// Gets or sets an <see cref="IServiceProviderAccessor"/> instance that will be used by Simple
        /// Injector to resolve cross-wired framework components.
        /// </summary>
        /// <value>The <see cref="IServiceProviderAccessor"/> instance.</value>
        /// <exception cref="ArgumentNullException">Thrown when a null value is provided.</exception>
        public IServiceProviderAccessor ServiceProviderAccessor
        {
            get => this.serviceProviderAccessor;
            set => this.serviceProviderAccessor = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not Simple Injector should try to load framework
        /// components from the framework's configuration system or not. The default is <c>true</c>.
        /// </summary>
        /// <value>A boolean value.</value>
        public bool AutoCrossWireFrameworkComponents { get; set; } = true;

        /// <summary>
        /// Gets or sets the value indicating whether the <see cref="Container"/> instance used by the 
        /// application should be disposed when the framework's <see cref="IServiceProvider"/> is disposed.
        /// The <see cref="IServiceProvider"/> is typically disposed of on application shutdown, which is also
        /// the time to dispose the Container. The default value is <b>true</b>. Please disable this option in case
        /// asynchronous disposal is required. If there are Singleton registrations that implement IAsyncDisposable,
        /// this property should be set to false and the Container should be disposed of manually by calling
        /// <see cref="Container.DisposeContainerAsync"/>.
        /// </summary>
        /// <value>A boolean value.</value>
        public bool DisposeContainerWithServiceProvider { get; set; } = true;

        /// <summary>
        /// Gets the framework's <see cref="IServiceScopeFactory"/> instance from the <see cref="ApplicationServices"/>.
        /// It's value will be set when
        /// <see cref="SimpleInjectorServiceCollectionExtensions.UseSimpleInjector(IServiceProvider, Container)">UseSimpleInjector</see>
        /// is called, or when ASP.NET Core resolves its hosted services (whatever comes first).
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the property has been called while ita value hasn't
        /// been set yet.</exception>
        public IServiceScopeFactory ServiceScopeFactory =>
            this.serviceScopeFactory ??= this.ApplicationServices.GetRequiredService<IServiceScopeFactory>();

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> instance that will be used by Simple Injector to resolve
        /// cross-wired framework components. It's value will be set when
        /// <see cref="SimpleInjectorServiceCollectionExtensions.UseSimpleInjector(IServiceProvider, Container)">UseSimpleInjector</see>
        /// is called, or when ASP.NET Core resolves its hosted services (whatever comes first).
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the property has been called while ita value hasn't
        /// been set yet.</exception>
        /// <value>The <see cref="IServiceProvider"/> instance.</value>
        public IServiceProvider ApplicationServices =>
            this.applicationServices ?? throw this.GetServiceProviderNotSetException();

        internal T GetRequiredFrameworkService<T>() => this.ApplicationServices.GetRequiredService<T>();

        internal void SetServiceProviderIfNull(IServiceProvider provider)
        {
            if (this.applicationServices is null)
            {
                this.applicationServices = provider;
            }
        }

        private InvalidOperationException GetServiceProviderNotSetException()
        {
            const string ServiceProviderRequiredMessage =
                "The Simple Injector integration hasn't been fully initialized. ";

            const string ServiceCollectionMessage =
                "Please make sure 'IServiceProvider.UseSimpleInjector(Container)' is called. " +
                "For more information, see: https://simpleinjector.org/servicecollection.";

            const string AspNetCoreMessage =
                "Please make sure 'IApplicationBuilder.UseSimpleInjector(Container)' is called " +
                "from inside the 'Configure' method of your ASP.NET Core Startup class. " +
                "For more information, see: https://simpleinjector.org/aspnetcore.";

            return new InvalidOperationException(
                ServiceProviderRequiredMessage + (
                    this.ServiceProviderAccessor is DefaultServiceProviderAccessor
                        ? ServiceCollectionMessage
                        : AspNetCoreMessage));
        }
    }
}