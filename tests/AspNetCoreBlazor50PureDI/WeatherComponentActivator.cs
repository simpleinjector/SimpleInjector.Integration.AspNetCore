using System;
using AspNetCoreBlazor50PureDI.Data;
using AspNetCoreBlazor50PureDI.Pages;
using AspNetCoreBlazor50PureDI.Shared;
using Microsoft.AspNetCore.Components;

namespace AspNetCoreBlazor50PureDI
{
public sealed record WeatherComponentActivator(IServiceProvider Provider) : IComponentActivator
{
    public IComponent CreateInstance(Type componentType) => (IComponent)this.Create(componentType);

    private object Create(Type type)
    {
        switch (type.Name)
        {
            case nameof(FetchData):
                return new FetchData(new WeatherForecastService());

            case nameof(App):
                return new App();

            case nameof(Counter):
                return new Counter();

            case nameof(MainLayout):
                return new MainLayout();

            case nameof(NavMenu):
                return new NavMenu();

            case nameof(Pages.Index):
                return new Pages.Index();

            case nameof(SurveyPrompt):
                return new SurveyPrompt();

            default:
                return type.Namespace.StartsWith("Microsoft")
                    ? Activator.CreateInstance(type) // Default framework behavior
                    : throw new NotImplementedException(type.FullName);
        }
    }
}
}