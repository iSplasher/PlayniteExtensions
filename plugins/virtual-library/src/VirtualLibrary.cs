using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Reflection;
using System.Security.Policy;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using System.Web;

namespace Plugin
{
    using Settings = GameSettings;
    using SettingsViewModel = GameSettingsViewModel;

    public static partial class NotificationID
    {
        public const string GameAdded = "VirtualLibrary_GameAdded";
    }

    public class VirtualLibrary
    {
        public static readonly ILogger log = LogManager.GetLogger();

        private const string ID_PREFIX = "virtual-library://";

        ConcurrentDictionary<string, Game> library { get; set; } = new ConcurrentDictionary<string, Game>();
        public Settings Settings { get; set; }

        public readonly Action RefreshLibrary;

        public VirtualLibrary(Settings settings)
        {
            Settings = settings;
            RefreshLibrary = Common.FuncUtils.Debounce(refreshLibrary, 300);

            Plugin.API.Database.Games.ItemCollectionChanged += (sender, args) =>
            {
                RefreshLibrary();
            };
        }

        public Game[] ImportLibrary()
        {
            log.Trace("ImportLibrary");
            return new Game[0];
        }

        public GameMetadata[] GetLibrary()
        {
            log.Trace("GetLibrary");
            return new GameMetadata[0];
        }

        public void refreshLibrary()
        {
            log.Trace("Refreshing virtual library");
            var games = new ConcurrentDictionary<string, Game>();
            foreach (var game in Plugin.API.Database.Games)
            {
                var gid = ResolveGameId(game, true);
                foreach (var id in gid)
                {
                    games[id] = game;
                }

            }
            library = games;
        }

        private string[] ResolveGameId(Game game, bool all = false)
        {
            var ids = new List<string>();
            if (!String.IsNullOrEmpty(game.GameId) && game.GameId.StartsWith(ID_PREFIX))
            {
                ids.Add(game.GameId);
            }

            if (ids.Count == 0 || all)
            {
                if (!String.IsNullOrEmpty(game.Name) && (ids.Count == 0 || all))
                {
                    ids.Add(ID_PREFIX + game.Name.ToLower());
                }

                if (game.Id != null && (ids.Count == 0 || all))
                {
                    ids.Add(ID_PREFIX + game.Id.ToString());
                }
            }

            return ids.ToArray();
        }


        private void OnRequestMetadata(MetadataPlugin plugin, Game game, MetadataRequestOptions opts, OnDemandMetadataProvider provider, GetMetadataFieldArgs args)
        {
            var fields = provider.AvailableFields;
            var emptyFields = Common.PluginUtils.GetEmptyMetadataFields(game);

            foreach (var field in fields)
            {
                if (emptyFields.Contains(field))
                {
                    try
                    {
                        Common.PluginUtils.ApplyMetadata(game, field, provider, args);
                    }
                    catch (Exception e)
                    {
                        log.Error(e, $"Failed to apply metadata {field} from plugin {plugin.Name} for {game.Name}: ${e.Message}");
                    }
                }
            }
        }

        public Game Match(Resolver.MatchOptions options)
        {
            log.Info($"Matching {options}");
            var resolvers = Resolver.ResolverBuilder.GetResolvers();

            Game game = null;
            Resolver.IResolver matchedResolver = null;

            Plugin.plugin.sidebarItem.ProgressMaximum += resolvers.Length;
            var p = 0;

            foreach (var resolver in resolvers)
            {
                var ropts = options.Clone();
                if (resolver.Match(ropts))
                {
                    log.Trace($"Resolver {resolver.Name} matched {options}");
                    game = Resolve(resolver, ropts);
                    if (game != null)
                    {
                        matchedResolver = resolver;
                        Plugin.plugin.sidebarItem.ProgressValue += resolvers.Length - p;
                        break;
                    }
                    else
                    {
                        log.Warn($"Resolver {resolver.Name} matched {options} but failed to resolve game.");
                    }
                }
                else
                {
                    log.Trace($"Resolver {resolver.Name} did not match {options}");
                }

                Plugin.plugin.sidebarItem.ProgressValue = ++p;
            }

            if (game == null)
            {
                log.Warn($"No resolvers returned a game for {options}");
            }
            else
            {
                log.Info($"Resolved game [{game.Name}] - [{game.GameId}] with resolver '{matchedResolver.Name}'");
            }

            return game;
        }

