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

namespace Plugin
{
    using SettingsPropertyEventArgs = System.ComponentModel.PropertyChangedEventArgs;
    using Settings = GameSettings;
    using SettingsView = GameSettingsView;
    using SettingsViewModel = GameSettingsViewModel;

    public static partial class NotificationID
    {
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

            if (view == null)
            {
                return base.GetSettingsView(firstRunSettings);
            }

            return view;
        }
    }
}
