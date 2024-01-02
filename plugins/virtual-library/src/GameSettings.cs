using System.Collections.Generic;

namespace Plugin
{
    public partial class GameSettings
    {
        private bool showTopPanel = true;
        private bool showSidebar = true;

        // ---------------------------------------

        public bool ShowTopPanel { get => showTopPanel; set => SetValue(ref showTopPanel, value); }
        public bool ShowSidebar { get => showSidebar; set => SetValue(ref showSidebar, value); }
    }

    public partial class GameSettingsViewModel
    {
    }
}
