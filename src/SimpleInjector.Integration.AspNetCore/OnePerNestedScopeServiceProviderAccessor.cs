// Copyright (c) Simple Injector Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace SimpleInjector.Integration.AspNetCore
{
    using System;
    using Microsoft.AspNetCore.Http;
    using SimpleInjector;
    using SimpleInjector.Integration.ServiceCollection;

    internal sealed class OnePerNestedScopeServiceProviderAccessor : IServiceProviderAccessor
    {
        private readonly Container container;
        private readonly IServiceProviderAccessor decoratee;

        internal OnePerNestedScopeServiceProviderAccessor(Container container, IServiceProviderAccessor decoratee)
        {
            this.decoratee = decoratee;
            this.container = container;
        }

        public IServiceProvider Current =>
            this.RootScopeHttpContext?.RequestServices ?? this.decoratee.Current;

        // Only the scope wrapping the request (the root scope) contains the HttpContext in its Items dictionary.
        // With nested scopes, this property returns null.
        private HttpContext? RootScopeHttpContext =>
            this.CurrentScope?.GetItem(RequestScopingStartupFilter.HttpContextKey) as HttpContext;

        private Scope? CurrentScope => Lifestyle.Scoped.GetCurrentScope(this.container);
    }
}