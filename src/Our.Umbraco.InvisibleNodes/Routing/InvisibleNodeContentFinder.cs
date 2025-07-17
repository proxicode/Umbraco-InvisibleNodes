using Microsoft.Extensions.Logging;
using Our.Umbraco.InvisibleNodes.Core.Caching;
using Our.Umbraco.InvisibleNodes.Core;
using System.Threading.Tasks;
using System;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using System.Linq;

public class InvisibleNodeContentFinder : IContentFinder
{
    private readonly IUmbracoContextAccessor _uContextAccessor;
    private readonly IInvisibleNodeCache _cache;
    private readonly IInvisibleNodeLocator _locator;
    private readonly ILogger<InvisibleNodeContentFinder> _logger;

    public InvisibleNodeContentFinder(
        IUmbracoContextAccessor uContextAccessor,
        IInvisibleNodeCache cache,
        IInvisibleNodeLocator locator,
        ILogger<InvisibleNodeContentFinder> logger)
    {
        _uContextAccessor = uContextAccessor;
        _cache = cache;
        _locator = locator;
        _logger = logger;
    }

    public Task<bool> TryFindContent(IPublishedRequestBuilder request)
    {
        if (!_uContextAccessor.TryGetUmbracoContext(out var ctx))
        {
            _logger.LogWarning("No UmbracoContext available");
            return Task.FromResult(false);
        }

        var contentCache = ctx.Content;
        var uri = request.Uri;
        var host = uri.GetLeftPart(UriPartial.Authority);
        var path = uri.AbsolutePath;

        if (path == "/" || string.IsNullOrWhiteSpace(path))
        {
            var homeNode = contentCache.GetAtRoot(request.Culture).FirstOrDefault();
            if (homeNode is not null)
            {
                request.SetPublishedContent(homeNode);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        _logger.LogDebug("Routing {Host}{Path}", host, path);

        if (_cache.GetRoute(host, path) is int cachedId)
        {
            var cached = contentCache.GetById(cachedId);
            if (cached != null)
            {
                request.SetPublishedContent(cached);
                return Task.FromResult(true);
            }
            _cache.ClearRoute(host, path);
        }

        var root = request.Domain is not null
            ? contentCache.GetById(request.Domain.ContentId)
            : contentCache.GetAtRoot(request.Culture).FirstOrDefault();

        if (root == null)
        {
            _logger.LogWarning("No root node found");
            return Task.FromResult(false);
        }

        var found = _locator.Locate(root, path, request.Culture);
        if (found != null)
        {
            _cache.StoreRoute(host, path, found.Id);
            request.SetPublishedContent(found);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}
