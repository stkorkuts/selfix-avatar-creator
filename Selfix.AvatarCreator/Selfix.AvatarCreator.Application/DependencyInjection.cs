using Microsoft.Extensions.DependencyInjection;
using Selfix.AvatarCreator.Application.UseCases;

namespace Selfix.AvatarCreator.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services) =>
        services.AddTransient<CreateAvatarUseCase>();
}