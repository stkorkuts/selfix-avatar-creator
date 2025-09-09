using System.Globalization;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Confluent.Kafka;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Selfix.AvatarCreator.Application.Abstractions;
using Selfix.AvatarCreator.Infrastructure.EventStreaming;
using Selfix.AvatarCreator.Infrastructure.ObjectStorage;
using Selfix.Jobs.Shared.Settings;
using Selfix.Schema.Kafka.Jobs.Avatars.V1.AvatarCreation;
using Serilog;

namespace Selfix.AvatarCreator.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection collection, KafkaSettings kafkaSettings, GenerationSettings generationSettings) =>
        collection
            .AddTransient<IResourceCleaner, ResourceCleaner>()
            .AddSingleton<IDirectoryService, DirectoryService>()
            .AddSingleton<IFileService, FileService>()
            .AddSingleton<IObjectStorage, AmazonS3ObjectStorage>()
            .AddSingleton<IAvatarCreator, AvatarCreation.AvatarCreator>()
            .AddSingleton<IJsonSerializer, SystemJsonSerializer>(_ =>
                new SystemJsonSerializer(new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                }))
            .AddAmazonS3Client()
            .AddKafka(kafkaSettings, generationSettings);

    private static IServiceCollection AddAmazonS3Client(this IServiceCollection collection) =>
        collection.AddSingleton<IAmazonS3>(serviceProvider =>
        {
            S3Settings settings = serviceProvider.GetRequiredService<IOptions<S3Settings>>().Value;

            var credentials = new BasicAWSCredentials(settings.AccessKey, settings.SecretKey);
            var config = new AmazonS3Config
            {
                AuthenticationRegion = settings.Region,
                ServiceURL = settings.Endpoint,
                ForcePathStyle = true
            };

            return new AmazonS3Client(credentials, config);
        });

    private static IServiceCollection AddKafka(this IServiceCollection collection, KafkaSettings kafkaSettings, GenerationSettings generationSettings) =>
        collection.AddMassTransit(configurator =>
        {
            configurator.SetKebabCaseEndpointNameFormatter();
            configurator.AddSerilog();
            
            configurator.AddConfigureEndpointsCallback((_,_,cfg) =>
            {
                cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
            });
            
            configurator.UsingInMemory();
            configurator.AddRider(rider =>
            {
                rider.AddConsumer<AvatarCreationConsumer>();
                rider.AddProducer<CreateAvatarResponseEvent>(kafkaSettings.TopicOutput);

                rider.UsingKafka((context, kafka) =>
                {                
                    Log.Information("Configuring Kafka with bootstrap server: {Server}", kafkaSettings.BootstrapServer);
                    kafka.Host(kafkaSettings.BootstrapServer, host => host.UseSasl(sasl =>
                    {
                        SaslSettings saslSettings = kafkaSettings.Sasl;

                        sasl.Username = saslSettings.Username;
                        sasl.Password = saslSettings.Password;
                        sasl.Mechanism = Enum.Parse<SaslMechanism>(saslSettings.KafkaSaslMechanism, true);
                        sasl.SecurityProtocol = Enum.Parse<SecurityProtocol>(saslSettings.KafkaSecurityProtocol, true);
                        
                        Log.Information("Kafka SASL configured with mechanism: {Mechanism}, protocol: {Protocol}", 
                            saslSettings.KafkaSaslMechanism, saslSettings.KafkaSecurityProtocol);
                    }));

                    kafka.TopicEndpoint<CreateAvatarRequestEvent>(kafkaSettings.TopicInput, 
                        $"{kafkaSettings.GroupId}-{kafkaSettings.TopicInput}",
                        endpointConfigurator =>
                        {
                            Log.Information("Configuring consumer for topic: {Topic}, group: {Group}", 
                                kafkaSettings.TopicInput, kafkaSettings.GroupId);
                            endpointConfigurator.ConfigureConsumer<AvatarCreationConsumer>(context);
                            endpointConfigurator.AutoOffsetReset = AutoOffsetReset.Earliest;
                            endpointConfigurator.EnableAutoOffsetStore = false;
                            
                            endpointConfigurator.MaxPollInterval = TimeSpan.FromMinutes(5) + TimeSpan.FromMinutes(generationSettings.EpochsCount * 1.5);
                        });
                });
            });
        });
}