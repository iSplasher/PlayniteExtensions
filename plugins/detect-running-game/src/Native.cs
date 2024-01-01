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
using System.Security.Permissions;
using System.Security.Principal;
using Playnite.SDK;

namespace Plugin
{
    public static class NativeMethods
    {
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        private const uint FILE_READ_EA = 0x0008;
        private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x2000000;

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern uint GetFinalPathNameByHandle(IntPtr hFile, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder lpszFilePath, uint cchFilePath, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CreateFile(
                [MarshalAs(UnmanagedType.LPTStr)] string filename,
                [MarshalAs(UnmanagedType.U4)] uint access,
                [MarshalAs(UnmanagedType.U4)] FileShare share,
                IntPtr securityAttributes, // optional SECURITY_ATTRIBUTES struct or IntPtr.Zero
                [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
                [MarshalAs(UnmanagedType.U4)] uint flagsAndAttributes,
                IntPtr templateFile);

        public static string GetFinalPathName(string path)
        {
            var h = CreateFile(path,
                FILE_READ_EA,
                FileShare.ReadWrite | FileShare.Delete,
                IntPtr.Zero,
                FileMode.Open,
                FILE_FLAG_BACKUP_SEMANTICS,
                IntPtr.Zero);
            if (h == INVALID_HANDLE_VALUE)
                return path;

            try
            {
                var sb = new StringBuilder(1024);
                var res = GetFinalPathNameByHandle(h, sb, 1024, 0);
                if (res == 0)
                {
                    return path;
                }


                return sb.ToString().Replace(@"\\?\", "");
            }
            finally
            {
                CloseHandle(h);
            }
        }

        public static IList<Process> QueryChildProcesses(this Process process)
        => new ManagementObjectSearcher(
                $"Select ProcessID From Win32_Process Where ParentProcessID={process.Id}")
            .Get()
            .Cast<ManagementObject>()
            .Select(mo =>
                Process.GetProcessById(Convert.ToInt32(mo["ProcessID"])))
            .ToList();


        public static T QueryProcessProperty<T>(this Process process, string property)
        {
            var query = $"Select {property} From Win32_Process Where ProcessID={process.Id}";
            var searcher = new ManagementObjectSearcher(query);
            var result = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
            if (result != null)
            {
                return (T)result[property];
            }
            return default;
        }

        [DllImport("Kernel32.dll")]
        private static extern bool QueryFullProcessImageName([In] IntPtr hProcess, [In] uint dwFlags, [Out] StringBuilder lpExeName, [In, Out] ref uint lpdwSize);

        public static string GetMainModuleFileName(this Process process, bool retry = true, int buffer = 1024)
        {
            var fileNameBuilder = new StringBuilder(buffer);
            uint bufferLength = (uint)fileNameBuilder.Capacity + 1;
            bool r = false;
            try
            {

                r = QueryFullProcessImageName(process.Handle, 0, fileNameBuilder, ref bufferLength);
            }
            catch
            {
                if (retry)
                {
                    IntPtr localProcessObj = IntPtr.Zero;
                    try
                    {

                        if (localProcessObj == IntPtr.Zero)
                        {
                            // try to open process with QueryLimitedInformation
                            localProcessObj = OpenProcess(process, ProcessAccessFlags.QueryLimitedInformation);

                            if (localProcessObj == IntPtr.Zero)
                            {
                                int err = Marshal.GetLastWin32Error();
                                throw new Win32Exception(err);
                            }
                        }

                        if (localProcessObj != IntPtr.Zero)
                        {
                            r = QueryFullProcessImageName(localProcessObj, 0, fileNameBuilder, ref bufferLength);
                        }
                    }
                    finally
                    {
                        if (localProcessObj != IntPtr.Zero) CloseHandle(localProcessObj);
                    }
                }

                if (!r)
                {
                    throw;
                }
            }

            return r ?
                fileNameBuilder.ToString() :
                String.Empty;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);


        public static string Owner(this Process process)
        {
            const UInt32 TOKEN_QUERY = 0x0008;

            IntPtr tokenHandle = IntPtr.Zero;
            IntPtr localProcessObj = IntPtr.Zero;
            try
            {
                if (localProcessObj == IntPtr.Zero)
                {
                    // try to open process with QueryLimitedInformation
                    localProcessObj = OpenProcess(process, ProcessAccessFlags.QueryLimitedInformation);

                    if (localProcessObj == IntPtr.Zero)
                    {
                        int err = Marshal.GetLastWin32Error();
                        throw new Win32Exception(err);
                    }
                };

                OpenProcessToken(localProcessObj, TOKEN_QUERY, out tokenHandle);
                if (tokenHandle == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new Win32Exception(err);
                }
                using (WindowsIdentity wi = new WindowsIdentity(tokenHandle))
                {
                    return wi.Name;
                }
            }
            finally
            {
                if (tokenHandle != IntPtr.Zero) CloseHandle(tokenHandle);
                if (localProcessObj != IntPtr.Zero) CloseHandle(localProcessObj);
            }
        }

        public static int ParentProcessId(this Process process)
        {
            return ParentProcessId(process.Id);
        }
        public static int ParentProcessId(int Id)
        {
            PROCESSENTRY32 pe32 = new PROCESSENTRY32 { };
            pe32.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
            using (var hSnapshot = CreateToolhelp32Snapshot(SnapshotFlags.Process, (uint)Id))
            {
                if (hSnapshot.IsInvalid)
                {
                    throw new Win32Exception();
                }

                if (!Process32First(hSnapshot, ref pe32))
                {
                    int errno = Marshal.GetLastWin32Error();
                    if (errno == ERROR_NO_MORE_FILES)
                        return -1;
                    throw new Win32Exception(errno);
                }
                do
                {
                    if (pe32.th32ProcessID == (uint)Id)
                        return (int)pe32.th32ParentProcessID;
                } while (Process32Next(hSnapshot, ref pe32));
            }
            return -1;
        }
        private const int ERROR_NO_MORE_FILES = 0x12;
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeSnapshotHandle CreateToolhelp32Snapshot(SnapshotFlags flags, uint id);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Process32First(SafeSnapshotHandle hSnapshot, ref PROCESSENTRY32 lppe);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Process32Next(SafeSnapshotHandle hSnapshot, ref PROCESSENTRY32 lppe);

        [Flags]
        private enum SnapshotFlags : uint
        {
            HeapList = 0x00000001,
            Process = 0x00000002,
            Thread = 0x00000004,
            Module = 0x00000008,
            Module32 = 0x00000010,
            All = (HeapList | Process | Thread | Module),
            Inherit = 0x80000000,
            NoHeaps = 0x40000000
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESSENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
        };
        [SuppressUnmanagedCodeSecurity, HostProtection(SecurityAction.LinkDemand, MayLeakOnAbort = true)]
        internal sealed class SafeSnapshotHandle : SafeHandleMinusOneIsInvalid
        {
            internal SafeSnapshotHandle() : base(true)
            {
            }

            [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
            internal SafeSnapshotHandle(IntPtr handle) : base(true)
            {
                base.SetHandle(handle);
            }

            protected override bool ReleaseHandle()
            {
                return CloseHandle(base.handle);
            }

            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success), DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
            private static extern bool CloseHandle(IntPtr handle);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(
             uint processAccess,
             bool bInheritHandle,
             uint processId
        );
        [Flags]
        public enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VirtualMemoryOperation = 0x00000008,
            VirtualMemoryRead = 0x00000010,
            VirtualMemoryWrite = 0x00000020,
            DuplicateHandle = 0x00000040,
            CreateProcess = 0x000000080,
            SetQuota = 0x00000100,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            QueryLimitedInformation = 0x00001000,
            Synchronize = 0x00100000
        }
        private static IntPtr OpenProcess(Process proc, ProcessAccessFlags flags)
        {
            return OpenProcess((uint)flags, false, (uint)proc.Id);
        }
    }


}
