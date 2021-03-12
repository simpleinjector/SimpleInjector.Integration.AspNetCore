using System.Threading.Tasks;

namespace AzureFunctionsV3_CQRS
{
    public interface IMediator
    {
        Task<TResult> HandleAsync<TResult>(IRequest<TResult> message);
    }
}