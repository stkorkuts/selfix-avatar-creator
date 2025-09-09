using LanguageExt;
using Microsoft.Extensions.Options;
using Selfix.AvatarCreator.Application.Abstractions;
using Selfix.Jobs.Shared.Settings;
using System.Diagnostics;
using System.Globalization;
using Selfix.Jobs.Shared.Utils;

namespace Selfix.AvatarCreator.Infrastructure;

internal sealed class ResourceCleaner : IResourceCleaner
{
    private readonly EnvironmentSettings _settings;

    public ResourceCleaner(
        IOptions<EnvironmentSettings> settings)
    {
        _settings = settings.Value;
    }

    public IO<Unit> Cleanup(CancellationToken cancellationToken) => IO<Unit>.LiftAsync(async () =>
    {
        await ExternalProcessHandler.RunExternalProcessAsync("python3",
            "-c \"import torch; torch.cuda.empty_cache() if torch.cuda.is_available() else None\"", cancellationToken);

        if (Directory.Exists(_settings.InputDir))
        {
            foreach (var file in Directory.GetFiles(_settings.InputDir))
            {
                var extension = Path.GetExtension(file).ToLower(CultureInfo.InvariantCulture);
                if (extension is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".txt" or ".npz")
                {
                    File.Delete(file);
                }
            }
        }

        if (Directory.Exists(_settings.OutputDir))
        {
            foreach (var file in Directory.GetFiles(_settings.OutputDir))
            {
                var extension = Path.GetExtension(file).ToLower(CultureInfo.InvariantCulture);
                if (extension is ".safetensors" or ".pt")
                {
                    File.Delete(file);
                }
            }
        }

        return Unit.Default;
    });
}