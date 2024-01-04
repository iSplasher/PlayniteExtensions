using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using Playnite.SDK;
using System.Threading.Tasks;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.Threading;
using System.Collections.Specialized;
using System.Windows;
using System.Collections.ObjectModel;
using System.Net.Http;

namespace Plugin
{
    using PlaynitePlugin = Playnite.SDK.Plugins.Plugin;

    static partial class PartialProperties
    {
    }

    public partial class Plugin
    {
        public static readonly ILogger log = LogManager.GetLogger();

        public static readonly HttpClient httpClient = new HttpClient();

        static public IPlayniteAPI API { get; private set; }
        static public Plugin plugin { get; private set; }

        public override Guid Id
        {
            get
            {
                var ass = Meta.AssemblyUtils.GetAssembly();
                var id = Meta.AssemblyUtils.GetMetadataAttribute(ass, "PluginId");
                return Guid.Parse(id);
            }
        }

        static Plugin()
        {
#if DEV
            // Force exceptions to be in English
            log.Debug("Forcing english exceptions");
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Threading.Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;
#endif
            ApplyPartialProps();
        }

        static partial void ApplyPartialProps();

        partial void BaseSetup(IPlayniteAPI api)
        {
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/115.0");
            API = api;
            plugin = this;

            var ass = Meta.AssemblyUtils.GetAssembly();

            log.Debug($"Initializing {Meta.AssemblyUtils.GetTitle(ass)} v{Meta.AssemblyUtils.GetVersion(ass)}");
        }
    }

    namespace Meta
    {

        static public class AssemblyUtils
        {
            static public Assembly GetAssembly()
            {
                return typeof(Plugin).Assembly;
            }

            static public string GetVersion(Assembly assembly)
            {
                var version = assembly.GetName().Version;
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }

            static public string GetTitle(Assembly assembly)
            {
                return assembly.GetCustomAttribute<AssemblyTitleAttribute>().Title;
            }

            static public string GetDescription(Assembly assembly)
            {
                return assembly.GetCustomAttribute<AssemblyDescriptionAttribute>().Description;
            }

            static public string GetCompany(Assembly assembly)
            {
                return assembly.GetCustomAttribute<AssemblyCompanyAttribute>().Company;
            }

            static public string GetMetadataAttribute(Assembly assembly, string key)
            {
                var attrs = assembly.CustomAttributes.Where(a => a.AttributeType == typeof(AssemblyMetadataAttribute));

                if (attrs != null)
                {
                    var attr = attrs.Where(a => a.ConstructorArguments[0].Value.ToString() == key);

                    if (attr != null)
                    {
                        return attr.First().ConstructorArguments[1].Value.ToString();
                    }
                }

                return null;
            }

            public static IEnumerable<Type> GetTypesWithInterface<T>(Assembly asm)
            {
                var it = typeof(T);
                return asm.GetLoadableTypes().Where(p => it.IsAssignableFrom(p) && !p.IsInterface).ToList();
            }
        }

