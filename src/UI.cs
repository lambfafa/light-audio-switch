using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LightAudioSwitch
{
    public sealed class SettingsForm : Form
    {
        private readonly SwitchController controller;
        private readonly DeviceCard headset = new DeviceCard(true), speaker = new DeviceCard(false);
        private readonly GameList games = new GameList();
        private readonly List<GameRule> rules = new List<GameRule>();
        private readonly ModeControl modes = new ModeControl();
        private readonly Toggle startup = new Toggle(), communications = new Toggle();
        private readonly DelayStepper delay = new DelayStepper();
        private readonly Label title, headline, context, gamesTitle, emptyGames, delayLabel, startupLabel, communicationsLabel, feedback;
        private readonly QuietButton add, refresh, help, save;
        private readonly Surface gameSurface = new Surface(), preferences = new Surface();
        private readonly Panel settingsPage = new Panel { Dock = DockStyle.Fill };
        private readonly Panel brand = new Panel();
        private readonly ToolTip tips = new ToolTip();
        private HelpPage helpPage;
        private float scale = 1;
        private bool building = true;

        public SettingsForm(SwitchController controller)
        {
            this.controller = controller;
            AutoScaleMode = AutoScaleMode.None;
            Font = Theme.Body; BackColor = Theme.Canvas; ForeColor = Theme.Ink;
            Text = "轻音切换"; MaximizeBox = false; FormBorderStyle = FormBorderStyle.FixedSingle;
            StartPosition = FormStartPosition.Manual;
            Icon = Theme.AppIcon;
            using (var graphics = CreateGraphics()) scale = graphics.DpiX / 96;
            title = Label("轻音切换", Theme.Title, Theme.Ink);
            headline = Label("正在读取输出", Theme.Display, Theme.Ink);
            context = Label("", Theme.Small, Theme.Muted);
            gamesTitle = Label("游戏", Theme.Body, Theme.Ink);
            emptyGames = Label("还没有添加游戏", Theme.Body, Theme.Muted);
            emptyGames.TextAlign = ContentAlignment.MiddleCenter;
            delayLabel = Label("切回音箱的延迟", Theme.Body, Theme.Ink);
            startupLabel = Label("登录时启动", Theme.Body, Theme.Ink);
            communicationsLabel = Label("语音输出跟随", Theme.Body, Theme.Ink);
            feedback = Label("", Theme.Small, Theme.Muted); feedback.TextAlign = ContentAlignment.MiddleRight;
            add = Button("＋ 添加", delegate { ShowAddMenu(); }, false);
            refresh = Button("刷新设备", delegate { RefreshDevices(); }, false);
            help = Button("帮助", delegate { OpenHelp(); }, false);
            save = Button("保存", delegate { SaveSettings(); }, true); save.Name = "SaveSettings";
            headset.Name = "HeadsetDevice"; speaker.Name = "SpeakerDevice";
            startup.Name = "Startup"; communications.Name = "Communications";
            startup.AccessibleName = "登录时启动"; communications.AccessibleName = "语音输出跟随";
            tips.SetToolTip(communications, "同时切换默认通信输出；麦克风不变");
            tips.SetToolTip(delay, "所有游戏退出后的等待时间，0～30 秒");
            tips.SetToolTip(refresh, "重新读取音频设备；重新配对后可在卡片中选择");
            brand.Paint += delegate(object sender, PaintEventArgs e) {
                e.Graphics.DrawIcon(Theme.AppIcon, brand.ClientRectangle);
            };
            Controls.Add(settingsPage);
            foreach (Control c in new Control[] { brand, title, modes, headline, context, headset, speaker, gamesTitle, add,
                gameSurface, preferences, refresh, help, feedback, save }) settingsPage.Controls.Add(c);
            gameSurface.Controls.Add(games); gameSurface.Controls.Add(emptyGames);
            foreach (Control c in new Control[] { delayLabel, delay, startupLabel, startup, communicationsLabel, communications }) preferences.Controls.Add(c);
            preferences.Paint += delegate(object sender, PaintEventArgs e) {
                using (var pen = new Pen(Theme.Line, 1)) {
                    e.Graphics.DrawLine(pen, D(20), D(50), preferences.Width - D(20), D(50));
                    e.Graphics.DrawLine(pen, D(20), D(100), preferences.Width - D(20), D(100));
                }
            };
            games.RemoveRequested += delegate(int index) {
                if (index >= 0 && index < rules.Count) { rules.RemoveAt(index); FillGames(); MarkDirty(); }
            };
            var removeMenu = new PopupMenu("游戏操作");
            var remove = removeMenu.AddOption("移除游戏", delegate {
                if (games.SelectedIndex >= 0) { rules.RemoveAt(games.SelectedIndex); FillGames(); MarkDirty(); }
            });
            removeMenu.Opening += delegate { remove.Enabled = games.SelectedIndex >= 0; };
            games.ContextMenuStrip = removeMenu;
            modes.ModeRequested += delegate(OutputMode mode) { controller.SetMode(mode); UpdateStatus(); };
            headset.SelectionChanged += delegate { MarkDirty(); UpdateStatus(); };
            speaker.SelectionChanged += delegate { MarkDirty(); UpdateStatus(); };
            startup.CheckedChanged += delegate { MarkDirty(); };
            communications.CheckedChanged += delegate { MarkDirty(); }; delay.ValueChanged += delegate { MarkDirty(); };
            foreach (var g in controller.Config.Games) rules.Add(new GameRule { Name = g.Name, Executable = g.Executable, FullPath = g.FullPath });
            FillDevices(controller.Devices, controller.Config.HeadsetId, controller.Config.HeadsetName, controller.Config.SpeakerId, controller.Config.SpeakerName);
            startup.Checked = StartupRegistration.IsEnabled(); communications.Checked = controller.Config.SwitchCommunications;
            delay.Value = controller.Config.ExitDelaySeconds; FillGames();
            ClientSize = new Size(D(744), D(700 + (rules.Count > 1 ? 54 : 0)));
            building = false; PerformLayout(); UpdateStatus(); controller.Changed += UpdateStatus;
            FormClosed += delegate { controller.Changed -= UpdateStatus; tips.Dispose(); removeMenu.Dispose(); };
            AcceptButton = save;
        }

        internal void ShowCentered()
        {
            // Measure the final window, including its frame, after layout and DPI scaling.
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            WindowState = FormWindowState.Normal;
            Location = new Point(
                area.Left + Math.Max(0, (area.Width - Width) / 2),
                area.Top + Math.Max(0, (area.Height - Height) / 2));
            Show();
            Activate();
        }

        private int D(float n) { return (int)Math.Round(n * scale); }
        private Label Label(string text, Font font, Color color)
        { return new Label { Text = text, Font = font, ForeColor = color, BackColor = Color.Transparent, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft }; }
        private QuietButton Button(string text, EventHandler handler, bool primary)
        {
            var b = new QuietButton { Text = text, Primary = primary };
            b.Click += handler; return b;
        }
        private void Place(Control c, int x, int y, int w, int h) { c.SetBounds(D(x), D(y), D(w), D(h)); }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e); if (building || title == null) return;
            int extra = rules.Count > 1 ? 54 : 0;
            Place(brand, 32, 26, 32, 32); Place(title, 75, 22, 220, 40); Place(modes, 488, 25, 224, 36);
            Place(headline, 32, 94, 680, 46); Place(context, 34, 143, 676, 23);
            Place(headset, 32, 184, 332, 116); Place(speaker, 380, 184, 332, 116);
            Place(gamesTitle, 34, 328, 200, 28); Place(add, 620, 324, 92, 32);
            Place(gameSurface, 32, 366, 680, 72 + extra); Place(games, 10, 9, 660, 54 + extra); Place(emptyGames, 10, 10, 660, 52);
            games.Height = games.ItemHeight * (extra > 0 ? 2 : 1);
            Place(preferences, 32, 460 + extra, 680, 150);
            Place(delayLabel, 20, 0, 400, 50); Place(delay, 556, 9, 104, 32);
            Place(startupLabel, 20, 50, 400, 50); Place(startup, 610, 60, 50, 30);
            Place(communicationsLabel, 20, 100, 400, 50); Place(communications, 610, 110, 50, 30);
            Place(refresh, 26, 636 + extra, 90, 34); Place(help, 128, 636 + extra, 56, 34);
            Place(feedback, 216, 636 + extra, 374, 34); Place(save, 608, 634 + extra, 104, 38);
        }
        private static string Id(DeviceCard card) { return card.SelectedDevice == null ? null : card.SelectedDevice.Id; }
        private void FillDevices(List<AudioDevice> devices, string h, string hn, string s, string sn)
        { headset.Devices = speaker.Devices = devices; headset.Choose(h, hn); speaker.Choose(s, sn); }
        private void RefreshDevices()
        {
            try {
                var h = headset.SelectedDevice; var s = speaker.SelectedDevice;
                FillDevices(controller.RefreshDevices(), Id(headset), h == null ? null : h.Name, Id(speaker), s == null ? null : s.Name); UpdateStatus();
            } catch (Exception ex) { Error(ex.Message); }
        }
        private void FillGames()
        {
            games.BeginUpdate(); games.Items.Clear(); foreach (var g in rules) games.Items.Add(g); games.EndUpdate();
            games.Visible = rules.Count > 0; emptyGames.Visible = rules.Count == 0;
            if (!building) { ClientSize = new Size(D(744), D(700 + (rules.Count > 1 ? 54 : 0))); PerformLayout(); }
        }
        private PopupMenu addMenu;
        private void ShowAddMenu()
        {
            if (addMenu == null) {
                addMenu = new PopupMenu("添加游戏");
                addMenu.AddOption("从运行程序添加…", delegate { AddRunning(); });
                addMenu.AddOption("选择游戏 EXE…", delegate { AddFile(); });
            }
            addMenu.ShowFor(add, alignRight: true);
        }
        protected override void Dispose(bool disposing)
        { if (disposing && addMenu != null) { addMenu.Dispose(); addMenu = null; } base.Dispose(disposing); }
        private void AddRule(string path, string exe)
        {
            if (AppConfig.IsLauncher(exe)) { Error("请选择游戏本体"); return; }
            if (rules.Exists(delegate(GameRule g) { return String.Equals(g.Executable, exe, StringComparison.OrdinalIgnoreCase)
                && String.Equals(g.FullPath ?? "", path ?? "", StringComparison.OrdinalIgnoreCase); })) return;
            rules.Add(new GameRule { Name = Path.GetFileNameWithoutExtension(exe), Executable = exe, FullPath = path });
            FillGames(); games.SelectedIndex = games.Items.Count - 1; MarkDirty();
        }
        private void AddFile()
        {
            using (var d = new OpenFileDialog { Title = "选择游戏本体", Filter = "程序 (*.exe)|*.exe", CheckFileExists = true })
                if (d.ShowDialog(this) == DialogResult.OK) AddRule(d.FileName, Path.GetFileName(d.FileName));
        }
        private void AddRunning()
        {
            try { using (var p = new ProcessPicker()) if (p.ShowDialog(this) == DialogResult.OK && p.Selected != null)
                AddRule(ProcessScanner.TryGetPath(p.Selected.Id), p.Selected.Name); } catch (Exception ex) { Error(ex.Message); }
        }
        private void OpenHelp()
        {
            if (helpPage == null)
            {
                helpPage = new HelpPage(scale);
                helpPage.BackRequested += delegate { ReturnToSettings(); };
                Controls.Add(helpPage);
            }
            AcceptButton = null;
            settingsPage.Hide();
            helpPage.Show();
            helpPage.BringToFront();
            helpPage.FocusBack();
        }

        private void ReturnToSettings()
        {
            helpPage.Hide();
            settingsPage.Show();
            AcceptButton = save;
            help.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (helpPage != null && helpPage.Visible)
            {
                if (keyData == Keys.Escape) { ReturnToSettings(); return true; }
                if (helpPage.ScrollByKey(keyData)) return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
        private void MarkDirty() { if (!building) { feedback.Text = "未保存"; feedback.ForeColor = Theme.Muted; } }
        private void SaveSettings()
        {
            try {
                if (String.IsNullOrEmpty(Id(headset)) || String.IsNullOrEmpty(Id(speaker))) {
                    Error("请选择耳机和音箱"); (String.IsNullOrEmpty(Id(headset)) ? headset : speaker).Focus(); return;
                }
                var c = controller.Config.Copy();
                c.HeadsetId = Id(headset); c.HeadsetName = headset.SelectedDevice.Name;
                c.SpeakerId = Id(speaker); c.SpeakerName = speaker.SelectedDevice.Name;
                c.ExitDelaySeconds = delay.Value; c.SwitchCommunications = communications.Checked; c.Games = new List<GameRule>(rules);
                controller.ApplySettings(c, startup.Checked); feedback.ForeColor = Theme.Muted; feedback.Text = "已保存";
            } catch (Exception ex) { Error(ex.Message); }
        }
        private void Error(string message) { feedback.ForeColor = Theme.Error; feedback.Text = message; tips.SetToolTip(feedback, message); }
        private void UpdateStatus()
        {
            if (IsDisposed) return;
            modes.SetMode(controller.Policy.Mode); string actual = null;
            try { actual = controller.Audio.GetDefault(1); } catch (COMException) { }
            headset.IsCurrent = actual != null && actual == Id(headset); speaker.IsCurrent = actual != null && actual == Id(speaker);
            headset.Invalidate(); speaker.Invalidate();
            headline.Text = headset.IsCurrent ? "正在使用耳机" : speaker.IsCurrent ? "正在使用音箱" : "系统默认输出";
            var device = controller.Devices.Find(delegate(AudioDevice d) { return d.Id == actual; });
            context.ForeColor = Theme.Muted;
            context.Text = headset.IsCurrent || speaker.IsCurrent ? controller.RunningGame ?? "" : device == null ? "请选择输出设备" : Theme.DeviceName(device.Name);
            if (controller.Policy.RemainingSeconds > 0) context.Text = controller.Policy.RemainingSeconds + " 秒后切回音箱";
            bool h = controller.Policy.Mode == OutputMode.Headset || (controller.Policy.Mode == OutputMode.Auto && (controller.RunningGame != null || controller.Policy.RemainingSeconds > 0));
            string targetId = h ? controller.Config.HeadsetId : controller.Config.SpeakerId;
            var target = controller.Devices.Find(delegate(AudioDevice d) { return d.Id == targetId; });
            if (String.IsNullOrEmpty(Id(headset)) && String.IsNullOrEmpty(Id(speaker))) {
                headline.Text = "选择输出设备"; context.Text = "";
            }
            else if (target == null || !target.Active) {
                context.Text = String.IsNullOrEmpty(targetId) ? (h ? "请选择游戏耳机" : "请选择日常音箱") : (h ? "耳机未连接" : "音箱未连接"); context.ForeColor = Theme.Error;
            }
            else if (!String.IsNullOrEmpty(controller.RoutingWarning)) {
                context.Text = "切换暂未完成"; context.ForeColor = Theme.Error;
                tips.SetToolTip(context, controller.RoutingWarning);
            }
            tips.SetToolTip(headset, headset.SelectedDevice == null ? "选择游戏输出设备" : headset.SelectedDevice.Name);
            tips.SetToolTip(speaker, speaker.SelectedDevice == null ? "选择日常输出设备" : speaker.SelectedDevice.Name);
        }
    }

    internal sealed class DelayStepper : Surface
    {
        private readonly QuietButton minus = new QuietButton(), plus = new QuietButton();
        private readonly Label number = new Label();
        private int value;
        public event Action ValueChanged;
        public int Value {
            get { return value; }
            set { int n = Math.Max(0, Math.Min(30, value)); if (this.value == n) return;
                this.value = n; RefreshValue(); if (ValueChanged != null) ValueChanged(); }
        }
        public DelayStepper()
        {
            BackColor = Theme.Canvas; Radius = 9; AccessibleRole = AccessibleRole.SpinButton; AccessibleName = "切回音箱的延迟";
            minus.Text = "−"; minus.AccessibleName = "减少延迟"; plus.Text = "＋"; plus.AccessibleName = "增加延迟";
            foreach (var b in new[] { minus, plus }) { b.Segment = true; Controls.Add(b); }
            number.TextAlign = ContentAlignment.MiddleCenter; number.Font = Theme.Small; number.ForeColor = Theme.Ink; number.BackColor = Color.Transparent;
            Controls.Add(number); minus.Click += delegate { Value--; }; plus.Click += delegate { Value++; }; RefreshValue();
        }
        private void RefreshValue() { number.Text = value + " 秒"; minus.Enabled = value > 0; plus.Enabled = value < 30; }
        protected override void OnLayout(LayoutEventArgs e) {
            base.OnLayout(e); if (minus == null) return; int w = (int)Math.Round(Width * 0.28);
            minus.SetBounds(0, 0, w, Height); plus.SetBounds(Width - w, 0, w, Height); number.SetBounds(w, 0, Width - w * 2, Height);
        }
    }

    internal sealed class ProcessPicker : Form
    {
        private readonly ListBox list = new ListBox();
        private readonly TextBox search = new TextBox();
        private List<ProcessEntry> entries = new List<ProcessEntry>();
        private readonly QuietButton add;
        public ProcessEntry Selected { get { return list.SelectedItem as ProcessEntry; } }
        public ProcessPicker()
        {
            Text = "添加游戏"; Icon = Theme.AppIcon; Font = Theme.Body; BackColor = Theme.Canvas; AutoScaleMode = AutoScaleMode.None;
            float s; using (var g = CreateGraphics()) s = g.DpiX / 96;
            ClientSize = new Size((int)(540 * s), (int)(460 * s)); StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MinimizeBox = MaximizeBox = false; ShowInTaskbar = false;
            var title = new Label { Text = "添加游戏", Font = Theme.Title, ForeColor = Theme.Ink };
            title.SetBounds((int)(24 * s), (int)(20 * s), (int)(480 * s), (int)(40 * s)); Controls.Add(title);
            var searchSurface = new Surface(); searchSurface.SetBounds((int)(24 * s), (int)(78 * s), (int)(492 * s), (int)(42 * s));
            search.BorderStyle = BorderStyle.None; search.Font = Theme.Body;
            search.SetBounds((int)(14 * s), (int)(11 * s), (int)(460 * s), (int)(25 * s));
            searchSurface.Controls.Add(search); Controls.Add(searchSurface);
            var listSurface = new Surface(); listSurface.SetBounds((int)(24 * s), (int)(136 * s), (int)(492 * s), (int)(248 * s));
            list.BorderStyle = BorderStyle.None; list.Font = Theme.Body; list.IntegralHeight = false; list.DrawMode = DrawMode.OwnerDrawFixed;
            list.ItemHeight = (int)(40 * s); list.SetBounds((int)(12 * s), (int)(12 * s), (int)(468 * s), (int)(224 * s));
            list.DrawItem += delegate(object sender, DrawItemEventArgs e) {
                if (e.Index < 0) return; var item = (ProcessEntry)list.Items[e.Index];
                e.Graphics.FillRectangle(Brushes.White, e.Bounds);
                if ((e.State & DrawItemState.Selected) != 0) Theme.FillRound(e.Graphics, e.Bounds, 8 * s, Theme.Canvas);
                Theme.Text(e.Graphics, item.Name, Theme.Body, Theme.Ink, new Rectangle(e.Bounds.X + (int)(10 * s), e.Bounds.Y, e.Bounds.Width - (int)(100 * s), e.Bounds.Height), TextFormatFlags.SingleLine);
                Theme.Text(e.Graphics, item.Id.ToString(), Theme.Small, Theme.Muted, new Rectangle(e.Bounds.Right - (int)(80 * s), e.Bounds.Y, (int)(65 * s), e.Bounds.Height), TextFormatFlags.Right | TextFormatFlags.SingleLine);
            };
            listSurface.Controls.Add(list); Controls.Add(listSurface);
            var refresh = new QuietButton { Text = "刷新" };
            refresh.SetBounds((int)(20 * s), (int)(408 * s), (int)(76 * s), (int)(32 * s)); refresh.Click += delegate { LoadEntries(); }; Controls.Add(refresh);
            add = new QuietButton { Text = "添加", Primary = true };
            add.SetBounds((int)(424 * s), (int)(404 * s), (int)(92 * s), (int)(38 * s)); add.Click += delegate { Choose(); }; Controls.Add(add); AcceptButton = add;
            search.TextChanged += delegate { Filter(); }; list.SelectedIndexChanged += delegate { add.Enabled = Selected != null; };
            list.DoubleClick += delegate { Choose(); };
            Shown += delegate { SendMessage(search.Handle, 0x1501, new IntPtr(1), "搜索程序"); search.Focus(); }; LoadEntries();
        }
        private void Choose() { if (Selected != null) { DialogResult = DialogResult.OK; Close(); } }
        private void LoadEntries() {
            entries = ProcessScanner.Snapshot(); entries.RemoveAll(delegate(ProcessEntry p) { return !p.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || AppConfig.IsLauncher(p.Name); });
            entries.Sort(delegate(ProcessEntry a, ProcessEntry b) { return StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name); }); Filter();
        }
        private void Filter() {
            list.BeginUpdate(); list.Items.Clear(); foreach (var p in entries) if (p.Name.IndexOf(search.Text, StringComparison.OrdinalIgnoreCase) >= 0) list.Items.Add(p);
            list.EndUpdate(); add.Enabled = false;
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, string lParam);
    }
}
