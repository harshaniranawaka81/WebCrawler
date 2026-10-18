using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebCrawler.Adapters;
using WebCrawler.Application.Services;
using WebCrawler.Configuration;
using WebCrawler.Core.Filters;
using WebCrawler.Core.Interfaces;
using WebCrawler.Core.Models;
using WebCrawler.Core.Services;
using WebCrawler.Infrastructure.Filters;

namespace WebCrawler.Extensions;

public static class CrawlerCoreServiceExtensions
{
    public static IServiceCollection AddCrawlerCore(this IServiceCollection services)
    {
        services.AddScoped<IOutputWriter, ConsoleOutputWriter>();
        services.AddScoped<IExceptionHandler, GlobalExceptionHandler>();
        services.AddScoped<IApplicationInteraction, ConsoleApplicationInteraction>();
        services.AddScoped<IApplicationOrchestrator, ApplicationOrchestrator>();

        services.AddScoped<IUrlFilterChain>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<UrlFilterChain>>();
            var robotsChecker = sp.GetRequiredService<IRobotsChecker>();

            var chain = new UrlFilterChain(logger);
            chain.AddFilter(new SameHostFilter(sp.GetRequiredService<ILogger<SameHostFilter>>()))
                  .AddFilter(new RobotsTextFilter(robotsChecker,
                      sp.GetRequiredService<ILogger<RobotsTextFilter>>()));

            return chain;
        });

        services.AddScoped<ICrawler>(sp => CreateCrawler(sp));

        return services;
    }

    private static ICrawler CreateCrawler(IServiceProvider sp)
    {
        return new Crawler(
            sp.GetRequiredService<IPageFetcher>(),
            sp.GetRequiredService<ILinkExtractor>(),
            sp.GetRequiredService<IOutputWriter>(),
            sp.GetRequiredService<ILinkStorage>(),
            sp.GetRequiredService<IExceptionHandler>(),
            sp.GetRequiredService<IRobotsChecker>(),
            sp.GetRequiredService<IOptions<WebCrawlerSettings>>().Value,
            sp.GetRequiredService<ILogger<Crawler>>());
    }
}
