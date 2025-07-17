using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Our.Umbraco.InvisibleNodes.Routing;
using Our.Umbraco.InvisibleNodes.Core;
using Our.Umbraco.InvisibleNodes.Core.Caching;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Our.Umbraco.InvisibleNodes.Notifications;
using Our.Umbraco.InvisibleNodes.Caching;
using Umbraco.Cms.Core.Routing;

namespace Our.Umbraco.InvisibleNodes.Composing;

public class PackageComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // Register your content finder BEFORE the default one
        builder.ContentFinders()
            .InsertBefore<ContentFinderByUrlNew, InvisibleNodeContentFinder>();

        // Optional: Remove Umbraco's default one if yours should override completely
        builder.ContentFinders()
            .Remove<ContentFinderByUrlNew>();

        builder.UrlProviders()
            .Remove<DefaultUrlProvider>()
            .Insert<InvisibleNodeUrlProvider>();

        builder.UrlSegmentProviders()
            .Insert<InvisibleNodeUrlSegmentProvider>(0); // make sure it's first

        builder.Services
            .Configure<InvisibleNodeSettings>(builder.Config.GetSection(InvisibleNodeSettings.InvisibleNodes));

        builder.Services
            .AddSingleton<IInvisibleNodeLocator, InvisibleNodeLocator>()
            .AddSingleton<IInvisibleNodeRulesManager, InvisibleNodeRulesManager>();

        builder.Services
            .AddSingleton<IInvisibleNodeCache>(provider =>
            {
                var settings = provider.GetRequiredService<IOptions<InvisibleNodeSettings>>();
                return settings.Value.CachingEnabled
                    ? new InvisibleNodeCache(provider.GetRequiredService<AppCaches>())
                    : new NoOpNodeCache();
            });

        builder
            .AddNotificationHandler<ContentSavingNotification, InvalidateCacheNotificationHandler>()
            .AddNotificationHandler<ContentMovingNotification, InvalidateCacheNotificationHandler>()
            .AddNotificationHandler<ContentMovingToRecycleBinNotification, InvalidateCacheNotificationHandler>();
    }
}
