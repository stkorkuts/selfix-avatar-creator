using LanguageExt;

namespace Selfix.AvatarCreator.Application.Abstractions;

public interface IUseCase<in TRequest, TResponse>
{
    IO<TResponse> Execute(TRequest request, CancellationToken cancellationToken);
}