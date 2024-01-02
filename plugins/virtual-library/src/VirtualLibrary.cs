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

namespace Plugin
{
    using Settings = GameSettings;
    using SettingsViewModel = GameSettingsViewModel;

    public static partial class NotificationID
    {
    }

    public class VirtualLibrary
    {

        readonly ObservableConcurrentDictionary<string, Game> library = new ObservableConcurrentDictionary<string, Game>();

        public static readonly ILogger log = LogManager.GetLogger();


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

        private void OnRequestMetadata(Game game, MetadataRequestOptions opts, OnDemandMetadataProvider provider, GetMetadataFieldArgs args)
        {
            var fields = provider.AvailableFields;
            var emptyFields = Common.PluginUtils.GetEmptyMetadataFields(game);

            foreach (var field in fields)
            {
                if (!emptyFields.Contains(field))
                {
                    continue;
                }

                switch (field)
                {
                    case MetadataField.Name:
                        game.Name = provider.GetName(args);
                        break;
                    case MetadataField.Description:
                        game.Description = provider.GetDescription(args);
                        break;
                    case MetadataField.Developers:
                        {
                            if (game.DeveloperIds == null)
                            {
                                game.DeveloperIds = new List<Guid>();
                            }
                            foreach (var dev in (provider.GetDevelopers(args)))
                            {
                                if (dev.GetType() == typeof(MetadataIdProperty))
                                {
                                    var value = dev as MetadataIdProperty;

                                    if (!game.DeveloperIds.Contains(value.Id))
                                    {
                                        game.DeveloperIds.Add(value.Id);
                                    }
                                }
                                else if (dev.GetType() == typeof(MetadataNameProperty))
                                {
                                    var value = dev as MetadataNameProperty;
                                    if (!game.Developers.Where(a => a.Name.ToLower() == value.Name.ToLower()).Any())
                                    {
                                        var n = new Company(value.Name);
                                        Plugin.API.Database.Companies.Add(n);
                                        game.DeveloperIds.Add(n.Id);
                                    }
                                }
                            }
                            break;
                        }
                    case MetadataField.Publishers:
                        {
                            if (game.PublisherIds == null)
                            {
                                game.PublisherIds = new List<Guid>();
                            }
                            foreach (var pub in (provider.GetPublishers(args)))
                            {
                                if (pub.GetType() == typeof(MetadataIdProperty))
                                {
                                    var value = pub as MetadataIdProperty;

                                    if (!game.PublisherIds.Contains(value.Id))
                                    {
                                        game.PublisherIds.Add(value.Id);
                                    }
                                }
                                else if (pub.GetType() == typeof(MetadataNameProperty))
                                {
                                    var value = pub as MetadataNameProperty;
                                    if (!game.Publishers.Where(a => a.Name.ToLower() == value.Name.ToLower()).Any())
                                    {
                                        var n = new Company(value.Name);
                                        Plugin.API.Database.Companies.Add(n);
                                        game.PublisherIds.Add(n.Id);
                                    }
                                }
                            }
                            break;
                        }
                    case MetadataField.Genres:
                        {
                            if (game.GenreIds == null)
                            {
                                game.GenreIds = new List<Guid>();
                            }
                            foreach (var genre in (provider.GetGenres(args)))
                            {
                                if (genre.GetType() == typeof(MetadataIdProperty))
                                {
                                    var value = genre as MetadataIdProperty;

                                    if (!game.GenreIds.Contains(value.Id))
                                    {
                                        game.GenreIds.Add(value.Id);
                                    }
                                }
                                else if (genre.GetType() == typeof(MetadataNameProperty))
                                {
                                    var value = genre as MetadataNameProperty;
                                    if (!game.Genres.Where(a => a.Name.ToLower() == value.Name.ToLower()).Any())
                                    {
                                        var n = new Genre(value.Name);
                                        Plugin.API.Database.Genres.Add(n);
                                        game.GenreIds.Add(n.Id);
                                    }
                                }
                            }
                            break;
                        }
                    case MetadataField.ReleaseDate:
                        {
                            var date = provider.GetReleaseDate(args);
                            if (date != null)
                            {
                                game.ReleaseDate = date;
                            }
                            break;
                        }
                    case MetadataField.CriticScore:
                        {
                            var score = provider.GetCriticScore(args);
                            if (score != null)
                            {
                                game.CriticScore = score;
                            }
                            break;
                        }
                    case MetadataField.CommunityScore:
                        {
                            var score = provider.GetCommunityScore(args);
                            if (score != null)
                            {
                                game.CommunityScore = score;
                            }
                            break;
                        }
                    case MetadataField.Tags:
                        {
                            if (game.TagIds == null)
                            {
                                game.TagIds = new List<Guid>();
                            }
                            foreach (var tag in (provider.GetTags(args)))
                            {
                                if (tag.GetType() == typeof(MetadataIdProperty))
                                {
                                    var value = tag as MetadataIdProperty;

                                    if (!game.TagIds.Contains(value.Id))
                                    {
                                        game.TagIds.Add(value.Id);
                                    }
                                }
                                else if (tag.GetType() == typeof(MetadataNameProperty))
                                {
                                    var value = tag as MetadataNameProperty;
                                    if (!game.Tags.Where(a => a.Name.ToLower() == value.Name.ToLower()).Any())
                                    {
                                        var n = new Tag(value.Name);
                                        Plugin.API.Database.Tags.Add(n);
                                        game.TagIds.Add(n.Id);
                                    }
                                }
                            }
                            break;
                        }
                    case MetadataField.Series:
                        {
                            if (game.SeriesIds == null)
                            {
                                game.SeriesIds = new List<Guid>();
                            }
                            foreach (var series in (provider.GetSeries(args)))
                            {
                                if (series.GetType() == typeof(MetadataIdProperty))
                                {
                                    var value = series as MetadataIdProperty;

                                    if (!game.SeriesIds.Contains(value.Id))
                                    {
                                        game.SeriesIds.Add(value.Id);
                                    }
                                }
                                else if (series.GetType() == typeof(MetadataNameProperty))
                                {
                                    var value = series as MetadataNameProperty;
                                    if (!game.Series.Where(a => a.Name.ToLower() == value.Name.ToLower()).Any())
                                    {
                                        var n = new Series(value.Name);
                                        Plugin.API.Database.Series.Add(n);
                                        game.SeriesIds.Add(n.Id);
                                    }
                                }
                            }
                            break;
                        }
                    case MetadataField.AgeRating:
                        {
                            log.Error("AgeRating not implemented");
                            break;
                        }
                    case MetadataField.Region:
                        {
                            if (game.RegionIds == null)
                            {
                                game.RegionIds = new List<Guid>();
                            }

                            foreach (var region in (provider.GetRegions(args)))
                            {
                                if (region.GetType() == typeof(MetadataIdProperty))
                                {
                                    var value = region as MetadataIdProperty;

                                    if (!game.RegionIds.Contains(value.Id))
                                    {
                                        game.RegionIds.Add(value.Id);
                                    }
                                }
                                else if (region.GetType() == typeof(MetadataNameProperty))
                                {
                                    var value = region as MetadataNameProperty;
                                    if (!game.Regions.Where(a => a.Name.ToLower() == value.Name.ToLower()).Any())
                                    {
                                        var n = new Region(value.Name);
                                        Plugin.API.Database.Regions.Add(n);
                                        game.RegionIds.Add(n.Id);
                                    }
                                }
                            }
                            break;
                        }
                    case MetadataField.Platform:
                        {
                            if (game.PlatformIds == null)
                            {
                                game.PlatformIds = new List<Guid>();
                            }

                            foreach (var platform in (provider.GetPlatforms(args)))
                            {
                                if (platform.GetType() == typeof(MetadataIdProperty))
                                {
                                    var value = platform as MetadataIdProperty;

                                    if (!game.PlatformIds.Contains(value.Id))
                                    {
                                        game.PlatformIds.Add(value.Id);
                                    }
                                }
                                else if (platform.GetType() == typeof(MetadataNameProperty))
                                {
                                    var value = platform as MetadataNameProperty;
                                    if (!game.Platforms.Where(a => a.Name.ToLower() == value.Name.ToLower()).Any())
                                    {
                                        var n = new Platform(value.Name);
                                        Plugin.API.Database.Platforms.Add(n);
                                        game.PlatformIds.Add(n.Id);
                                    }
                                }
                            }
                            break;
                        }
                    case MetadataField.Icon:
                        {
                            var icon = provider.GetIcon(args);
                            if (icon != null)
                            {
                                game.Icon = icon.Path;
                            }
                            break;
                        }
                    case MetadataField.CoverImage:
                        {
                            var cover = provider.GetCoverImage(args);
                            if (cover != null)
                            {
                                game.CoverImage = cover.Path;
                            }
                            break;
                        }
                    case MetadataField.BackgroundImage:
                        {
                            var bg = provider.GetBackgroundImage(args);
                            if (bg != null)
                            {
                                game.BackgroundImage = bg.Path;
                            }
                            break;
                        }
                    case MetadataField.Links:
                        {
                            if (game.Links == null)
                            {
                                game.Links = new ObservableCollection<Link>();
                            }
                            foreach (var link in (provider.GetLinks(args)))
                            {
                                if (!game.Links.Where(a =>
                                a.Name.ToLower() == link.Name.ToLower() ||
                                a.Url.ToLower() == link.Url.ToLower()
                                ).Any())
                                {
                                    game.Links.Add(link);
                                }
                            }
                            break;
                        }
                }
            }
        }

