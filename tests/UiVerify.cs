using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using LightAudioSwitch;

class UiVerify
{
    static readonly List<string> results = new List<string>();
    static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Field(object o, string name) { return o.GetType().GetField(name, Hidden).GetValue(o); }
    static object Call(object o, string name, params object[] args) { return o.GetType().GetMethod(name, Hidden).Invoke(o, args); }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); results.Add("PASS " + message); }
    static void Choose(object card, string id, string name) { card.GetType().GetMethod("Choose").Invoke(card, new object[] { id, name }); }
    static bool Wheel(IntPtr target, Point screen, int delta)
    {
        const int WM_MOUSEWHEEL = 0x020A;
        var position = new IntPtr(unchecked((int)(((uint)(ushort)screen.Y << 16) | (ushort)screen.X)));
        var message = Message.Create(target, WM_MOUSEWHEEL, new IntPtr(delta << 16), position);
        return Application.FilterMessage(ref message);
    }
    static void ExerciseDeviceMenu(object card)
    {
        var field = card.GetType().GetField("deviceMenu", Hidden);
        var devicesField = card.GetType().GetField("Devices");
        var original = devicesField.GetValue(card);
        var devices = new List<AudioDevice> {
            new AudioDevice { Id = "menu-headset", Name = "扬声器 (Example Headset)", State = 1 },
            new AudioDevice { Id = "menu-speaker", Name = "Example Speaker", State = 1 },
            new AudioDevice { Id = "menu-offline", Name = "Example Offline Output", State = 0 }
        };
        devicesField.SetValue(card, devices);
        Choose(card, "menu-headset", null);
        var anchor = (Control)card;
        for (int pass = 0; pass < 3; pass++) {
            Call(card,"OpenMenu"); Application.DoEvents(); var menu = (ContextMenuStrip)field.GetValue(card);
            Check(menu.Visible && !menu.IsDisposed, "device menu reopens, pass " + pass);
            Check(menu.Width == anchor.Width && !menu.Region.IsVisible(0, 0), "popup matches card width and has clipped corners");
            Check(((ToolStripMenuItem)menu.Items[0]).Checked && menu.Items[2].AccessibleName.Contains("离线"), "selected and offline states are exposed");
            menu.Items[0].Select(); Call(menu,"ProcessDialogKey",Keys.Down); Call(menu,"ProcessDialogKey",Keys.Enter); Application.DoEvents();
            Check(!menu.Visible && !menu.IsDisposed, "Enter selects without disposing menu, pass " + pass);
            var chosen = (AudioDevice)card.GetType().GetProperty("SelectedDevice").GetValue(card,null);
            Check(chosen.Id == "menu-speaker", "Down then Enter selects the highlighted device");
            Call(card,"OpenMenu"); Application.DoEvents(); Call(menu,"ProcessDialogKey",Keys.Escape); Application.DoEvents();
            Check(!menu.Visible && !menu.IsDisposed, "Escape cancels without disposing menu, pass " + pass);
            Check(ReferenceEquals(chosen, card.GetType().GetProperty("SelectedDevice").GetValue(card,null)), "Escape preserves the selected device");
            Choose(card, "menu-headset", null);
        }
        var empty = new List<AudioDevice>();
        devicesField.SetValue(card, empty);
        Call(card,"OpenMenu"); Application.DoEvents();
        var popup = (ContextMenuStrip)field.GetValue(card);
        Check(popup.Items.Count == 1 && !popup.Items[0].Enabled, "empty device menu shows an unavailable placeholder");
        popup.Close();
        for (int i = 0; i < 60; i++) empty.Add(new AudioDevice { Id = "output-" + i, Name = "Output " + i, State = 1 });
        Call(card,"OpenMenu"); Application.DoEvents();
        Check(Screen.FromControl(anchor).WorkingArea.Contains(popup.Bounds), "long device menu stays inside the screen");
        int firstRowTop = popup.Items[0].Bounds.Top;
        var downArrow = popup.GetChildAtPoint(new Point(popup.Width / 2, popup.Height - 4));
        Check(downArrow != null && downArrow.Visible && downArrow.Enabled, "overflow exposes a clickable down arrow");
        var press = new MouseEventArgs(MouseButtons.Left, 1, downArrow.Width / 2, downArrow.Height / 2, 0);
        Call(downArrow,"OnMouseDown",press); Call(downArrow,"OnMouseUp",press);
        Check(popup.Visible && popup.Items[0].Bounds.Top < firstRowTop, "the down arrow scrolls the menu");
        var upArrow = popup.GetChildAtPoint(new Point(popup.Width / 2, 3));
        Check(upArrow != null && upArrow.Visible && upArrow.Enabled, "scrolling enables the up arrow");
        var lift = new MouseEventArgs(MouseButtons.Left, 1, upArrow.Width / 2, upArrow.Height / 2, 0);
        Call(upArrow,"OnMouseDown",lift); Call(upArrow,"OnMouseUp",lift);
        Check(popup.Visible && popup.Items[0].Bounds.Top == firstRowTop, "the up arrow returns to the first row");
        if (SystemInformation.MouseWheelScrollLines != 0)
        {
            var center = new Point(popup.Left + popup.Width / 2, popup.Top + popup.Height / 2);
            int top = popup.Items[0].Bounds.Top;
            Check(!Wheel(anchor.Handle, new Point(popup.Left - 5, center.Y), -120), "wheel outside the popup is not intercepted");
            Check(Wheel(anchor.Handle, center, -60) && popup.Items[0].Bounds.Top == top, "partial wheel delta is accumulated");
            Check(Wheel(anchor.Handle, center, -60) && popup.Items[0].Bounds.Top < top && popup.Visible,
                "wheel over the popup scrolls rows even when delivered to the card");
            for (int i = 0; i < empty.Count; i++) Wheel(popup.Handle, center, -120);
            Check(popup.Visible && popup.Items[59].Bounds.Bottom <= popup.DisplayRectangle.Bottom && popup.Items[59].Bounds.Top >= popup.DisplayRectangle.Top,
                "wheel reaches the last device without wrapping or dismissing the menu");
            for (int i = 0; i < empty.Count; i++) Wheel(popup.Handle, center, 120);
            Check(popup.Visible && popup.Items[0].Bounds.Top == top, "reverse wheel reaches the first device without wrapping");
        }
        popup.Items[0].Select();
        for (int i = 1; i < empty.Count; i++) Call(popup,"ProcessDialogKey",Keys.Down);
        Check(popup.Items[59].Selected && popup.ClientRectangle.Contains(popup.Items[59].Bounds), "keyboard scroll reaches the last device");
        Call(popup,"ProcessDialogKey",Keys.Enter);
        var last = (AudioDevice)card.GetType().GetProperty("SelectedDevice").GetValue(card,null);
        Check(!popup.Visible && last.Id == "output-59", "the last device can be selected");
        Check(!Wheel(anchor.Handle, popup.Location, -120), "closed popup releases wheel routing");
        devicesField.SetValue(card, original);
    }
    static void ShowOffscreen(Form f)
    { f.ShowInTaskbar = false; f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-30000, -30000); f.Show(); Application.DoEvents(); }

    static void CheckCentered(SettingsForm form, string message)
    {
        Call(form, "ShowCentered");
        Application.DoEvents();
        Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
        var bounds = form.Bounds;
        bool centered = (bounds.Width > area.Width ? bounds.Left == area.Left : Math.Abs(bounds.Left - area.Left - (area.Right - bounds.Right)) <= 1)
            && (bounds.Height > area.Height ? bounds.Top == area.Top : Math.Abs(bounds.Top - area.Top - (area.Bottom - bounds.Bottom)) <= 1);
        Check(form.Visible && form.WindowState == FormWindowState.Normal && centered, message);
    }
    static void CheckBounds(Control parent)
    {
        foreach(Control c in parent.Controls)
        {
            if (c.Visible && c.Width > 0 && c.Height > 0)
                Check(c.Left >= 0 && c.Top >= 0 && c.Right <= parent.ClientSize.Width + 1 && c.Bottom <= parent.ClientSize.Height + 1,
                    "bounds " + c.GetType().Name + "/" + c.Name);
            CheckBounds(c);
        }
    }
    [STAThread] static int Main(string[] args)
    {
        string report = Path.Combine(args[0], "ui-test-results.txt");
        try
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            AppLog.Folder = args[0];
            string configPath = Path.Combine(args[0], "ui-test-config.json");
            var config = new AppConfig { HeadsetId = "test-headset", HeadsetName = "Example Headset", SpeakerId = "test-speaker", SpeakerName = "Example Speaker" };
            config.Games.Add(new GameRule { Name = "Example Game", Executable = "ExampleGame.exe" });
            ConfigStore.Save(configPath, config);
            using(var audio = new AudioService())
            using(var controller = new SwitchController(configPath))
            {
                string originalDefaults = String.Join("|", audio.GetDefaults());
                using(var form = new SettingsForm(controller))
                {
                    form.ShowInTaskbar = false;
                    CheckCentered(form, "settings opens centered after sizing");
                    form.Location = new Point(form.Left + 50, form.Top + 50);
                    form.WindowState = FormWindowState.Minimized;
                    CheckCentered(form, "reopening restores and centers the settings window");
                    ShowOffscreen(form); CheckBounds(form);
                    var h = Field(form,"headset"); var s = Field(form,"speaker");
                    Check(((Control)h).AccessibleRole == AccessibleRole.ComboBox && ((Control)h).AccessibleName == "游戏输出设备", "device selector accessibility");
                    Check(((Control)h).AccessibleDescription.Contains("Example Headset"), "selected device is available to assistive technology");
                    ExerciseDeviceMenu(h); ExerciseDeviceMenu(s);
                    Choose(h,"test-headset","Example Headset"); Choose(s,"test-speaker","Example Speaker");
                    var devicePopup = (ContextMenuStrip)Field(h,"deviceMenu");
                    var addButton = (IButtonControl)Field(form,"add");
                    for (int i = 0; i < 3; i++) {
                        addButton.PerformClick(); Application.DoEvents(); var menu = (ContextMenuStrip)Field(form,"addMenu");
                        Check(menu.Visible && menu.Renderer == devicePopup.Renderer && !menu.Region.IsVisible(0, 0),
                            "Add button opens the shared rounded menu");
                        Call(menu,"ProcessDialogKey",Keys.Escape); Application.DoEvents();
                        Check(!menu.Visible && !menu.IsDisposed,"add menu can be cancelled and reopened");
                    }
                    addButton.PerformClick();
                    var addPopup = (ContextMenuStrip)Field(form,"addMenu");
                    bool openedProcessPicker = false;
                    using (var cancel = new Timer { Interval = 30 }) {
                        cancel.Tick += delegate {
                            foreach (Form open in Application.OpenForms) {
                                if (open.GetType().Name != "ProcessPicker") continue;
                                openedProcessPicker = true;
                                open.Close();
                                break;
                            }
                        };
                        cancel.Start();
                        addPopup.Items[0].Select(); Call(addPopup,"ProcessDialogKey",Keys.Enter);
                    }
                    Check(openedProcessPicker && !addPopup.IsDisposed, "Add action opens the process picker without disposing its menu");
                    var save = (IButtonControl)Field(form,"save");
                    string oldConfig = File.ReadAllText(configPath);
                    Choose(h,null,null); save.PerformClick();
                    Check(File.ReadAllText(configPath) == oldConfig, "missing device does not write config");
                    Check(((Label)Field(form,"feedback")).Text.Contains("请选择"), "missing device shows inline feedback");
                    Choose(h,"test-headset","Example Headset"); Choose(s,"test-headset","Example Headset"); save.PerformClick();
                    Check(File.ReadAllText(configPath) == oldConfig, "duplicate devices are rejected without changing config");
                    Choose(s,"test-speaker","Example Speaker");
                    var stepper = Field(form,"delay"); var value = stepper.GetType().GetProperty("Value");
                    ((IButtonControl)Field(stepper,"plus")).PerformClick();
                    Check((int)value.GetValue(stepper,null) == 4, "delay plus button");
                    value.SetValue(stepper,0,null); ((IButtonControl)Field(stepper,"minus")).PerformClick();
                    Check((int)value.GetValue(stepper,null) == 0 && !((Control)Field(stepper,"minus")).Enabled, "delay lower bound");
                    value.SetValue(stepper,30,null);
                    Check(!((Control)Field(stepper,"plus")).Enabled, "delay upper bound");
                    value.SetValue(stepper,7,null);
                    var helpButton = (IButtonControl)Field(form,"help");
                    var settingsPage = (Control)Field(form,"settingsPage");
                    helpButton.PerformClick(); Application.DoEvents();
                    var helpPage = (Control)Field(form,"helpPage");
                    Check(helpPage.Visible && helpPage.FindForm() == form && !settingsPage.Visible && form.AcceptButton == null,
                        "Help opens in the settings window without leaving Save active");
                    var back = (IButtonControl)Field(helpPage,"back");
                    back.PerformClick(); Application.DoEvents();
                    Check(!helpPage.Visible && settingsPage.Visible && form.AcceptButton == save,
                        "Back restores settings and its default button");
                    Check((int)value.GetValue(stepper,null) == 7 && ((Label)Field(form,"feedback")).Text == "未保存" && File.ReadAllText(configPath) == oldConfig,
                        "round-trip to Help preserves the unsaved draft");
                    helpButton.PerformClick();
                    Check((bool)Call(form,"ProcessCmdKey",new Message(),Keys.Escape) && settingsPage.Visible && !helpPage.Visible,
                        "Escape returns from Help to settings");
                    var toggle = (Control)Field(form,"communications"); var checkedProperty = toggle.GetType().GetProperty("Checked"); checkedProperty.SetValue(toggle,true,null);
                    Check(((Label)Field(form,"feedback")).Text == "未保存", "toggle changes mark draft unsaved"); checkedProperty.SetValue(toggle,false,null);
                    toggle.Focus(); Call(toggle,"OnKeyDown",new KeyEventArgs(Keys.Space)); Call(toggle,"OnKeyUp",new KeyEventArgs(Keys.Space));
                    Check((bool)checkedProperty.GetValue(toggle,null),"Space toggles switch once");
                    Check((toggle.AccessibilityObject.State & AccessibleStates.Checked) != 0,"switch exposes checked state");
                    checkedProperty.SetValue(toggle,false,null);
                    var games = (ListBox)Field(form,"games"); games.SelectedIndex = 0;
                    var removePopup = games.ContextMenuStrip;
                    removePopup.Show(games, new Point(8,8)); Application.DoEvents();
                    Check(removePopup.Visible && removePopup.Renderer == devicePopup.Renderer && !removePopup.Region.IsVisible(0,0),
                        "game context menu uses the shared rounded style");
                    Call(removePopup,"ProcessDialogKey",Keys.Escape); Application.DoEvents();
                    games.GetType().GetMethod("OnKeyDown", Hidden).Invoke(games,new object[] { new KeyEventArgs(Keys.Delete) });
                    Check(games.Items.Count == 0 && ((Label)Field(form,"emptyGames")).Visible, "Delete removes a game and exposes the empty state");
                    Call(form,"AddRule",null,"ExampleGame.exe"); Call(form,"AddRule",null,"ExampleGame.exe");
                    Check(games.Items.Count == 1, "duplicate game addition is ignored");
                    Call(form,"AddRule",null,"wegame.exe"); Check(games.Items.Count == 1, "launcher is rejected");
                    Call(form,"AddRule",null,"SecondGame.exe"); CheckBounds(form);
                    Check(games.ClientSize.Height >= games.ItemHeight * 2, "two games receive two full visible rows");
                    save.PerformClick();
                    var saved = ConfigStore.Load(configPath);
                    Check(saved.ExitDelaySeconds == 7 && saved.Games.Count == 2 && !saved.SwitchCommunications, "saving preserves all edited values");
                    Check(((Label)Field(form,"feedback")).Text == "已保存", "save uses inline confirmation");
                    Check(String.Join("|", audio.GetDefaults()) == originalDefaults, "UI tests preserve actual audio defaults");
                    form.Close(); controller.Tick();
                    Check(true, "closing settings leaves controller usable");
                }
                var picker = (Form)Activator.CreateInstance(typeof(SettingsForm).Assembly.GetType("LightAudioSwitch.ProcessPicker"), true);
                using(picker)
                {
                    ShowOffscreen(picker); CheckBounds(picker);
                    ((TextBox)Field(picker,"search")).Text = "__NO_MATCH_7d30ac8e__";
                    Check(((ListBox)Field(picker,"list")).Items.Count == 0 && !((Control)Field(picker,"add")).Enabled, "empty search disables add");
                    picker.Close();
                }
            }
            File.WriteAllText(report,String.Join(Environment.NewLine,results),Encoding.UTF8); return 0;
        }
        catch(Exception ex)
        { File.WriteAllText(report,String.Join(Environment.NewLine,results) + Environment.NewLine + ex,Encoding.UTF8); return 1; }
    }
}
