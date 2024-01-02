using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;


namespace Plugin
{


    static partial class PartialProperties
    {
        public static bool HasClient { get; set; } = false;
    }


    public partial class Plugin : LibraryPlugin
    {

        public override string Name
        {
            get
            {
                var ass = Meta.AssemblyUtils.GetAssembly();
                return Meta.AssemblyUtils.GetTitle(ass);
            }
        }

        // Implementing Client adds ability to open it via special menu in playnite.
        public override LibraryClient Client
        {
            get
            {
                if (PartialProperties.HasClient)
                {
                    return new PluginClient();
                }

                return null;
            }
        }


        partial void PartialPluginSetup(IPlayniteAPI API)
        {
            Properties = new LibraryPluginProperties
            {
                HasSettings = PartialProperties.HasSettings,

            };
        }

        // --------------------------------------------------------------------------------


        partial void GetGamesPartial(ref IEnumerable<GameMetadata> games, LibraryGetGamesArgs args);

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            IEnumerable<GameMetadata> games = new List<GameMetadata>();

            using (Common.GuardedAction.Call(() =>
           {
               GetGamesPartial(ref games, args);
           })) { }

            return games;
        }


        partial void ImportGamesPartial(ref IEnumerable<Game> games, LibraryImportGamesArgs args);

        public override IEnumerable<Game> ImportGames(LibraryImportGamesArgs args)
        {
            IEnumerable<Game> games = new List<Game>();

            using (Common.GuardedAction.Call(() =>
           {
               ImportGamesPartial(ref games, args);
           })) { }

            return games;
        }


    }
}
