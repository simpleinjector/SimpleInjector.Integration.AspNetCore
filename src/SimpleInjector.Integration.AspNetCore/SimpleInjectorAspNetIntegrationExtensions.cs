// Copyright (c) Simple Injector Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace SimpleInjector
{
    using System;
    using Integration.AspNetCore;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using SimpleInjector.Lifestyles;

    /// <summary>
    /// Extension methods for integrating Simple Injector with ASP.NET applications.
    /// </summary>
    public static class SimpleInjectorAspNetCoreIntegrationExtensions
    {
        /// <summary>Wraps ASP.NET requests in an <see cref="AsyncScopedLifestyle"/>.</summary>
        /// <param name="services">The ASP.NET application builder instance that references all
        /// framework components.</param>
        /// <param name="container">The container.</param>
        public static void UseSimpleInjectorAspNetRequestScoping(
            this IServiceCollection services, Container container)
        {
            Requires.IsNotNull(services, nameof(services));
            Requires.IsNotNull(container, nameof(container));

            services.AddSingleton<IStartupFilter>(new RequestScopingStartupFilter(container));
        }

        /// <summary>
        /// Get service of type <typeparamref name="T"/> from the list of request-specific services of the
        /// application builder. This preserves the lifestyle of the registered component.
        /// </summary>
        /// <typeparam name="T">The type of service object to get.</typeparam>
        /// <param name="builder">The IApplicationBuilder to retrieve the service object from.</param>
        /// <returns>A service object of type T or null if there is no such service.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the method is called outside the
        /// context of a web request.</exception>
#if NETSTANDARD2_0
        public static T GetRequestService<T>(this IApplicationBuilder builder)
#endif
#if NET6_0_OR_GREATER
        public static T? GetRequestService<T>(this IApplicationBuilder builder)
#endif
        {
            Requires.IsNotNull(builder, nameof(builder));

            return GetRequestServiceProvider(builder.GetApplicationServices(), typeof(T)).GetService<T>();
        }

        /// <summary>
        /// Get service of type <typeparamref name="T"/> from the list of request-specific services of the
        /// application builder. This preserves the lifestyle of the registered component.
        /// </summary>
        /// <typeparam name="T"> The type of service object to get.</typeparam>
        /// <param name="builder">The IApplicationBuilder to retrieve the service object from.</param>
        /// <returns>A service object of type T.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the method is called outside the
        /// context of a web request, or when there is no service of type <typeparamref name="T"/>.</exception>
#if NETSTANDARD2_0
        public static T GetRequiredRequestService<T>(this IApplicationBuilder builder)
#endif
#if NET6_0_OR_GREATER
        public static T GetRequiredRequestService<T>(this IApplicationBuilder builder)
            where T : notnull
#endif
        {
            Requires.IsNotNull(builder, nameof(builder));

            return GetRequestServiceProvider(
                builder.GetApplicationServices(), typeof(T)).GetRequiredService<T>();
        }

        // WARNING: Although most of the extension methods in this class can become obsolete, because of the
        // new fluent API, this method should not be obsoleted, as there is no alternative for the following
        // call: app.Map("/api/queries", builder => builder.UseMiddleware<QueryHandlerMiddleware>(container));

        /// <summary>
        /// Adds a middleware type to the application's request pipeline. The middleware will be resolved from
        /// the supplied the Simple Injector <paramref name="container"/>. The middleware will be added to the
        /// container for verification.
        /// </summary>
        /// <typeparam name="TMiddleware">The middleware type.</typeparam>
        /// <param name="app">The <see cref="IApplicationBuilder"/> instance.</param>
        /// <param name="container">The container to resolve <typeparamref name="TMiddleware"/> from.</param>
        /// <returns>The supplied <see cref="IApplicationBuilder"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when one of the arguments is a null reference.</exception>
        /// <exception cref="ArgumentException">Thrown when the <typeparamref name="TMiddleware"/> does not
        /// implement <see cref="IMiddleware"/> or not a concrete constructable
        /// type.</exception>
        public static IApplicationBuilder UseMiddleware<TMiddleware>(
            this IApplicationBuilder app, Container container)
        {
            // DESIGN: The "where TMiddleware : class, IMiddleware" generic type constraint was deliberately removed
            // from this generic method. In case a user calls app.UserMiddleware<T>(container) on a T that didn't
            // implement IMiddleware, C# would call one of ASP.NET Core's built-in extension method (i.e.
            // UseMiddlewareExtensions.UseMiddleware<T>(IApplicationBuilder, object[])) instead of giving a compile
            // error. This would result in a really confusing error message. By removing the constraint, C# will still
            // prefer calling this method, in which case we can throw an expressive exception explaining that
            // TMiddleware should implement IMiddleware. See #5
            return UseMiddlewareInternal(app, typeof(TMiddleware), container, "TMiddleware");
        }

        /// <summary>
        /// Adds a middleware type to the application's request pipeline. The middleware will be resolved from
        /// the supplied the Simple Injector <paramref name="container"/>. The middleware will be added to the
        /// container for verification.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/> instance.</param>
        /// <param name="middlewareType">The middleware type that needs to be applied. This type must 
        /// implement <see cref="IMiddleware"/>.</param>
        /// <param name="container">The container to resolve <paramref name="middlewareType"/> from.</param>
        /// <returns>The supplied <see cref="IApplicationBuilder"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when one of the arguments is a null reference.</exception>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="middlewareType"/> does not
        /// implement <see cref="IMiddleware"/>, is open-generic, or not a concrete constructable type.</exception>
        public static IApplicationBuilder UseMiddleware(
            this IApplicationBuilder app, Type middlewareType, Container container)
        {
            return UseMiddlewareInternal(app, middlewareType, container, nameof(middlewareType));
        }

        private static IApplicationBuilder UseMiddlewareInternal(
            IApplicationBuilder app, Type middlewareType, Container container, string middlewareTypeArgName)
        {
            Requires.IsNotNull(app, nameof(app));
            Requires.IsNotNull(middlewareType, middlewareTypeArgName);
            Requires.IsNotNull(container, nameof(container));

            Requires.ServiceIsAssignableFromImplementation(typeof(IMiddleware), middlewareType, middlewareTypeArgName);

            Requires.IsNotOpenGenericType(middlewareType, middlewareTypeArgName);

            var lifestyle = container.Options.LifestyleSelectionBehavior.SelectLifestyle(middlewareType);

            // By creating an InstanceProducer up front, it will be known to the container, and will be part of the
            // verification process of the container (in case verification is done after a call to UseMiddleware).
            // NOTE: the middleware can't be registered in the container using container.Register, because:
            // 1. at this point the container might already be locked (which will happen when the new ASP.NET Core 3
            //    Host class is used).
            // 2. This method might be used in a map, e.g. app.Map("/api/abc", b => b.UseMiddleware<M>(container)).
            //    When there are multiple mappings to the same middleware type M, the second call would fail.
            InstanceProducer<IMiddleware> producer = lifestyle.CreateProducer<IMiddleware>(middlewareType, container);

            // NOTE: CreateProducer will deduplicate the underlying Registration for middlewareType, which means its
            // lifestyle will be respected in case there are multiple maps to the same type. But in case decorators are
            // applied to IMiddleware, each map gets its own decorator (even if the decorator is registered as
            // singleton). This isn't per se the best design, but keep this in mind when changing this; users might be
            // depending on this behavior.
            app.Use((c, next) =>
            {
                IMiddleware middleware = producer.GetInstance();
                return middleware.InvokeAsync(c, _ => next());
            });

            return app;
        }

        private static IServiceProvider GetApplicationServices(this IApplicationBuilder builder)
        {
            var appServices = builder.ApplicationServices;

            if (appServices is null)
            {
                throw new ArgumentNullException(nameof(builder) + ".ApplicationServices");
            }

            return appServices;
        }

        private static IServiceProvider GetRequestServiceProvider(
            IServiceProvider appServices, Type serviceType)
        {
            IHttpContextAccessor accessor = GetHttpContextAccessor(appServices);

            var context = accessor.HttpContext;

            if (context is null)
            {
                throw new InvalidOperationException(
                    $"Unable to request service '{serviceType.ToFriendlyName()} from ASP.NET Core request " +
                    "services. Please make sure this method is called within the context of an active HTTP " +
                    "request.");
            }

            return context.RequestServices;
        }

        private static IHttpContextAccessor GetHttpContextAccessor(IServiceProvider appServices)
        {
            var accessor = appServices.GetService<IHttpContextAccessor>();

            if (accessor is null)
            {
                throw new InvalidOperationException(
                    "Type 'Microsoft.AspNetCore.Http.IHttpContextAccessor' is not available in the " +
                    "IApplicationBuilder.ApplicationServices collection. Please make sure it is registered " +
                    "by adding it to the ConfigureServices method as follows: " + Environment.NewLine +
                    "services.AddHttpContextAccessor();");
            }

            return accessor;
        }
    }
}