using LanguageExt;
using Selfix.AvatarCreator.Application.Abstractions.Schema;

namespace Selfix.AvatarCreator.Application.Abstractions;

public interface IAvatarCreator
{
    IO<AvatarCreationResult> CreateAvatar(CancellationToken cancellationToken);
}