        public List<bool> HandleLink(string link)
        {
            return new List<bool>();
        }


        public bool AddGame(GameMetadata metadata)
        {
            log.Info($"Adding game {metadata.GameId} = {metadata.Name}");

            if (library.ContainsKey(metadata.GameId))
            {
                log.Warn("Game already exists.");
                return false;
            }

            var game = Plugin.API.Database.ImportGame(metadata, Plugin.plugin);

            library.Add(metadata.GameId, game);


            Task.Run(async () => await Common.PluginUtils.RequestMetadata(game, OnRequestMetadata));

            log.Debug($"Added game {metadata.GameId} = {metadata.Name}");

            return true;
        }

        public bool AddGame(string value)
        {
            var metadata = new GameMetadata()
            {
                Name = "Granblue Fantasy: Relink",
                GameId = "Granblue Fantasy: Relink",
            };

            if (!AddGame(metadata))
            {
                metadata = new GameMetadata()
                {
                    Name = "Calculator",
                    GameId = "calc",
                    GameActions = new List<GameAction>
                    {
                        new GameAction()
                        {
                            Type = GameActionType.File,
                            Path = "calc.exe",
                            IsPlayAction = true
                        }
                    },
                    IsInstalled = true,
                    Icon = new MetadataFile(@"https://playnite.link/applogo.png"),
                    BackgroundImage = new MetadataFile(@"https://playnite.link/applogo.png")
                };

                return AddGame(metadata);
            }

            return true;
        }
    }

