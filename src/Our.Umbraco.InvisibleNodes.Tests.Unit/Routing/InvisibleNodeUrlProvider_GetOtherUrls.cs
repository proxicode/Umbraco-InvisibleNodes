// Copyright 2023 Luke Fisher
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Our.Umbraco.InvisibleNodes.Core;
using Our.Umbraco.InvisibleNodes.Routing;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Extensions;

namespace Our.Umbraco.InvisibleNodes.Tests.Unit.Routing;

public class InvisibleNodeUrlProvider_GetOtherUrls
{
    private static readonly IOptions<RequestHandlerSettings> RequestHandlerOptions = Options.Create(
        new RequestHandlerSettings
        {
            AddTrailingSlash = true,
        });

    [Fact]
    public void Should_Return_EmptyForMatchingRoot()
    {
        // Arrange
        var domains = UmbracoTestHelper.GenerateDomains(1, "example.org");

        var root = UmbracoTestHelper.GenerateNode(1, "Home", "");

        var umbracoContextAccessor = UmbracoTestHelper.GenerateUmbracoContextAccessor(
            domains: domains,
            content: root.AsEnumerableOfOne());

        var variationContextAccessor = new ThreadCultureVariationContextAccessor();
        var siteDomainMapper = new SiteDomainMapper();

        var mockRulesManager = new Mock<IInvisibleNodeRulesManager>();

        mockRulesManager
            .Setup(m => m.IsInvisibleNode(It.IsAny<IPublishedContent>()))
            .Returns(false);

        var uri = new Uri("https://example.org/");

        var mockLogger = new Mock<ILogger<InvisibleNodeUrlProvider>>();

        var rulesManager = new Mock<IInvisibleNodeRulesManager>();
        rulesManager
            .Setup(m => m.IsInvisibleNode(It.IsAny<IPublishedContent>()))
            .Returns(false);

        var provider = new InvisibleNodeUrlProvider(
            umbracoContextAccessor,
            variationContextAccessor,
            siteDomainMapper,
            rulesManager.Object,
            RequestHandlerOptions,
            mockLogger.Object);

        // Act
        var urls = provider.GetOtherUrls(root.Id, uri);

        // Assert
        urls.Should().NotBeNull();
        urls.Should().BeEmpty();
    }

    [Fact]
    public void Should_Return_1UrlForMatchingRoot()
    {
        // Arrange
        var domains = UmbracoTestHelper.GenerateDomains(1, "example.org", "example.com");

        var root = UmbracoTestHelper.GenerateNode(1, "Home", "");

        var umbracoContextAccessor = UmbracoTestHelper.GenerateUmbracoContextAccessor(
            domains: domains,
            content: root.AsEnumerableOfOne());

        var variationContextAccessor = new ThreadCultureVariationContextAccessor();
        var siteDomainMapper = new SiteDomainMapper();

        var mockRulesManager = new Mock<IInvisibleNodeRulesManager>();

        mockRulesManager
            .Setup(m => m.IsInvisibleNode(It.IsAny<IPublishedContent>()))
            .Returns(false);

        var uri = new Uri("https://example.org/");

        var mockLogger = new Mock<ILogger<InvisibleNodeUrlProvider>>();

        var rulesManager = new Mock<IInvisibleNodeRulesManager>();
        rulesManager
            .Setup(m => m.IsInvisibleNode(It.IsAny<IPublishedContent>()))
            .Returns(false);

        var provider = new InvisibleNodeUrlProvider(
            umbracoContextAccessor,
            variationContextAccessor,
            siteDomainMapper,
            rulesManager.Object,
            RequestHandlerOptions,
            mockLogger.Object);

        // Act
        var urls = provider.GetOtherUrls(root.Id, uri).ToArray();

        // Assert
        urls.Should()
            .NotBeNullOrEmpty()
            .And
            .ContainSingle(value => value.Text == "https://example.com/");
    }
}