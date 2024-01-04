using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace Plugin
{
    namespace Resolver
    {

        public interface IResolver
        {
            /*
             * Name of the resolver
             */
            string Name { get; }

            /*
             * Example input patterns the resolver can match, used only to inform the user
             */
            string[] ExamplePatterns { get; }

            /*
             * Returns true if the resolver can match the given values.
             */
            bool Match(MatchOptions options);

            /*
             * Return a metadata provider for the given game. This follows the same pattern as Metadata plugins.
             * See https://api.playnite.link/docs/tutorials/extensions/metadataPlugins.html#ondemandmetadataprovider
             * You can return null if you don't want to provide metadata for the given game.
             */
            OnDemandMetadataProvider Resolve(Game game, ResolveOptions options);

            /*
             * Return a string that will be used as the game id for the given game.
             */
            string ResolveGameId(Game game, ResolveOptions options);

            /*
             * Return a string that will be used as the game source for the given game.
             */
            string ResolveGameSource(Game game, ResolveOptions options);
        }


        public class MatchOptions
        {
            public Uri Url { get; set; } = null;
            public string Name { get; set; } = null;

            public virtual MatchOptions Clone()
            {
                var o = new MatchOptions();
                o.Url = Url;
                o.Name = Name;
                return o;
            }

            public override string ToString()
            {
                return $"<Name: {Name} Url: {Url}>";
            }
        }

        public class ResolveOptions : MatchOptions
        {
            public MetadataRequestOptions MetadataRequestOptions { get; private set; } = null;

            public Uri OriginalUrl { get; private set; } = null;
            public bool IsBackgroundDownload { get { return MetadataRequestOptions.IsBackgroundDownload; } }

            public ResolveOptions(Uri url, MetadataRequestOptions options, MatchOptions opts = null)
            {
                OriginalUrl = url;
                MetadataRequestOptions = options;
                if (opts != null)
                {
                    Url = opts.Url;
                    Name = opts.Name;
                }
            }

            public override MatchOptions Clone()
            {
                throw new Exception("ResolveOptions cannot be cloned");
            }
        }



        static public class ResolverBuilder
        {
            public static readonly ILogger log = LogManager.GetLogger();
            private static readonly Dictionary<string, IResolver> resolvers = new Dictionary<string, IResolver>();

            static public T Create<T>() where T : IResolver
            {
                return (T)Create(typeof(T));
            }

            static public IResolver Create(Type resolver)
            {
                if (resolver.GetType().IsAssignableFrom(typeof(IResolver)))
                {
                    throw new Exception("Given object is not a Resolver");
                }

                return (IResolver)Activator.CreateInstance(resolver);
            }

            static public IResolver[] GetResolvers()
            {
                var ass = Meta.AssemblyUtils.GetAssembly();
                foreach (var t in Meta.AssemblyUtils.GetTypesWithInterface<IResolver>(ass))
                {
                    if (!resolvers.ContainsKey(t.FullName))
                    {
                        try
                        {
                            resolvers.Add(t.FullName, Create(t));
                        }
                        catch (Exception)
                        {
                            log.Error($"Failed to create resolver {t.FullName}");
                        }
                    }
                }

                var res = resolvers.Values.ToList();

                log.Debug($"Found {resolvers.Count} resolvers");

#if DEV
                log.Trace($"{String.Join("\n", resolvers.Select(x => x.Value.Name))}");
#endif

                return res.ToArray();
            }
        }

    }
}
