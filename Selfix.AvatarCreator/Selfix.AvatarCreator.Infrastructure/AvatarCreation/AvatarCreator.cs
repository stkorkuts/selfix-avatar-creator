using System.Diagnostics;
using System.Globalization;
using System.Text;
using LanguageExt;
using Microsoft.Extensions.Options;
using Selfix.AvatarCreator.Application.Abstractions;
using Selfix.AvatarCreator.Application.Abstractions.Schema;
using Selfix.Jobs.Shared.Settings;
using Selfix.Jobs.Shared.Utils;
using Serilog;

namespace Selfix.AvatarCreator.Infrastructure.AvatarCreation;

internal sealed class AvatarCreator : IAvatarCreator
{
    private readonly EnvironmentSettings _envSettings;
    private readonly GenerationSettings _generationSettings;

    public AvatarCreator(IOptions<EnvironmentSettings> envOptions, IOptions<GenerationSettings> generationOptions)
    {
        _envSettings = envOptions.Value;
        _generationSettings = generationOptions.Value;
    }

    public IO<AvatarCreationResult> CreateAvatar(CancellationToken cancellationToken) =>
        IO<AvatarCreationResult>.LiftAsync(async () =>
        {
            Log.Information("Starting avatar creation process");
            const string MODEL_NAME = "lora";

            try
            {
                // Create captions for files
                Log.Debug("Running caption generation script at {ScriptPath}", _envSettings.CaptionsScriptPath);
                await ExternalProcessHandler.RunExternalProcessAsync(
                    "python3",
                    _envSettings.CaptionsScriptPath,
                    cancellationToken);
                Log.Information("Caption generation completed successfully");

                Log.Debug("Creating avatar description from captions");
                var avatarDescription = await CreateAvatarDescriptionFromCaptions();
                Log.Debug("Avatar description created, length: {Length}", avatarDescription.Length);

                // Create avatar
                var avatarArguments = BuildAvatarCreatorArguments(MODEL_NAME);
                Log.Information("Starting avatar model training with accelerate");
                Log.Information("Training arguments: {Arguments}", avatarArguments);
                
                await ExternalProcessHandler.RunExternalProcessAsync(
                    "accelerate",
                    avatarArguments,
                    cancellationToken);
                
                var avatarPath = Path.Combine(_envSettings.OutputDir, $"{MODEL_NAME}.safetensors");
                
                if (File.Exists(avatarPath))
                {
                    Log.Information("Avatar created successfully at {AvatarPath}", avatarPath);
                }
                else
                {
                    Log.Warning("Avatar file not found at expected path {AvatarPath}", avatarPath);
                    throw new FileNotFoundException("Avatar file not found");
                }

                return new AvatarCreationResult(avatarPath, avatarDescription);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred during avatar creation");
                throw;
            }
        });

    private async Task<string> CreateAvatarDescriptionFromCaptions()
    {
        Log.Debug("Reading caption files from {InputDir}", _envSettings.InputDir);
        var captionFiles = Directory.EnumerateFiles(_envSettings.InputDir, "*.txt", SearchOption.TopDirectoryOnly).ToList();
        Log.Debug("Found {Count} caption files", captionFiles.Count);
        
        var sb = new StringBuilder();
        foreach (var captionFile in captionFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(captionFile);
                sb.AppendLine(content);
                Log.Debug("Added content from caption file {FileName}", Path.GetFileName(captionFile));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to read caption file {FileName}", Path.GetFileName(captionFile));
            }
        }
        return sb.ToString();
    }

    private string BuildAvatarCreatorArguments(string modelName)
    {
        Log.Debug("Building avatar creator arguments for model {ModelName}", modelName);
        var builder = new ArgumentsBuilder()
            .AddSwitch("launch")
            .AddParameter("--mixed_precision", "bf16")
            .AddParameter("--num_cpu_threads_per_process", "1")
            .AddSwitch($"\"{_envSettings.TrainScriptPath}\"")
            .AddParameter("--pretrained_model_name_or_path", $"\"{_envSettings.UnetPath}\"")
            .AddParameter("--clip_l", $"\"{_envSettings.ClipLargePath}\"")
            .AddParameter("--t5xxl", $"\"{_envSettings.T5XXLPath}\"")
            .AddParameter("--ae", $"\"{_envSettings.VAEPath}\"")
            .AddSwitch("--cache_latents_to_disk")
            .AddParameter("--save_model_as", "safetensors")
            .AddSwitch("--sdpa")
            .AddSwitch("--persistent_data_loader_workers")
            .AddParameter("--max_data_loader_n_workers", "2")
            .AddParameter("--seed", "42")
            .AddSwitch("--gradient_checkpointing")
            .AddParameter("--mixed_precision", "bf16")
            .AddParameter("--save_precision", "bf16")
            .AddParameter("--network_module", "networks.lora_flux")
            .AddParameter("--network_dim", "4")
            .AddParameter("--learning_rate", "6e-4")
            .AddSwitch("--cache_text_encoder_outputs")
            .AddSwitch("--cache_text_encoder_outputs_to_disk")
            .AddSwitch("--fp8_base")
            .AddSwitch("--highvram")
            .AddParameter("--max_train_epochs", _generationSettings.EpochsCount.ToString(CultureInfo.InvariantCulture))
            .AddParameter("--save_every_n_epochs", "100")
            .AddParameter("--output_dir", $"\"{_envSettings.OutputDir}\"")
            .AddParameter("--output_name", $"\"{modelName}\"")
            .AddParameter("--timestep_sampling", "shift")
            .AddParameter("--discrete_flow_shift", "3.1582")
            .AddParameter("--model_prediction_type", "raw")
            .AddParameter("--guidance_scale", "1")
            .AddParameter("--loss_type", "l2");

        if (_envSettings.IsHighVram)
        {
            AddHighVramParams(builder);
        }
        else
        {
            AddLowVramParams(builder);
        }

        return builder.ToString();
    }

    private void AddHighVramParams(ArgumentsBuilder argumentsBuilder)
    {
        const string datasetName = "dataset-highvram.toml";
        argumentsBuilder
            .AddParameter("--dataset_config", $"\"{Path.Combine(_envSettings.InputDir, datasetName)}\"")
            .AddParameter("--optimizer_type", "adamw8bit");
    }
    
    private void AddLowVramParams(ArgumentsBuilder argumentsBuilder)
    {
        const string datasetName = "dataset-lowvram.toml";
        argumentsBuilder
            .AddParameter("--dataset_config", $"\"{Path.Combine(_envSettings.InputDir, datasetName)}\"")
            .AddParameter("--lr_scheduler", "constant_with_warmup")
            .AddParameter("--max_grad_norm", "0.0")
            .AddParameter("--optimizer_type", "adafactor")
            .AddParameter("--optimizer_args", "\"relative_step=False\" \"scale_parameter=False\" \"warmup_init=False\"");
    }
}