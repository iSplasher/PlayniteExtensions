using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Security.Principal;
using System.ComponentModel;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;


namespace Plugin
{
    public partial class Plugin : GenericPlugin
    {

        partial void PartialPluginSetup(IPlayniteAPI API)
        {
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
        }

        // --------------------------------------------------------------------------------

        partial void OnGameInstalledPartial(OnGameInstalledEventArgs args);
        public override void OnGameInstalled(OnGameInstalledEventArgs args)
        {
            // Add code to be executed when game is finished installing.
            using (Common.GuardedAction.Call(() =>
            {
                OnGameInstalledPartial(args);
            })) { }
        }

        partial void OnGameStartedPartial(OnGameStartedEventArgs args);
        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
            // Add code to be executed when game is started running.
            using (Common.GuardedAction.Call(() =>
            {
                OnGameStartedPartial(args);
            })) { }
        }

        partial void OnGameStartingPartial(OnGameStartingEventArgs args);
        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            // Add code to be executed when game is preparing to be started.
            using (Common.GuardedAction.Call(() =>
            {
                OnGameStartingPartial(args);
            })) { }
        }

        partial void OnGameStoppedPartial(OnGameStoppedEventArgs args);
        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            // Add code to be executed when game is preparing to be started.

            using (Common.GuardedAction.Call(() =>
            {
                OnGameStoppedPartial(args);
            })) { }

        }

        partial void OnGameUninstalledPartial(OnGameUninstalledEventArgs args);
        public override void OnGameUninstalled(OnGameUninstalledEventArgs args)
        {
            // Add code to be executed when game is uninstalled.
            using (Common.GuardedAction.Call(() =>
            {
                OnGameUninstalledPartial(args);
            })) { }
        }

        partial void OnApplicationStartedPartial(OnApplicationStartedEventArgs args);
        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            // Add code to be executed when Playnite is initialized.
            using (Common.GuardedAction.Call(() =>
            {
                OnApplicationStartedPartial(args);
            })) { }

        }

        partial void OnApplicationStoppedPartial(OnApplicationStoppedEventArgs args);
        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            // Add code to be executed when Playnite is shutting down.
            using (Common.GuardedAction.Call(() =>
            {
                OnApplicationStoppedPartial(args);
            })) { }
        }

        partial void OnLibraryUpdatedPartial(OnLibraryUpdatedEventArgs args);
        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            // Add code to be executed when library is updated.
            using (Common.GuardedAction.Call(() =>
            {
                OnLibraryUpdatedPartial(args);
            })) { }

        }

    }
}
