namespace AzureFunctionsV3_CQRS.Infrastructure
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using SimpleInjector;
    using SimpleInjector.Integration.ServiceCollection;
    using SimpleInjector.Lifestyles;

    public sealed class AzureToSimpleInjectorMediator : IMediator
    {
        private readonly Container container;
        private readonly IServiceProvider serviceProvider;

        public AzureToSimpleInjectorMediator(
            // NOTE: Do note remove the Completion dependency. Its resolution triggers the
            // finalization of the Simple Injector integration.
            Startup.Completion completor, Container container, IServiceProvider provider)
        {
            this.container = container;
            this.serviceProvider = provider;
        }

        private interface IRequestHandler<TResult>
        {
            Task<TResult> HandleAsync(IRequest<TResult> message);
        }

        // NOTE: There seems to be no support for async disposal for framework types in AF3,
        // but using the code below, atleast Simple Injector-registered components will get
        // disposed asynchronously.
        public async Task<TResult> HandleAsync<TResult>(IRequest<TResult> message)
        {
            // Wrap the operation in a Simple Injector scope
            await using (AsyncScopedLifestyle.BeginScope(this.container))
            {
                // Allow Simple Injector to cross wire framework dependencies.
                this.container.GetInstance<ServiceScopeProvider>().ServiceScope =
                    new ServiceScope(this.serviceProvider);

                return await this.HandleCoreAsync(message);
            }
        }

        private async Task<TResult> HandleCoreAsync<TResult>(IRequest<TResult> message) =>
            await this.GetHandler(message).HandleAsync(message);

        private IRequestHandler<TResult> GetHandler<TResult>(IRequest<TResult> message)
        {
            var handlerType = typeof(IRequestHandler<,>)
                .MakeGenericType(message.GetType(), typeof(TResult));
            var wrapperType = typeof(RequestHandlerWrapper<,>)
                .MakeGenericType(message.GetType(), typeof(TResult));

            return (IRequestHandler<TResult>)Activator.CreateInstance(
                wrapperType, container.GetInstance(handlerType));
        }

        private class RequestHandlerWrapper<TRequest, TResult> : IRequestHandler<TResult>
            where TRequest : IRequest<TResult>
        {
            public RequestHandlerWrapper(IRequestHandler<TRequest, TResult> handler) =>
                this.Handler = handler;

            public IRequestHandler<TRequest, TResult> Handler { get; }

            public Task<TResult> HandleAsync(IRequest<TResult> message) =>
                this.Handler.HandleAsync((TRequest)message);
        }

        private sealed class ServiceScope : IServiceScope
        {
            public ServiceScope(IServiceProvider serviceProvider) =>
                this.ServiceProvider = serviceProvider;

            public IServiceProvider ServiceProvider { get; }

            public void Dispose() { }
        }
    }
}