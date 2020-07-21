// Copyright (c) Simple Injector Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector.Integration.ServiceCollection;

namespace SimpleInjector
{
    /// <summary>
    /// Specifies the behavior of how ASP.NET Core <see cref="IServiceScope"/> instances are reused.
    /// </summary>
    public enum ServiceScopeReuseBehavior
    {
        /// <summary>
        /// Within the context of a web request (or SignalR connection), Simple Injector will reuse the same
        /// <see cref="IServiceScope"/> instance, irregardless of how many Simple Injector <see cref="Scope"/>
        /// instances are created. Outside the context of a (web) request (i.e.
        /// <see cref="IHttpContextAccessor.HttpContext"/> returns <c>null</c>), this behavior falls back to
        /// <see cref="Unchanged" />.
        /// </summary>
        OnePerRequest = 0,

        /// <summary>
        /// Within the context of a web request (or SignalR connection), Simple Injector will use the request's
        /// <see cref="IServiceScope"/> within its root scope. Within a nested scope or outside the context of a (web)
        /// request, this behavior falls back to <see cref="Unchanged" />.
        /// </summary>
        OnePerNestedScope = 1,

        /// <summary>
        /// This leaves the original configured <see cref="SimpleInjectorAddOptions.ServiceProviderAccessor"/> as-is.
        /// If <see cref="SimpleInjectorAddOptions.ServiceProviderAccessor">ServiceProviderAccessor</see> is not
        /// replaced, the default value, as returned by
        /// <see cref="SimpleInjectorServiceCollectionExtensions.AddSimpleInjector"/>, ensures the creation of a new
        /// .NET Core <see cref="IServiceScope"/> instance, for every Simple Injector <see cref="Scope"/>. The
        /// <see cref="IServiceScope"/> is <i>scoped</i> to that <see cref="Scope"/>. The ASP.NET Core's request
        /// <see cref="IServiceScope"/> will <b>NEVER</b> be used. Instead Simple Injector creates a new one for the
        /// request (and one for each nested scope). This disallows accessing ASP.NET Core services that depend on or
        /// return request-specific data.
        /// </summary>
        Unchanged = 2,
    }
}