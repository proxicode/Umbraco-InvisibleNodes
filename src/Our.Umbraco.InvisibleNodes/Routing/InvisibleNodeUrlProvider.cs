// Copyright 2023 Luke Fisher
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Our.Umbraco.InvisibleNodes.Core;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;
using static Umbraco.Cms.Core.Constants.Conventions;

namespace Our.Umbraco.InvisibleNodes.Routing;

public class InvisibleNodeUrlProvider : IUrlProvider
{
    private readonly IUmbracoContextAccessor _umbracoContextAccessor;
    private readonly IVariationContextAccessor _variationContextAccessor;
    private readonly ISiteDomainMapper _siteDomainMapper;
    private readonly IInvisibleNodeRulesManager _rulesManager;
    private readonly IOptions<RequestHandlerSettings> _requestHandlerOptions;
    private readonly ILogger<InvisibleNodeUrlProvider> _logger;

    public InvisibleNodeUrlProvider(
        IUmbracoContextAccessor umbracoContextAccessor,
        IVariationContextAccessor variationContextAccessor,
        ISiteDomainMapper siteDomainMapper,
        IInvisibleNodeRulesManager rulesManager,
        IOptions<RequestHandlerSettings> requestHandlerOptions,
        ILogger<InvisibleNodeUrlProvider> logger)
    {
        _siteDomainMapper = siteDomainMapper;
        _variationContextAccessor = variationContextAccessor;
        _umbracoContextAccessor = umbracoContextAccessor;
        _rulesManager = rulesManager;
        _requestHandlerOptions = requestHandlerOptions;
        _logger = logger;
    }

    /// <inheritdoc />
    public UrlInfo? GetUrl(IPublishedContent content, UrlMode mode, string? culture, Uri current)
    {
        if (!_umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext) ||
            umbracoContext.Domains is null ||
            umbracoContext.Content is null)
            return null;

        var domainCache = umbracoContext.Domains;
        string defaultCulture = domainCache.DefaultCulture;

        var matchingDomain = GetMatchingDomain(domainCache, content, current, culture);
        var root = matchingDomain is not null
            ? umbracoContext.Content.GetById(matchingDomain.ContentId)
            : umbracoContext.Content.GetAtRoot(culture).FirstOrDefault();

        if (root != null)
        {
            string route = GenerateRoute(content, root, null, true);
            var baseUri = new Uri(current.GetLeftPart(UriPartial.Authority));
            var uri = CombineUri(baseUri, route);

            if (uri is not null)
            {
                return ToUrlInfo(uri, UrlMode.Relative, null, baseUri);
            }
        }


