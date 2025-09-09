using LanguageExt;
using Microsoft.Extensions.Options;
using Selfix.AvatarCreator.Application.Abstractions;
using Selfix.Jobs.Shared.Extensions;
using Selfix.Jobs.Shared.Settings;
using Serilog;

namespace Selfix.AvatarCreator.Application.UseCases;

public sealed class CreateAvatarUseCase : IUseCase<CreateAvatarRequest, CreateAvatarResponse>
{
    private readonly IObjectStorage _objectStorage;
    private readonly IFileService _fileService;
    private readonly IAvatarCreator _avatarCreator;
    private readonly IResourceCleaner _resourceCleaner;
    private readonly S3Settings _s3Settings;
    private readonly EnvironmentSettings _environmentSettings;

    public CreateAvatarUseCase(
        IObjectStorage objectStorage,
        IFileService fileService,
        IAvatarCreator avatarCreator,
        IOptions<S3Settings> s3Options,
        IOptions<EnvironmentSettings> envOptions,
        IResourceCleaner resourceCleaner)
    {
        _objectStorage = objectStorage;
        _fileService = fileService;
        _avatarCreator = avatarCreator;
        _resourceCleaner = resourceCleaner;
        _s3Settings = s3Options.Value;
        _environmentSettings = envOptions.Value;
    }

    public IO<CreateAvatarResponse> Execute(CreateAvatarRequest request, CancellationToken cancellationToken) =>
        from _1 in _resourceCleaner.Cleanup(cancellationToken).WithLogging(
            () => Log.Information("Cleaning started"),
            () => Log.Information("Cleaning succeed"),
            error => Log.Error(error, "Error cleaning resources"))
        from response in (
                from images in DownloadsImages(request.SourceImagesPaths, cancellationToken).WithLogging(
                    () => Log.Information("Start downloading images"),
                    () => Log.Information("Images downloaded"),
                    error => Log.Error(error, "Error downloading images"))
                from creationResult in _avatarCreator.CreateAvatar(cancellationToken).WithLogging(
                    () => Log.Information("Start creating avatar"),
                    () => Log.Information("Avatar created successfully"),
                    error => Log.Error(error, "Error creating avatar"))
                from bucketAvatarPath in UploadAvatar(creationResult.LocalAvatarPath, request.JobId, cancellationToken)
                    .WithLogging(
                        () => Log.Information("Start uploading avatar"),
                        () => Log.Information("Avatar uploaded successfully"),
                        error => Log.Error(error, "Error uploading avatar"))
                from _3 in _resourceCleaner.Cleanup(cancellationToken).WithLogging(
                    () => Log.Information("Cleaning started"),
                    () => Log.Information("Cleaning succeed"),
                    error => Log.Error(error, "Error cleaning resources"))
                select new CreateAvatarResponse(bucketAvatarPath, creationResult.AvatarDescription))
            .TapOnFail(_ => _resourceCleaner.Cleanup(cancellationToken).WithLogging(
                () => Log.Information("Start final cleanup of resources after error"),
                () => Log.Information("Final resources cleanup after error completed successfully"),
                cleanupErr => Log.Error(cleanupErr, "Final resource cleanup after error failed")))
        select response;

    private IO<Unit> DownloadsImages(Iterable<string> imagesKeys, CancellationToken cancellationToken) =>
        imagesKeys.Traverse(path => DownloadImage(path, cancellationToken)).IgnoreF().As();

    private IO<Unit> DownloadImage(string imageKey, CancellationToken cancellationToken) =>
        from stream in _objectStorage.GetObject(_s3Settings.SourceImagesBucketName, imageKey, cancellationToken)
            .WithLogging(
                () => Log.Information("Start getting object {ImageKey} from bucket {Bucket}", imageKey,
                    _s3Settings.SourceImagesBucketName),
                () => Log.Information("Successfully retrieved object {ImageKey} from bucket {Bucket}", imageKey,
                    _s3Settings.SourceImagesBucketName),
                error => Log.Error(error, "Error getting object {ImageKey} from bucket {Bucket}", imageKey,
                    _s3Settings.SourceImagesBucketName))
        let outputPath = Path.Combine(_environmentSettings.InputDir, Path.GetFileName(imageKey))
        from _1 in _fileService.WriteStreamToFile(outputPath, stream, cancellationToken).WithLogging(
            () => Log.Information("Start writing file to {FilePath}", outputPath),
            () => Log.Information("File written successfully to {FilePath}", outputPath),
            error => Log.Error(error, "Error writing file to {FilePath}", outputPath))
        from _2 in stream.DisposeAsyncIO()
        select _2;

    private IO<string> UploadAvatar(string avatarPath, string jobId, CancellationToken cancellationToken) =>
        from stream in _fileService.OpenRead(avatarPath).WithLogging(
            () => Log.Information("Start reading avatar file from {FilePath}", avatarPath),
            () => Log.Information("Avatar file read successfully from {FilePath}", avatarPath),
            error => Log.Error(error, "Error reading avatar file from {FilePath}", avatarPath))
        let key = $"jobs/{jobId}/{Path.GetFileName(avatarPath)}"
        from _1 in _objectStorage.PutObject(_s3Settings.AvatarsBucketName, key, stream, cancellationToken).WithLogging(
            () => Log.Information("Start uploading avatar with key {Key} to bucket {Bucket}", key,
                _s3Settings.AvatarsBucketName),
            () => Log.Information("Avatar uploaded successfully with key {Key} to bucket {Bucket}", key,
                _s3Settings.AvatarsBucketName),
            error => Log.Error(error, "Error uploading avatar with key {Key} to bucket {Bucket}", key,
                _s3Settings.AvatarsBucketName))
        from _2 in stream.DisposeAsyncIO()
        select key;
}