namespace AzureFunctionsV3_CQRS
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;

    public class Function1
    {
        private readonly IMediator mediator;

        public Function1(IMediator mediator) => this.mediator = mediator;

        [FunctionName("Function1")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req)
        {
            return await this.mediator.HandleAsync(new Function1Request(req));
        }
    }
}