        return null;
    }


    /// <inheritdoc />
    public IEnumerable<UrlInfo> GetOtherUrls(int id, Uri current)
    {
        if (!_umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext) ||
            umbracoContext.Content is null ||
            umbracoContext.Domains is null)
            return Enumerable.Empty<UrlInfo>();

        var content = umbracoContext.Content.GetById(id);

        if (content is null)
            return Enumerable.Empty<UrlInfo>();

        var domainCache = umbracoContext.Domains;
        string defaultCulture = domainCache.DefaultCulture;

        var mappedDomains = GetMatchingDomains(domainCache, content, current);

        var urls = new List<UrlInfo>();

        foreach (var mappedDomain in mappedDomains)
        {
            var root = umbracoContext.Content.GetById(mappedDomain.ContentId);
            string? culture = mappedDomain.Culture;

            // Fixed: Use true for includeNode to ensure consistent URL generation
            string route = GenerateRoute(content, root, culture, true);

            var uri = CombineUri(mappedDomain.Uri, route);

            if (uri is null)
                continue;

            var url = ToUrlInfo(uri, UrlMode.Absolute, culture, mappedDomain.Uri);

            urls.Add(url);
        }

        return urls;
    }

    /// <summary>
    /// Generates out the correct route based on the <see cref="InvisibleNodeRulesManager"/>
    /// </summary>
    /// <param name="content"></param>
    /// <param name="root"></param>
    /// <param name="culture"></param>
    /// <param name="includeNode"></param>
    /// <returns></returns>
    private string GenerateRoute(
        IPublishedContent content,
        IPublishedContent? root,
        string? culture,
        bool includeNode)
    {
        var segments = content.AncestorsOrSelf()
            .TakeWhile(n => root == null || n.Id != root.Id)
            .Where(n =>
            {
                bool excludedByRules = n.IsInvisibleNode(_rulesManager);
                bool shouldIncludeInUrl = IsVisible(n, root, content);

                _logger.LogDebug(
                    "Node: {Name} (ID: {Id}) - ExcludedByRules: {Excluded} - IncludedInUrl: {Included}",
                    n.Name, n.Id, excludedByRules, shouldIncludeInUrl);

                return shouldIncludeInUrl;
            })
            .Select(n => n.UrlSegment(_variationContextAccessor, culture))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Reverse()
            .ToList();

        return string.Join('/', segments).EnsureStartsWith("/");
    }

    /// <summary>
    /// Checks if the node is visible in the URL
    /// </summary>
    /// <param name="node"></param>
    /// <param name="root"></param>
    /// <param name="includeNode"></param>
    /// <returns></returns>
    private bool IsVisible(IPublishedContent node, IPublishedContent? root, IPublishedContent content)
    {
        if (node == root)
            return false;

        if (node.Id == content.Id)
            return true;

        return !node.IsInvisibleNode(_rulesManager);
    }

    /// <summary>
    /// Tries to locate the matching domain for the content
    /// </summary>
    /// <param name="content"></param>
    /// <param name="current"></param>
    /// <param name="culture"></param>
    /// <param name="domainCache"></param>
    /// <returns></returns>
    private DomainAndUri? GetMatchingDomain(
        IDomainCache domainCache,
        IPublishedContent content,
        Uri current,
        string? culture)
    {
        var domains = content.AncestorsOrSelf()
            .Select(node => domainCache.GetAssigned(node.Id, includeWildcards: false))
            .FirstOrDefault(domains => domains.Any());

        return DomainUtilities.SelectDomain(
            domains,
            current,
            culture,
            domainCache.DefaultCulture,
            _siteDomainMapper.MapDomain);
    }

    /// <summary>
    /// Tries to locate the matching domains for the content
    /// </summary>
    /// <param name="domainCache"></param>
    /// <param name="content"></param>
    /// <param name="current"></param>
    /// <returns></returns>
    private IEnumerable<DomainAndUri> GetMatchingDomains(
        IDomainCache domainCache,
        IPublishedContent content,
        Uri current)
    {
        var domainAndUris = content.AncestorsOrSelf()
            .SelectMany(node => domainCache.GetAssigned(node.Id, includeWildcards: false))
            .Select(domain => new DomainAndUri(domain, current))
            .ToArray();

        return _siteDomainMapper.MapDomains(domainAndUris, current, true, null, domainCache.DefaultCulture);
    }

    /// <summary>
    /// Converts to a <see cref="UrlInfo"/>
    /// </summary>
    /// <param name="uri"></param>
    /// <param name="mode"></param>
    /// <param name="culture"></param>
    /// <param name="current"></param>
    /// <returns></returns>
    private UrlInfo ToUrlInfo(Uri uri, UrlMode mode, string? culture, Uri current)
    {
        // Generate with an absolute path if the authorities do not match
        var newMode = mode == UrlMode.Absolute || !Equals(uri.Authority, current.Authority)
            ? UrlMode.Absolute
            : UrlMode.Relative;

        if (newMode != UrlMode.Absolute)
            return UrlInfo.Url(uri.AbsolutePath, culture);

        return UrlInfo.Url(uri.ToString(), culture);
    }

    /// <summary>
    /// Combines the <paramref name="uri"/> and <paramref name="relativePath"/>
    /// </summary>
    /// <param name="uri"></param>
    /// <param name="relativePath"></param>
    /// <returns></returns>
    private Uri? CombineUri(Uri uri, string relativePath)
    {
        // Combine the absolute path and relative path
        string combinedPath = CombinePaths(uri.AbsolutePath, relativePath)
            .EnsureStartsWith('/');

        // Ensure ends with trailing slash if configured
        string path = _requestHandlerOptions.Value.AddTrailingSlash
            ? combinedPath.EnsureEndsWith("/")
            : combinedPath;

        // Get the authority for the new Uri
        var authority = new Uri(uri.GetLeftPart(UriPartial.Authority));

        if (Uri.TryCreate(authority, path, out var absolute))
            return absolute;

        if (Uri.TryCreate(path, UriKind.Relative, out var relative))
            return relative;

        return null;
    }

    /// <summary>
    /// Combine two paths with the <paramref name="separator"/>
    /// </summary>
    /// <param name="first">first path</param>
    /// <param name="second">second path</param>
    /// <param name="separator">separator</param>
    /// <returns></returns>
    private string CombinePaths(string first, string second, char separator = '/') =>
        string.Join(separator, first.Trim(separator), second.Trim(separator));
}