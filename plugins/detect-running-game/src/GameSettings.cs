using System.Collections.Generic;

namespace Plugin
{
    public partial class GameSettings
    {
        private int activeCheckInterval = 5;
        private int idleCheckInterval = 10;
        private int processQueryInterval = 3;

        private bool skipHiddenGames = true;
        private bool skipUninstalledGames = true;

        private bool monitorOnStart = true;
        private bool monitorOnLibraryUpdate = true;

        private string skipIfTag = "";
        private string monitorOnlyTag = "";

        private string tagActive = "";

        private List<string> ignoreList = new List<string>(
            new string[] {
                "Playnite.DesktopApp.exe",

                // System
                "explorer.exe",
                "taskmgr.exe",
                "svchost.exe",
                "conhost.exe",
                "sihost.exe",
                "ntoskrnl.exe",
                "WerFault.exe",
                "backgroundTaskHost.exe",
                "backgroundTransferHost.exe",
                "winlogon.exe",
                "wininit.exe",
                "csrss.exe",
                "lsass.exe",
                "smss.exe",
                "services.exe",
                "taskeng.exe",
                "taskhost.exe",
                "dwm.exe",
                }
        );


        // private bool optionThatWontBeSaved = false;

        // ---------------------------------------

        public int ActiveCheckInterval { get => activeCheckInterval; set => SetValue(ref activeCheckInterval, value); }
        public int IdleCheckInterval { get => idleCheckInterval; set => SetValue(ref idleCheckInterval, value); }
        public int ProcessQueryInterval { get => processQueryInterval; set => SetValue(ref processQueryInterval, value); }

        public bool SkipHiddenGames { get => skipHiddenGames; set => SetValue(ref skipHiddenGames, value); }
        public bool SkipUninstalledGames { get => skipUninstalledGames; set => SetValue(ref skipUninstalledGames, value); }

        public bool MonitorOnStart { get => monitorOnStart; set => SetValue(ref monitorOnStart, value); }
        public bool MonitorOnLibraryUpdate { get => monitorOnLibraryUpdate; set => SetValue(ref monitorOnLibraryUpdate, value); }

        public string MonitorOnlyTag { get => monitorOnlyTag; set => SetValue(ref monitorOnlyTag, value); }
        public string SkipIfTag { get => skipIfTag; set => SetValue(ref skipIfTag, value); }

        public string TagActive { get => tagActive; set => SetValue(ref tagActive, value); }

        public List<string> IgnoreList { get => ignoreList; set => SetValue(ref ignoreList, value); }

        // Playnite serializes settings object to a JSON object and saves it as text file.
        // If you want to exclude some property from being saved then use `JsonDontSerialize` ignore attribute.
        // [DontSerialize]
        // public bool OptionThatWontBeSaved { get => optionThatWontBeSaved; set => SetValue(ref optionThatWontBeSaved, value); }
    }

    public partial class GameSettingsViewModel
    {
    }
}
