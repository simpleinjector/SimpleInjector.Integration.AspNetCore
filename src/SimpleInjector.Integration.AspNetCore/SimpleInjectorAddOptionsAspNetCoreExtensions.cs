// Copyright (c) Simple Injector Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace SimpleInjector
{
    using System;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using SimpleInjector.Integration.AspNetCore;
    using SimpleInjector.Integration.ServiceCollection;
    using SimpleInjector.Lifestyles;

    /// <summary>
    /// Extensions for configuring Simple Injector with ASP.NET Core using
    /// <see cref="SimpleInjectorAddOptions"/>.
    /// </summary>
    public static class SimpleInjectorAddOptionsAspNetCoreExtensions
    {
        /// <summary>
        /// Adds basic Simple Injector integration for ASP.NET Core and returns a builder object that allow
        /// additional integration options to be applied. These basic integrations includes wrapping each web
        /// request in an <see cref="AsyncScopedLifestyle"/> scope and making the nessesary changes that make
        /// it possible for enabling the injection of framework components in Simple Injector-constructed
        /// components when
        /// <see cref="SimpleInjectorServiceCollectionExtensions.UseSimpleInjector(IServiceProvider, Container)"/>
        /// is called. This method uses the default <see cref="ServiceScopeReuseBehavior"/>, which is
        /// <see cref="ServiceScopeReuseBehavior.OnePerRequest"/>. This means that within a single web request, the same
        /// <see cref="IServiceScope"/> instance will be used, irregardless of the number of Simple Injector
        /// <see cref="Scope"/> instances you create. Outside the context of a web request, the ASP.NET Core
        /// integration falls back to the default behavior specified by the
        /// <see cref="SimpleInjectorAddOptions.ServiceProviderAccessor"/>. By default, a new
        /// <see cref="IServiceScope"/> instance will be created per Simple Injector <see cref="Scope"/>.
        /// </summary>
        /// <param name="options">The options to which the integration should be applied.</param>
        /// <returns>A new <see cref="SimpleInjectorAspNetCoreBuilder"/> instance that allows additional
        /// configurations to be made.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
        public static SimpleInjectorAspNetCoreBuilder AddAspNetCore(this SimpleInjectorAddOptions options)
        {
            return AddAspNetCore(options, ServiceScopeReuseBehavior.OnePerRequest);
        }

        /// <summary>
        /// Adds basic Simple Injector integration for ASP.NET Core and returns a builder object that allow
        /// additional integration options to be applied. These basic integrations includes wrapping each web
        /// request in an <see cref="AsyncScopedLifestyle"/> scope and making the nessesary changes that make
        /// it possible for enabling the injection of framework components in Simple Injector-constructed
        /// components when
        /// <see cref="SimpleInjectorServiceCollectionExtensions.UseSimpleInjector(IServiceProvider, Container)"/>
        /// is called.
        /// </summary>
        /// <param name="options">The options to which the integration should be applied.</param>
        /// <param name="serviceScopeBehavior">Defines in which way Simple Injector should use and reuse the ASP.NET
        /// Core <see cref="IServiceScope"/>, which is used to resolve cross-wired dependencies from.</param>
        /// <returns>A new <see cref="SimpleInjectorAspNetCoreBuilder"/> instance that allows additional
        /// configurations to be made.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
        public static SimpleInjectorAspNetCoreBuilder AddAspNetCore(
            this SimpleInjectorAddOptions options, ServiceScopeReuseBehavior serviceScopeBehavior)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            IServiceCollection services = options.Services;

            var container = options.Container;

            // Add the IHttpContextAccessor to allow Simple Injector cross wiring to work in ASP.NET Core.
            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            options.ServiceProviderAccessor = CreateServiceProviderAccessor(options, serviceScopeBehavior);

            services.UseSimpleInjectorAspNetRequestScoping(container);

            return new SimpleInjectorAspNetCoreBuilder(options);
        }

        private static IServiceProviderAccessor CreateServiceProviderAccessor(
            SimpleInjectorAddOptions options, ServiceScopeReuseBehavior serviceScopeBehavior)
        {
            if (serviceScopeBehavior < ServiceScopeReuseBehavior.OnePerRequest
                || serviceScopeBehavior > ServiceScopeReuseBehavior.Unchanged)
            {
                throw new ArgumentOutOfRangeException(nameof(serviceScopeBehavior));
            }

            if (serviceScopeBehavior == ServiceScopeReuseBehavior.OnePerRequest)
            {
                // This IServiceProviderAccessor uses IHttpContextAccessor to resolve instances that are scoped inside
                // the current request.
                return new OnePerRequestServiceProviderAccessor(
                    new HttpContextAccessor(),
                    options.ServiceProviderAccessor);
            }
            else if (serviceScopeBehavior == ServiceScopeReuseBehavior.OnePerNestedScope)
            {
                // This IServiceProviderAccessor resolves cross-wired services from the request's IServiceScope, but
                // uses a new IServiceScope within a nested scope.
                return new OnePerNestedScopeServiceProviderAccessor(options.Container, options.ServiceProviderAccessor);
            }
            else
            {
                // This uses the default behavior.
                return options.ServiceProviderAccessor;
            }
        }
    }
}