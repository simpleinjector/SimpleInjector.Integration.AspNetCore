namespace AzureFunctionsV3_CQRS
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;

    public sealed class Function1Request : IRequest<ObjectResult>
    {
        public Function1Request(HttpRequest req) => this.Req = req;

        public HttpRequest Req { get; }
    }
}