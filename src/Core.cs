using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Microsoft.Win32;

namespace LightAudioSwitch
{
    [DataContract]
    public sealed class GameRule
    {
        [DataMember(Order = 0)] public string Name { get; set; }
        [DataMember(Order = 1)] public string Executable { get; set; }
        [DataMember(Order = 2)] public string FullPath { get; set; }
        public override string ToString() { return Name + "  ·  " + Executable; }
    }
    [DataContract]
    public sealed class AppConfig
    {
        [DataMember(Order = 0)] public int Version = 1;
        [DataMember(Order = 1)] public string HeadsetId;
        [DataMember(Order = 2)] public string HeadsetName;
        [DataMember(Order = 3)] public string SpeakerId;
        [DataMember(Order = 4)] public string SpeakerName;
        [DataMember(Order = 5)] public int ExitDelaySeconds = 3;
        [DataMember(Order = 6)] public bool SwitchCommunications = false;
        [DataMember(Order = 7)] public List<GameRule> Games = new List<GameRule>();
        public void Validate()
        {
            if (Version != 1) throw new InvalidDataException("配置版本不受支持，请保留配置并使用对应版本的程序。");
            if (ExitDelaySeconds < 0 || ExitDelaySeconds > 30) throw new InvalidDataException("退出延迟必须在 0～30 秒之间。");
            if (Games == null) Games = new List<GameRule>();
            if (Games.Count > 100) throw new InvalidDataException("最多支持 100 条游戏规则。");
            if (!String.IsNullOrEmpty(HeadsetId) && HeadsetId == SpeakerId) throw new InvalidDataException("耳机与音箱不能选择同一设备。");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var game in Games)
            {
                if (game == null || String.IsNullOrWhiteSpace(game.Executable)) throw new InvalidDataException("游戏规则缺少程序名称。");
                game.Executable = Path.GetFileName(game.Executable.Trim());
                if (!game.Executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("游戏程序需要是 .exe 文件。");
                if (IsLauncher(game.Executable)) throw new InvalidDataException("请添加游戏本体，不要添加 WeGame、浏览器或反作弊程序。");
                string key = game.Executable + "|" + (game.FullPath ?? "");
                if (!names.Add(key)) throw new InvalidDataException("存在重复的游戏规则。");
                if (String.IsNullOrWhiteSpace(game.Name)) game.Name = Path.GetFileNameWithoutExtension(game.Executable);
                if (!String.IsNullOrEmpty(game.FullPath)) game.FullPath = Path.GetFullPath(game.FullPath);
            }
        }
        public static bool IsLauncher(string name)
        {
            string n = name.ToLowerInvariant();
            return n == "wegame.exe" || n == "wegame_env.exe" || n == "tgp_daemon.exe"
                || n == "chrome.exe" || n == "msedge.exe" || n == "steam.exe"
                || n == "ace-service64.exe" || n == "ace-helper.exe" || n.StartsWith("sguard")
                || n == "lightaudioswitch.exe";
        }
        public AppConfig Copy()
        {
            var copy = new AppConfig { Version = Version, HeadsetId = HeadsetId, HeadsetName = HeadsetName,
                SpeakerId = SpeakerId, SpeakerName = SpeakerName, ExitDelaySeconds = ExitDelaySeconds,
                SwitchCommunications = SwitchCommunications };
            foreach (var g in Games) copy.Games.Add(new GameRule { Name = g.Name, Executable = g.Executable, FullPath = g.FullPath });
            return copy;
        }
        public static AudioDevice UniqueMatch(List<AudioDevice> devices, string fragment)
        {
            var matches = devices.FindAll(delegate(AudioDevice d) { return d.Name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0; });
            var active = matches.FindAll(delegate(AudioDevice d) { return d.Active; });
            if (active.Count == 1) return active[0];
            return matches.Count == 1 ? matches[0] : null;
        }
    }
    public static class ConfigStore
    {
        public static AppConfig Load(string path)
        {
            if (!File.Exists(path)) return new AppConfig();
            if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("配置文件过大。");
            using (var stream = File.OpenRead(path))
            {
                var c = (AppConfig)new DataContractJsonSerializer(typeof(AppConfig)).ReadObject(stream);
                if (c == null) throw new InvalidDataException("配置内容为空。");
                c.Validate();
                return c;
            }
        }
        public static void Save(string path, AppConfig config)
        {
            config.Validate();
            string temporary = path + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                new DataContractJsonSerializer(typeof(AppConfig)).WriteObject(stream, config);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
    }
    public enum OutputMode { Auto, Headset, Speaker }
    public sealed class RoutePolicy
    {
        public OutputMode Mode = OutputMode.Auto;
        public int ExitDelaySeconds = 3;
        private bool wasGaming;
        private double? exitAt;
        public int RemainingSeconds { get; private set; }
        public OutputMode Evaluate(bool gaming, double seconds)
        {
            RemainingSeconds = 0;
            if (Mode != OutputMode.Auto)
            {
                wasGaming = false; exitAt = null;
                return Mode;
            }
            if (gaming)
            {
                wasGaming = true; exitAt = null;
                return OutputMode.Headset;
            }
            if (wasGaming) { exitAt = seconds; wasGaming = false; }
            if (exitAt.HasValue)
            {
                double left = ExitDelaySeconds - (seconds - exitAt.Value);
                if (left > 0) { RemainingSeconds = (int)Math.Ceiling(left); return OutputMode.Headset; }
                exitAt = null;
            }
            return OutputMode.Speaker;
        }
    }

