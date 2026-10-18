using Serilog;
using WebCrawler.Configuration;
using WebCrawler.Core.Interfaces;
using WebCrawler.Extensions;
using WebCrawler.Infrastructure.Policies;
using WebCrawler.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
// Initialize application
var configuration = BuildConfiguration();
ConfigureLogging(configuration);

int exitCode = 0;
try
{
    var serviceProvider = ConfigureServices(configuration);
    exitCode = await RunApplicationAsync(serviceProvider);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    exitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}

return exitCode;

#region Setup

IConfiguration BuildConfiguration()
{
    return new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddEnvironmentVariables()
        .Build();
}

void ConfigureLogging(IConfiguration configuration)
{
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(configuration.GetSection("Serilog"))
        .Enrich.FromLogContext()
        .CreateLogger();
}

IServiceProvider ConfigureServices(IConfiguration configuration)
{
    var services = new ServiceCollection();

    // Configure logging
    services.AddLogging(config => config.AddSerilog(Log.Logger));

    // Configure application options
    services.Configure<WebCrawlerSettings>(configuration.GetSection("WebCrawlerSettings"));
    services.Configure<HttpClientPolicies>(configuration.GetSection("HttpClientPolicies"));

    // Add crawler services
    services.AddCrawlerInfrastructure(configuration);
    services.AddCrawlerCore();

    return services.BuildServiceProvider();
}

#endregion

#region Execution

async Task<int> RunApplicationAsync(IServiceProvider serviceProvider)
{
    try
    {
        var orchestrator = serviceProvider.GetRequiredService<IApplicationOrchestrator>();
        return await orchestrator.RunAsync();
    }
    catch (Exception ex)
    {
        var exceptionHandler = serviceProvider.GetRequiredService<IExceptionHandler>();
        exceptionHandler.Handle(ex);
        Log.Fatal(ex, "Application failed with an unhandled exception");
        return 1;
    }
}

#endregion

