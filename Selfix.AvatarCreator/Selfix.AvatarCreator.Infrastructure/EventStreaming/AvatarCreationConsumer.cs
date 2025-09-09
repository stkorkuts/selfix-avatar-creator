using System.Text.Json;
using LanguageExt;
using MassTransit;
using Microsoft.Extensions.Options;
using Selfix.AvatarCreator.Application.UseCases;
using Selfix.Jobs.Shared.Settings;
using Selfix.Schema.Kafka.Jobs.Avatars.V1.AvatarCreation;
using Serilog;

namespace Selfix.AvatarCreator.Infrastructure.EventStreaming;

internal sealed class AvatarCreationConsumer : IConsumer<CreateAvatarRequestEvent>
{
    private readonly CreateAvatarUseCase _useCase;
    private readonly ITopicProducer<CreateAvatarResponseEvent> _producer;

    public AvatarCreationConsumer(CreateAvatarUseCase useCase, ITopicProducer<CreateAvatarResponseEvent> producer) =>
        (_useCase, _producer) = (useCase, producer);

    public async Task Consume(ConsumeContext<CreateAvatarRequestEvent> context)
    {
        Log.Information("Received message with JobId: {JobId}", context.Message.JobId);

        CreateAvatarRequestEvent message = context.Message;
        CreateAvatarRequest request = new(message.JobId, message.SourceImagesPaths.AsIterable());

        try
        {
            var response = await _useCase
                .Execute(request, context.CancellationToken)
                .RunAsync();
            await _producer.Produce(new CreateAvatarResponseEvent
            {
                JobId = message.JobId,
                Success = new CreateAvatarResponseEventSuccessData
                {
                    AvatarPath = response.AvatarPath,
                    AvatarDescription = response.AvatarDescription,
                },
                IsSuccess = true
            }, context.CancellationToken);
        }
        catch (Exception ex)
        {
            await _producer.Produce(new CreateAvatarResponseEvent
            {
                JobId = message.JobId,
                Fail = new CreateAvatarResponseEventFailData { Error = ex.Message },
                IsSuccess = false
            }, context.CancellationToken);
        }
        finally
        {
            await context.NotifyConsumed(TimeSpan.Zero, nameof(AvatarCreationConsumer));
        }
    }
}