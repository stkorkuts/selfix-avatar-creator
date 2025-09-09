using LanguageExt;

namespace Selfix.AvatarCreator.Application.UseCases;

public sealed record CreateAvatarRequest(string JobId, Iterable<string> SourceImagesPaths);