        public Game Match(string value)
        {
            var v = value.Trim();
            Uri uri = null;
            if (Common.Stringutils.IsUrl(v))
            {
                uri = Common.Stringutils.ToUri(HttpUtility.UrlDecode(v));
            }

            var opts = new Resolver.MatchOptions();
            opts.Url = uri;
            opts.Name = uri == null ? v : null;

            return Match(opts);
        }

        private Game Resolve(Resolver.IResolver resolver, Resolver.MatchOptions options)
        {
            var applied = false;

            var game = new Game();
            game.PluginId = Plugin.plugin.Id;

            var args = new GetMetadataFieldArgs();
            var backgroundDownload = false;
            var reqopts = new MetadataRequestOptions(game, backgroundDownload);
            var resolveopts = new Resolver.ResolveOptions(options.Url, reqopts, options);

            var provider = resolver.Resolve(game, resolveopts);
            if (provider == null)
            {
                log.Warn($"Resolver {resolver.Name} returned null provider for {options}");
                return null;
            }

            var emptyFields = Common.PluginUtils.GetEmptyMetadataFields(game);
            var fields = provider.AvailableFields;
            if (fields.Where(f => f == MetadataField.Name).Any())
            {
                fields.Remove(MetadataField.Name);
                fields.Insert(0, MetadataField.Name);
            }


            foreach (var field in fields)
            {
                if (emptyFields.Contains(field))
                {
                    try
                    {
                        if (Common.PluginUtils.ApplyMetadata(game, field, provider, args))
                        {
                            applied = true;
                            log.Debug($"Applied metadata {field} from resolver {resolver.Name}");
                        }
                    }
                    catch (Exception e)
                    {
                        log.Error(e, $"Failed to apply metadata {field} from resolver {resolver.Name}: ${e.Message}");
                    }

                    if (field == MetadataField.Name && !applied)
                    {
                        break;
                    }
                }
            }

            if (String.IsNullOrEmpty(game.Name))
            {
                log.Warn($"Resolver {resolver.Name} did not apply a name for {options}");
                return null;
            }

            if (applied)
            {
                var gid = resolver.ResolveGameId(game, resolveopts);
                if (!String.IsNullOrEmpty(gid))
                {
                    game.GameId = $"{ID_PREFIX}{gid}";
                }

                var source = resolver.ResolveGameSource(game, resolveopts);
                if (!String.IsNullOrEmpty(source))
                {
                    var n = Plugin.API.Database.Sources.Where(s => s.Name == source).FirstOrDefault();
                    if (n == null)
                    {
                        n = new GameSource(source);
                        Plugin.API.Database.Sources.Add(n);
                    }
                    game.SourceId = n.Id;
                }
            }

            return applied ? game : null;
        }

        private void RequestMetadata(Game game)
        {

            Task.Run(async () =>
            {
                try
                {
                    var g = new Game();
                    g.Name = game.Name;
                    g.PluginId = game.PluginId;
                    g.SourceId = game.SourceId;

                    await Common.PluginUtils.RequestMetadata(g, OnRequestMetadata);
                    Common.PluginUtils.MergeMetadata(game, g);
                }
                catch (Exception e)
                {
                    log.Error(e, $"Failed to request metadata for {game.Name}: {e.Message}");
                }

            }).ContinueWith((t) =>
            {
                log.Trace($"Updating game {game.Name}");
                Plugin.API.Database.Games.Update(game);
                Plugin.plugin.sidebarItem.ProgressValue = 0;
                Plugin.plugin.sidebarItem.ProgressMaximum = 1;
            });
        }

        public bool AddGame(Game game)
        {
            log.Info($"Adding game {game.GameId} = {game.Name}");

            foreach (var id in ResolveGameId(game, true))
            {
                if (library.ContainsKey(id))
                {
                    log.Warn("Game already exists.");
                    return false;
                }
            }

            Plugin.API.Database.Games.Add(game);

            var downloadMetadata = Settings.DownloadMetadata == DownloadMetadataValue.Always ||
                (Settings.DownloadMetadata == DownloadMetadataValue.Default && Plugin.API.ApplicationSettings.DownloadMetadataOnImport);

            library.AddOrUpdate(game.GameId, game, (k, v) => game);
            log.Info($"Added game {game.GameId} = {game.Name}");

            if (true)
            {
                Plugin.API.Notifications.Add(NotificationID.GameAdded, $"Added {game.Name}", NotificationType.Info);
            }

            if (downloadMetadata)
            {
                RequestMetadata(game);
            }

            return true;
        }
    }

