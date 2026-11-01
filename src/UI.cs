using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace LightAudioSwitch
{
    internal static class Theme
    {
        public static readonly Color Blue = Color.FromArgb(40, 91, 215);
        public static readonly Color Ink = Color.FromArgb(28, 40, 62);
        public static readonly Color Muted = Color.FromArgb(92, 105, 125);
        public static Button Button(string text, EventHandler action, bool primary)
        {
            var b = new Button { Text = text, AutoSize = true, Height = 32, MinimumSize = new Size(96, 32),
                Padding = new Padding(8, 2, 8, 2), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                BackColor = primary ? Blue : Color.White, ForeColor = primary ? Color.White : Ink,
                Margin = new Padding(0, 0, 8, 0), UseVisualStyleBackColor = false };
            b.FlatAppearance.BorderColor = primary ? Blue : Color.FromArgb(207, 215, 229);
            b.Click += action;
            return b;
        }
        public static Label Label(string text) { return new Label { Text = text, AutoSize = true, ForeColor = Ink, Margin = new Padding(0, 6, 0, 6) }; }
    }

    public sealed class SettingsForm : Form
    {
        private readonly SwitchController controller;
        private readonly ComboBox headset = new ComboBox(), speaker = new ComboBox();
        private readonly ListBox games = new ListBox();
        private readonly NumericUpDown delay = new NumericUpDown();
        private readonly CheckBox startup = new CheckBox(), communications = new CheckBox();
        private readonly Label status = new Label(), current = new Label();
        private readonly List<GameRule> rules = new List<GameRule>();
        private readonly ToolTip tips = new ToolTip();
        public SettingsForm(SwitchController controller)
        {
            this.controller = controller;
            Text = "轻音切换 · 设置";
            Font = new Font("Microsoft YaHei UI", 9F);
            ForeColor = Theme.Ink;
            BackColor = Color.FromArgb(246, 248, 252);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(730, 690);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24, 14, 24, 14),
                ColumnCount = 1, RowCount = 7, BackColor = BackColor };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 128));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 91));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
            Controls.Add(root);

            var heading = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            heading.Controls.Add(new Label { Text = "轻音切换", Font = new Font(Font.FontFamily, 21F, FontStyle.Bold),
                ForeColor = Theme.Ink, AutoSize = true, Location = new Point(0, 0) });
            heading.Controls.Add(new Label { Text = "游戏戴耳机，日常听音箱。", ForeColor = Theme.Muted,
                AutoSize = true, Location = new Point(2, 43) });
            root.Controls.Add(heading, 0, 0);

            var state = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
                BackColor = Color.FromArgb(231, 238, 253), Padding = new Padding(12, 7, 12, 7), Margin = new Padding(0, 4, 0, 10) };
            state.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            state.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            status.Dock = DockStyle.Fill;
            status.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold);
            status.ForeColor = Theme.Blue;
            status.AutoEllipsis = true;
            status.TextAlign = ContentAlignment.MiddleLeft;
            state.Controls.Add(status, 0, 0);
            var modes = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            modes.Controls.Add(Theme.Button("自动切换", delegate { controller.SetMode(OutputMode.Auto); }, true));
            modes.Controls.Add(Theme.Button("锁定耳机", delegate { controller.SetMode(OutputMode.Headset); }, false));
            modes.Controls.Add(Theme.Button("锁定音箱", delegate { controller.SetMode(OutputMode.Speaker); }, false));
            state.Controls.Add(modes, 0, 1);
            root.Controls.Add(state, 0, 1);

            var deviceBox = new GroupBox { Text = "输出设备", Dock = DockStyle.Fill, Padding = new Padding(10), Margin = new Padding(0, 0, 0, 9) };
            var deviceTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3 };
            deviceTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            deviceTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            deviceTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            deviceTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            deviceTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            deviceTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            deviceTable.Controls.Add(Theme.Label("游戏 → 耳机"), 0, 0);
            deviceTable.Controls.Add(Theme.Label("日常 → 音箱"), 0, 1);
            foreach (ComboBox combo in new[] { headset, speaker })
            {
                combo.DropDownStyle = ComboBoxStyle.DropDownList;
                combo.Dock = DockStyle.Fill;
                combo.DropDownWidth = 570;
                combo.Margin = new Padding(0, 2, 6, 2);
            }
            deviceTable.Controls.Add(headset, 1, 0);
            deviceTable.Controls.Add(speaker, 1, 1);
            var refresh = Theme.Button("刷新", delegate {
                try { FillDevices(controller.RefreshDevices(), SelectedId(headset), SelectedId(speaker)); }
                catch (Exception ex) { ShowError(ex); }
            }, false);
            refresh.MinimumSize = new Size(66, 28);
            refresh.Padding = Padding.Empty;
            refresh.Dock = DockStyle.Fill;
            deviceTable.Controls.Add(refresh, 2, 0);
            var hint = new Label { Text = "设备选择在保存后生效；重新配对后请刷新并重新选择。", Dock = DockStyle.Fill,
                ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            deviceTable.Controls.Add(hint, 0, 2);
            deviceTable.SetColumnSpan(hint, 3);
            deviceBox.Controls.Add(deviceTable);
            root.Controls.Add(deviceBox, 0, 2);

            var gameBox = new GroupBox { Text = "游戏识别 · 任意一款运行期间保持耳机", Dock = DockStyle.Fill,
                Padding = new Padding(10, 7, 10, 8), Margin = new Padding(0, 0, 0, 8) };
            var gameTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            gameTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            gameTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
            gameTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            games.Dock = DockStyle.Fill;
            games.HorizontalScrollbar = true;
            games.IntegralHeight = false;
            games.BorderStyle = BorderStyle.FixedSingle;
            var pathLabel = new Label { Dock = DockStyle.Fill, ForeColor = Theme.Muted,
                AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Text = "预置《三角洲行动》；WeGame 常驻不会触发。" };
            games.SelectedIndexChanged += delegate {
                var g = games.SelectedItem as GameRule;
                pathLabel.Text = g == null ? "请选择游戏本体，不要添加启动器。" : g.FullPath ?? g.Executable;
                tips.SetToolTip(pathLabel, pathLabel.Text);
            };
            var gameActions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            gameActions.Controls.Add(Theme.Button("从运行程序添加", delegate { AddRunning(); }, false));
            gameActions.Controls.Add(Theme.Button("选择游戏 EXE", delegate { AddFile(); }, false));
            gameActions.Controls.Add(Theme.Button("删除", delegate {
                if (games.SelectedIndex >= 0) { rules.RemoveAt(games.SelectedIndex); FillGames(); }
            }, false));
            gameTable.Controls.Add(games, 0, 0);
            gameTable.Controls.Add(pathLabel, 0, 1);
            gameTable.Controls.Add(gameActions, 0, 2);
            gameBox.Controls.Add(gameTable);
            root.Controls.Add(gameBox, 0, 3);

            var preferences = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
            preferences.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            preferences.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            preferences.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            preferences.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            var delayRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            delayRow.Controls.Add(Theme.Label("所有游戏退出后，等待"));
            delay.Minimum = 0; delay.Maximum = 30; delay.Width = 55; delay.Value = controller.Config.ExitDelaySeconds;
            delay.Margin = new Padding(6, 3, 6, 0);
            delayRow.Controls.Add(delay);
            delayRow.Controls.Add(Theme.Label("秒切回音箱；临时切桌面保持耳机。"));
            startup.Text = "登录 Windows 后自动启动（后台运行）";
            startup.AutoSize = true;
            startup.Checked = StartupRegistration.IsEnabled();
            communications.Text = "同时切换默认通信输出（语音聊天）；麦克风不变";
            communications.AutoSize = true;
            communications.Checked = controller.Config.SwitchCommunications;
            preferences.Controls.Add(delayRow, 0, 0);
            preferences.Controls.Add(startup, 0, 1);
            preferences.Controls.Add(communications, 0, 2);
            root.Controls.Add(preferences, 0, 4);
            current.Dock = DockStyle.Fill;
            current.ForeColor = Theme.Muted;
            current.AutoEllipsis = true;
            current.TextAlign = ContentAlignment.MiddleLeft;
            root.Controls.Add(current, 0, 5);

            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0, 7, 0, 0) };
            footer.Controls.Add(Theme.Button("保存设置", delegate { SaveSettings(); }, true));
            footer.Controls.Add(Theme.Button("关闭到托盘", delegate { Close(); }, false));
            footer.Controls.Add(Theme.Button("使用说明", delegate {
                try { System.Diagnostics.Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "使用说明.txt")); }
                catch (Exception ex) { ShowError(ex); }
            }, false));
            root.Controls.Add(footer, 0, 6);

            foreach (var g in controller.Config.Games)
                rules.Add(new GameRule { Name = g.Name, Executable = g.Executable, FullPath = g.FullPath });
            FillDevices(controller.Devices, controller.Config.HeadsetId, controller.Config.SpeakerId);
            FillGames();
            UpdateStatus();
            controller.Changed += UpdateStatus;
            FormClosed += delegate { controller.Changed -= UpdateStatus; tips.Dispose(); };
        }
        private string SelectedId(ComboBox box) { var d = box.SelectedItem as AudioDevice; return d == null ? null : d.Id; }
        private void FillDevices(List<AudioDevice> devices, string h, string s)
        {
            FillCombo(headset, devices, h, controller.Config.HeadsetName);
            FillCombo(speaker, devices, s, controller.Config.SpeakerName);
        }
        private static void FillCombo(ComboBox box, List<AudioDevice> devices, string selected, string savedName)
        {
            box.BeginUpdate();
            box.Items.Clear();
            box.Items.Add(new AudioDevice { Name = "请选择输出设备", Id = null, State = 1 });
            int index = 0;
            foreach (var d in devices)
            {
                int i = box.Items.Add(d);
                if (d.Id == selected) index = i;
            }
            if (index == 0 && !String.IsNullOrEmpty(selected))
                index = box.Items.Add(new AudioDevice { Id = selected, Name = savedName ?? "原设备", State = 0 });
            box.SelectedIndex = index;
            box.EndUpdate();
        }
        private void FillGames()
        {
            games.BeginUpdate(); games.Items.Clear();
            foreach (var g in rules) games.Items.Add(g);
            games.EndUpdate();
        }
        private void AddRule(string path, string exe)
        {
            if (AppConfig.IsLauncher(exe)) { MessageBox.Show(this, "请添加游戏本体，不要选择 WeGame、浏览器或反作弊程序。", "选择游戏", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (rules.Exists(delegate(GameRule g) { return String.Equals(g.Executable, exe, StringComparison.OrdinalIgnoreCase)
                && String.Equals(g.FullPath ?? "", path ?? "", StringComparison.OrdinalIgnoreCase); })) return;
            rules.Add(new GameRule { Name = Path.GetFileNameWithoutExtension(exe), Executable = exe, FullPath = path });
            FillGames(); games.SelectedIndex = games.Items.Count - 1;
        }
        private void AddFile()
        {
            using (var dialog = new OpenFileDialog { Title = "选择游戏本体（不是 WeGame 启动器）", Filter = "程序 (*.exe)|*.exe", CheckFileExists = true })
                if (dialog.ShowDialog(this) == DialogResult.OK) AddRule(dialog.FileName, Path.GetFileName(dialog.FileName));
        }
        private void AddRunning()
        {
            try
            {
                using (var picker = new ProcessPicker())
                    if (picker.ShowDialog(this) == DialogResult.OK && picker.Selected != null)
                        AddRule(ProcessScanner.TryGetPath(picker.Selected.Id), picker.Selected.Name);
            }
            catch (Exception ex) { ShowError(ex); }
        }
        private void SaveSettings()
        {
            try
            {
                var h = headset.SelectedItem as AudioDevice;
                var s = speaker.SelectedItem as AudioDevice;
                if (h == null || String.IsNullOrEmpty(h.Id) || s == null || String.IsNullOrEmpty(s.Id))
                    throw new InvalidOperationException("请先选择耳机和日常音箱。");
                var c = controller.Config.Copy();
                c.HeadsetId = h.Id; c.HeadsetName = h.Name;
                c.SpeakerId = s.Id; c.SpeakerName = s.Name;
                c.ExitDelaySeconds = (int)delay.Value;
                c.SwitchCommunications = communications.Checked;
                c.Games = new List<GameRule>(rules);
                controller.ApplySettings(c, startup.Checked);
                MessageBox.Show(this, "设置已保存。关闭窗口后，程序仍会在托盘中自动运行。", "轻音切换", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { ShowError(ex); }
        }
        private void UpdateStatus()
        {
            status.Text = controller.Status ?? "正在检查音频设备…";
            current.Text = "当前输出：" + (controller.CurrentOutput ?? "正在读取…");
            tips.SetToolTip(current, current.Text);
        }
        private void ShowError(Exception ex) { MessageBox.Show(this, ex.Message, "操作未完成", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    internal sealed class ProcessPicker : Form
    {
        private readonly ListBox list = new ListBox();
        private readonly TextBox search = new TextBox();
        private List<ProcessEntry> entries;
        public ProcessEntry Selected { get { return list.SelectedItem as ProcessEntry; } }
        public ProcessPicker()
        {
            Text = "选择正在运行的游戏本体";
            Font = new Font("Microsoft YaHei UI", 9F);
            ClientSize = new Size(580, 420);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false; MaximizeBox = false; ShowInTaskbar = false;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 4 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.Controls.Add(Theme.Label("搜索程序名；请在启动游戏后添加，避开启动器与反作弊程序。"), 0, 0);
            search.Dock = DockStyle.Fill;
            layout.Controls.Add(search, 0, 1);
            list.Dock = DockStyle.Fill; list.HorizontalScrollbar = true; list.IntegralHeight = false;
            layout.Controls.Add(list, 0, 2);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 7, 0, 0) };
            actions.Controls.Add(Theme.Button("添加选中程序", delegate { if (Selected != null) { DialogResult = DialogResult.OK; Close(); } }, true));
            actions.Controls.Add(Theme.Button("刷新", delegate { LoadEntries(); }, false));
            layout.Controls.Add(actions, 0, 3);
            Controls.Add(layout);
            search.TextChanged += delegate { Filter(); };
            list.DoubleClick += delegate { if (Selected != null) { DialogResult = DialogResult.OK; Close(); } };
            LoadEntries();
        }
        private void LoadEntries()
        {
            entries = ProcessScanner.Snapshot();
            entries.RemoveAll(delegate(ProcessEntry p) { return !p.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || AppConfig.IsLauncher(p.Name); });
            entries.Sort(delegate(ProcessEntry a, ProcessEntry b) { return StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name); });
            Filter();
        }
        private void Filter()
        {
            list.BeginUpdate(); list.Items.Clear();
            foreach (var p in entries) if (p.Name.IndexOf(search.Text, StringComparison.OrdinalIgnoreCase) >= 0) list.Items.Add(p);
            list.EndUpdate();
        }
    }
}
