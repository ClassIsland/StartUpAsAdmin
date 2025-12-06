using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;

namespace StartUpAsAdmin
{
    public static class CIServerServiceInstaller
    {
        public const string DefaultServiceName = "CIServerService";
        public const string DefaultDisplayName = "CIServer Service";
        public const string DefaultDescription = "启动 CIServer.exe(由插件创建)";

        public static void InstallService(string serviceName, string displayName, string description, string exePath)
        {
            if (string.IsNullOrWhiteSpace(serviceName)) throw new ArgumentNullException(nameof(serviceName));
            if (string.IsNullOrWhiteSpace(exePath)) throw new ArgumentNullException(nameof(exePath));
            if (!File.Exists(exePath)) throw new FileNotFoundException("指定的可执行文件不存在。", exePath);

            IntPtr scm = Native.OpenSCManager(null, null, Native.SC_MANAGER_CREATE_SERVICE);
            if (scm == IntPtr.Zero) ThrowLastError("OpenSCManager");

            try
            {
                // 如果已存在则删除再创建（以保证路径更新）
                IntPtr existing = Native.OpenService(scm, serviceName, Native.SERVICE_ALL_ACCESS);
                if (existing != IntPtr.Zero)
                {
                    try
                    {
                        // 尝试停止
                        try { StopServiceInternal(existing); } catch { }
                        if (!Native.DeleteService(existing)) ThrowLastError("DeleteService");
                    }
                    finally
                    {
                        Native.CloseServiceHandle(existing);
                    }
                }

                string binaryPath = $"\"{exePath}\"";
                IntPtr service = Native.CreateService(
                    scm,
                    serviceName,
                    displayName ?? serviceName,
                    Native.SERVICE_ALL_ACCESS,
                    Native.SERVICE_WIN32_OWN_PROCESS,
                    Native.SERVICE_AUTO_START,
                    Native.SERVICE_ERROR_NORMAL,
                    binaryPath,
                    null,
                    IntPtr.Zero,
                    null,
                    null,
                    null);

                if (service == IntPtr.Zero) ThrowLastError("CreateService");

                try
                {
                    // 设置描述
                    if (!string.IsNullOrEmpty(description))
                    {
                        var desc = new Native.SERVICE_DESCRIPTION { lpDescription = description };
                        IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(desc));
                        try
                        {
                            Marshal.StructureToPtr(desc, ptr, false);
                            if (!Native.ChangeServiceConfig2(service, Native.SERVICE_CONFIG_DESCRIPTION, ptr))
                                ThrowLastError("ChangeServiceConfig2");
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(ptr);
                        }
                    }
                }
                finally
                {
                    Native.CloseServiceHandle(service);
                }
            }
            finally
            {
                Native.CloseServiceHandle(scm);
            }
        }

        public static void UninstallService(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName)) throw new ArgumentNullException(nameof(serviceName));
            IntPtr scm = Native.OpenSCManager(null, null, Native.SC_MANAGER_CONNECT);
            if (scm == IntPtr.Zero) ThrowLastError("OpenSCManager");

            try
            {
                IntPtr service = Native.OpenService(scm, serviceName, Native.SERVICE_ALL_ACCESS);
                if (service == IntPtr.Zero) ThrowLastError("OpenService");

                try
                {
                    // stop if running
                    try { StopServiceInternal(service); } catch { }
                    if (!Native.DeleteService(service)) ThrowLastError("DeleteService");
                }
                finally
                {
                    Native.CloseServiceHandle(service);
                }
            }
            finally
            {
                Native.CloseServiceHandle(scm);
            }
        }

        public static bool IsServiceInstalled(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName)) throw new ArgumentNullException(nameof(serviceName));
            using var sc = new ServiceController(serviceName);
            try
            {
                var _ = sc.Status; // 如果不存在会抛异常
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        public static void StartService(string serviceName, int timeoutMilliseconds = 15000)
        {
            if (string.IsNullOrWhiteSpace(serviceName)) throw new ArgumentNullException(nameof(serviceName));
            using var sc = new ServiceController(serviceName);
            if (sc.Status == ServiceControllerStatus.Running) return;

            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMilliseconds(timeoutMilliseconds));
        }

        private static void StopServiceInternal(IntPtr serviceHandle)
        {
            // CONTROL_STOP = 0x00000001
            var status = new Native.SERVICE_STATUS();
            if (!Native.ControlService(serviceHandle, Native.SERVICE_CONTROL_STOP, ref status))
            {
                // 如果无法停止就忽略
            }
            else
            {
                // 等待停止完成（轮询）
                int retries = 30;
                while (retries-- > 0)
                {
                    Native.QueryServiceStatus(serviceHandle, out status);
                    if (status.dwCurrentState == Native.SERVICE_STOPPED) break;
                    Thread.Sleep(200);
                }
            }
        }

        private static void ThrowLastError(string apiName)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"调用 {apiName} 失败。");
        }

        private static class Native
        {
            public const uint SC_MANAGER_CONNECT = 0x0001;
            public const uint SC_MANAGER_CREATE_SERVICE = 0x0002;
            public const uint SERVICE_ALL_ACCESS = 0xF01FF;
            public const uint SERVICE_WIN32_OWN_PROCESS = 0x00000010;
            public const uint SERVICE_AUTO_START = 0x00000002;
            public const uint SERVICE_DEMAND_START = 0x00000003;
            public const uint SERVICE_ERROR_NORMAL = 0x00000001;
            public const uint SERVICE_CONTROL_STOP = 0x00000001;
            public const uint SERVICE_QUERY_STATUS = 0x0004;
            public const uint SERVICE_START = 0x0010;
            public const uint SERVICE_STOP = 0x0020;
            public const uint SERVICE_CHANGE_CONFIG = 0x0002;
            public const uint SERVICE_CONFIG_DESCRIPTION = 1;

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct SERVICE_DESCRIPTION
            {
                public string lpDescription;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct SERVICE_STATUS
            {
                public uint dwServiceType;
                public uint dwCurrentState;
                public uint dwControlsAccepted;
                public uint dwWin32ExitCode;
                public uint dwServiceSpecificExitCode;
                public uint dwCheckPoint;
                public uint dwWaitHint;
            }

            [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr OpenSCManager(string machineName, string databaseName, uint dwAccess);

            [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr CreateService(
                IntPtr hSCManager,
                string lpServiceName,
                string lpDisplayName,
                uint dwDesiredAccess,
                uint dwServiceType,
                uint dwStartType,
                uint dwErrorControl,
                string lpBinaryPathName,
                string lpLoadOrderGroup,
                IntPtr lpdwTagId,
                string lpDependencies,
                string lpServiceStartName,
                string lpPassword);

            [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern int CloseServiceHandle(IntPtr hSCObject);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool DeleteService(IntPtr hService);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool StartService(IntPtr hService, int dwNumServiceArgs, string[] lpServiceArgVectors);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool ControlService(IntPtr hService, uint dwControl, ref SERVICE_STATUS lpServiceStatus);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool QueryServiceStatus(IntPtr hService, out SERVICE_STATUS lpServiceStatus);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool ChangeServiceConfig2(IntPtr hService, uint dwInfoLevel, IntPtr lpInfo);
        }
    }
}