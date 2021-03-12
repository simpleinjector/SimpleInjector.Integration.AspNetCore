using System.Threading.Tasks;

namespace AzureFunctionsV3_CQRS
{
    public interface IRequestHandler<TRequest, TResult> where TRequest : IRequest<TResult>
    {       
        Task<TResult> HandleAsync(TRequest message);
    }
}