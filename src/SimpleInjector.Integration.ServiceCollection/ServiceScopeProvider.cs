// Copyright (c) Simple Injector Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace SimpleInjector
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Allows providing an <see cref="IServiceScope"/> that can be used within a single Simple Injector
    /// <see cref="Scope"/>. When the <see cref="ServiceScope"/> property is set before an
    /// <see cref="IServiceScope"/> is resolved for the first time within a <see cref="Scope"/>, the provided value
    /// will be returned. When this property is set <i>after</i> <see cref="IServiceScope"/> is resolved, it has
    /// no effect.
    /// </summary>
    /// <remarks>
    /// <code lang="cs"><![CDATA[
    /// using (AsyncScopedLifestyle.BeginScope(container))
    /// {
    ///     container.GetInstance<ServiceScopeProvider>().ServiceScope = serviceScope;
    ///     
    ///     var resolvedScope = container.GetInstance<IServiceScope>();
    ///     
    ///     Assert.AreSame(serviceScope, resolvedScope);
    /// }
    /// ]]></code>
    /// </remarks>
    public sealed class ServiceScopeProvider
    {
        /// <summary>
        /// Gets or sets the <see cref="IServiceScope"/>.
        /// </summary>
        public IServiceScope? ServiceScope { get; set; }
    }

}