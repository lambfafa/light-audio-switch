using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using LightAudioSwitch;

class Verify
{
    static List<string> passed = new List<string>();
    static void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); passed.Add("PASS " + name); Console.WriteLine("PASS " + name); }
    static void Reject(Action action, string name) { bool rejected = false; try { action(); } catch { rejected = true; } Check(rejected, name); }
    static void Pump(int milliseconds)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(20); }
    }
    [STAThread]
    static int Main(string[] args)
    {
        string folder = Path.GetFullPath(args[0]);
        string report = Path.GetFullPath(args[1]);
        AppLog.Folder = folder;
        string[] original = null;
        try
        {
            var p = new RoutePolicy();
            Check(p.Evaluate(false, 0) == OutputMode.Speaker, "启动无游戏 -> 音箱");
            Check(p.Evaluate(true, 1) == OutputMode.Headset, "识别游戏 -> 耳机");
            Check(p.Evaluate(true, 5) == OutputMode.Headset, "游戏持续运行 -> 保持耳机");
            Check(p.Evaluate(false, 6) == OutputMode.Headset && p.RemainingSeconds == 3, "退出开始三秒防抖");
            Check(p.Evaluate(false, 8.99) == OutputMode.Headset, "退出延迟未到不切换");
            Check(p.Evaluate(false, 9) == OutputMode.Speaker, "三秒后返回音箱");
            p.Evaluate(true, 10); p.Evaluate(false, 11); p.Evaluate(true, 12);
            Check(p.Evaluate(true, 20) == OutputMode.Headset && p.RemainingSeconds == 0, "重新进入游戏取消待执行切换");
            p.Mode = OutputMode.Speaker;
            Check(p.Evaluate(true, 21) == OutputMode.Speaker, "手动锁定音箱优先于游戏");
            p.Mode = OutputMode.Headset;
            Check(p.Evaluate(false, 22) == OutputMode.Headset, "无游戏时保持手动耳机锁定");
            p.Mode = OutputMode.Auto;
            Check(p.Evaluate(false, 23) == OutputMode.Speaker, "解除锁定恢复自动日常模式");
            p.ExitDelaySeconds = 0; p.Evaluate(true, 24);
            Check(p.Evaluate(false, 25) == OutputMode.Speaker, "零秒延迟立即切回");

            var cfg = new AppConfig();
            cfg.Games.Add(new GameRule { Name = "测试游戏", Executable = "AudioSwitchTestGame.exe" });
            var launchers = new List<ProcessEntry> { new ProcessEntry { Id = 0, Name = "wegame.exe" } };
            Check(ProcessScanner.FindGame(launchers, cfg.Games) == null, "WeGame 常驻不会触发游戏模式");
            var twoGames = new List<ProcessEntry> { new ProcessEntry { Id = 0, Name = "AUDIOSWITCHTESTGAME.EXE" }, new ProcessEntry { Id = 1, Name = "Other.exe" } };
            Check(ProcessScanner.FindGame(twoGames, cfg.Games) == "测试游戏", "游戏名大小写不敏感");
            var self = Process.GetCurrentProcess();
            var pathRule = new List<GameRule> { new GameRule { Name = "路径检查", Executable = Path.GetFileName(self.MainModule.FileName), FullPath = @"C:\not-the-same.exe" } };
            Check(ProcessScanner.FindGame(ProcessScanner.Snapshot(), pathRule) == null, "可查询时拒绝同名不同路径");
            Check(ProcessScanner.TryGetPath((uint)self.Id) == self.MainModule.FileName, "仅查询进程路径接口工作正常");
            cfg.ExitDelaySeconds = 40; Reject(cfg.Validate, "拒绝超范围延迟"); cfg.ExitDelaySeconds = 3;
            cfg.HeadsetId = "same"; cfg.SpeakerId = "same"; Reject(cfg.Validate, "拒绝耳机音箱重复设备"); cfg.HeadsetId = null; cfg.SpeakerId = null;
            var bad = new AppConfig(); bad.Games.Add(new GameRule { Name = "启动器", Executable = "wegame.exe" });
            Reject(bad.Validate, "拒绝添加 WeGame 启动器");
            var mock = new List<AudioDevice> { new AudioDevice { Id = "a", Name = "Example Headset", State = 1 }, new AudioDevice { Id = "b", Name = "Example Headset", State = 4 } };
            Check(AppConfig.UniqueMatch(mock, "Example Headset").Id == "a", "设备匹配优先唯一在线端点");
            mock[1].State = 1;
            Check(AppConfig.UniqueMatch(mock, "Example Headset") == null, "同名在线设备有歧义时不猜测");
            cfg.HeadsetId = "saved";
            Check(cfg.Copy().HeadsetId == "saved", "复制配置时保留设备选择");
            string testConfig = Path.Combine(folder, "roundtrip.json");
            ConfigStore.Save(testConfig, cfg); cfg.ExitDelaySeconds = 4; ConfigStore.Save(testConfig, cfg);
            Check(ConfigStore.Load(testConfig).ExitDelaySeconds == 4 && ConfigStore.Load(testConfig + ".bak").ExitDelaySeconds == 3, "配置原子保存、读取和备份");
            File.WriteAllText(testConfig, "not json");
            Reject(delegate { ConfigStore.Load(testConfig); }, "损坏配置报错");
            Check(File.ReadAllText(testConfig) == "not json", "损坏配置不被静默覆盖");

            if (args.Length < 4)
            {
                File.WriteAllText(report, String.Join("\r\n", passed) + "\r\n硬件测试未启用；此测试未修改音频设备。\r\n", Encoding.UTF8);
                return 0;
            }
            using (var audio = new AudioService())
            {
                original = audio.GetDefaults();
                var all = audio.Enumerate();
                var h = AppConfig.UniqueMatch(all, args[2]);
                var s = AppConfig.UniqueMatch(all, args[3]);
                Check(h != null && h.Active && s != null && s.Active && h.Id != s.Id, "指定的两台真实设备均在线、唯一且不同");
                audio.ConsumeChanged();
                try
                {
                    audio.SetDefault(h.Id, false); Pump(300);
                    Check(audio.IsDefault(h.Id, false), "真实设备：切换到耳机并读回确认");
                    Check(audio.GetDefault(2) == original[2], "常规切换保留默认通信输出");
                    Check(audio.ConsumeChanged(), "默认音频设备事件通知正常");
                    audio.SetDefault(s.Id, false); Pump(250);
                    Check(audio.IsDefault(s.Id, false), "真实设备：切换到小爱音箱并读回确认");
                    Reject(delegate { audio.SetDefault("missing-device-id", false); }, "拒绝切换到不存在的设备");
                    Check(audio.IsDefault(s.Id, false), "目标不可用时保留现有输出");

                    var runConfig = new AppConfig { HeadsetId = h.Id, HeadsetName = h.Name,
                        SpeakerId = s.Id, SpeakerName = s.Name, ExitDelaySeconds = 3 };
                    runConfig.Games.Add(new GameRule { Name = "受控测试进程", Executable = "AudioSwitchTestGame.exe", FullPath = Path.Combine(folder, "AudioSwitchTestGame.exe") });
                    string runPath = Path.Combine(folder, "integration-config.json");
                    ConfigStore.Save(runPath, runConfig);
                    Process fixture = null, fixture2 = null;
                    using (var controller = new SwitchController(runPath))
                    {
                        controller.Tick(); Check(audio.IsDefault(s.Id, false), "完整控制器：无游戏使用音箱");
                        try
                        {
                            fixture = Process.Start(new ProcessStartInfo(runConfig.Games[0].FullPath) { UseShellExecute = false, CreateNoWindow = true });
                            Pump(150); controller.Tick();
                            Check(controller.RunningGame == "受控测试进程" && audio.IsDefault(h.Id, false), "完整控制器：检测真实测试进程并切耳机");
                            controller.SetMode(OutputMode.Speaker); controller.Tick();
                            Check(audio.IsDefault(s.Id, false), "完整控制器：游戏中手动锁定音箱");
                            controller.SetMode(OutputMode.Auto);
                            Check(audio.IsDefault(h.Id, false), "完整控制器：解除锁定重新切耳机");
                            fixture2 = Process.Start(new ProcessStartInfo(runConfig.Games[0].FullPath) { UseShellExecute = false, CreateNoWindow = true });
                            fixture.Kill(); fixture.WaitForExit(2000); Pump(150); controller.Tick();
                            Check(audio.IsDefault(h.Id, false) && controller.RunningGame != null, "多个命中进程：退出一个仍保持耳机");
                            fixture2.Kill(); fixture2.WaitForExit(2000); Pump(100); controller.Tick();
                            Check(audio.IsDefault(h.Id, false) && controller.Policy.RemainingSeconds == 3, "完整控制器：最后一个游戏退出进入延迟");
                            Pump(3200); controller.Tick();
                            Check(audio.IsDefault(s.Id, false), "完整控制器：退出三秒后自动切回音箱");
                            controller.Config.HeadsetId = "missing-device-id";
                            controller.SetMode(OutputMode.Headset);
                            Check(audio.IsDefault(s.Id, false) && controller.Status.Contains("未连接"), "完整控制器：离线目标不破坏当前输出");
                            controller.Config.HeadsetId = h.Id; controller.Tick();
                            Check(audio.IsDefault(h.Id, false), "完整控制器：目标恢复可用后恢复切换");
                            controller.SetMode(OutputMode.Auto);
                        }
                        finally
                        {
                            if (fixture != null) { if (!fixture.HasExited) { fixture.Kill(); fixture.WaitForExit(2000); } fixture.Dispose(); }
                            if (fixture2 != null) { if (!fixture2.HasExited) { fixture2.Kill(); fixture2.WaitForExit(2000); } fixture2.Dispose(); }
                        }
                    }
                    var watch = Stopwatch.StartNew();
                    for (int i = 0; i < 200; i++) ProcessScanner.Snapshot();
                    watch.Stop();
                    passed.Add("INFO 进程快照平均耗时 " + (watch.Elapsed.TotalMilliseconds / 200).ToString("F3") + " ms（200 次）");
                }
                finally { audio.RestoreDefaults(original); Pump(200); }
                var restored = audio.GetDefaults();
                Check(String.Join("|", original) == String.Join("|", restored), "测试后完整恢复三个默认输出角色");
            }
            File.WriteAllText(report, "轻音切换验证报告\r\n" + DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz") + "\r\n"
                + Environment.OSVersion.VersionString + "\r\n\r\n" + String.Join("\r\n", passed) + "\r\n\r\n未启动真实游戏，也未验证实际抖音播放的音频流迁移。\r\n", Encoding.UTF8);
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(report, String.Join("\r\n", passed) + "\r\n" + ex.ToString(), Encoding.UTF8);
            Console.WriteLine(ex);
            return 1;
        }
    }
}
