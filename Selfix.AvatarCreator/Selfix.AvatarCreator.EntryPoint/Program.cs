using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Selfix.AvatarCreator.Application;
using Selfix.AvatarCreator.EntryPoint.Extensions;
using Selfix.AvatarCreator.Infrastructure;
using Selfix.Jobs.Shared.Settings;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        formatProvider: CultureInfo.InvariantCulture)
    .CreateLogger();

try
{
    Log.Information("Starting application");
    HostApplicationBuilder builder = Host.CreateApplicationBuilder();

    builder
        .AddSettings<KafkaSettings>("Kafka")
        .AddSettings<S3Settings>("S3")
        .AddSettings<EnvironmentSettings>("Environment")
        .AddSettings<GenerationSettings>("Generation");

    ServiceProvider serviceProvider = builder.Services.BuildServiceProvider();
    KafkaSettings kafkaSettings = serviceProvider.GetRequiredService<IOptions<KafkaSettings>>().Value;
    GenerationSettings generationSettings = serviceProvider.GetRequiredService<IOptions<GenerationSettings>>().Value;
    
    builder.Logging.AddSerilog(new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(serviceProvider)
        .Enrich.FromLogContext()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
            formatProvider: CultureInfo.InvariantCulture)
        .CreateLogger());

    builder.Services
        .AddApplication()
        .AddInfrastructure(kafkaSettings, generationSettings);

    await builder.Build().RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}