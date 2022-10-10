﻿using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading.Channels;
using Newtonsoft.Json.Linq;
using WebReaper.Queue.Concrete.InMemory;
using WebReaper.Sinks.Concrete;
using WebReaper.Domain.Selectors;
using WebReaper.Queue.Abstract;
using WebReaper.Domain.Parsing;
using WebReaper.Domain;
using WebReaper.LinkTracker.Abstract;
using WebReaper.Sinks.Abstract;

namespace WebReaper.Core;

public class Scraper
{
    protected ScraperConfigBuilder ConfigBuilder { get; set; } = new();
    protected SpiderBuilder SpiderBuilder { get; set; } = new();
    protected ScraperRunner Runner { get; set; }

    protected ILogger Logger { get; set; } = NullLogger.Instance;

    protected IJobQueueReader JobQueueReader;

    protected IJobQueueWriter JobQueueWriter;

    private readonly Channel<Job> JobChannel = Channel.CreateUnbounded<Job>();

    public Scraper()
    {
        JobQueueReader = new JobQueueReader(JobChannel.Reader);
        JobQueueWriter = new JobQueueWriter(JobChannel.Writer);
    }

    public Scraper AddSink(IScraperSink sink)
    {
        SpiderBuilder.AddSink(sink);
        return this;
    }

    public Scraper Authorize(Func<CookieContainer> authorize)
    {
        SpiderBuilder.Authorize(authorize);
        return this;
    }

    public Scraper IgnoreUrls(params string[] urls)
    {
        SpiderBuilder.IgnoreUrls(urls);
        return this;
    }

    public Scraper Limit(int limit)
    {
        SpiderBuilder.Limit(limit);
        return this;
    }

    public Scraper WithLinkTracker(ICrawledLinkTracker linkTracker)
    {
        SpiderBuilder.WithLinkTracker(linkTracker);
        return this;
    }

    public Scraper WithLogger(ILogger logger)
    {
        SpiderBuilder.WithLogger(logger);
        ConfigBuilder.WithLogger(logger);

        Logger = logger;

        return this;
    }

    public Scraper WriteToConsole()
    {
        SpiderBuilder.WriteToConsole();
        return this;
    }

    public Scraper AddScrapedDataHandler(Action<JObject> eventHandler)
    {
        SpiderBuilder.AddScrapedDataHandler(eventHandler);
        return this;
    }

    public Scraper WriteToCosmosDb(
        string endpointUrl,
        string authorizationKey,
        string databaseId,
        string containerId)
    {
        SpiderBuilder.AddSink(new CosmosSink(endpointUrl, authorizationKey, databaseId, containerId, Logger));
        return this;
    }

    public Scraper WriteToCsvFile(string filePath)
    {
        SpiderBuilder.AddSink(new CsvFileSink(filePath));
        return this;
    }

    public Scraper WriteToJsonFile(string filePath)
    {
        SpiderBuilder.AddSink(new JsonFileSink(filePath));
        return this;
    }

    public Scraper Parse(Schema schema)
    {
        ConfigBuilder.WithScheme(schema);
        return this;
    }

    public Scraper WithStartUrl(string url, PageType pageType = PageType.Static, string? initScript = null)
    {
        ConfigBuilder.WithStartUrl(url, pageType, initScript);
        return this;
    }

    public Scraper FollowLinks(
        string linkSelector,
        PageType pageType = PageType.Static,
        string? script = null)
    {
        ConfigBuilder.FollowLinks(linkSelector, pageType, script);
        return this;
    }

    public Scraper FollowLinks(
        string linkSelector,
        string paginationSelector,
        PageType pageType = PageType.Static,
        string? script = null)
    {
        ConfigBuilder.FollowLinks(linkSelector, paginationSelector, pageType, script);
        return this;
    }

    public Scraper WithJobQueueWriter(IJobQueueWriter jobQueueWriter)
    {
        JobQueueWriter = jobQueueWriter;
        return this;
    }

    public Scraper WithJobQueueReader(IJobQueueReader jobQueueReader)
    {
        JobQueueReader = jobQueueReader;
        return this;
    }

    public async Task Run(int parallelismDegree)
    {
        var config = ConfigBuilder.Build();
        var spider = SpiderBuilder.Build();

        Runner = new ScraperRunner(config, JobQueueReader, JobQueueWriter, spider, Logger);

        await Runner.Run(parallelismDegree);
    }

    public async Task Stop()
    {
        await Runner.Stop();
    }
}