        public static class TypeLoaderExtensions
        {
            public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
            {
                if (assembly == null) throw new ArgumentNullException("assembly");
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    return e.Types.Where(t => t != null);
                }
            }
        }

    }

    namespace Common
    {
        using RequestMetadataCallback = Action<MetadataPlugin, Game, MetadataRequestOptions, OnDemandMetadataProvider, GetMetadataFieldArgs>;

        static class PluginUtils
        {
            public static readonly ILogger log = LogManager.GetLogger();

            public static MetadataField[] GetEmptyMetadataFields(Game game)
            {
                var fields = (MetadataField[])Enum.GetValues(typeof(MetadataField));

                return fields.Where(f =>
                {
                    switch (f)
                    {
                        case MetadataField.Name:
                            {
                                return GeneralUtils.IsNullOrEmpty(game.Name);
                            }
                        case MetadataField.Description:
                            {
                                return GeneralUtils.IsNullOrEmpty(game.Description);
                            }
                        case MetadataField.Developers:
                            {
                                return GeneralUtils.IsNullOrEmpty(game.Developers);
                            }
                        case MetadataField.Publishers:
                            {
                                return GeneralUtils.IsNullOrEmpty(game.Publishers);
                            }
                        case MetadataField.ReleaseDate:
                            {
                                return GeneralUtils.IsNullOrEmpty(game.ReleaseDate);
                            }
                        case MetadataField.CriticScore:
                            {
                                return GeneralUtils.IsNullOrEmpty(game.CriticScore);
                            }
                        case MetadataField.CommunityScore:
                            {
                                return GeneralUtils.IsNullOrEmpty(game.CommunityScore);
                            }
                        case MetadataField.Genres:
                            {
                                return GeneralUtils.IsNullOrEmpty(game.Genres);
                            }
                        case MetadataField.Tags:
                            {
                                return GeneralUtils.IsNullOrEmpty(game.Tags);
                            }
                        case MetadataField.CoverImage:
                            {
                                return GeneralUtils.IsNullOrEmpty(game.CoverImage);
                            }
                        case MetadataField.Icon:
                            {
                                return GeneralUtils.IsNullOrEmpty(game.Icon);
                            }
                        case MetadataField.BackgroundImage:
                            {
                                return GeneralUtils.IsNullOrEmpty(game.BackgroundImage);
                            }
                        case MetadataField.Links:
                            {
                                return GeneralUtils.IsNullOrEmpty(game.Links);
                            }
                        case MetadataField.Region:
                            {
                                return GeneralUtils.IsNullOrEmpty(game.Regions);
                            }
                        case MetadataField.AgeRating:
                            {
                                return GeneralUtils.IsNullOrEmpty(game.AgeRatings);
                            }
                        case MetadataField.Platform:
                            {
                                return GeneralUtils.IsNullOrEmpty(game.Platforms);
                            }
                        case MetadataField.Features:
                            {
                                return GeneralUtils.IsNullOrEmpty(game.Features);
                            }
                        case MetadataField.Series:
                            {
                                return GeneralUtils.IsNullOrEmpty(game.Series);
                            }
                        case MetadataField.InstallSize:
                            {
                                return GeneralUtils.IsNullOrEmpty(game.InstallSize);
                            }
                        default:
                            {
                                throw new Exception($"Unhandled metadata field {f}");
                            }
                    }
                }).ToArray();

            }

            public static void TriggerLibraryUpdated(PlaynitePlugin plugin)
            {
                log.Trace($"Calling OnLibraryUpdated handler for {plugin.Id}");
                Task.Run(() =>
                {
                    using (GuardedAction.Call(() =>
                   {
                       plugin.OnLibraryUpdated(new OnLibraryUpdatedEventArgs { });
                   })) { }
                });
            }

            public static void TriggerLibraryUpdated()
            {
                foreach (var plugin in Plugin.API.Addons.Plugins)
                {
                    TriggerLibraryUpdated(plugin);
                }
            }

            public static bool MergeMetadata(Game game, MetadataField field, IEnumerable<Guid> value)
            {
                if (value == null)
                {
                    return false;
                }
                return MergeMetadata(game, field, value.Select(a => new MetadataIdProperty(a)));
            }

            public static bool MergeMetadata(Game target, Game source)
            {
                var fields = (MetadataField[])Enum.GetValues(typeof(MetadataField));
                var s = false;

                try
                {

                    foreach (var f in fields)
                    {
                        switch (f)
                        {
                            case MetadataField.Name:
                                {
                                    s = MergeMetadata(target, f, source.Name);
                                    break;
                                }
                            case MetadataField.Description:
                                {
                                    s = MergeMetadata(target, f, source.Description);
                                    break;
                                }
                            case MetadataField.Developers:
                                {
                                    s = MergeMetadata(target, f, source.DeveloperIds);
                                    break;
                                }
                            case MetadataField.Publishers:
                                {
                                    s = MergeMetadata(target, f, source.PublisherIds);
                                    break;
                                }
                            case MetadataField.ReleaseDate:
                                {
                                    s = MergeMetadata(target, f, source.ReleaseDate);
                                    break;
                                }
                            case MetadataField.CriticScore:
                                {
                                    s = MergeMetadata(target, f, source.CriticScore);
                                    break;
                                }
                            case MetadataField.CommunityScore:
                                {
                                    s = MergeMetadata(target, f, source.CommunityScore);
                                    break;
                                }
                            case MetadataField.Genres:
                                {
                                    s = MergeMetadata(target, f, source.GenreIds);
                                    break;
                                }
                            case MetadataField.Tags:
                                {
                                    s = MergeMetadata(target, f, source.TagIds);
                                    break;
                                }
                            case MetadataField.CoverImage:
                                {
                                    s = MergeMetadata(target, f, source.CoverImage);
                                    break;
                                }
                            case MetadataField.Icon:
                                {
                                    s = MergeMetadata(target, f, source.Icon);
                                    break;
                                }
                            case MetadataField.BackgroundImage:
                                {
                                    s = MergeMetadata(target, f, source.BackgroundImage);
                                    break;
                                }
                            case MetadataField.Links:
                                {
                                    s = MergeMetadata(target, f, source.Links);
                                    break;
                                }
                            case MetadataField.Region:
                                {
                                    s = MergeMetadata(target, f, source.RegionIds);
                                    break;
                                }
                            case MetadataField.AgeRating:
                                {
                                    s = MergeMetadata(target, f, source.AgeRatingIds);
                                    break;
                                }
                            case MetadataField.Platform:
                                {
                                    s = MergeMetadata(target, f, source.PlatformIds);
                                    break;
                                }
                            case MetadataField.Features:
                                {
                                    s = MergeMetadata(target, f, source.FeatureIds);
                                    break;
                                }
                            case MetadataField.Series:
                                {
                                    s = MergeMetadata(target, f, source.SeriesIds);
                                    break;
                                }
                            case MetadataField.InstallSize:
                                {
                                    s = MergeMetadata(target, f, source.InstallSize);
                                    break;
                                }
                            default:
                                {
                                    throw new Exception($"Unhandled metadata field {f}");
                                }
                        }
                    }

                }
                catch (Exception e)
                {
                    log.Error(e, $"Failed to merge metadata");
                    throw;
                }

                return s;
            }

            public static bool MergeMetadata(Game game, MetadataField field, int? value)
            {
                var s = false;

                log.Trace($"Merging metadata on {field}");

                try
                {

                    switch (field)
                    {
                        case MetadataField.CriticScore:
                            {
                                var score = value;
                                if (score != null)
                                {
                                    game.CriticScore = score;
                                    s = true;
                                }
                                break;
                            }
                        case MetadataField.CommunityScore:
                            {
                                var score = value;
                                if (score != null)
                                {
                                    game.CommunityScore = score;
                                    s = true;
                                }
                                break;
                            }
                        default:
                            {
                                throw new Exception($"Unhandled metadata field {field}");
                            }
                    }

                    log.Trace($"Merged metadata on {field} = {s}");
                }
                catch (Exception e)
                {
                    log.Error(e, $"Failed to merge metadata on {field}");
                    throw;
                }

                return s;
            }

            public static bool MergeMetadata(Game game, MetadataField field, ReleaseDate? value)
            {
                var s = false;

                log.Trace($"Merging metadata on {field}");

                try
                {

                    switch (field)
                    {
                        case MetadataField.ReleaseDate:
                            {
                                var date = value;
                                if (date != null)
                                {
                                    game.ReleaseDate = date;
                                    s = true;
                                }
                                break;
                            }
                        default:
                            {
                                throw new Exception($"Unhandled metadata field {field}");
                            }
                    }

                    log.Trace($"Merged metadata on {field} = {s}");

                }
                catch (Exception e)
                {
                    log.Error(e, $"Failed to merge metadata on {field}");
                    throw;
                }

                return s;
            }

            public static bool MergeMetadata(Game game, MetadataField field, IEnumerable<Link> value)
            {
                var s = false;

                log.Trace($"Merging metadata on {field}");

                try
                {
                    if (value != null)
                    {
                        switch (field)
                        {
                            case MetadataField.Links:
                                {
                                    if (game.Links == null)
                                    {
                                        game.Links = new ObservableCollection<Link>();
                                        s = true;
                                    }
                                    foreach (var link in value)
                                    {
                                        if (!game.Links.Where(a =>
                                            a.Name.ToLower() == link.Name.ToLower() ||
                                            a.Url.ToLower() == link.Url.ToLower()
                                            ).Any())
                                        {
                                            game.Links.Add(link);
                                            s = true;
                                        }
                                    }
                                    break;
                                }
                            default:
                                {
                                    throw new Exception($"Unhandled metadata field {field}");
                                }
                        }
                    }

                    log.Trace($"Merged metadata on {field} = {s}");

                }
                catch (Exception e)
                {
                    log.Error(e, $"Failed to merge metadata on {field}");
                    throw;
                }

                return s;
            }

            public static bool MergeMetadata(Game game, MetadataField field, MetadataFile value)
            {
                var s = false;

                log.Trace($"Merging metadata on {field}");

                string fspath = null;


                if (game.Id != null && value != null && (value.HasContent || Stringutils.IsUrl(value.Path)))
                {

                    // generate guid
                    var fname = Guid.NewGuid().ToString() + Path.GetExtension(value.FileName ?? "");
                    fname = Path.Combine(Path.GetTempPath(), fname);
                    log.Trace($"Saving file to {fname}");

                    if (Stringutils.IsUrl(value.Path))
                    {
                        // download file
                        try
                        {
                            Plugin.httpClient.DefaultRequestHeaders.Referrer = new Uri(value.Path);
                            var t = Plugin.httpClient.GetAsync(value.Path);
                            var response = t.Result;
                            response.EnsureSuccessStatusCode();
                            var data = response.Content.ReadAsByteArrayAsync().Result;
                            value.Content = data;
                        }
                        catch (Exception e)
                        {
                            log.Error(e, $"Failed to download file from {value.Path}");
                            throw;
                        }
                    }

                    try
                    {
                        using (var fs = new FileStream(fname, FileMode.Create, FileAccess.Write))
                        {
                            fs.Write(value.Content, 0, value.Content.Length);
                        }
                    }
                    catch (Exception e)
                    {
                        log.Error(e, $"Failed to save file to {fname}");
                        throw;
                    }

                    fspath = Plugin.API.Database.AddFile(fname, game.Id);
                    log.Trace($"DB file for {game.Id}: {fspath}");
                }

                try
                {
                    switch (field)
                    {
                        case MetadataField.Icon:
                            {
                                var icon = value;
                                if (icon != null)
                                {
                                    game.Icon = fspath ?? icon.Path;
                                    s = true;
                                }
                                break;
                            }
                        case MetadataField.CoverImage:
                            {
                                var cover = value;
                                if (cover != null)
                                {
                                    game.CoverImage = fspath ?? cover.Path;
                                    s = true;
                                }
                                break;
                            }
                        case MetadataField.BackgroundImage:
                            {
                                var bg = value;
                                if (bg != null)
                                {
                                    game.BackgroundImage = fspath ?? bg.Path;
                                    s = true;
                                }
                                break;
                            }
                        default:
                            {
                                throw new Exception($"Unhandled metadata field {field}");
                            }
                    }

                    log.Trace($"Merged metadata on {field} = {s}");

                }
                catch (Exception e)
                {
                    log.Error(e, $"Failed to merge metadata on {field}");
                    throw;
                }
                finally
                {
                    if (!s && fspath != null)
                    {
                        Plugin.API.Database.RemoveFile(fspath);
                    }
                }

                return s;
            }
            public static bool MergeMetadata(Game game, MetadataField field, ulong? value)
            {
                var s = false;

                log.Trace($"Merging metadata on {field}");

                try
                {

                    switch (field)
                    {
                        case MetadataField.InstallSize:
                            {
                                var size = value;
                                if (size != null)
                                {
                                    game.InstallSize = size;
                                    s = true;
                                }
                                break;
                            }
                        default:
                            {
                                throw new Exception($"Unhandled metadata field {field}");
                            }
                    }

                    log.Trace($"Merged metadata on {field} = {s}");

                }
                catch (Exception e)
                {
                    log.Error(e, $"Failed to merge metadata on {field}");
                    throw;
                }

                return s;
            }

            public static bool MergeMetadata(Game game, MetadataField field, string value)
            {
                var s = false;

                log.Trace($"Merging metadata on {field}");
                try
                {
                    switch (field)
                    {
                        case MetadataField.Name:
                            {
                                if (String.IsNullOrEmpty(game.Name))
                                {
                                    game.Name = value;
                                    if (!String.IsNullOrEmpty(game.Name))
                                    {
                                        s = true;
                                    }
                                }
                                break;
                            }
                        case MetadataField.Description:
                            {
                                if (String.IsNullOrEmpty(game.Description))
                                {

                                    game.Description = value;
                                    if (!String.IsNullOrEmpty(game.Description))
                                    {
                                        s = true;
                                    }
                                }
                                break;
                            }
                        case MetadataField.CoverImage:
                            {
                                if (String.IsNullOrEmpty(game.CoverImage))
                                {
                                    game.CoverImage = value;
                                    if (!String.IsNullOrEmpty(game.CoverImage))
                                    {
                                        s = true;
                                    }
                                }
                                break;
                            }
                        case MetadataField.Icon:
                            {
                                if (String.IsNullOrEmpty(game.Icon))
                                {
                                    game.Icon = value;
                                    if (!String.IsNullOrEmpty(game.Icon))
                                    {
                                        s = true;
                                    }
                                }
                                break;
                            }
                        case MetadataField.BackgroundImage:
                            {
                                if (String.IsNullOrEmpty(game.BackgroundImage))
                                {
                                    game.BackgroundImage = value;
                                    if (!String.IsNullOrEmpty(game.BackgroundImage))
                                    {
                                        s = true;
                                    }
                                }
                                break;
                            }
                        default:
                            {
                                throw new Exception($"Unhandled metadata field {field}");
                            }
                    }
                    log.Trace($"Merged metadata on {field} = {s}");
                }
                catch (Exception e)
                {
                    log.Error(e, $"Failed to merge metadata on {field}");
                    throw;
                }

                return s;
            }

            public static bool MergeMetadata(Game game, MetadataField field, IEnumerable<MetadataProperty> values)
            {
                var s = false;

                log.Trace($"Merging metadata on {field}");

                try
                {

                    switch (field)
                    {
                        case MetadataField.Developers:
                            {
                                if (game.DeveloperIds == null)
                                {
                                    game.DeveloperIds = new List<Guid>();
                                }
                                foreach (var dev in values)
                                {
                                    if (dev.GetType() == typeof(MetadataIdProperty))
                                    {
                                        var value = dev as MetadataIdProperty;

                                        if (!game.DeveloperIds.Contains(value.Id))
                                        {
                                            game.DeveloperIds.Add(value.Id);
                                            s = true;
                                        }
                                    }
                                    else if (dev.GetType() == typeof(MetadataNameProperty))
                                    {
                                        var value = dev as MetadataNameProperty;
                                        if (game.Developers == null || !game.Developers.Where(a => a.Name.ToLower() == value.Name.ToLower()).Any())
                                        {
                                            var n = Plugin.API.Database.Companies.Where(a => a.Name.ToLower() == value.Name.ToLower()).FirstOrDefault();
                                            if (n == null)
                                            {
                                                n = new Company(value.Name);
                                                Plugin.API.Database.Companies.Add(n);
                                            }

                                            game.DeveloperIds.Add(n.Id);
                                            s = true;
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
                                foreach (var pub in values)
                                {
                                    if (pub.GetType() == typeof(MetadataIdProperty))
                                    {
                                        var value = pub as MetadataIdProperty;

                                        if (!game.PublisherIds.Contains(value.Id))
                                        {
                                            game.PublisherIds.Add(value.Id);
                                            s = true;
                                        }
                                    }
                                    else if (pub.GetType() == typeof(MetadataNameProperty))
                                    {
                                        var value = pub as MetadataNameProperty;
                                        if (game.Publishers == null || !game.Publishers.Where(a => a.Name.ToLower() == value.Name.ToLower()).Any())
                                        {
                                            var n = Plugin.API.Database.Companies.Where(a => a.Name.ToLower() == value.Name.ToLower()).FirstOrDefault();
                                            if (n == null)
                                            {
                                                n = new Company(value.Name);
                                                Plugin.API.Database.Companies.Add(n);
                                            }

                                            game.PublisherIds.Add(n.Id);
                                            s = true;
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
                                foreach (var genre in values)
                                {
                                    if (genre.GetType() == typeof(MetadataIdProperty))
                                    {
                                        var value = genre as MetadataIdProperty;

                                        if (!game.GenreIds.Contains(value.Id))
                                        {
                                            game.GenreIds.Add(value.Id);
                                            s = true;
                                        }
                                    }
                                    else if (genre.GetType() == typeof(MetadataNameProperty))
                                    {
                                        var value = genre as MetadataNameProperty;
                                        if (game.Genres == null || !game.Genres.Where(a => a.Name.ToLower() == value.Name.ToLower()).Any())
                                        {
                                            var n = Plugin.API.Database.Genres.Where(a => a.Name.ToLower() == value.Name.ToLower()).FirstOrDefault();
                                            if (n == null)
                                            {
                                                n = new Genre(value.Name);
                                                Plugin.API.Database.Genres.Add(n);
                                            }
                                            game.GenreIds.Add(n.Id);
                                            s = true;
                                        }
                                    }
                                }
                                break;
                            }

                        case MetadataField.Tags:
                            {
                                if (game.TagIds == null)
                                {
                                    game.TagIds = new List<Guid>();
                                }
                                foreach (var tag in values)
                                {
                                    if (tag.GetType() == typeof(MetadataIdProperty))
                                    {
                                        var value = tag as MetadataIdProperty;

                                        if (!game.TagIds.Contains(value.Id))
                                        {
                                            game.TagIds.Add(value.Id);
                                            s = true;
                                        }
                                    }
                                    else if (tag.GetType() == typeof(MetadataNameProperty))
                                    {
                                        var value = tag as MetadataNameProperty;
                                        if (game.Tags == null || !game.Tags.Where(a => a.Name.ToLower() == value.Name.ToLower()).Any())
                                        {
                                            var n = Plugin.API.Database.Tags.Where(a => a.Name.ToLower() == value.Name.ToLower()).FirstOrDefault();
                                            if (n == null)
                                            {
                                                n = new Tag(value.Name);
                                                Plugin.API.Database.Tags.Add(n);
                                            }
                                            game.TagIds.Add(n.Id);
                                            s = true;
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
                                foreach (var series in values)
                                {
                                    if (series.GetType() == typeof(MetadataIdProperty))
                                    {
                                        var value = series as MetadataIdProperty;

                                        if (!game.SeriesIds.Contains(value.Id))
                                        {
                                            game.SeriesIds.Add(value.Id);
                                            s = true;
                                        }
                                    }
                                    else if (series.GetType() == typeof(MetadataNameProperty))
                                    {
                                        var value = series as MetadataNameProperty;
                                        if (game.Series == null || !game.Series.Where(a => a.Name.ToLower() == value.Name.ToLower()).Any())
                                        {
                                            var n = Plugin.API.Database.Series.Where(a => a.Name.ToLower() == value.Name.ToLower()).FirstOrDefault();
                                            if (n == null)
                                            {
                                                n = new Series(value.Name);
                                                Plugin.API.Database.Series.Add(n);
                                            }
                                            game.SeriesIds.Add(n.Id);
                                            s = true;
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

                                foreach (var region in values)
                                {
                                    if (region.GetType() == typeof(MetadataIdProperty))
                                    {
                                        var value = region as MetadataIdProperty;

                                        if (!game.RegionIds.Contains(value.Id))
                                        {
                                            game.RegionIds.Add(value.Id);
                                            s = true;
                                        }
                                    }
                                    else if (region.GetType() == typeof(MetadataNameProperty))
                                    {
                                        var value = region as MetadataNameProperty;
                                        if (game.Regions == null || !game.Regions.Where(a => a.Name.ToLower() == value.Name.ToLower()).Any())
                                        {
                                            var n = Plugin.API.Database.Regions.Where(a => a.Name.ToLower() == value.Name.ToLower()).FirstOrDefault();
                                            if (n == null)
                                            {
                                                n = new Region(value.Name);
                                                Plugin.API.Database.Regions.Add(n);
                                            }
                                            game.RegionIds.Add(n.Id);
                                            s = true;
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

                                foreach (var platform in values)
                                {
                                    if (platform.GetType() == typeof(MetadataIdProperty))
                                    {
                                        var value = platform as MetadataIdProperty;

                                        if (!game.PlatformIds.Contains(value.Id))
                                        {
                                            game.PlatformIds.Add(value.Id);
                                            s = true;
                                        }
                                    }
                                    else if (platform.GetType() == typeof(MetadataNameProperty))
                                    {
                                        var value = platform as MetadataNameProperty;

                                        if (game.Platforms == null || !(game.Platforms).Where(a => a.Name.ToLower() == value.Name.ToLower()).Any())
                                        {
                                            var n = Plugin.API.Database.Platforms.Where(a => a.Name.ToLower() == value.Name.ToLower()).FirstOrDefault();
                                            if (n == null)
                                            {
                                                n = new Platform(value.Name);
                                                Plugin.API.Database.Platforms.Add(n);
                                            }
                                            game.PlatformIds.Add(n.Id);
                                            s = true;
                                        }
                                    }
                                }
                                break;
                            }
                        default:
                            {
                                throw new Exception($"Unhandled metadata field {field}");
                            }
                    }

                    log.Trace($"Merged metadata on {field} = {s}");

                }
                catch (Exception e)
                {
                    log.Error(e, $"Failed to merge metadata on {field}");
                    throw;
                }

                return s;
            }

            public static bool ApplyMetadata(Game game, MetadataField field, OnDemandMetadataProvider provider, GetMetadataFieldArgs args)
            {
                var s = false;

                log.Trace($"Applying metadata on {field}");

                switch (field)
                {
                    case MetadataField.Name:
                        {
                            s = MergeMetadata(game, field, provider.GetName(args));
                            break;
                        }
                    case MetadataField.Description:
                        {
                            s = MergeMetadata(game, field, provider.GetDescription(args));
                            break;
                        }
                    case MetadataField.Developers:
                        {
                            s = MergeMetadata(game, field, provider.GetDevelopers(args));
                            break;
                        }
                    case MetadataField.Publishers:
                        {
                            s = MergeMetadata(game, field, provider.GetPublishers(args));
                            break;
                        }
                    case MetadataField.Genres:
                        {
                            s = MergeMetadata(game, field, provider.GetGenres(args));
                            break;
                        }
                    case MetadataField.ReleaseDate:
                        {
                            s = MergeMetadata(game, field, provider.GetReleaseDate(args));
                            break;
                        }
                    case MetadataField.CriticScore:
                        {
                            s = MergeMetadata(game, field, provider.GetCriticScore(args));
                            break;
                        }
                    case MetadataField.CommunityScore:
                        {
                            s = MergeMetadata(game, field, provider.GetCommunityScore(args));
                            break;
                        }
                    case MetadataField.Tags:
                        {
                            s = MergeMetadata(game, field, provider.GetTags(args));
                            break;
                        }
                    case MetadataField.Series:
                        {
                            s = MergeMetadata(game, field, provider.GetSeries(args));
                            break;
                        }
                    case MetadataField.AgeRating:
                        {
                            s = MergeMetadata(game, field, provider.GetAgeRatings(args));
                            break;
                        }
                    case MetadataField.Region:
                        {
                            s = MergeMetadata(game, field, provider.GetRegions(args));
                            break;
                        }
                    case MetadataField.Platform:
                        {
                            s = MergeMetadata(game, field, provider.GetPlatforms(args));
                            break;
                        }
                    case MetadataField.Icon:
                        {
                            s = MergeMetadata(game, field, provider.GetIcon(args));
                            break;
                        }
                    case MetadataField.CoverImage:
                        {
                            s = MergeMetadata(game, field, provider.GetCoverImage(args));
                            break;
                        }
                    case MetadataField.BackgroundImage:
                        {
                            s = MergeMetadata(game, field, provider.GetBackgroundImage(args));
                            break;
                        }
                    case MetadataField.Links:
                        {
                            s = MergeMetadata(game, field, provider.GetLinks(args));
                            break;
                        }
                    default:
                        {
                            throw new Exception($"Unhandled metadata field {field}");
                        }
                }

                log.Trace($"Applied metadata on {field} = {s}");

                return s;
            }


            async public static Task RequestMetadata(PlaynitePlugin plugin, MetadataRequestOptions opts, GetMetadataFieldArgs args, RequestMetadataCallback callback)
            {
                await Task.Run(() =>
                {
                    using (GuardedAction.Call(() =>
                {
                    if (!typeof(MetadataPlugin).IsAssignableFrom(plugin.GetType()))
                    {
                        throw new Exception("Plugin is not a MetadataPlugin");
                    }

                    var plug = plugin as MetadataPlugin;
                    var game = opts.GameData;

                    log.Trace($"Calling GetMetadataProvider handler for {plugin.Id}");
                    var provider = plug.GetMetadataProvider(opts);
                    if (provider == null)
                    {
                        log.Error($"No provider returned for {plugin.Id}");
                        return;
                    }
                    using (provider)
                    {
                        callback(plug, game, opts, provider, args);
                    }
                })) { }
                });
            }
            async public static Task RequestMetadata(PlaynitePlugin plugin, Game game, RequestMetadataCallback callback, bool backgroundDownload = true)
            {
                var opts = new MetadataRequestOptions(game, backgroundDownload);
                var args = new GetMetadataFieldArgs();
                await RequestMetadata(plugin, opts, args, callback);
            }


            async public static Task RequestMetadata(MetadataRequestOptions opts, RequestMetadataCallback callback, bool backgroundDownload = true)
            {
                var args = new GetMetadataFieldArgs();
                var tasks = new List<Task>();

                foreach (var plugin in Plugin.API.Addons.Plugins)
                {
                    if (args.CancelToken.IsCancellationRequested)
                    {
                        log.Trace("Metadata request cancelled");
                        break;
                    }
                    if (typeof(MetadataPlugin).IsAssignableFrom(plugin.GetType()))
                    {
                        log.Debug($"type: {plugin.GetType().Name}");

                        await RequestMetadata(plugin, opts, args, callback);
                    }
                }

            }

            async public static Task RequestMetadata(Game game, RequestMetadataCallback callback, bool backgroundDownload = true)
            {
                var args = new GetMetadataFieldArgs();
                var tasks = new List<Task>();

                foreach (var plugin in Plugin.API.Addons.Plugins)
                {
                    if (args.CancelToken.IsCancellationRequested)
                    {
                        log.Trace("Metadata request cancelled");
                        break;
                    }
                    if (typeof(MetadataPlugin).IsAssignableFrom(plugin.GetType()))
                    {
                        log.Debug($"type: {plugin.GetType().Name}");
                        var opts = new MetadataRequestOptions(game, backgroundDownload);
                        await RequestMetadata(plugin, opts, args, callback);
                    }
                }

            }

            public static T GetPlugin<T>(Guid id) where T : PlaynitePlugin
            {

                foreach (var plugin in Plugin.API.Addons.Plugins)
                {
                    if (plugin.Id == id)
                    {
                        if (!typeof(T).IsAssignableFrom(plugin.GetType()))
                        {
                            throw new Exception($"Plugin {id} is not of type {typeof(T).Name}");
                        }
                        return plugin as T;
                    }
                }

                return null;
            }

            public static Window CreateWindow(string title, int width = 768, int height = 768, object content = null, object dataContext = null)
            {
                var window = Plugin.API.Dialogs.CreateWindow(new WindowCreationOptions
                {
                    ShowMinimizeButton = false
                });

                window.Height = height;
                window.Width = width;
                window.Title = title;

                if (content == null)
                {
                    // Set content of a window. Can be loaded from xaml, loaded from UserControl or created from code behind
                    window.Content = content;
                }

                if (dataContext == null)
                {
                    // Set data context if you want to use MVVM pattern
                    window.DataContext = dataContext;
                }

                // Set owner if you need to create modal dialog window
                window.Owner = Plugin.API.Dialogs.GetCurrentAppWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

                return window;
            }

        }

        static class Stringutils
        {
            public static bool IsUrl(string url)
            {
                if (!string.IsNullOrEmpty(url))
                {
                    var uri = new Uri(url);
                    if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                    {
                        return true;
                    }

                    if (uri.Host.StartsWith("www.") && uri.Host.Count(x => x == '.') > 1)
                    {
                        return true;
                    }
                }

                return false;
            }

            public static Uri ToUri(string url)
            {
                try
                {
                    return new Uri(url);
                }
                catch (InvalidOperationException)
                {
                    if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                    {
                        url = "https://" + url;
                        try
                        {
                            return new Uri(url);
                        }
                        catch (InvalidOperationException)
                        {
                        }
                    }

                }
                return null;
            }
        }

        static class UriUtils
        {
            public static NameValueCollection ParseQuery(Uri uri)
            {
                return System.Web.HttpUtility.ParseQueryString(uri.Query);
            }
        }

        static class PathUtils
        {
            public static string CommonRoot(string path1, string path2, bool caseSensitive = false)
            {
                var path1Parts = path1.Split('\\', '/');
                var path2Parts = path2.Split('\\', '/');

                var idx = -1;

                for (int i = 0; i < path1Parts.Length; i++)
                {
                    if (i >= path2Parts.Length)
                    {
                        break;
                    }

                    var a = caseSensitive ? path1Parts[i] : path1Parts[i].ToLower();
                    var b = caseSensitive ? path2Parts[i] : path2Parts[i].ToLower();

                    if (a != b)
                    {
                        break;
                    }

                    idx += a.Length + 1;
                }

                if (idx < 0)
                {
                    return "";
                }

                return path1.Substring(0, idx);
            }
        }

        static class UiUtils
        {
            static public void Call(Action callback)
            {
                Dispatch(callback);
            }
            static public void Dispatch(Action callback)
            {
                Plugin.API.MainView.UIDispatcher.Invoke(callback);
            }
        }

        static class GeneralUtils
        {
            static public bool IsNullOrEmpty(ReleaseDate? v)
            {
                return v == null;
            }
            static public bool IsNullOrEmpty(string str)
            {
                return string.IsNullOrEmpty(str);
            }
            static public bool IsNullOrEmpty(ulong? v)
            {
                return v == null;
            }
            static public bool IsNullOrEmpty(int? v)
            {
                return v == null;
            }
            static public bool IsNullOrEmpty<T>(IEnumerable<T> list)
            {
                return list == null || list.Count() == 0;
            }

        }

        static class FuncUtils
        {
            public static Action Debounce(Action func, int milliseconds = 300)
            {
                var r = Debounce<object>(_ => func(), milliseconds);
                return () => r(null);
            }
            public static Action<T> Debounce<T>(Action<T> func, int milliseconds = 300)
            {
                CancellationTokenSource cancelTokenSource = null;

                return arg =>
                {
                    cancelTokenSource?.Cancel();
                    cancelTokenSource = new CancellationTokenSource();

                    Task.Delay(milliseconds, cancelTokenSource.Token)
                        .ContinueWith(t =>
                        {
                            if (t.IsCompleted && !t.IsCanceled && !t.IsFaulted)
                            {
                                func(arg);
                            }
                        }, TaskScheduler.Default);
                };
            }
        }

        class GuardedAction : IDisposable
        {
            private static readonly ILogger log = LogManager.GetLogger();

            private Exception exception = null;
            private ILogger logger = log;
            private string msg = "Unhandled exception";

            public GuardedAction(Action action)
            {
            }

            public GuardedAction(string msg, ILogger logger, Action action)
            {
                this.msg = msg ?? this.msg;
                this.logger = logger ?? log;
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    exception = e;
                }
            }



            public void Dispose()
            {
                if (exception != null)
                {
                    logger.Error(exception, $"{msg} - {exception.Message}");
                }
            }


            static public GuardedAction Call(string msg, ILogger logger, Action action) => new GuardedAction(msg, logger, action);
            static public GuardedAction Call(ILogger logger, Action action) => new GuardedAction(null, logger, action);
            static public GuardedAction Call(string msg, Action action) => new GuardedAction(msg, null, action);
            static public GuardedAction Call(Action action) => new GuardedAction(null, null, action);
        }


#if DEV
        public class TimeIt : IDisposable
        {
            private static readonly ILogger log = LogManager.GetLogger();

            private string name;
            private bool predicate;
            private Stopwatch stopwatch = new Stopwatch();

            public TimeIt(string name, bool predicate = true)
            {
                this.predicate = predicate;

                if (predicate)
                {
                    log.Trace($"Timing {name}");
                    this.name = name;
                    stopwatch.Start();
                }
            }

            public void Stop()
            {
                if (!predicate)
                {
                    return;
                }

                stopwatch.Stop();

                if (stopwatch.ElapsedMilliseconds > 10000)
                {
                    log.Error($"Timing {name} took {stopwatch.ElapsedMilliseconds} ms");
                }
                else if (stopwatch.ElapsedMilliseconds > 1000)
                {
                    log.Warn($"Timing {name} took {stopwatch.ElapsedMilliseconds} ms");
                }
                else
                {
                    log.Trace($"Timing {name} took {stopwatch.ElapsedMilliseconds} ms");
                }
            }

            public void Dispose()
            {
                Stop();
            }
        }
#else
        public class TimeIt : IDisposable
        {
            public TimeIt(string name, bool predicate = true)
            {

            }

            public void Stop()
            {
            }

            public void Dispose()
            {
            }
        }
#endif

        // see https://learn.microsoft.com/en-us/windows/win32/seccrypto/common-hresult-values
        public static class HRESULT
        {
            public const int E_ACCESSDENIED = unchecked((int)0x80070005);
        }

    }
}
