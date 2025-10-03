using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Infrastructure.Providers.Base;

internal static class RssReader
{
    public static async Task<IReadOnlyList<SyndicationItem>> LoadAsync(string feedUrl, CancellationToken ct)
    {
        // SyndicationFeed.Load is sync; wrap in Task.Run to avoid blocking the thread pool
        return await Task.Run(() =>
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
            using var xr = XmlReader.Create(feedUrl, settings);
            var feed = SyndicationFeed.Load(xr);
            return (IReadOnlyList<SyndicationItem>)(feed?.Items?.ToList() ?? new List<SyndicationItem>());
        }, ct);
    }
}