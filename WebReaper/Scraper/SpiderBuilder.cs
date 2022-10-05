﻿using System.Net;
using Microsoft.Extensions.Logging;
using System.Net.Security;
using WebReaper.Abstractions.Parsers;
using WebReaper.Core.Parser;
using WebReaper.Core.Sinks;
using WebReaper.Core.LinkTracker;
using WebReaper.Core.Loaders;
using Microsoft.Extensions.Logging.Abstractions;
using WebReaper.Core.Spiders;
using Microsoft.Azure.Cosmos;
using WebReaper.Abstractions.Spider;
using WebReaper.Abstractions.Sinks;
using WebReaper.Abstractions.LinkTracker;
using Newtonsoft.Json.Linq;
using WebReaper.Abstractions.Loaders;

namespace WebReaper.Core.Scraper;

public class SpiderBuilder
{
    public SpiderBuilder()
    {
        // default implementations
        Logger = NullLogger.Instance;
        ContentParser = new ContentParser(Logger);
        LinkParser = new LinkParserByCssSelector();
        SiteLinkTracker = new InMemoryCrawledLinkTracker();
        StaticPageLoader = new HttpPageLoader(HttpClient.Value, Logger);
        DynamicPageLoader = new PuppeteerPageLoader(Logger);
    }

    public List<IScraperSink> Sinks { get; } = new();

    protected int limit = int.MaxValue;

    protected ILogger Logger { get; set; }

    protected ILinkParser LinkParser { get; set; }

    protected ICrawledLinkTracker SiteLinkTracker { get; set; }

    protected IContentParser ContentParser { get; }
    
    public IPageLoader StaticPageLoader { get; set; }
    public IPageLoader DynamicPageLoader { get; set; }

    protected event Action<JObject> ScrapedData;

    protected static SocketsHttpHandler httpHandler = new()
    {
        MaxConnectionsPerServer = 10000,
        SslOptions = new SslClientAuthenticationOptions
        {
            // Leave certs unvalidated for debugging
            RemoteCertificateValidationCallback = delegate { return true; },
        },
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        PooledConnectionLifetime = Timeout.InfiniteTimeSpan
    };

    protected Lazy<HttpClient> HttpClient = new(() => new(httpHandler));

    protected List<string> UrlBlackList = new();

    public SpiderBuilder WithLogger(ILogger logger)
    {
        Logger = logger;
        return this;
    }

    public SpiderBuilder WithLinkTracker(ICrawledLinkTracker linkTracker)
    {
        SiteLinkTracker = linkTracker;
        return this;
    }

    public SpiderBuilder Authorize(Func<CookieContainer> authorize)
    {
        var cookieContainer = authorize();

        httpHandler.CookieContainer = cookieContainer;

        return this;
    }

    public SpiderBuilder AddSink(IScraperSink sink)
    {
        Sinks.Add(sink);
        return this;
    }

    public SpiderBuilder IgnoreUrls(params string[] urls)
    {
        UrlBlackList.AddRange(urls);
        return this;
    }

    public SpiderBuilder Limit(int limit)
    {
        this.limit = limit;
        return this;
    }

    public SpiderBuilder WriteToConsole() => AddSink(new ConsoleSink());

    public SpiderBuilder AddScrapedDataHandler(Action<JObject> eventHandler)
    {
        ScrapedData += eventHandler;
        return this;
    }

    public SpiderBuilder WriteToJsonFile(string filePath) => AddSink(new JsonFileSink(filePath));

    public SpiderBuilder WriteToCosmosDb(
        string endpointUrl,
        string authorizationKey,
        string databaseId,
        string containerId)
    {
        return AddSink(new CosmosSink(endpointUrl, authorizationKey, databaseId, containerId, Logger));
    }
    
    public SpiderBuilder WithStaticPageLoader(IPageLoader pageLoader)
    {
        StaticPageLoader = pageLoader;
        return this;
    }

    public SpiderBuilder WithBrowserPageLoader()
    {
        DynamicPageLoader = new PuppeteerPageLoader(Logger);
        return this;
    }

    public SpiderBuilder WriteToCsvFile(string filePath) => AddSink(new CsvFileSink(filePath));

    public ISpider Build()
    {
        ISpider spider = new WebReaperSpider(
            Sinks,
            LinkParser,
            ContentParser,
            SiteLinkTracker,
            StaticPageLoader,
            DynamicPageLoader,
            Logger)
        {
            UrlBlackList = UrlBlackList.ToList(),

            PageCrawlLimit = limit
        };

        spider.ScrapedData += ScrapedData;

        return spider;
    }
}
