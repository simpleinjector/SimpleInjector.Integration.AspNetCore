using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AzureFunctionsV3
{
    public interface IFxService { }
    public class ScopedFxService : IFxService, IDisposable
    {
        public void Dispose() =>
            Console.WriteLine($"Ms.DI scoped instance {nameof(ScopedFxService)} ({this.GetHashCode()}) was disposed.");
    }

    public interface IMyService
    {
        int Code();
    }
    public class MyScopedService : IMyService, IDisposable
    {
        public MyScopedService(IFxService s) => this.S = s;

        public IFxService S { get; }

        public int Code() => this.S.GetHashCode();

        public void Dispose() =>
            Console.WriteLine($"Simple Injector scoped instance {nameof(MyScopedService)} ({this.GetHashCode()}) was disposed.");
    }

    public class Function1
    {
        private readonly IFxService fxService;
        private readonly Resolver resolver;

        public Function1(IFxService fxService, Resolver resolver)
        {
            this.resolver = resolver;
            this.fxService = fxService;
        }

        [FunctionName("Function1")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var code = this.resolver.Resolve<IMyService>().Code();
            var code2 = fxService.GetHashCode();

            if (code != code2) throw new InvalidOperationException("Invalid scoping");

            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {name}. This HTTP triggered function executed successfully.";

            return new OkObjectResult(responseMessage);
        }
    }
}