    public partial class Plugin
    {
        private VirtualLibrary virtualLibrary;

        public SidebarItem sidebarItem { get; private set; } = null;
        public TopPanelItem topPanelItem { get; private set; } = null;

        partial void BaseSetup(IPlayniteAPI api);

        static partial void ApplyPartialProps()
        {
            PartialProperties.HasSettings = false;
        }

        partial void Setup(IPlayniteAPI API)
        {
            virtualLibrary = new VirtualLibrary(settings);

            CreateUiElements();
        }

        private void AddVirtualGame(string value)
        {
            sidebarItem.ProgressValue = 0;
            sidebarItem.ProgressMaximum = 1;

            var game = virtualLibrary.Match(value);

            if (game != null)
            {
                virtualLibrary.AddGame(game);
            }

            sidebarItem.ProgressValue = 0;
            sidebarItem.ProgressMaximum = 1;
        }

        private void OnAddVirtualGame()
        {
            var resolvers = Resolver.ResolverBuilder.GetResolvers();
            var examples = resolvers.SelectMany(a => a.ExamplePatterns).ToArray();

            var examplesTxt = "";
            if (examples.Length > 0)
            {
                examplesTxt = "\nPatterns:\n" + String.Join("\n", examples.Select(v => "- " + v)) + "\n";
            }

            var r = Plugin.API.Dialogs.SelectString(
                            $"Enter source.{examplesTxt}",
                            "Add Virtual Game",
                            ""
                        );

            if (!r.Result || String.IsNullOrEmpty(r.SelectedString))
            {
                return;
            }

            var value = r.SelectedString.Trim();

            Task.Run(() =>
            {
                using (Common.GuardedAction.Call(() =>
                {
                    AddVirtualGame(value);
                })) { }
            });
        }

        private void CreateUiElements()
        {
            sidebarItem = new SidebarItem
            {
                Type = SiderbarItemType.Button,
                Title = "Add Virtual Game",
                Icon = new TextBlock
                {
                    Text = char.ConvertFromUtf32(0xefc2),
                    FontSize = 20,
                    FontFamily = ResourceProvider.GetResource("FontIcoFont") as FontFamily
                },
                ProgressValue = 0,
                ProgressMaximum = 1,
                Visible = settings.ShowSidebar,
                Activated = OnAddVirtualGame
            };


            topPanelItem = new TopPanelItem
            {
                Title = "Add Virtual Game",
                Icon = new TextBlock
                {
                    Text = char.ConvertFromUtf32(0xefc2),
                    FontSize = 20,
                    FontFamily = ResourceProvider.GetResource("FontIcoFont") as FontFamily
                },
                Visible = settings.ShowTopPanel,
                Activated = OnAddVirtualGame
            };

        }

        partial void OnSettingsChangedPartial(Settings settings, string propertyName)
        {
            virtualLibrary.Settings = settings;
        }

        // --------------------------------------------------------------------

        partial void OnApplicationStartedPartial(OnApplicationStartedEventArgs args)
        {
            virtualLibrary.RefreshLibrary();
        }
        partial void OnLibraryUpdatedPartial(OnLibraryUpdatedEventArgs args)
        {
            log.Trace("OnLibraryUpdated");
            virtualLibrary.RefreshLibrary();
        }

        partial void GetSidebarItemsPartial(ref IEnumerable<SidebarItem> items)
        {
            items = new List<SidebarItem> { sidebarItem };
        }

        partial void GetTopPanelItemsPartial(ref IEnumerable<TopPanelItem> items)
        {
            items = new List<TopPanelItem> { topPanelItem };
        }

        partial void ImportGamesPartial(ref IEnumerable<Game> games, LibraryImportGamesArgs args)
        {
            games = virtualLibrary.ImportLibrary();
        }

    }
}
