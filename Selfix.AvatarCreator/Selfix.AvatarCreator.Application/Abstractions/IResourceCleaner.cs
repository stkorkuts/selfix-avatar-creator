using LanguageExt;

namespace Selfix.AvatarCreator.Application.Abstractions;

public interface IResourceCleaner
{
    public IO<Unit> Cleanup(CancellationToken cancellationToken);
}