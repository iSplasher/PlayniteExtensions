using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Management;
using System.ComponentModel;
using System.Runtime.ConstrainedExecution;
using System.Security;
using System.Reflection;
using System.Security.Permissions;
using System.Security.Principal;
using Playnite.SDK;

namespace Plugin
{
    public partial class Plugin
    {
        public static readonly ILogger log = LogManager.GetLogger();

        static public IPlayniteAPI API { get; private set; }

        public override Guid Id
        {
            get
            {
                var ass = Meta.AssemblyUtils.GetAssembly();
                var id = Meta.AssemblyUtils.GetMetadataAttribute(ass, "PluginId");
                return Guid.Parse(id);
            }
        }

        partial void BaseSetup(IPlayniteAPI api)
        {
            API = api;

#if DEV
            // Force exceptions to be in English
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Threading.Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;
#endif

            var ass = Meta.AssemblyUtils.GetAssembly();

            log.Debug($"Initializing {Meta.AssemblyUtils.GetTitle(ass)} v{Meta.AssemblyUtils.GetVersion(ass)}");

        }
    }

    namespace Meta
    {

        static public class AssemblyUtils
        {

            static public Assembly GetAssembly()
            {
                return typeof(Plugin).Assembly;
            }

            static public string GetVersion(Assembly assembly)
            {
                var version = assembly.GetName().Version;
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }

            static public string GetTitle(Assembly assembly)
            {
                return assembly.GetCustomAttribute<AssemblyTitleAttribute>().Title;
            }

            static public string GetDescription(Assembly assembly)
            {
                return assembly.GetCustomAttribute<AssemblyDescriptionAttribute>().Description;
            }

            static public string GetCompany(Assembly assembly)
            {
                return assembly.GetCustomAttribute<AssemblyCompanyAttribute>().Company;
            }

            static public string GetMetadataAttribute(Assembly assembly, string key)
            {
                var attrs = assembly.CustomAttributes.Where(a => a.AttributeType == typeof(AssemblyMetadataAttribute));

                if (attrs != null)
                {
                    var attr = attrs.Where(a => a.ConstructorArguments[0].Value.ToString() == key);

                    if (attr != null)
                    {
                        return attr.First().ConstructorArguments[1].Value.ToString();
                    }
                }

                return null;
            }
        }

    }

    namespace Common
    {

        class Path
        {
            public static string CommonRoot(string path1, string path2, bool caseSensitive = false)
            {
                var path1Parts = path1.Split('\\', '/');
                var path2Parts = path2.Split('\\', '/');

                var idx = -1;

                for (int i = 0; i < path1Parts.Length; i++)
                {
                    if (i >= path2Parts.Length)
                    {
                        break;
                    }

                    var a = caseSensitive ? path1Parts[i] : path1Parts[i].ToLower();
                    var b = caseSensitive ? path2Parts[i] : path2Parts[i].ToLower();

                    if (a != b)
                    {
                        break;
                    }

                    idx += a.Length + 1;
                }

                if (idx < 0)
                {
                    return "";
                }

                return path1.Substring(0, idx);
            }
        }

        class GuardedAction : IDisposable
        {
            private static readonly ILogger log = LogManager.GetLogger();

            private Exception exception = null;
            private ILogger logger = log;
            private string msg = "Unhandled exception";

            public GuardedAction(Action action)
            {
            }

            public GuardedAction(string msg, ILogger logger, Action action)
            {
                this.msg = msg ?? this.msg;
                this.logger = logger ?? log;
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    exception = e;
                }
            }



            public void Dispose()
            {
                if (exception != null)
                {
                    logger.Error(exception, $"{msg} - {exception.Message}");
                }
            }


            static public GuardedAction Call(string msg, ILogger logger, Action action) => new GuardedAction(msg, logger, action);
            static public GuardedAction Call(ILogger logger, Action action) => new GuardedAction(null, logger, action);
            static public GuardedAction Call(string msg, Action action) => new GuardedAction(msg, null, action);
            static public GuardedAction Call(Action action) => new GuardedAction(null, null, action);
        }


#if DEV
        public class TimeIt : IDisposable
        {
            private static readonly ILogger log = LogManager.GetLogger();

            private string name;
            private bool predicate;
            private Stopwatch stopwatch = new Stopwatch();

            public TimeIt(string name, bool predicate = true)
            {
                this.predicate = predicate;

                if (predicate)
                {
                    log.Trace($"Timing {name}");
                    this.name = name;
                    stopwatch.Start();
                }
            }

            public void Stop()
            {
                if (!predicate)
                {
                    return;
                }

                stopwatch.Stop();

                if (stopwatch.ElapsedMilliseconds > 10000)
                {
                    log.Error($"Timing {name} took {stopwatch.ElapsedMilliseconds} ms");
                }
                else if (stopwatch.ElapsedMilliseconds > 1000)
                {
                    log.Warn($"Timing {name} took {stopwatch.ElapsedMilliseconds} ms");
                }
                else
                {
                    log.Trace($"Timing {name} took {stopwatch.ElapsedMilliseconds} ms");
                }
            }

            public void Dispose()
            {
                Stop();
            }
        }
#else
        public class TimeIt : IDisposable
        {
            public TimeIt(string name)
            {

            }

            public void Stop()
            {
            }

            public void Dispose()
            {
            }
        }
#endif

        // see https://learn.microsoft.com/en-us/windows/win32/seccrypto/common-hresult-values
        public static class HRESULT
        {
            public const int E_ACCESSDENIED = unchecked((int)0x80070005);
        }

    }
}
