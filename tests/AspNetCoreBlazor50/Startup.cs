namespace AspNetCoreBlazor50
{
    using AspNetCoreBlazor50.Data;

    using System;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using SimpleInjector;
    using SimpleInjector.Advanced;
    using SimpleInjector.Diagnostics;
    using SimpleInjector.Integration.ServiceCollection;
    using SimpleInjector.Lifestyles;

    // Your custom attribute
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class DependencyAttribute : Attribute { }

    public class SomethingScoped { }

    public class Startup
    {
        private Container container = new SimpleInjector.Container();

        // custom property selection behavior that allows Simple Injector to inject properties
        // marked with [Dependency]
        class DependencyAttributePropertySelectionBehavior : IPropertySelectionBehavior
        {
            public bool SelectProperty(Type type, PropertyInfo prop) =>
                prop.GetCustomAttributes(typeof(DependencyAttribute)).Any();
        }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            container.Options.PropertySelectionBehavior =
                new DependencyAttributePropertySelectionBehavior();
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRazorPages();
            services.AddServerSideBlazor();

            services.AddSimpleInjector(container, options =>
            {
                options.AddServerSideBlazor(this.GetType().Assembly);
            });

            InitializeContainer();
        }

        private void InitializeContainer()
        {
            container.RegisterSingleton<WeatherForecastService>();
            container.Register<SomethingScoped>(Lifestyle.Scoped);
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.ApplicationServices.UseSimpleInjector(container);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });

            container.Verify();
        }
    }


    // Registered in MS.DI and gives access to the Simple Injector Scope, while ensuring that
    // the Simple Injector scope is disposed of with the MS.DI service scope.
    public sealed class ScopeAccessor : IAsyncDisposable, IDisposable
    {
        public Scope Scope { get; set; }
        public ValueTask DisposeAsync() => this.Scope?.DisposeAsync() ?? default;
        public void Dispose() => this.Scope?.Dispose();
    }

    public static class BlazorExtensions
    {
        public static void AddServerSideBlazor(
            this SimpleInjectorAddOptions options, params Assembly[] assemblies)
        {
            var services = options.Services;

            // HACK: This internal ComponentHub type needs to be added for the
            // SimpleInjectorBlazorHubActivator to work.
            // There's an issue for this with Microsoft: https://github.com/dotnet/aspnetcore/issues/29194
            services.AddTransient(
                typeof(Microsoft.AspNetCore.Components.Server.CircuitOptions)
                    .Assembly.GetTypes().First(
                    t => t.FullName ==
                        "Microsoft.AspNetCore.Components.Server.ComponentHub"));

            services.AddScoped(
                typeof(IHubActivator<>), typeof(SimpleInjectorBlazorHubActivator<>));
            services.AddScoped<IComponentActivator, SimpleInjectorComponentActivator>();

            RegisterBlazorComponents(options, assemblies);

            services.AddScoped<ScopeAccessor>();
            services.AddTransient<ServiceScopeApplier>();
        }

        private static void RegisterBlazorComponents(
            SimpleInjectorAddOptions options, Assembly[] assemblies)
        {
            var container = options.Container;
            var types = container.GetTypesToRegister<IComponent>(
                assemblies,
                new TypesToRegisterOptions { IncludeGenericTypeDefinitions = true });

            foreach (Type type in types.Where(t => !t.IsGenericTypeDefinition))
            {
                var registration =
                    Lifestyle.Transient.CreateRegistration(type, container);

                registration.SuppressDiagnosticWarning(
                    DiagnosticType.DisposableTransientComponent,
                    "Blazor will dispose components.");

                container.AddRegistration(type, registration);
            }

            foreach (Type type in types.Where(t => t.IsGenericTypeDefinition))
            {
                container.Register(type, type, Lifestyle.Transient);
            }
        }
    }

    public sealed class SimpleInjectorComponentActivator : IComponentActivator
    {
        private readonly ServiceScopeApplier applier;
        private readonly Container container;

        public SimpleInjectorComponentActivator(
            ServiceScopeApplier applier, Container container)
        {
            this.applier = applier;
            this.container = container;
        }

        public IComponent CreateInstance(Type type)
        {
            this.applier.ApplyServiceScope();

            IServiceProvider provider = this.container;
            var component = provider.GetService(type) ?? Activator.CreateInstance(type);
            return (IComponent)component;
        }
    }

    public sealed class SimpleInjectorBlazorHubActivator<T>
        : IHubActivator<T> where T : Hub
    {
        private readonly ServiceScopeApplier applier;
        private readonly Container container;

        public SimpleInjectorBlazorHubActivator(
            ServiceScopeApplier applier, Container container)
        {
            this.applier = applier;
            this.container = container;
        }

        public T Create()
        {
            this.applier.ApplyServiceScope();
            return this.container.GetInstance<T>();
        }

        public void Release(T hub) { }
    }

    // Registered as transient or scoped in the IServiceCollection
    public sealed class ServiceScopeApplier
    {
        private static readonly AsyncScopedLifestyle lifestyle = new AsyncScopedLifestyle();

        private readonly IServiceScope serviceScope;
        private readonly ScopeAccessor accessor;
        private readonly Container container;

        public ServiceScopeApplier(
            IServiceProvider requestServices, ScopeAccessor accessor, Container container)
        {
            this.serviceScope = (IServiceScope)requestServices;
            this.accessor = accessor;
            this.container = container;
        }

        public void ApplyServiceScope()
        {
            if (this.accessor.Scope is null)
            {
                var scope = AsyncScopedLifestyle.BeginScope(this.container);

                this.accessor.Scope = scope;

                // ServiceScopeAccessor is registered in Container by .AddSimpleInjector v5.3.
                scope.GetInstance<ServiceScopeProvider>().ServiceScope = this.serviceScope;
                // post condition: scope.GetInstance<IServiceScope>() == this.serviceScope;
            }
            else
            {
                lifestyle.SetCurrentScope(this.accessor.Scope);
            }
        }
    }

    // HACK: allow applying the service scope to the Simple Injector Scope when
    // Blazor events are called. Derive from this BaseComponent instead of ComponentBase.
    public abstract class BaseComponent : ComponentBase, IHandleEvent
    {
        [Dependency] public ServiceScopeApplier Applier { get; set; }

        Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object arg)
        {
            this.Applier.ApplyServiceScope();

            // Code below copied from ComponentBase
            var task = callback.InvokeAsync(arg);
            var shouldAwaitTask = task.Status != TaskStatus.RanToCompletion &&
                task.Status != TaskStatus.Canceled;

            // After each event, we synchronously re-render (unless !ShouldRender())
            // This just saves the developer the trouble of putting "StateHasChanged();"
            // at the end of every event callback.
            StateHasChanged();

            return shouldAwaitTask ?
                CallStateHasChangedOnAsyncCompletion(task) :
                Task.CompletedTask;
        }

        // Code below copied from ComponentBase
        private async Task CallStateHasChangedOnAsyncCompletion(Task task)
        {
            try
            {
                await task;
            }
            catch // avoiding exception filters for AOT runtime support
            {
                // Ignore exceptions from task cancellations, but don't bother issuing a state change.
                if (task.IsCanceled)
                {
                    return;
                }

                throw;
            }

            base.StateHasChanged();
        }
    }
}