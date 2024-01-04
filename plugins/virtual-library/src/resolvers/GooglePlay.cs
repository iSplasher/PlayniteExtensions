using System;
using System.Collections.Generic;
using System.Net.Http;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using HtmlAgilityPack;
using Playnite.SDK;
using System.Web;

namespace Plugin
{

    namespace Resolver
    {

        static class GooglePlayNotificatonID
        {
            public const string HTTPError = "VL_GooglePlayHTTPError";
        }

        public class GooglePlayMetadataProvider : OnDemandMetadataProvider
        {
            static public readonly ILogger log = LogManager.GetLogger();

            static private HttpClient client = Plugin.httpClient;

            private ResolveOptions options;
            private Game game;

            private HtmlDocument doc = null;
            private bool failed = false;

            public GooglePlayMetadataProvider(ResolveOptions options) : base()
            {
                this.options = options;
                this.game = options.MetadataRequestOptions.GameData;
            }


            public override List<MetadataField> AvailableFields { get; } = new List<MetadataField>{
                MetadataField.Name,
                MetadataField.Platform,
                MetadataField.Publishers,
                MetadataField.Links,
            };


            public override void Dispose()
            {
                base.Dispose();
            }

            private bool Load()
            {
                if (failed)
                {
                    return false;
                }

                if (doc == null)
                {
                    try
                    {
                        var d = new HtmlDocument();
                        var t = client.GetAsync(options.Url);
                        var response = t.Result;
                        response.EnsureSuccessStatusCode();
                        var html = response.Content.ReadAsStringAsync().Result;
                        d.LoadHtml(html);
                        doc = d;
                    }
                    catch (Exception e)
                    {
                        if (!options.IsBackgroundDownload && e is HttpRequestException)
                        {
                            Plugin.API.Notifications.Add(GooglePlayNotificatonID.HTTPError, $"Failed to load {options.Url}\t\n{e.Message}", NotificationType.Error);
                        }

                        failed = true;
                        log.Error(e, $"Failed to load {options.Url}: {e.Message}");
                        return false;
                    }
                }
                return true;
            }

            public override string GetName(GetMetadataFieldArgs args)
            {
                if (Load())
                {
                    var name = doc.DocumentNode.SelectSingleNode("//h1");
                    if (name != null)
                    {
                        return HttpUtility.HtmlDecode(name.InnerText);
                    }

                }
                return null;
            }

            public override IEnumerable<MetadataProperty> GetPlatforms(GetMetadataFieldArgs args)
            {
                var platforms = new List<MetadataProperty>{
                    new MetadataNameProperty("Android"),
                };

                return platforms;
            }

            public override IEnumerable<MetadataProperty> GetPublishers(GetMetadataFieldArgs args)
            {
                var publishers = new List<MetadataProperty>();
                if (Load())
                {
                    do
                    {
                        var node = doc.DocumentNode.SelectSingleNode("//h1");
                        if (node == null)
                        {
                            break;
                        }
                        node = node.NextSibling;
                        if (node == null)
                        {
                            break;
                        }
                        node = node.FirstChild;
                        if (node == null)
                        {
                            break;
                        }

                        var publisher = HttpUtility.HtmlDecode(node.InnerText);
                        if (String.IsNullOrEmpty(publisher))
                        {
                            break;
                        }

                        publishers.Add(new MetadataNameProperty(publisher));
                        break;
                    } while (true);
                }
                return publishers;
            }

            public override IEnumerable<Link> GetLinks(GetMetadataFieldArgs args)
            {
                var links = new List<Link>();
                var gid = GooglePlay.resolveGameId(game, options);
                if (gid != null)
                {
                    links.Add(new Link("Google Play", $"https://play.google.com/store/apps/details?id={gid}"));
                }
                return links;
            }

        }

        public class GooglePlay : IResolver
        {

            public string Name { get; } = "Google Play";

            public string[] ExamplePatterns { get; } = new string[]{
                "https://play.google.com/store/apps/details?id={id}",
            };

            public bool Match(MatchOptions options)
            {
                return options.Url != null && options.Url.AbsoluteUri.Contains("play.google.com/store/apps/details");
            }

            public string ResolveGameSource(Game game, ResolveOptions options)
            {
                return "Google Play";
            }


            static public string resolveGameId(Game game, ResolveOptions options)
            {
                var queryParams = Common.UriUtils.ParseQuery(options.Url);
                foreach (string key in queryParams.AllKeys)
                {
                    if (key == "id")
                    {
                        return queryParams[key].Split(',')[0];
                    }
                }
                return null;
            }


            public string ResolveGameId(Game game, ResolveOptions options)
            {
                return resolveGameId(game, options);
            }

            public OnDemandMetadataProvider Resolve(Game game, ResolveOptions options)
            {
                return new GooglePlayMetadataProvider(options);
            }
        }

    }
}
