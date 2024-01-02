using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
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
    using Settings = GameSettings;
    using SettingsViewModel = GameSettingsViewModel;

    public static partial class NotificationID
    {
        public static readonly string GameRunning = "DetectRunningGame_GameRunning";
    }

    class ProcessMonitor
    {

        public class ProcessInfo : IDisposable, IEquatable<ProcessInfo>
        {
            private ProcessMonitor monitor = null;
            public Process Process;
            public int Id { get; private set; } = 0;
            public string Name { get; private set; } = "";

            public bool IsChild { get; private set; } = false;

            private string path = null;
            private bool pathFailed = false;
            // private IntPtr localProcessObj = IntPtr.Zero;
            public string Path
            {
                get
                {
                    if (String.IsNullOrEmpty(path) && !pathFailed)
                    {
                        using (new Common.TimeIt("ProcessInfo.Path"))
                        {
                            try
                            {
                                path = Process.GetMainModuleFileName(!IsChild);

                                if (String.IsNullOrEmpty(path))
                                {
                                    log.Trace($"Failed to get path for {this} through GetMainModuleFileName");
                                    path = Process.QueryProcessProperty<string>("ExecutablePath");
                                }
                            }
                            catch (Exception e)
                            {
                                log.Warn(e, $"Failed to get path for {this}: {e.Message}");
                            }
                        }

                        if (String.IsNullOrEmpty(path))
                        {
                            pathFailed = true;
                        }
                    }
                    return path;
                }
                private set { }
            }

            private FileVersionInfo versionInfo = null;
            private bool versionInfoFailed = false;
            public FileVersionInfo VersionInfo
            {
                get
                {
                    if (versionInfo == null && !versionInfoFailed && !String.IsNullOrEmpty(Path))
                    {
                        using (new Common.TimeIt("ProcessInfo.VersionInfo"))
                        {
                            try
                            {
                                versionInfo = FileVersionInfo.GetVersionInfo(Path);
                            }
                            catch (Exception e)
                            {
                                log.Warn(e, $"Failed to get version info for {this}|{Path}: {e.Message}");
                            }
                        }

                        if (versionInfo == null)
                        {

                            versionInfoFailed = true;
                        }
                    }
                    return versionInfo;
                }
                private set { }
            }


            private string owner = null;
            private bool ownerFailed = false;
            public string Owner
            {
                get
                {
                    if (String.IsNullOrEmpty(owner) && !ownerFailed)
                    {
                        using (new Common.TimeIt("ProcessInfo.Owner"))
                        {
                            try
                            {
                                owner = Process.Owner();
                            }
                            catch (Exception e)
                            {
                                log.Warn(e, $"Failed to get owner for {this}: {e.Message}");
                            }
                        }

                        if (String.IsNullOrEmpty(owner))
                        {
                            ownerFailed = true;
                        }
                    }
                    return owner;
                }
                private set { }
            }

            public string Description
            {
                get
                {
                    var vi = VersionInfo;
                    if (vi != null)
                    {
                        return vi.FileDescription;
                    }
                    return "";
                }
                private set { }
            }

            public string CompanyName
            {
                get
                {
                    var vi = VersionInfo;
                    if (vi != null)
                    {
                        return vi.CompanyName;
                    }
                    return "";
                }
                private set { }
            }

            public string ProductName
            {
                get
                {
                    var vi = VersionInfo;
                    if (vi != null)
                    {
                        return vi.ProductName;
                    }
                    return "";
                }
                private set { }
            }

            private string key = null;
            public string Key
            {
                get
                {
                    if (key == null)
                    {
                        try
                        {
                            var _ = Process.StartTime;
                            key = ResolveKey(Process);
                        }
                        catch
                        {
                            return ResolveKey(Process);
                        }
                    }
                    return key;
                }
                private set { }
            }
            public static string ResolveKey(Process process)
            {
                DateTime startTime;
                try
                {
                    startTime = process.StartTime;
                }
                catch
                {
                    startTime = DateTime.MinValue;
                }
                return $"{process.Id}|{startTime}|{process.ProcessName}";
            }

            private int parentId = 0;
            private int ResolveParentId(bool wmiQuery = true)
            {
                using (new Common.TimeIt("ProcessInfo.ResolveParentId", parentId == 0))
                {
                    if (parentId == 0)
                    {
                        try
                        {
                            parentId = Process.ParentProcessId();
                            log.Trace($"Parent process id for {this} is {parentId} | 1");
                        }
                        catch
                        {
                            log.Trace($"Failed to get parent process id through CreateToolhelp32Snapshot for child {this}");
                            throw;
                        }
                    }
                    if (parentId == 0 && wmiQuery)
                    {
                        try
                        {
                            parentId = Process.QueryProcessProperty<int>("ParentProcessId");
                            log.Trace($"Parent process id for {this} is {parentId} | 2");
                        }
                        catch
                        {
                            log.Trace($"Failed to get parent process id through WMI for {this}");
                            throw;
                        }
                    }
                }

                return parentId;
            }
            public int ParentId
            {
                get
                {
                    try
                    {
                        return ResolveParentId();
                    }
                    catch
                    {
                        return 0;
                    }
                }
                private set { }
            }

            private ProcessInfo[] children = null;
            internal bool resolveChildren = true;

            public ProcessInfo[] Children
            {
                get
                {
                    if (children == null || resolveChildren)
                    {
                        using (new Common.TimeIt($"ProcessInfo.Children | {this}"))
                        {
                            var err = false;
                            var procs = monitor.Processes.ToDictionary(x => x.Id);

                            if (monitor != null)
                            {
                                if (procs.Count > 0)
                                {
                                    children = procs.Where(x =>
                                    {
                                        if (Id == x.Value.Id)
                                        {
                                            return false;
                                        }

                                        try
                                        {
                                            var r = x.Value.ResolveParentId(false) == Id;
                                            if (r)
                                            {
                                                x.Value.IsChild = true;
                                            }
                                            return r;
                                        }
                                        catch
                                        {
                                            err = true;
                                            return false;
                                        }
                                    }).Select(x => x.Value).ToArray();
                                }
                            }

                            if (children == null || (err && children.Length == 0))
                            {
                                try
                                {

                                    children = Process.QueryChildProcesses().Select(x =>
                                    {
                                        if (procs.TryGetValue(x.Id, out ProcessInfo pinfo))
                                        {
                                            pinfo.IsChild = true;
                                            pinfo.parentId = Id;
                                            return pinfo;
                                        }
                                        else
                                        {
                                            return null;
                                        }
                                    }).Where(x => x != null).ToArray();
                                }
                                catch (Exception e)
                                {
                                    log.Warn(e, $"Failed to get children for {this}: {e.Message}");
                                }
                            }
                            resolveChildren = false;
                        }
                    }

                    return children;
                }
                private set { }
            }

            public ProcessInfo(Process process, ProcessMonitor monitor = null)
            {
                this.monitor = monitor;
                Process = process;
                Id = process.Id;
                Name = process.ProcessName;
                path = process.StartInfo?.FileName;
                try
                {
                    if (String.IsNullOrEmpty(path))
                    {
                        path = process.MainModule?.FileName;
                    }
                    versionInfo = process.MainModule?.FileVersionInfo;
                }
                catch { }
            }

            public void Dispose()
            {
            }

            public bool Equals(ProcessInfo other)
            {
                return Key == other.Key;
            }

            public int GetHashCode(ProcessInfo obj)
            {
                return Key.GetHashCode();
            }

            public override string ToString()
            {
                return $"{Id}|{Name}";
            }
        }

        private static readonly ILogger log = LogManager.GetLogger();

        static private int sessionId = Process.GetCurrentProcess().SessionId;

        static private string userName = WindowsIdentity.GetCurrent().Name;

        private int intervalMs = 1000 * 1; // 1 seconds

        public readonly HashSet<string> ignoredProcessNames = new HashSet<string>();
        private HashSet<string> ignoredProcessKeys = new HashSet<string>();

        static private readonly object mutex = new object();

        private bool IgnoreProcess(Process process)
        {
            using (new Common.TimeIt("IgnoreProcess(Process)"))
            {

                var key = ProcessInfo.ResolveKey(process);

                if (ignoredProcessKeys.Contains(key))
                {
                    return true;
                }

                try
                {
                    if (process.SessionId != sessionId)
                    {
                        ignoredProcessKeys.Add(key);
                        return true;
                    }
                }
                catch (Win32Exception) { }

                return false;
            }
        }

        private bool IgnoreProcess(ProcessInfo pinfo)
        {
            using (new Common.TimeIt("IgnoreProcess(ProcessInfo)"))
            {

                if (ignoredProcessKeys.Contains(pinfo.Key))
                {
                    return true;
                }

                if (ignoredProcessNames.Contains(pinfo.Name.ToLower()))
                {
                    ignoredProcessKeys.Add(pinfo.Key);
                    log.Debug($"Ignoring process in provided ignorelist {pinfo.Name} ({pinfo.Id})");
                    foreach (var c in pinfo.Children)
                    {
                        ignoredProcessKeys.Add(c.Key);
                    }
                    return true;
                }

                if (pinfo.IsChild && processes.TryGetValue(pinfo.ParentId, out ProcessInfo parent))
                {
                    if (ignoredProcessKeys.Contains(parent.Key))
                    {
                        ignoredProcessKeys.Add(pinfo.Key);
                        log.Debug($"Ignoring process {pinfo} which is child of ignored parent {parent}");
                        return true;
                    }
                }

                if (!String.IsNullOrEmpty(pinfo.Owner) && pinfo.Owner != userName)
                {
                    ignoredProcessKeys.Add(pinfo.Key);
                    log.Debug($"Process {pinfo} is owned by {pinfo.Owner} vs {userName}");
                    foreach (var c in pinfo.Children)
                    {
                        ignoredProcessKeys.Add(c.Key);
                    }
                    return true;
                }
            }

            return false;
        }

        public void Start(int interval = 0)
        {
            if (IsRunning)
            {
                return;
            }

            if (interval > 0)
            {
                this.UpdateInterval(interval);
            }

            log.Debug($"Starting process monitor with interval {intervalMs} ms");

            IsRunning = true;

            Task.Run(() =>
            {
                using (Common.GuardedAction.Call(() =>
                {
                    while (IsRunning)
                    {
                        lock (mutex)
                        {
                            using (new Common.TimeIt("ProcessMonitor"))
                            {
                                List<ProcessInfo> replacement = new List<ProcessInfo>();
                                Process[] procs;
                                using (new Common.TimeIt("ProcessMonitor.GetProcesses"))
                                {
                                    procs = Process.GetProcesses();
                                }

                                foreach (var p in procs)
                                {
                                    if (IgnoreProcess(p))
                                    {
                                        continue;
                                    }

                                    ProcessInfo pinfo;
                                    if (processes.TryGetValue(p.Id, out pinfo))
                                    {
                                        pinfo.resolveChildren = true;
                                    }
                                    else
                                    {
                                        pinfo = new ProcessInfo(p, this);
                                    }

                                    if (IgnoreProcess(pinfo))
                                    {
                                        continue;
                                    }

                                    replacement.Add(pinfo);
                                }

                                Processes = replacement.ToArray();
                            }

                        }

                        log.Debug($"Found {Processes.Length} processes + {ignoredProcessKeys.Count} ignored");

                        Task.Delay(intervalMs).Wait();
                    }
                }))
                { }
            });
        }

        public void UpdateInterval(int interval)
        {
            this.intervalMs = interval * 1000;
            log.Debug($"Updated process monitor interval to {interval}s");
        }


        public void Stop()
        {
            IsRunning = false;
            lock (mutex)
            {
                disposeProcesses();
                Processes = new ProcessInfo[0];
            }
        }


        private bool isDisposing = false;
        private SpinLock disposeLock = new SpinLock();
        private ManualResetEventSlim disposing = new ManualResetEventSlim(true);

        private void disposeProcesses()
        {
            if (isDisposing)
            {
                return;
            }
            try
            {
                disposing.Reset();
                disposeLock.Enter(ref isDisposing);
                foreach (var p in Processes)
                {
                    p.Dispose();
                }
            }
            finally
            {
                if (isDisposing)
                {
                    disposeLock.Exit();
                }
                disposing.Set();
            }
        }

        public bool IsRunning { get; private set; } = false;

        static private ConcurrentDictionary<int, ProcessInfo> processes = new ConcurrentDictionary<int, ProcessInfo>();
        public ProcessInfo[] Processes
        {
            get
            {
                disposing.Wait();
                return processes.Values.ToArray();
            }
            private set
            {
                disposing.Wait();
                processes = new ConcurrentDictionary<int, ProcessInfo>(value.ToDictionary(x => x.Id));
            }
        }

        public ProcessInfo[] Query(string name, bool exact = false)
        {
            using (new Common.TimeIt($"ProcessMonitor.Query | exact={exact} | {name}"))
            {

                if (exact)
                {
                    return Processes.Where(x => x.Name == name).ToArray();
                }
                else
                {
                    return Processes.Where(x => x.Name.Contains(name)).ToArray();
                }
            }
        }
    }

    namespace GameProcessResolver
    {
        interface IResolver
        {
            GameProcess.NameInfo[] ResolveNames();
        }

        class EqualityComparer<T> : IEqualityComparer<T> where T : IResolver
        {
            static public EqualityComparer<T> Default = new EqualityComparer<T>();
            public bool Equals(T a, T b)
            {
                var clsNameA = a.GetType().FullName;
                var clsNameB = b.GetType().FullName;
                return clsNameA == clsNameB;
            }

            public int GetHashCode(T value) => value.GetType().FullName.GetHashCode();
        }

        class ResolverBuilder
        {
            static public T Create<T>(GameProcess process) where T : IResolver
            {
                return (T)Activator.CreateInstance(typeof(T), process);
            }
        }

        // ------------------------------------------------------------------------

        class PlayActionFile : IResolver
        {
            private static readonly ILogger log = LogManager.GetLogger();
            private GameProcess process;
            private string gameDir;

            public PlayActionFile(GameProcess process)
            {
                this.process = process;
                this.gameDir = process.Game.InstallDirectory ?? "";
            }
            public string ResolvePath(Game game, string path)
            {
                if (String.IsNullOrEmpty(path))
                {
                    return path;
                }

                log.Trace($"Resolving path {path} for {game.Name}");
                path = path.Replace("{InstallDir}", game.InstallDirectory);
                log.Trace($"Resolved path {path} for {game.Name}");

                return NativeMethods.GetFinalPathName(path);
            }

            private bool Match(GameProcess.NameInfo name, ProcessMonitor.ProcessInfo info)
            {
                // Product name matches title
                var prod = info.ProductName;
                var title = process.Game.Name;

                if (!String.IsNullOrEmpty(prod) && !String.IsNullOrEmpty(title) && prod.Trim().ToLower() == title.Trim().ToLower())
                {
                    return true;
                }
                else
                {
                    log.Trace($"Process match {process}: ({prod}) does not match game title {title}");
                }

                // Description matches title
                var desc = info.Description;

                if (!String.IsNullOrEmpty(desc) && !String.IsNullOrEmpty(title) && desc.Trim().ToLower() == title.Trim().ToLower())
                {
                    return true;
                }
                else
                {
                    log.Trace($"Process match {process}: ({desc}) does not match game title {title}");
                }

                // Is in game directory
                if (!String.IsNullOrEmpty(gameDir) && Common.PathUtils.CommonRoot(gameDir, info.Path) == gameDir)
                {
                    return true;
                }
                else
                {
                    log.Trace($"Process match {process}: ({info.Path}) is not in game directory ({gameDir})");
                }

                // Company matches devs
                var company = info.CompanyName;
                var developers = process.Game.Developers;
                if (developers != null)
                {
                    foreach (var dev in developers)
                    {
                        var devName = dev.Name;
                        if (!String.IsNullOrEmpty(company) && !String.IsNullOrEmpty(devName) && company.Trim().ToLower() == devName.Trim().ToLower())
                        {
                            return true;
                        }
                    }
                    log.Trace($"Process match {process}: ({company}) does not match any game developer in ({String.Join(", ", developers.Select(x => x.Name).ToArray())})");
                }


                return false;
            }

            public GameProcess.NameInfo[] ResolveNames()
            {
                var game = process.Game;
                var names = new List<GameProcess.NameInfo>();

                // From exe when pressing play
                var actions = game.GameActions;

                if (actions != null)
                {
                    foreach (var act in actions)
                    {
                        var action = act;
                        if (!action.IsPlayAction)
                        {
                            continue;
                        }

                        if (action.Type == GameActionType.File)
                        {

                            if (Plugin.API != null)
                            {
                                action = Plugin.API.ExpandGameVariables(game, action);
                            }

                            string path = ResolvePath(game, action.Path);
                            if (File.Exists(path))
                            {
                                names.Add(new GameProcess.NameInfo
                                {
                                    Name = Path.GetFileNameWithoutExtension(path),
                                    Path = path,
                                    IsRoot = true,
                                    ExactQuery = true,
                                    Match = Match
                                });
                                log.Debug($"Resolved process name for {game.Name} from play action path: {path}");
                            }
                        }
                    }
                }

                return names.ToArray();
            }
        }

    }




    class GameProcess
    {

        public class NameInfo
        {
            public string Name { get; set; } = "";
            public string Path { get; set; } = "";
            public bool IsRoot { get; set; } = false;
            public bool ExactQuery { get; set; } = false;

            public Func<NameInfo, ProcessMonitor.ProcessInfo, bool> Match { get; set; } = null;
        }


        private static readonly ILogger log = LogManager.GetLogger();

        private readonly object localCheckMutex = new object();

        public static ProcessMonitor processMonitor { get; private set; }

        private ProcessMonitor.ProcessInfo[] activeProcesses = new ProcessMonitor.ProcessInfo[0];

        private List<string> overrides = null;

        private bool resolved = false;

        public HashSet<GameProcessResolver.IResolver> resolvers = new HashSet<GameProcessResolver.IResolver>(
            GameProcessResolver.EqualityComparer<GameProcessResolver.IResolver>.Default);

        public GameProcess(in Game game, SettingsViewModel settingsView, string[] overrides = null)
        {
            processMonitor = processMonitor ?? new ProcessMonitor();

            this.Game = game;
            if (overrides != null)
            {
                this.overrides = overrides.ToList();
            }
        }

        public Game Game { get; private set; }

        public NameInfo[] Names { get; private set; } = new NameInfo[0];

        public bool IsRunning
        {
            get
            {
                return activeProcesses.Length > 0;
            }
        }

        public void AddResolver<T>() where T : GameProcessResolver.IResolver
        {
            resolvers.Add(GameProcessResolver.ResolverBuilder.Create<T>(this));
        }




        public bool Check()
        {
            if (processMonitor == null)
            {
                throw new Exception("Process monitor is not initialized");
            }

            if (resolved && Names.Length == 0)
            {
                return false;
            }


            lock (localCheckMutex)
            {
                using (new Common.TimeIt($"GameProcess.Check | {Game.Name}"))
                {

                    if (!resolved)
                    {
                        log.Warn($"Expected {this} to be resolved before checking, resolving now");
                        if (!Resolve())
                        {
                            return false;
                        }
                    }

                    log.Trace($"Checking if {Game.Name} is running");


                    var processes = new Dictionary<int, ProcessMonitor.ProcessInfo>();
                    foreach (var name in Names)
                    {
                        foreach (var process in processMonitor.Query(name.Name, name.ExactQuery))
                        {
                            if (name.Match != null && !name.Match(name, process))
                            {
                                continue;
                            }

                            processes[process.Id] = process;
                            if (name.IsRoot)
                            {
                                foreach (var child in process.Children)
                                {
                                    processes[child.Id] = child;
                                }
                            }
                        }
                    }

                    activeProcesses = processes.Values.ToArray();

                    log.Debug($"Found {activeProcesses.Length} active processes for {Game.Name}");
#if DEV
                    foreach (var p in activeProcesses)
                    {
                        log.Trace($"Active process {p})");
                    }
#endif
                }

                return IsRunning;
            }
        }

        public bool Resolve()
        {
            resolved = true;

            if (resolvers.Count == 0)
            {
                AddResolver<GameProcessResolver.PlayActionFile>();
            }

            if (!ResolveNames())
            {
                log.Warn($"Failed to resolve process names for {Game.Name}");
                return false;
            }


            return true;
        }

        private bool ResolveNames()
        {
            if (overrides != null && overrides.Count > 0)
            {
                Names = overrides.Select(x => new NameInfo
                {
                    Name = x,
                    Path = x,
                    IsRoot = false
                }).ToArray();

                log.Debug($"Resolved process names for {Game.Name} from overrides: {String.Join(", ", Names.Select(x => x.Name).ToArray())}");
                return true;
            }

            var names = new List<NameInfo>();

            foreach (var resolver in resolvers)
            {
                names.AddRange(resolver.ResolveNames());
            }

            Names = names.ToArray();

            return names.Count > 0;
        }

    }


    public partial class Plugin
    {

        public class GameState
        {
            public bool Seen { get; set; } = false;
        }

        ConcurrentDictionary<Guid, GameProcess> gameProcesses = new ConcurrentDictionary<Guid, GameProcess>();

        public readonly Dictionary<Guid, GameState> gameStates = new Dictionary<Guid, GameState>();

        static private readonly object checkMutex = new object();
        private bool isMonitoring = false;

        partial void BaseSetup(IPlayniteAPI api);
        partial void Setup(IPlayniteAPI API)
        {

        }

        public static bool GameHasTag(Game game, string tagName)
        {
            if (game.Tags is null)
            {
                return false;
            }

            foreach (var t in game.Tags)
            {
                if (t.Name == tagName)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool AddGameTag(Game game, string tagName, bool updateDB = true)
        {
            if (GameHasTag(game, tagName))
            {
                return true;
            }

            bool added = false;


            using (API.Database.BufferedUpdate())
            {

                var tag = API.Database.Tags.Add(tagName);

                if (game.Tags is null)
                {
                    game.TagIds = new List<Guid> { };
                }

                game.TagIds.Add(tag.Id);
                added = true;

                if (updateDB)
                {
                    API.Database.Games.Update(game);
                }
            }

            return added;
        }

        private GameProcess GetGameProcess(in Game game)
        {
            if (game.Hidden && settings.SkipHiddenGames)
            {
                return null;
            }

            if (!game.IsInstalled && settings.SkipUninstalledGames)
            {
                return null;
            }

            if (String.IsNullOrEmpty(game.InstallDirectory))
            {
                return null;
            }

            if (!String.IsNullOrEmpty(settings.SkipIfTag))
            {
                if (GameHasTag(game, settings.SkipIfTag))
                {
                    return null;
                }
            }

            if (!String.IsNullOrEmpty(settings.MonitorOnlyTag))
            {
                if (!GameHasTag(game, settings.MonitorOnlyTag))
                {
                    return null;
                }
            }

            var g = new GameProcess(game, settingsView);

            if (!g.Resolve())
            {
                return null;
            }

            return g;
        }

        private ref ConcurrentDictionary<Guid, GameProcess> RefreshGameProcessess(bool full)
        {
            log.Debug("Refreshing game processes");
            var procs = new ConcurrentDictionary<Guid, GameProcess>();

            using (new Common.TimeIt("RefreshGameProcessess"))
            {
                log.Info("Refreshing game processes");
                foreach (var game in API.Database.Games)
                {
                    var process = GetGameProcess(game);
                    if (process != null)
                    {
                        procs[game.Id] = process;
                        EnsureGameState(process.Game);
                    }
                }
            }

            gameProcesses = procs;

            log.Debug($"Found {gameProcesses.Count} game processes");

            return ref gameProcesses;
        }

        public void StopMonitoring()
        {
            isMonitoring = false;
            if (GameProcess.processMonitor != null)
            {
                GameProcess.processMonitor.Stop();
            }
        }

        public bool StartMonitoring(bool fullRefresh = true)
        {
            if (isMonitoring)
            {
                return true;
            }
            isMonitoring = true;

            log.Info("Starting monitoring");
            RefreshGameProcessess(fullRefresh);

            Task.Run(() =>
            {
                using (Common.GuardedAction.Call(() =>
                {
                    bool found = false;
                    while (isMonitoring)
                    {

                        if (gameProcesses.Count == 0)
                        {
                            log.Debug("No game processes to check");

                            if (GameProcess.processMonitor != null)
                            {
                                GameProcess.processMonitor.Stop();
                            }

                            Task.Delay((settings.IdleCheckInterval * 2) * 1000).Wait();
                            continue;
                        }
                        else
                        {
                            if (GameProcess.processMonitor != null && !GameProcess.processMonitor.IsRunning)
                            {
                                GameProcess.processMonitor.Start(settings.ProcessQueryInterval);
                            }
                            SetIgnoreList();
                        }


                        found = CheckGameProcesses();

                        var ms = (found ? settings.ActiveCheckInterval : settings.IdleCheckInterval) * 1000;
                        log.Trace($"Checking game process in {ms} ms");
                        Task.Delay(ms).Wait();
                    }
                })) { }
            });

            log.Info($"Started monitoring {gameProcesses.Count} game processes");

            return true;
        }

        private GameState EnsureGameState(Game game)
        {
            if (!gameStates.ContainsKey(game.Id))
            {
                gameStates[game.Id] = new GameState();
            }

            return gameStates[game.Id];
        }

        public bool GoToGame(Game game)
        {
            var id = game.Id;
            API.MainView.UIDispatcher.Invoke(() =>
            {
                API.MainView.SelectGame(id);
            });
            return true;
        }

        private void OnGameRunning(GameProcess process)
        {
            var state = EnsureGameState(process.Game);
            if (!state.Seen)
            {
                state.Seen = true;
                GoToGame(process.Game);
                API.Notifications.Remove(NotificationID.GameRunning);
                API.Notifications.Add(new NotificationMessage(
                    NotificationID.GameRunning,
                    $"Game {process.Game.Name} is running",
                    NotificationType.Info, () => GoToGame(process.Game)));
            }
        }

        private bool CheckGameProcess(GameProcess process)
        {
            if (process.Check())
            {
                OnGameRunning(process);
                return true;
            }
            return false;
        }

        private bool CheckGameProcesses()
        {
            bool found = false;
            lock (checkMutex)
            {
                using (new Common.TimeIt("CheckGameProcesses"))
                {
                    log.Trace($"Checking {gameProcesses.Count} game processes");
                    foreach (var process in gameProcesses.Values)
                    {
                        if (CheckGameProcess(process))
                        {
                            found = true;
                        }
                    }
                }
            }
            return found;
        }

        private void SetIgnoreList()
        {
            if (GameProcess.processMonitor != null)
            {
                GameProcess.processMonitor.ignoredProcessNames.Clear();
                foreach (var i in settings.IgnoreList)
                {
                    var name = i.ToLower();
                    if (name.EndsWith(".exe"))
                    {
                        name = name.Substring(0, name.Length - 4);
                    }
                    GameProcess.processMonitor.ignoredProcessNames.Add(name);
                }
            }
        }

        partial void OnSettingsChangedPartial(Settings settings, string propertyName)
        {
            if (propertyName == nameof(Settings.ProcessQueryInterval) && GameProcess.processMonitor != null)
            {
                if (GameProcess.processMonitor != null)
                {
                    GameProcess.processMonitor.UpdateInterval(settings.ProcessQueryInterval);
                }

            }

            if (propertyName == nameof(Settings.IgnoreList))
            {
                SetIgnoreList();
            }
        }

        // --------------------------------------------------------------------------------

        partial void OnApplicationStartedPartial(OnApplicationStartedEventArgs args)
        {
            if (settings.MonitorOnStart)
            {
                if (!StartMonitoring())
                {
                    log.Error("Failed to start monitoring on application start");
                }
            }
            else
            {
                RefreshGameProcessess(true);
            }

        }

        partial void OnApplicationStoppedPartial(OnApplicationStoppedEventArgs args)
        {
            isMonitoring = false;
        }

        partial void OnLibraryUpdatedPartial(OnLibraryUpdatedEventArgs args)
        {
            if (!settings.MonitorOnLibraryUpdate || isMonitoring)
            {
                RefreshGameProcessess(false);
            }

            if (settings.MonitorOnLibraryUpdate)
            {
                if (!StartMonitoring())
                {
                    log.Error("Failed to start monitoring on library update");
                }
            }

        }

    }
}
