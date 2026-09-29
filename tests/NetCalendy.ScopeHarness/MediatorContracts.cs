// Test-only interfaces match the handler/notification signatures used by the
// linked source. This harness does not execute MediatR or its pipeline.
namespace MediatR;
public interface INotification { }
public interface IRequest<out TResponse> { }
public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
