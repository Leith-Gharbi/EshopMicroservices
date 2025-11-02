using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Elasticsearch;

namespace BuildingBlocks.Logging;

public static class LoggingExtensions
{
    public static WebApplicationBuilder AddSerilogLogging(this WebApplicationBuilder builder)
    {

        #region Permet d’enrichir les logs (ajouter ces infos comme champs dans chaque message de log).
        var environment = builder.Environment.EnvironmentName;
        var configuration = builder.Configuration;
        var applicationName = builder.Environment.ApplicationName;

        #endregion


        // Configure Serilog
        Log.Logger = new LoggerConfiguration()                        //Configuration du logger global
            .ReadFrom.Configuration(configuration) //Charge les paramètres depuis appsettings.json (ou appsettings.Development.json).
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Environment", environment)
            .Enrich.WithProperty("ApplicationName", applicationName)
            .Enrich.WithMachineName()
            .WriteTo.Console(



                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"
            )
            .WriteTo.File(
                path: $"logs/{applicationName}-{environment}-.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}",
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 10485760 // 10MB
            )
            .WriteTo.Elasticsearch(ConfigureElasticsearch(configuration, environment, applicationName))
            .CreateLogger();

        builder.Host.UseSerilog();

        return builder;
    }

    private static ElasticsearchSinkOptions ConfigureElasticsearch(
        IConfiguration configuration,
        string environment,
        string applicationName)
    {
        var elasticsearchUrl = configuration["ElasticConfiguration:Uri"] ?? "http://localhost:9200";

        var options = new ElasticsearchSinkOptions(new Uri(elasticsearchUrl))
        {
            AutoRegisterTemplate = true,  //→ Serilog crée automatiquement le template d’index dans Elastic.
            AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
            IndexFormat = $"eshop-microservices-{applicationName.ToLower()}-{environment.ToLower()}-{DateTime.UtcNow:yyyy-MM}",
            NumberOfShards = 2,
            NumberOfReplicas = 1,
            MinimumLogEventLevel = LogEventLevel.Information,
            EmitEventFailure = EmitEventFailureHandling.WriteToSelfLog | EmitEventFailureHandling.RaiseCallback,
            FailureCallback = (logEvent, ex) =>
            {
                Console.WriteLine($"Unable to submit event to Elasticsearch: {ex?.Message}");
                Console.WriteLine($"Failed log event: {logEvent?.MessageTemplate?.Text}");
            },
            // Batch settings for better performance
            BatchPostingLimit = 50, //→ Envoie les logs par paquets de 50 toutes les 2 secondes (meilleure perf).
            Period = TimeSpan.FromSeconds(2),
            // Custom fields to be logged
            ModifyConnectionSettings = x => x
                .BasicAuthentication(
                    configuration["ElasticConfiguration:Username"],
                    configuration["ElasticConfiguration:Password"]
                )
        };

        return options;
    }
}
