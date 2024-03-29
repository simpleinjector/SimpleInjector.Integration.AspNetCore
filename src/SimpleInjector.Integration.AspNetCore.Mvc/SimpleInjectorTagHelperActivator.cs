﻿// Copyright (c) Simple Injector Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace SimpleInjector.Integration.AspNetCore.Mvc
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using Microsoft.AspNetCore.Mvc.Razor;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Razor.TagHelpers;

    /// <summary>Tag Helper Activator for Simple Injector.</summary>
    public class SimpleInjectorTagHelperActivator : ITagHelperActivator
    {
        private readonly ConcurrentDictionary<Type, InstanceProducer> tagHelperProducers = new();

        private readonly Container container;
        private readonly Predicate<Type> tagHelperSelector;
        private readonly ITagHelperActivator activator;

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleInjectorTagHelperActivator"/> class.
        /// </summary>
        /// <param name="container">The container instance.</param>
        /// <param name="tagHelperSelector">The predicate that determines which tag helpers should be created
        /// by the supplied <paramref name="container"/> (when the predicate returns true) or using the
        /// supplied <paramref name="frameworkTagHelperActivator"/> (when the predicate returns false).</param>
        /// <param name="frameworkTagHelperActivator">The framework's tag helper activator.</param>
        public SimpleInjectorTagHelperActivator(
            Container container,
            Predicate<Type> tagHelperSelector,
            ITagHelperActivator frameworkTagHelperActivator)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.tagHelperSelector = tagHelperSelector ?? throw new ArgumentNullException(nameof(tagHelperSelector));
            this.activator = frameworkTagHelperActivator ?? throw new ArgumentNullException(nameof(frameworkTagHelperActivator));
        }

        /// <inheritdoc />
        public TTagHelper Create<TTagHelper>(ViewContext context) where TTagHelper : ITagHelper =>
            this.UseSimpleInjector(typeof(TTagHelper))
                ? (TTagHelper)this.GetInstanceFromSimpleInjector(typeof(TTagHelper), context)
                : this.activator.Create<TTagHelper>(context);

        private bool UseSimpleInjector(Type type) => this.tagHelperSelector.Invoke(type);

        private object GetInstanceFromSimpleInjector(Type type, ViewContext context)
        {
            var scope = context.HttpContext.GetScope();

            var producer = this.tagHelperProducers.GetOrAdd(type, this.GetTagHelperProducer);

            // Scope will be null when the core integration's RequestScopingStartupFilter didn't run. This
            // can happen if this activator is used without the application being configured using the
            // SimpleInjectorAddOptions.AddAspNetCore() extension method. In that case we call .GetInstance()
            // and expect the scoping to run using the default ambient scoping mechanism (non flowing).
            return scope is null ? producer.GetInstance() : producer.GetInstance(scope);
        }

        // Find the registration for the tag helper in the container
        // and fallback to creating one when no registration exists.
        private InstanceProducer GetTagHelperProducer(Type type) =>
            this.container.GetCurrentRegistrations().SingleOrDefault(r => r.ServiceType == type)
                ?? Lifestyle.Transient.CreateProducer(type, type, this.container);
    }
}