using System;
using AspNetCoreRazorPages50PureDI.Pages;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace AspNetCoreRazorPages50PureDI
{
public class CommercePageModelActivatorProvider : IPageModelActivatorProvider, IDisposable
{
    // Singletons
    private readonly ILoggerFactory loggerFactory;

    public CommercePageModelActivatorProvider(ILoggerFactory f) => this.loggerFactory = f;

    public Func<PageContext, object> CreateActivator(CompiledPageActionDescriptor desc) =>
        c => this.CreatePageModelType(c, desc.ModelTypeInfo.AsType());

    public Action<PageContext, object> CreateReleaser(CompiledPageActionDescriptor desc) =>
        (c, pm) => { };

    private object CreatePageModelType(PageContext c, Type pageModelType)
    {
        // Create Scoped components
        var context = new CommerceContext().TrackDisposable(c);

        // Create Transient components
        switch (pageModelType.Name)
        {
            case nameof(IndexModel):
                return new IndexModel(this.Logger<IndexModel>(), context);
            case nameof(PrivacyModel):
                return new PrivacyModel(this.Logger<PrivacyModel>());
            default: throw new NotImplementedException(pageModelType.FullName);
        }
    }

    public void Dispose() { /* Release Singletons here, if needed */ }

    private ILogger<T> Logger<T>() => this.loggerFactory.CreateLogger<T>();
}

public static class DisposableExtensions
{
    public static T TrackDisposable<T>(this T instance, PageContext c) where T : IDisposable
    {
        c.HttpContext.Response.RegisterForDispose(instance);
        return instance;
    }
}
}