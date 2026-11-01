using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace LightAudioSwitch
{
    public sealed class SwitchController : IDisposable
    {
        public readonly AudioService Audio;
        public readonly RoutePolicy Policy = new RoutePolicy();
        public AppConfig Config { get; private set; }
        public List<AudioDevice> Devices { get; private set; }
        public string Status { get; private set; }
        public string CurrentOutput { get; private set; }
        public string RunningGame { get; private set; }
        public event Action Changed;
        public event Action<string> Warning;
        private readonly string configPath;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private bool dirty = true;
        private double refreshAt;
        private double retryAt;
        private int failureCount;
        private string lastTarget;
        private string lastWarning;
        private string currentId;
        public SwitchController(string configPath)
        {
            this.configPath = configPath;
            bool exists = File.Exists(configPath);
            Config = ConfigStore.Load(configPath);
            Audio = new AudioService();
            try
            {
                Devices = Audio.Enumerate();
                if (!exists)
                {
                    Config.Games.Add(new GameRule { Name = "三角洲行动", Executable = "DeltaForceClient-Win64-Shipping.exe",
                        FullPath = null });
                }
                if (!exists) ConfigStore.Save(configPath, Config);
                Policy.ExitDelaySeconds = Config.ExitDelaySeconds;
                refreshAt = 30;
            }
            catch { Audio.Dispose(); throw; }
        }
        public void SetMode(OutputMode mode)
        {
            Policy.Mode = mode;
            retryAt = 0;
            failureCount = 0;
            dirty = true;
            lastWarning = null;
            Tick();
        }
        public void ApplySettings(AppConfig config, bool startup)
        {
            config.Validate();
            bool wasEnabled = StartupRegistration.IsEnabled();
            if (wasEnabled != startup) StartupRegistration.SetEnabled(startup);
            try { ConfigStore.Save(configPath, config); }
            catch
            {
                if (wasEnabled != startup) StartupRegistration.SetEnabled(wasEnabled);
                throw;
            }
            Config = config;
            Policy.ExitDelaySeconds = Config.ExitDelaySeconds;
            dirty = true;
            retryAt = 0;
            failureCount = 0;
            lastWarning = null;
            Tick();
        }
        public List<AudioDevice> RefreshDevices()
        {
            Devices = Audio.Enumerate();
            dirty = true;
            retryAt = 0;
            return Devices;
        }
        public void Tick()
        {
            double now = clock.Elapsed.TotalSeconds;
            try
            {
                bool notification = Audio.ConsumeChanged();
                if (notification || now >= refreshAt)
                {
                    Devices = Audio.Enumerate();
                    currentId = Audio.GetDefault(1);
                    refreshAt = now + 30;
                    dirty = true;
                }
                // A failed process snapshot must not be interpreted as "game exited".
                RunningGame = Policy.Mode == OutputMode.Auto ? ProcessScanner.FindGame(ProcessScanner.Snapshot(), Config.Games) : null;
                OutputMode target = Policy.Evaluate(RunningGame != null, now);
                string id = target == OutputMode.Headset ? Config.HeadsetId : Config.SpeakerId;
                string targetLabel = target == OutputMode.Headset ? "耳机" : "小爱音箱";
                string phase = Policy.Mode == OutputMode.Auto
                    ? (RunningGame != null ? "游戏模式 · " + RunningGame : Policy.RemainingSeconds > 0
                        ? "游戏已退出 · " + Policy.RemainingSeconds + " 秒后切回音箱" : "日常模式")
                    : "手动锁定 · " + targetLabel;
                if (!String.Equals(id, lastTarget, StringComparison.OrdinalIgnoreCase))
                {
                    lastTarget = id; dirty = true; retryAt = 0; failureCount = 0; lastWarning = null;
                }
                var device = Devices.Find(delegate(AudioDevice d) { return d.Id == id; });
                if (device == null || !device.Active)
                {
                    string warning = String.IsNullOrEmpty(id) ? "请在设置中选择" + targetLabel : targetLabel + "未连接；保留当前输出";
                    SetStatus(phase + "\n" + warning);
                    WarnOnce(warning);
                    return;
                }
                if (dirty && now >= retryAt)
                {
                    if (!Audio.IsDefault(id, Config.SwitchCommunications))
                    {
                        Audio.SetDefault(id, Config.SwitchCommunications);
                        AppLog.Write("切换输出：" + device.Name + "（" + phase + "）");
                    }
                    currentId = id;
                    dirty = false;
                    failureCount = 0;
                    retryAt = 0;
                    lastWarning = null;
                }
                if (failureCount > 0) SetStatus(phase + "\n切换未成功，正在等待重试");
                else SetStatus(phase + "\n" + device.Name);
            }
            catch (Exception ex)
            {
                dirty = true;
                failureCount++;
                retryAt = now + Math.Min(30, 5 * Math.Max(1, failureCount));
                string warning = "切换暂未完成：" + ex.Message;
                SetStatus(warning);
                WarnOnce(warning);
            }
        }
        private void SetStatus(string value)
        {
            var current = Devices == null ? null : Devices.Find(delegate(AudioDevice d) { return d.Id == currentId; });
            string output = current == null ? "等待设备信息" : current.Name;
            if (Status == value && CurrentOutput == output) return;
            Status = value;
            CurrentOutput = output;
            if (Changed != null) Changed();
        }
        private void WarnOnce(string warning)
        {
            if (lastWarning == warning) return;
            lastWarning = warning;
            AppLog.Write(warning);
            if (Warning != null) Warning(warning);
        }
        public void Dispose() { Audio.Dispose(); }
    }
}