    public sealed class ProcessEntry
    {
        public uint Id;
        public string Name;
        public override string ToString() { return Name + "  (PID " + Id + ")"; }
    }
    public static class ProcessScanner
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PROCESSENTRY32
        {
            public uint dwSize, cntUsage, th32ProcessID;
            public UIntPtr th32DefaultHeapID;
            public uint th32ModuleID, cntThreads, th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
        }
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
        [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool Process32First(IntPtr snapshot, ref PROCESSENTRY32 entry);
        [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool Process32Next(IntPtr snapshot, ref PROCESSENTRY32 entry);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder path, ref uint size);
        public static List<ProcessEntry> Snapshot()
        {
            var result = new List<ProcessEntry>();
            IntPtr handle = CreateToolhelp32Snapshot(2, 0); // Processes only, never modules or memory.
            if (handle == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var entry = new PROCESSENTRY32();
                entry.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
                if (!Process32First(handle, ref entry)) throw new Win32Exception(Marshal.GetLastWin32Error());
                do { result.Add(new ProcessEntry { Id = entry.th32ProcessID, Name = entry.szExeFile }); }
                while (Process32Next(handle, ref entry));
                int error = Marshal.GetLastWin32Error();
                if (error != 18) throw new Win32Exception(error); // ERROR_NO_MORE_FILES
            }
            finally { CloseHandle(handle); }
            return result;
        }
        public static string TryGetPath(uint id)
        {
            IntPtr handle = OpenProcess(0x1000, false, id); // QUERY_LIMITED_INFORMATION only.
            if (handle == IntPtr.Zero) return null;
            try
            {
                var buffer = new StringBuilder(32768);
                uint size = (uint)buffer.Capacity;
                return QueryFullProcessImageName(handle, 0, buffer, ref size) ? buffer.ToString() : null;
            }
            finally { CloseHandle(handle); }
        }
        public static string FindGame(List<ProcessEntry> processes, List<GameRule> games)
        {
            foreach (var process in processes)
                foreach (var game in games)
                    if (String.Equals(process.Name, game.Executable, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!String.IsNullOrEmpty(game.FullPath))
                        {
                            string path = TryGetPath(process.Id);
                            // Protected games may deny path queries. Their exact executable
                            // name is still available through Toolhelp; no elevation is needed.
                            if (path != null && !String.Equals(path, game.FullPath, StringComparison.OrdinalIgnoreCase)) continue;
                        }
                        return game.Name;
                    }
            return null;
        }
    }
    public static class StartupRegistration
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "LightAudioSwitch";
        private static string Command { get { return "\"" + System.Windows.Forms.Application.ExecutablePath + "\" --background"; } }
        public static bool IsEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                return key != null && String.Equals(key.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase);
        }
        public static void SetEnabled(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled) key.SetValue(ValueName, Command, RegistryValueKind.String);
                else key.DeleteValue(ValueName, false);
            }
        }
    }
    public static class AppLog
    {
        public static string Folder;
        private static string last;
        public static void Write(string message)
        {
            if (message == last) return;
            last = message;
            try
            {
                string path = Path.Combine(Folder, "运行日志.txt");
                if (File.Exists(path) && new FileInfo(path).Length > 128 * 1024)
                {
                    string backup = path + ".old";
                    if (File.Exists(backup)) File.Delete(backup);
                    File.Move(path, backup);
                }
                File.AppendAllText(path, DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz") + "  " + message + Environment.NewLine, Encoding.UTF8);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
