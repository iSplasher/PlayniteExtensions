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
                HasSettings = PartialProperties.HasSettings,
            };
        }

        // --------------------------------------------------------------------------------

    }
}
