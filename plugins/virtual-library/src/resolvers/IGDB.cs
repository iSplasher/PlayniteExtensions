using System;
using System.Collections.Generic;
using System.Net.Http;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using HtmlAgilityPack;
using Playnite.SDK;
using System.Web;
using System.Reflection;
using System.Threading.Tasks;


namespace Plugin
{

    namespace Resolver
    {

        static class IGDBNotificatonID
        {
            public const string HTTPError = "VL_IGDBHTTPError";
            public const string NeedPlugin = "VL_IGDBNeedPlugin";
        }

        public class IGDBMetadataProvider : OnDemandMetadataProvider
        {
            static public readonly ILogger log = LogManager.GetLogger();

            static private HttpClient client = Plugin.httpClient;

            private ResolveOptions options;
            private MetadataRequestOptions metadataRequestOptions;
            private Game game;

            private OnDemandMetadataProvider provider = null;
            private MetadataPlugin plugin = null;

            private HtmlDocument doc = null;
            private bool failed = false;

            public IGDBMetadataProvider(ResolveOptions options, MetadataPlugin plugin) : base()
            {
                this.options = options;
                this.game = options.MetadataRequestOptions.GameData;
                this.metadataRequestOptions = new MetadataRequestOptions(
                    options.MetadataRequestOptions.GameData.GetCopy(),
                    options.MetadataRequestOptions.IsBackgroundDownload
                    );
                this.plugin = plugin;
            }

            public override List<MetadataField> AvailableFields { get; } = new List<MetadataField>{
                MetadataField.Name,
                MetadataField.ReleaseDate,
            };


            public override void Dispose()
            {
                if (provider != null)
                {
                    provider.Dispose();
                }
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
                            Plugin.API.Notifications.Add(IGDBNotificatonID.HTTPError, $"Failed to load {options.Url}\t\n{e.Message}", NotificationType.Error);
                        }

                        failed = true;
                        log.Error(e, $"Failed to load {options.Url}: {e.Message}");
                        return false;
                    }
                }
                return true;
            }

            private OnDemandMetadataProvider GetProvider()
            {
                if (provider == null)
                {
                    provider = plugin.GetMetadataProvider(metadataRequestOptions);
                }
                return provider;
            }

            public override string GetName(GetMetadataFieldArgs args)
            {
                if (String.IsNullOrEmpty(options.MetadataRequestOptions.GameData.Name))
                {
                    var id = IGDB.resolveGameId(game, options);
                    if (id == null)
                    {
                        return null;

                    }
                    var name = id.Replace("-", " ");
                    metadataRequestOptions.GameData.Name = name;
                }
                return GetProvider().GetName(args);
            }

            public override ReleaseDate? GetReleaseDate(GetMetadataFieldArgs args)
            {
                return GetProvider().GetReleaseDate(args);
            }
        }

        public class IGDB : IResolver
        {

            public string Name { get; } = "IGDB";

            public string[] ExamplePatterns { get; } = new string[]{
                "https://www.igdb.com/games/{id}",
            };

            private static MetadataPlugin GetPlugin()
            {
                var plugin = Common.PluginUtils.GetPlugin<MetadataPlugin>(new Guid("000001db-dbd1-46c6-b5d0-b1ba559d10e4"));
                if (plugin == null)
                {
                    Plugin.API.Notifications.Add(IGDBNotificatonID.NeedPlugin, "IGDB metadata plugin is not installed", NotificationType.Error);
                }
                return plugin;
            }

            public bool Match(MatchOptions options)
            {
                if (GetPlugin() == null)
                {
                    return false;
                }
                return options.Url != null && options.Url.AbsoluteUri.Contains("igdb.com/games/");
            }

            public string ResolveGameSource(Game game, ResolveOptions options)
            {
                return "IGDB";
            }


            static public string resolveGameId(Game game, ResolveOptions options)
            {
                var url = options.Url;

                if (url.Segments.Length > 2)
                {

                    var id = url.Segments[2];
                    if (url.Segments[1].ToLower() == "games/")
                    {
                        if (id.EndsWith("/"))
                        {
                            id = id.Substring(0, id.Length - 1);
                        }
                        return id;
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
                var plugin = GetPlugin();
                if (plugin == null)
                {
                    return null;
                }

                return new IGDBMetadataProvider(options, plugin);
            }
        }

    }
}
