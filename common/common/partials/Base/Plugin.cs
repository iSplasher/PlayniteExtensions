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
    using SettingsPropertyEventArgs = System.ComponentModel.PropertyChangedEventArgs;
    using Settings = GameSettings;
    using SettingsView = GameSettingsView;
    using SettingsViewModel = GameSettingsViewModel;

    public static partial class NotificationID
    {
    }

    static partial class PartialProperties
    {
        public static bool HasSettings { get; set; } = true;
    }

    public partial class Plugin
    {

        private SettingsViewModel settingsView;
        private Settings settings { get => settingsView.Settings; }

        partial void Setup(IPlayniteAPI API);
        partial void PartialPluginSetup(IPlayniteAPI API);

        public Plugin(IPlayniteAPI api) : base(api)
        {
            BaseSetup(api);

            settingsView = new SettingsViewModel(this);

            settingsView.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(SettingsViewModel.Settings))
                {
                    var view = sender as SettingsViewModel;
                    view.Settings.PropertyChanged += OnSettingsChanged;
                }
            };

            PartialPluginSetup(api);

            Setup(api);
        }

        partial void OnSettingsChangedPartial(Settings settings, string propertyName);
        private void OnSettingsChanged(object sender, SettingsPropertyEventArgs args)
        {
            var settings = sender as GameSettings;

            using (Common.GuardedAction.Call(() =>
            {
                OnSettingsChangedPartial(settings, args.PropertyName);
            })) { }
        }

        // --------------------------------------------------------------------------------

        partial void GetSidebarItemsPartial(ref IEnumerable<SidebarItem> items);

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            IEnumerable<SidebarItem> items = new List<SidebarItem>();

            using (Common.GuardedAction.Call(() =>
           {
               GetSidebarItemsPartial(ref items);
           })) { }

            return items;
        }

        partial void GetTopPanelItemsPartial(ref IEnumerable<TopPanelItem> items);

        public override IEnumerable<TopPanelItem> GetTopPanelItems()
        {
            IEnumerable<TopPanelItem> items = new List<TopPanelItem>();

            using (Common.GuardedAction.Call(() =>
           {
               GetTopPanelItemsPartial(ref items);
           })) { }

            return items;
        }

        partial void GetSettingsPartial(bool firstRunSettings);
        public override ISettings GetSettings(bool firstRunSettings)
        {
            using (Common.GuardedAction.Call(() =>
            {
                GetSettingsPartial(firstRunSettings);
            })) { }

            return settingsView;
        }

        partial void GetSettingsViewPartial(SettingsView view, bool firstRunSettings);
        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            SettingsView view = null;
            using (Common.GuardedAction.Call(() =>
            {
                view = new SettingsView();
                GetSettingsViewPartial(view, firstRunSettings);
            })) { }

            log.Warn("GetSettingsView: " + view.ToString());

            if (view == null)
            {
                return base.GetSettingsView(firstRunSettings);
            }

            return view;
        }


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
