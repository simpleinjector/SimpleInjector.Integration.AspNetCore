using AspNetCoreMvc50;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using SimpleInjector;
using System;
using System.Linq;

namespace WebApplicationFactoryConsole
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var client = new CustomWebApplicationFactory().CreateClient();

            Console.WriteLine("Hello World!");
        }
    }

    public class CustomWebApplicationFactory : WebApplicationFactory<Startup>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestContainer<Container>(container =>
            {
                //useless, it's impossible to get here
            });

            builder.ConfigureTestServices(services =>
            {
                var container =
                    (Container)services
                    // System.InvalidOperationException: Sequence contains no matching element
                    .Last(d => d.ServiceType == typeof(Container))
                    .ImplementationInstance;

                container.Options.AllowOverridingRegistrations = true;

                container.Register<IUserService, FakUserService>(Lifestyle.Singleton);

            });
        }
    }

    public class FakUserService : IUserService
    {

    }
}
