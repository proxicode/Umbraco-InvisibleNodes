using Our.Umbraco.InvisibleNodes;
using Our.Umbraco.InvisibleNodes.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Strings;
using Umbraco.Extensions;

public class InvisibleNodeUrlSegmentProvider : IUrlSegmentProvider
{
    private readonly IInvisibleNodeRulesManager _rules;
    private readonly IShortStringHelper _shortStringHelper;

    public InvisibleNodeUrlSegmentProvider(
        IInvisibleNodeRulesManager rules,
        IShortStringHelper shortStringHelper)
    {
        _rules = rules;
        _shortStringHelper = shortStringHelper;
    }

    public string? GetUrlSegment(IContentBase content, string? culture = null)
    {
        if (content is not IPublishedContent published)
            return null;

        if (!published.IsInvisibleNode(_rules))
            return published.UrlSegment(culture);

        // Fallback: generate a slug from the name using IShortStringHelper
        var fallbackSlug = _shortStringHelper.CleanStringForUrlSegment(
            published.Name ?? "node",
            culture: culture);

        return $"invisible-{fallbackSlug}";
    }
}
