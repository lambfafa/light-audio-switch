using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("轻音切换")]
[assembly: System.Reflection.AssemblyDescription("游戏耳机 / 日常音箱自动切换")]
[assembly: System.Reflection.AssemblyCompany("lambfafa")]
[assembly: System.Reflection.AssemblyProduct("LightAudioSwitch")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: ComVisible(false)]

namespace LightAudioSwitch
{
    public static class Program
    {
        public static string InstanceName
        {
            get { return @"Local\LightAudioSwitch_" + WindowsIdentity.GetCurrent().User.Value.Replace('-', '_'); }
        }
        [STAThread]
        public static int Main(string[] args)
        {
            string folder = AppDomain.CurrentDomain.BaseDirectory;
            AppLog.Folder = folder;
            if (args.Length > 0 && args[0] == "--diagnose")
            {
                if (args.Length != 2) return 2;
                try { WriteDiagnostics(args[1]); return 0; }
                catch (Exception ex) { File.WriteAllText(args[1] + ".error.txt", ex.ToString(), Encoding.UTF8); return 1; }
            }
            if (args.Length > 0 && args[0] == "--control")
                return args.Length == 2 && Signal(args[1]) ? 0 : 2;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool created;
            using (var mutex = new Mutex(true, InstanceName, out created))
            {
                if (!created) { Signal("settings"); return 0; }
                try
                {
                    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                    Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) {
                        AppLog.Write(e.Exception.ToString());
                        MessageBox.Show("操作未完成：" + e.Exception.Message, "轻音切换", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    };
                    using (var controller = new SwitchController(Path.Combine(folder, "config.json")))
                    {
                        if (args.Length == 2 && args[0] == "--render")
                        {
                            controller.Tick();
                            using (var form = new SettingsForm(controller))
                            {
                                form.ShowInTaskbar = false;
                                form.StartPosition = FormStartPosition.Manual;
                                form.Location = new Point(-32000, -32000);
                                form.Show();
                                Application.DoEvents();
                                form.Refresh();
                                using (var bitmap = new Bitmap(form.Width, form.Height))
                                {
                                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                                    bitmap.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
                                }
                                form.Close();
                            }
                            return 0;
                        }
                        bool background = Array.IndexOf(args, "--background") >= 0;
                        using (var context = new TrayContext(controller, !background)) Application.Run(context);
                    }
                    return 0;
                }
                catch (Exception ex)
                {
                    AppLog.Write(ex.ToString());
                    MessageBox.Show("轻音切换未能启动：\n" + ex.Message
                        + "\n\n程序与配置保留在当前文件夹中，详细信息见运行日志。", "轻音切换", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
                finally { mutex.ReleaseMutex(); }
            }
        }
        public static bool Signal(string action)
        {
            if (action != "settings" && action != "auto" && action != "headset" && action != "speaker" && action != "quit") return false;
            try { using (var handle = EventWaitHandle.OpenExisting(InstanceName + "_" + action)) return handle.Set(); }
            catch (WaitHandleCannotBeOpenedException) { return false; }
        }
        private static void WriteDiagnostics(string path)
        {
            using (var audio = new AudioService())
            {
                var report = new DiagnosticReport { Time = DateTimeOffset.Now.ToString("o"),
                    WindowsVersion = Environment.OSVersion.VersionString, ProcessArchitecture = Environment.Is64BitProcess ? "x64" : "x86",
                    Devices = audio.Enumerate(), DefaultIds = audio.GetDefaults() };
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                report.Config = ConfigStore.Load(configPath);
                report.DetectedGame = ProcessScanner.FindGame(ProcessScanner.Snapshot(), report.Config.Games);
                using (var stream = File.Create(path)) new DataContractJsonSerializer(typeof(DiagnosticReport)).WriteObject(stream, report);
            }
        }
    }
    [DataContract]
    public sealed class DiagnosticReport
    {
        [DataMember] public string Time;
        [DataMember] public string WindowsVersion;
        [DataMember] public string ProcessArchitecture;
        [DataMember] public List<AudioDevice> Devices;
        [DataMember] public string[] DefaultIds;
        [DataMember] public AppConfig Config;
        [DataMember] public string DetectedGame;
    }
    internal sealed class TrayContext : ApplicationContext
    {
        private readonly SwitchController controller;
        private readonly NotifyIcon tray;
        private readonly Icon icon;
        private readonly ContextMenuStrip menu;
        private readonly ToolStripMenuItem auto, headset, speaker, summary;
        private readonly System.Windows.Forms.Timer timer;
        private readonly Control dispatcher = new Control();
        private readonly List<EventWaitHandle> events = new List<EventWaitHandle>();
        private readonly List<RegisteredWaitHandle> waits = new List<RegisteredWaitHandle>();
        private SettingsForm settings;
        private bool disposing;
        public TrayContext(SwitchController controller, bool showSettings)
        {
            this.controller = controller;
            IntPtr ignored = dispatcher.Handle;
            menu = new ContextMenuStrip { Font = new Font("Microsoft YaHei UI", 9F) };
            summary = new ToolStripMenuItem("轻音切换") { Enabled = false };
            menu.Items.Add(summary);
            menu.Items.Add(new ToolStripSeparator());
            auto = new ToolStripMenuItem("自动切换", null, delegate { controller.SetMode(OutputMode.Auto); });
            headset = new ToolStripMenuItem("锁定耳机", null, delegate { controller.SetMode(OutputMode.Headset); });
            speaker = new ToolStripMenuItem("锁定小爱音箱", null, delegate { controller.SetMode(OutputMode.Speaker); });
            menu.Items.Add(auto); menu.Items.Add(headset); menu.Items.Add(speaker);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("设置…", null, delegate { ShowSettings(); }));
            menu.Items.Add(new ToolStripMenuItem("退出", null, delegate { ExitThread(); }));
            icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            tray = new NotifyIcon { Icon = icon, Text = "轻音切换", ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += delegate { ShowSettings(); };
            controller.Changed += UpdateTray;
            controller.Warning += Warn;
            AddSignal("settings", ShowSettings);
            AddSignal("auto", delegate { controller.SetMode(OutputMode.Auto); });
            AddSignal("headset", delegate { controller.SetMode(OutputMode.Headset); });
            AddSignal("speaker", delegate { controller.SetMode(OutputMode.Speaker); });
            AddSignal("quit", ExitThread);
            timer = new System.Windows.Forms.Timer { Interval = 1000 };
            timer.Tick += delegate { controller.Tick(); };
            controller.Tick();
            UpdateTray();
            timer.Start();
            if (showSettings || String.IsNullOrEmpty(controller.Config.HeadsetId) || String.IsNullOrEmpty(controller.Config.SpeakerId))
                dispatcher.BeginInvoke((Action)ShowSettings);
        }
        private void AddSignal(string action, Action callback)
        {
            var handle = new EventWaitHandle(false, EventResetMode.AutoReset, Program.InstanceName + "_" + action);
            events.Add(handle);
            waits.Add(ThreadPool.RegisterWaitForSingleObject(handle, delegate {
                if (disposing) return;
                try { dispatcher.BeginInvoke(callback); }
                catch (InvalidOperationException) { }
            }, null, Timeout.Infinite, false));
        }
        private void ShowSettings()
        {
            if (disposing) return;
            if (settings == null || settings.IsDisposed)
            {
                settings = new SettingsForm(controller);
                settings.FormClosed += delegate { settings = null; };
                settings.Show();
            }
            else { settings.WindowState = FormWindowState.Normal; settings.Show(); settings.Activate(); }
        }
        private void UpdateTray()
        {
            auto.Checked = controller.Policy.Mode == OutputMode.Auto;
            headset.Checked = controller.Policy.Mode == OutputMode.Headset;
            speaker.Checked = controller.Policy.Mode == OutputMode.Speaker;
            string text = (controller.Status ?? "轻音切换").Replace("\n", " · ");
            tray.Text = text.Length > 63 ? text.Substring(0, 60) + "…" : text;
            summary.Text = (controller.Status ?? "轻音切换").Split('\n')[0];
        }
        private void Warn(string message)
        {
            tray.BalloonTipTitle = "轻音切换";
            tray.BalloonTipText = message;
            tray.BalloonTipIcon = ToolTipIcon.Warning;
            tray.ShowBalloonTip(4000);
        }
        protected override void Dispose(bool disposingManaged)
        {
            if (disposingManaged && !disposing)
            {
                disposing = true;
                timer.Stop(); timer.Dispose();
                controller.Changed -= UpdateTray;
                controller.Warning -= Warn;
                foreach (var wait in waits) wait.Unregister(null);
                foreach (var handle in events) handle.Dispose();
                if (settings != null) settings.Close();
                tray.Visible = false; tray.Dispose(); icon.Dispose(); menu.Dispose(); dispatcher.Dispose();
            }
            base.Dispose(disposingManaged);
        }
    }
}