    public partial class Plugin
    {

        private VirtualLibrary virtualLibrary;

        partial void BaseSetup(IPlayniteAPI api);

        static partial void ApplyPartialProps()
        {
            PartialProperties.HasSettings = false;
        }

        partial void Setup(IPlayniteAPI API)
        {
            virtualLibrary = new VirtualLibrary();
        }


        private void OnAddVirtualGame()
        {
            log.Debug("add item activated.");



            virtualLibrary.AddGame("");
        }

        // --------------------------------------------------------------------

        partial void GetSidebarItemsPartial(ref IEnumerable<SidebarItem> items)
        {
            var r = new Random();
            var v = r.Next(0, 100);
            var l = new List<SidebarItem>();
            l.Add(new SidebarItem
            {
                Type = SiderbarItemType.Button,
                Title = "Add Virtual Game",
                Icon = new TextBlock
                {
                    Text = char.ConvertFromUtf32(0xefc2),
                    FontSize = 20,
                    FontFamily = ResourceProvider.GetResource("FontIcoFont") as FontFamily
                },
                // Random value
                ProgressValue = 0,
                Visible = settings.ShowSidebar,
                Activated = OnAddVirtualGame
            });
            items = l;
        }

        partial void GetTopPanelItemsPartial(ref IEnumerable<TopPanelItem> items)
        {
            var l = new List<TopPanelItem>();
            l.Add(new TopPanelItem
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
            });
            items = l;
        }

        partial void ImportGamesPartial(ref IEnumerable<Game> games, LibraryImportGamesArgs args)
        {
            games = virtualLibrary.ImportLibrary();
        }

    }
}
