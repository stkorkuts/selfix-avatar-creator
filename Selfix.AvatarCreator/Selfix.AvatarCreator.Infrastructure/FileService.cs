using LanguageExt;
using Selfix.AvatarCreator.Application.Abstractions;

namespace Selfix.AvatarCreator.Infrastructure;

internal sealed class FileService : IFileService
{
    public IO<Stream> Open(string path, FileMode mode, FileAccess access, FileShare share) =>
        IO.lift(Stream () => File.Open(path, mode, access, share));
}