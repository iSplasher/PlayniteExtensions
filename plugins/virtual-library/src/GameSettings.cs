using System.Collections.Generic;

namespace Plugin
{

    public static class DownloadMetadataValue
    {
        public const int Default = 0;
        public const int Always = 1;
        public const int Never = 2;
    }

    public partial class GameSettings
    {
        private bool showTopPanel = true;
        private bool showSidebar = true;
        private int downloadMetadata = DownloadMetadataValue.Default;

        // ---------------------------------------

        public bool ShowTopPanel { get => showTopPanel; set => SetValue(ref showTopPanel, value); }
        public bool ShowSidebar { get => showSidebar; set => SetValue(ref showSidebar, value); }
        public int DownloadMetadata { get => downloadMetadata; set => SetValue(ref downloadMetadata, value); }
    }

    public partial class GameSettingsViewModel
    {
    }
}
