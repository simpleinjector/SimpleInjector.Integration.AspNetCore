namespace AzureFunctionsV3_CQRS
{
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public sealed class Function1Handler : IRequestHandler<Function1Request, ObjectResult>
    {
        private readonly ILogger log;

        public Function1Handler(ILogger log)
        {
            this.log = log;
        }

        public async Task<ObjectResult> HandleAsync(Function1Request message)
        {
            // NOTE: Don't forget to add your applications root namespace to logging/logLevel
            // node of the the application's host.json. Otherwise the line below won't log.
            this.log.LogInformation("C# HTTP trigger function processed a request.");

            string name = message.Req.Query["name"];

            string requestBody = await new StreamReader(message.Req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the " +
                    "query string or in the request body for a personalized response."
                : $"Hello, {name}. This HTTP triggered function executed successfully.";

            return new OkObjectResult(responseMessage);
        }
    }
}