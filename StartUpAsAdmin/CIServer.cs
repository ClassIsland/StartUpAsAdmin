using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;

namespace ClassIslandService
{
    public partial class Service1 : ServiceBase
    {
        private const string EventSourceName = "ClassIslandService";
        private const string EventLogName = "Application";

        public Service1()
        {
            InitializeComponent();
            ServiceName = "ClassIslandService";
            CanStop = true;
            CanHandleSessionChangeEvent = true;

            // Ensure event source exists
            try
            {
                if (!EventLog.SourceExists(EventSourceName))
                {
                    EventLog.CreateEventSource(new EventSourceCreationData(EventSourceName, EventLogName));
                }
            }
            catch
            {
                // 忽略创建失败，继续使用默认 EventLog 写入（可能会失败）
            }
        }

        protected override void OnStart(string[] args)
        {
            LogInfo("服务启动");
            LaunchInActiveSession();
        }

        protected override void OnStop()
        {
            LogInfo("服务停止");
        }

        protected override void OnSessionChange(SessionChangeDescription changeDescription)
        {
            if (changeDescription.Reason == SessionChangeReason.SessionLogon ||
                changeDescription.Reason == SessionChangeReason.SessionUnlock)
            {
                LogInfo($"检测到会话变化: {changeDescription.Reason} (SessionId={changeDescription.SessionId})");
                LaunchInSession(changeDescription.SessionId);
            }
        }

        private void LaunchInActiveSession()
        {
            uint sessionId = WTSGetActiveConsoleSessionId();
            if (sessionId != 0xFFFFFFFF)
            {
                LaunchInSession((int)sessionId);
            }
            else
            {
                LogWarning("未找到活动控制台会话（WTSGetActiveConsoleSessionId 返回 0xFFFFFFFF）");
            }
        }

        private void LaunchInSession(int sessionId)
        {
            try
            {
                // 可根据实际安装路径决定 exePath，避免硬编码
                string exePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ClassIsland", "ClassIsland.exe");
                string arguments = "--uri classisland://app/settings/classisland.startUpAsAdmin";

                if (!File.Exists(exePath))
                {
                    LogError($"目标可执行文件不存在: {exePath}");
                    return;
                }

                if (LaunchProcessInSession(sessionId, exePath, arguments))
                {
                    LogInfo($"在会话 {sessionId} 中成功启动 ClassIsland");
                }
                else
                {
                    LogWarning($"在会话 {sessionId} 中启动 ClassIsland 失败");
                }
            }
            catch (Exception ex)
            {
                LogError($"启动异常: {ex}");
            }
        }

        private bool LaunchProcessInSession(int sessionId, string exePath, string arguments)
        {
            IntPtr userToken = IntPtr.Zero;
            IntPtr primaryToken = IntPtr.Zero;
            IntPtr environmentBlock = IntPtr.Zero;
            PROCESS_INFORMATION procInfo = new PROCESS_INFORMATION();
            try
            {
                if (!WTSQueryUserToken((uint)sessionId, out userToken))
                {
                    LogWin32Error("WTSQueryUserToken");
                    return false;
                }

                const uint TOKEN_ALL_ACCESS = 0xF01FF;
                const int SecurityImpersonation = 2;
                const int TokenPrimary = 1;

                if (!DuplicateTokenEx(userToken, TOKEN_ALL_ACCESS, IntPtr.Zero, SecurityImpersonation, TokenPrimary, out primaryToken))
                {
                    LogWin32Error("DuplicateTokenEx");
                    return false;
                }

                if (!CreateEnvironmentBlock(out environmentBlock, primaryToken, false))
                {
                    LogWin32Error("CreateEnvironmentBlock");
                    return false;
                }

                STARTUPINFO startupInfo = new STARTUPINFO();
                startupInfo.cb = Marshal.SizeOf(startupInfo);
                startupInfo.lpDesktop = "winsta0\\default";

                string commandLine = $"\"{exePath}\" {arguments}";
                string currentDirectory = Path.GetDirectoryName(exePath);

                const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;

                bool result = CreateProcessAsUser(
                    primaryToken,
                    null,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    CREATE_UNICODE_ENVIRONMENT,
                    environmentBlock,
                    currentDirectory,
                    ref startupInfo,
                    out procInfo);

                if (!result)
                {
                    LogWin32Error("CreateProcessAsUser");
                    return false;
                }

                // 关闭返回的句柄
                if (procInfo.hProcess != IntPtr.Zero) CloseHandle(procInfo.hProcess);
                if (procInfo.hThread != IntPtr.Zero) CloseHandle(procInfo.hThread);

                return true;
            }
            finally
            {
                if (environmentBlock != IntPtr.Zero) DestroyEnvironmentBlock(environmentBlock);
                if (primaryToken != IntPtr.Zero) CloseHandle(primaryToken);
                if (userToken != IntPtr.Zero) CloseHandle(userToken);
            }
        }

        #region Helpers & Logging
        private void LogInfo(string message) => SafeWriteEvent(message, EventLogEntryType.Information);
        private void LogWarning(string message) => SafeWriteEvent(message, EventLogEntryType.Warning);
        private void LogError(string message) => SafeWriteEvent(message, EventLogEntryType.Error);

        private void LogWin32Error(string apiName)
        {
            int err = Marshal.GetLastWin32Error();
            SafeWriteEvent($"{apiName} 失败，GetLastWin32Error={err}", EventLogEntryType.Error);
        }

        private void SafeWriteEvent(string message, EventLogEntryType type)
        {
            try
            {
                EventLog.WriteEntry(EventSourceName, message, type);
            }
            catch
            {
                // 最后兜底，避免抛出影响服务
                try { Trace.WriteLine($"{type}: {message}"); } catch { }
            }
        }
        #endregion

        #region Native
        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr phToken);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool DuplicateTokenEx(
            IntPtr hExistingToken,
            uint dwDesiredAccess,
            IntPtr lpTokenAttributes,
            int ImpersonationLevel,
            int TokenType,
            out IntPtr phNewToken);

        [DllImport("userenv.dll", SetLastError = true)]
        private static extern bool CreateEnvironmentBlock(out IntPtr lpEnvironment, IntPtr hToken, bool bInherit);

        [DllImport("userenv.dll", SetLastError = true)]
        private static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcessAsUser(
            IntPtr hToken,
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        private static extern uint WTSGetActiveConsoleSessionId();

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }
        #endregion
    }
}