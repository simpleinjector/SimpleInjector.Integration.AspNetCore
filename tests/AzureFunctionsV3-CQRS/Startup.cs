using System;
using AzureFunctionsV3_CQRS.Infrastructure;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;

[assembly: FunctionsStartup(typeof(AzureFunctionsV3_CQRS.Startup))]
namespace AzureFunctionsV3_CQRS
{
    public class Startup : FunctionsStartup
    {
        private readonly Container container = new Container();

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(this);
            services.AddSingleton<Completion>();
            services.AddScoped(typeof(IMediator), typeof(AzureToSimpleInjectorMediator));

            services.AddSimpleInjector(container, options =>
            {
                // Prevent the use of hosted services (not supported by Azure Functions).
                options.EnableHostedServiceResolution = false;

                // Allow injecting ILogger into application components.
                options.AddLogging();
            });

            InitializeContainer();
        }

        private void InitializeContainer()
        {
            // Batch-register all your request handlers.
            container.Register(typeof(IRequestHandler<,>), this.GetType().Assembly);
        }

        public void Configure(IServiceProvider app)
        {
            // Complete the Simple Injector integration (enables cross wiring).
            app.UseSimpleInjector(container);

            container.Verify();
        }

        public override void Configure(IFunctionsHostBuilder builder) =>
            this.ConfigureServices(builder.Services);

        // HACK: Triggers the completion of the Simple Injector integration
        public sealed class Completion
        {
            public Completion(Startup s, IServiceProvider app) => s.Configure(app);
        }
    }
}