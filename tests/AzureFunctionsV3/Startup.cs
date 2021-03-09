using System;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;
using SimpleInjector.Lifestyles;

[assembly: FunctionsStartup(typeof(AzureFunctionsV3.Startup))]
namespace AzureFunctionsV3
{
    public class Startup : FunctionsStartup
    {
        private readonly Container container = new SimpleInjector.Container();

        public override void Configure(IFunctionsHostBuilder builder)
        {
            var services = builder.Services;

            // Special integration classes (see below). Resolver needs to be
            // injected into every Azure Function class.
            services.AddScoped<Resolver>();
            services.AddSingleton<Resolver.Completion>();

            services.AddSimpleInjector(this.container, options =>
            {
                // Use of hosted services is not supported by Azure Functions.
                options.EnableHostedServiceResolution = false; // #15
            });

            InitializeContainer();

            services.AddScoped<IFxService, ScopedFxService>();
        }

        private void InitializeContainer()
        {
            container.Register<IMyService, MyScopedService>(Lifestyle.Scoped);
            container.Register<IUserService, UserService>(Lifestyle.Scoped);
        }
    }

    public interface IUserService { }
    public class UserService : IUserService { }

    public sealed class Resolver : IDisposable
    {
        private readonly Container container;
        private readonly Scope scope;

        public Resolver(
            Completion completor, // don't remove this dependency, it calls .UseSimpleInjector()
            Container container,
            IServiceProvider azureServiceProvider)
        {
            this.container = container;

            this.scope = AsyncScopedLifestyle.BeginScope(container);
            
            // Set the current service scope for Simple Injector to cross wire from.
            this.container.GetInstance<ServiceScopeProvider>().ServiceScope =
                new AzureServiceScopeWrapper { ServiceProvider = azureServiceProvider };
        }

        private sealed class AzureServiceScopeWrapper : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; set; }

            // AF reused the same IServiceProvider instance across the entire application (seriously).
            // This means it shouldn't be disposed, because that would break the application.
            public void Dispose() { }
        }

        public T Resolve<T>() where T : class => this.container.GetInstance<T>();

        // You probably want DisposeAsync to be called, but I didn't succeed in
        // getting Azure Functions to dispose anything asynchronously.
        public void Dispose() => this.scope.Dispose();

        public sealed class Completion
        {
            public Completion(IServiceProvider rootProvider, Container container)
            {
                // Complete the Simple Injector integration
                rootProvider.UseSimpleInjector(container);

                container.Verify();
            }
        }
    }
}