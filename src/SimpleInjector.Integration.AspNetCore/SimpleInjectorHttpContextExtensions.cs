// Copyright (c) Simple Injector Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace SimpleInjector.Integration.AspNetCore
{
    using Microsoft.AspNetCore.Http;
    using SimpleInjector;

    /// <summary>
    /// Extension methods on top of HttpContext
    /// </summary>
    public static class SimpleInjectorHttpContextExtensions
    {
        private static readonly object HttpContextKey = new();

        /// <summary>
        /// Gets the <see cref="Scope"/> connected to the supplied <see cref="HttpContext"/>.
        /// </summary>
        /// <param name="httpContext">The <see cref="HttpContext"/> to retrieve the <see cref="Scope"/> from.</param>
        /// <returns>The scope or null.</returns>
        public static Scope? GetScope(this HttpContext httpContext) => (Scope)httpContext.Items[HttpContextKey];

        internal static HttpContext? GetHttpContext(this Scope scope) => scope.GetItem(HttpContextKey) as HttpContext;

        internal static void ConnectToScope(this HttpContext httpContext, Scope scope)
        {
            scope.SetItem(HttpContextKey, httpContext);
            httpContext.Items[HttpContextKey] = scope;
        }
    }
}