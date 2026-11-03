using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace SteamOfflineSwitch
{
    internal sealed class MainForm : Form
    {
        // 单实例“唤出窗口”通道：后启动的进程连上此管道，通知已有实例把窗口显示出来
        private static readonly string ActivationPipe = "SteamOfflineSwitch_act_" + Environment.UserName;
        private Thread _actThread;

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr h);

        // 由“第二个实例”调用：让正在运行的实例把主窗口显示到前台。
        public static void ActivateExisting()
        {
            // 优先用命名管道通知（跨完整性级别也可用）
            for (int attempt = 0; attempt < 12; attempt++)
            {
                try
                {
                    using (NamedPipeClientStream c = new NamedPipeClientStream(".", ActivationPipe, PipeDirection.Out))
                    {
                        c.Connect(350);
                        c.WriteByte(1);
                        c.Flush();
                        return;
                    }
                }
                catch { Thread.Sleep(120); }
            }
            // 兜底：按标题找到窗口并恢复/置前（同一账户、同权限级别时可用）
            try
            {
                IntPtr h = FindWindow(null, AppInfo.Title + "  v" + AppInfo.Version);
                if (h != IntPtr.Zero)
                {
                    ShowWindow(h, 9); // SW_RESTORE
                    SetForegroundWindow(h);
                }
            }
            catch { }
        }

        private Settings _settings;
        private readonly string[] _args;

        private ToggleSwitch _switch;
        private Label _statusTitle;
        private Label _statusDesc;
        private Label _steamPathLabel;
        private NotifyIcon _tray;
        private ToolStripMenuItem _trayToggle;
        private System.Windows.Forms.Timer _timer;
        private bool _busy;
        private bool _balloonShown;
        private bool _loadingSettings;

        private CheckBox _chkAutoRestore;
        private CheckBox _chkCloseToTray;
        private CheckBox _chkStartWithWindows;
        private CheckBox _chkShowTray;

        public MainForm(Settings settings, string[] args)
        {
            _settings = settings;
            _args = args ?? new string[0];

            BuildUi();
            LoadSettingsToUi();
            ResolveSteamPath();
            ReconcileRules();
            ApplyAutoRestoreOnStart();
            RefreshState();

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 2500;
            _timer.Tick += delegate { if (!_busy) RefreshState(); };
            _timer.Start();

            StartActivationServer();

            // 开机自启（--autostart）时默认收进托盘，不主动弹窗，避免开机打扰
            if (HasArg("--autostart") && _settings.ShowTray && _settings.MinimizeToTray)
            {
                Shown += delegate { HideToTray(); };
            }
        }

        // 后台监听“唤出窗口”请求
        private void StartActivationServer()
        {
            _actThread = new Thread(delegate ()
            {
                while (true)
                {
                    try
                    {
                        using (NamedPipeServerStream s = new NamedPipeServerStream(
                            ActivationPipe, PipeDirection.In, 1,
                            PipeTransmissionMode.Byte, PipeOptions.None))
                        {
                            s.WaitForConnection();
                            try { s.ReadByte(); } catch { }
                        }
                        try
                        {
                            if (IsHandleCreated && !IsDisposed)
                                BeginInvoke(new Action(RevealWindow));
                        }
                        catch { }
                    }
                    catch { Thread.Sleep(200); }
                }
            });
            _actThread.IsBackground = true;
            _actThread.Start();
        }

        // 供后台线程通过 BeginInvoke 调用的“显示并置前”方法
        private void RevealWindow()
        {
            try
            {
                if (!Visible) Show();
                if (WindowState == FormWindowState.Minimized || IsIconic(Handle))
                    WindowState = FormWindowState.Normal;
                ShowInTaskbar = true;
                Activate();
                BringToFront();
                IntPtr fg = Handle;
                SetForegroundWindow(fg);
            }
            catch { }
        }

        // ---------------- UI ----------------
        private void BuildUi()
        {
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            SuspendLayout();
            Text = AppInfo.Title + "  v" + AppInfo.Version;
            Font = new Font("Microsoft YaHei UI", 9f);
            ClientSize = new Size(460, 604);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            Icon = UiIcons.Get(false);

            // 顶部标题栏
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 64;
            header.BackColor = Color.FromArgb(45, 110, 220);

            Label title = new Label();
            title.Text = AppInfo.Title;
            title.ForeColor = Color.White;
            title.Font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(18, 12);
            header.Controls.Add(title);

            Label sub = new Label();
            sub.Text = "家庭共享 · 选择性离线";
            sub.ForeColor = Color.FromArgb(220, 232, 250);
            sub.Font = new Font("Microsoft YaHei UI", 9f);
            sub.AutoSize = true;
            sub.Location = new Point(20, 42);
            header.Controls.Add(sub);

            Controls.Add(header);

            // 开关 + 状态
            _switch = new ToggleSwitch();
            _switch.Location = new Point((ClientSize.Width - _switch.Width) / 2, 86);
            _switch.Click += delegate { OnToggleClick(); };
            Controls.Add(_switch);

            _statusTitle = new Label();
            _statusTitle.TextAlign = ContentAlignment.MiddleCenter;
            _statusTitle.Font = new Font("Microsoft YaHei UI", 16f, FontStyle.Bold);
            _statusTitle.AutoSize = false;
            _statusTitle.Size = new Size(ClientSize.Width - 32, 30);
            _statusTitle.Location = new Point(16, 142);
            Controls.Add(_statusTitle);

            _statusDesc = new Label();
            _statusDesc.TextAlign = ContentAlignment.MiddleCenter;
            _statusDesc.ForeColor = Color.FromArgb(95, 100, 110);
            _statusDesc.Font = new Font("Microsoft YaHei UI", 9f);
            _statusDesc.AutoSize = true;
            _statusDesc.MaximumSize = new Size(ClientSize.Width - 44, 0);
            _statusDesc.Location = new Point(22, 176);
            Controls.Add(_statusDesc);

            // Steam 路径卡片
            Panel card = new Panel();
            card.BorderStyle = BorderStyle.FixedSingle;
            card.BackColor = Color.FromArgb(247, 248, 250);
            card.Location = new Point(20, 222);
            card.Size = new Size(ClientSize.Width - 40, 108);

            Label pathCap = new Label();
            pathCap.Text = "Steam 程序";
            pathCap.ForeColor = Color.FromArgb(120, 125, 135);
            pathCap.AutoSize = true;
            pathCap.Location = new Point(12, 10);
            card.Controls.Add(pathCap);

            _steamPathLabel = new Label();
            _steamPathLabel.AutoSize = false;
            _steamPathLabel.Size = new Size(card.Width - 24, 24);
            _steamPathLabel.Location = new Point(12, 30);
            _steamPathLabel.ForeColor = Color.FromArgb(40, 44, 52);
            _steamPathLabel.AutoEllipsis = true;
            card.Controls.Add(_steamPathLabel);

            Button btnPath = MakeButton("选择路径…", new Point(12, 64), new Size(120, 30));
            btnPath.Click += OnChoosePath;
            card.Controls.Add(btnPath);

            Button btnReset = MakeButton("重建规则", new Point(142, 64), new Size(120, 30));
            btnReset.Click += OnResetRules;
            card.Controls.Add(btnReset);

            Controls.Add(card);

            // 选项
            _chkAutoRestore = MakeCheck("开机 / 重启后自动恢复为联网（推荐）", new Point(22, 346));
            _chkCloseToTray = MakeCheck("关闭窗口时最小化到托盘", new Point(22, 374));
            _chkStartWithWindows = MakeCheck("开机自动启动本工具", new Point(22, 402));
            _chkShowTray = MakeCheck("显示托盘图标", new Point(22, 430));
            _chkAutoRestore.CheckedChanged += delegate { PersistSettings(); };
            _chkCloseToTray.CheckedChanged += delegate { PersistSettings(); };
            _chkStartWithWindows.CheckedChanged += delegate { PersistSettings(); };
            _chkShowTray.CheckedChanged += delegate { PersistSettings(); };

            Label hint = new Label();
            hint.Text = "只拦截 Steam 客户端的联网，不影响其他软件与本地网络。"
                + "离线期间建议关闭该游戏的“Steam 云”存档，回来联网后再同步。";
            hint.ForeColor = Color.FromArgb(140, 145, 155);
            hint.Font = new Font("Microsoft YaHei UI", 8.3f);
            hint.AutoSize = true;
            hint.MaximumSize = new Size(ClientSize.Width - 44, 0);
            hint.Location = new Point(22, 468);
            Controls.Add(hint);

            // 底部链接
            Button btnLog = MakeButton("打开日志文件夹", new Point(20, 536), new Size(150, 30));
            btnLog.Click += delegate { OpenFolder(Store.Dir); };
            Controls.Add(btnLog);

            Button btnFw = MakeButton("Windows 防火墙", new Point(182, 536), new Size(150, 30));
            btnFw.Click += delegate { OpenExe("wf.msc"); };
            Controls.Add(btnFw);

            // 托盘
            _tray = new NotifyIcon();
            _tray.Icon = UiIcons.Get(false);
            _tray.Text = AppInfo.Title;
            ContextMenuStrip menu = new ContextMenuStrip();
            _trayToggle = new ToolStripMenuItem("切换");
            _trayToggle.Click += delegate { OnToggleClick(); };
            menu.Items.Add(_trayToggle);
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem miShow = new ToolStripMenuItem("打开窗口");
            miShow.Click += delegate { ShowMain(); };
            menu.Items.Add(miShow);
            ToolStripMenuItem miRestart = new ToolStripMenuItem("重启 Steam");
            miRestart.Click += delegate { RestartSteam(); };
            menu.Items.Add(miRestart);
            ToolStripMenuItem miDel = new ToolStripMenuItem("删除全部防火墙规则…");
            miDel.Click += delegate { OnRemoveRules(); };
            menu.Items.Add(miDel);
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem miExit = new ToolStripMenuItem("退出");
            miExit.Click += delegate { RealExit(); };
            menu.Items.Add(miExit);
            menu.Opening += delegate { UpdateTrayMenuText(); };
            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += delegate { ShowMain(); };

            Resize += delegate
            {
                if (WindowState == FormWindowState.Minimized && Visible
                    && _settings.ShowTray && _settings.MinimizeToTray)
                {
                    HideToTray();
                }
            };
            FormClosing += OnFormClosing;

            ResumeLayout(false);
        }

        private static Button MakeButton(string text, Point loc, Size size)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = loc;
            b.Size = size;
            b.FlatStyle = FlatStyle.System;
            b.UseVisualStyleBackColor = true;
            return b;
        }

        private CheckBox MakeCheck(string text, Point loc)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.AutoSize = true;
            c.Location = loc;
            c.ForeColor = Color.FromArgb(55, 60, 70);
            Controls.Add(c);
            return c;
        }

        // ---------------- 状态 ----------------
        private void RefreshState()
        {
            bool blocked;
            try { blocked = Firewall.IsBlocked(); }
            catch { blocked = false; }

            if (blocked)
            {
                _switch.On = false;
                _statusTitle.Text = "已离线";
                _statusTitle.ForeColor = Color.FromArgb(216, 132, 44);
                _statusDesc.Text = "Steam 已断开外网，可与家庭组好友同时游玩同一款游戏。\n玩完请切回“在线”，存档会自动重新同步。";
            }
            else
            {
                _switch.On = true;
                _statusTitle.Text = "在线";
                _statusTitle.ForeColor = Color.FromArgb(45, 150, 90);
                _statusDesc.Text = "Steam 正常联网。游玩家庭共享游戏时，若好友也在玩，可能被挤下线。";
            }
            Icon = UiIcons.Get(blocked);
            if (_tray != null)
            {
                _tray.Icon = UiIcons.Get(blocked);
                _tray.Text = AppInfo.Title + (blocked ? " · 已离线" : " · 在线");
            }
            if (_trayToggle != null) UpdateTrayMenuText();
        }

        private void UpdateTrayMenuText()
        {
            bool blocked;
            try { blocked = Firewall.IsBlocked(); } catch { blocked = false; }
            _trayToggle.Text = blocked ? "恢复联网" : "断网（离线玩）";
        }

        // ---------------- 切换 ----------------
        private void OnToggleClick()
        {
            if (_busy) return;
            bool wantBlocked = _switch.On; // 当前在线 -> 点一下变离线
            _busy = true;
            Cursor = Cursors.WaitCursor;
            _switch.Enabled = false;
            try
            {
                if (!Firewall.RulesExist())
                {
                    if (!Firewall.EnsureRules(_settings.SteamExe))
                    {
                        Warn("创建防火墙规则失败，请确认 Steam 路径正确，且以管理员身份运行。");
                        return;
                    }
                }
                if (!Firewall.SetBlocked(wantBlocked))
                {
                    Warn("切换失败：无法修改防火墙规则。请确认本程序以管理员身份运行。");
                    return;
                }
                // 立即反馈
                _switch.On = !wantBlocked;
                if (wantBlocked)
                {
                    _statusTitle.Text = "已离线";
                    _statusTitle.ForeColor = Color.FromArgb(216, 132, 44);
                }
                else
                {
                    _statusTitle.Text = "在线";
                    _statusTitle.ForeColor = Color.FromArgb(45, 150, 90);
                }
            }
            catch (Exception ex)
            {
                Log.E("切换失败", ex);
                Warn("切换时出错：" + ex.Message);
            }
            finally
            {
                _busy = false;
                _switch.Enabled = true;
                Cursor = Cursors.Default;
                RefreshState();
            }
        }

        // ---------------- 初始化逻辑 ----------------
        private void ResolveSteamPath()
        {
            if (string.IsNullOrEmpty(_settings.SteamExe) || !File.Exists(_settings.SteamExe))
            {
                string found = SteamLocator.Find();
                if (!string.IsNullOrEmpty(found)) _settings.SteamExe = found;
                Store.Save(_settings);
            }
            _steamPathLabel.Text = string.IsNullOrEmpty(_settings.SteamExe)
                ? "（未找到，请手动选择 steam.exe）"
                : _settings.SteamExe;
            if (string.IsNullOrEmpty(_settings.SteamExe))
                _steamPathLabel.ForeColor = Color.Firebrick;
            else
                _steamPathLabel.ForeColor = Color.FromArgb(40, 44, 52);
        }

        private void ReconcileRules()
        {
            if (string.IsNullOrEmpty(_settings.SteamExe) || !File.Exists(_settings.SteamExe)) return;
            try
            {
                bool need = !Firewall.RulesExist();
                if (!need)
                {
                    string app = Firewall.GetRuleApplication();
                    if (!string.Equals(app, _settings.SteamExe, StringComparison.OrdinalIgnoreCase))
                        need = true;
                }
                if (need) Firewall.EnsureRules(_settings.SteamExe);
            }
            catch (Exception ex) { Log.E("初始化防火墙规则失败", ex); }
        }

        private void ApplyAutoRestoreOnStart()
        {
            if (!_settings.AutoRestoreOnStart) return;
            bool isAutostart = HasArg("--autostart");
            if (isAutostart && Firewall.IsBlocked())
            {
                Firewall.SetBlocked(false);
                Log.I("开机自启：已自动恢复联网");
            }
        }

        // ---------------- 设置同步 ----------------
        private void LoadSettingsToUi()
        {
            _loadingSettings = true;
            try
            {
                _chkAutoRestore.Checked = _settings.AutoRestoreOnStart;
                _chkCloseToTray.Checked = _settings.CloseToTray;
                _chkStartWithWindows.Checked = _settings.StartWithWindows;
                _chkShowTray.Checked = _settings.ShowTray;
            }
            finally { _loadingSettings = false; }
            _tray.Visible = _settings.ShowTray;
        }

        private void PersistSettings()
        {
            if (_loadingSettings) return;
            _settings.AutoRestoreOnStart = _chkAutoRestore.Checked;
            _settings.CloseToTray = _chkCloseToTray.Checked;
            _settings.ShowTray = _chkShowTray.Checked;

            bool wantStartup = _chkStartWithWindows.Checked;
            if (wantStartup != _settings.StartWithWindows)
            {
                _settings.StartWithWindows = wantStartup;
                Startup.Set(wantStartup);
            }
            Store.Save(_settings);
            if (_tray != null) _tray.Visible = _settings.ShowTray;
        }

        // ---------------- 按钮 ----------------
        private void OnChoosePath(object sender, EventArgs e)
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = "选择 steam.exe";
                d.Filter = "Steam 主程序 (steam.exe)|steam.exe|可执行文件 (*.exe)|*.exe";
                if (File.Exists(_settings.SteamExe))
                {
                    try { d.InitialDirectory = Path.GetDirectoryName(_settings.SteamExe); } catch { }
                }
                if (d.ShowDialog(this) != DialogResult.OK) return;

                string chosen = d.FileName;
                if (!string.Equals(Path.GetFileName(chosen), "steam.exe", StringComparison.OrdinalIgnoreCase))
                {
                    if (!Confirm("选择的文件不是 steam.exe，仍要使用它吗？\n\n" + chosen)) return;
                }
                _settings.SteamExe = Path.GetFullPath(chosen);
                Store.Save(_settings);
                ResolveSteamPath();

                if (Firewall.RulesExist())
                {
                    if (Confirm("是否按新路径重建防火墙规则？\n（当前联网状态会被重置为“在线”）"))
                        Firewall.EnsureRules(_settings.SteamExe);
                }
                else
                {
                    Firewall.EnsureRules(_settings.SteamExe);
                }
                RefreshState();
            }
        }

        private void OnResetRules(object sender, EventArgs e)
        {            if (string.IsNullOrEmpty(_settings.SteamExe) || !File.Exists(_settings.SteamExe))
            {
                Warn("请先选择有效的 steam.exe 路径。");
                return;
            }
            if (!Confirm("将删除并按当前路径重建本工具的防火墙规则（会重置为“在线”）。继续？"))
                return;
            if (Firewall.EnsureRules(_settings.SteamExe))
            {
                RefreshState();
                Info("防火墙规则已重建。");
            }
            else
            {
                Warn("重建失败，详见日志。");
            }
        }

        private void OnRemoveRules()
        {
            if (!Firewall.RulesExist())
            {
                Info("当前没有需要删除的规则。");
                return;
            }
            if (!Confirm("将删除本工具创建的全部防火墙规则，Steam 恢复正常联网。\n（之后仍可在主界面重新开启）继续？"))
                return;
            if (Firewall.RemoveAll())
            {
                RefreshState();
                Info("已删除全部规则。");
            }
            else
            {
                Warn("删除失败，详见日志。");
            }
        }

        // ---------------- 托盘/窗口 ----------------
        private void HideToTray()
        {
            Hide();
            if (!_balloonShown && _tray.Visible)
            {
                _balloonShown = true;
                try
                {
                    _tray.ShowBalloonTip(2000, AppInfo.Title,
                        "程序已最小化到托盘，双击图标可重新打开。",
                        ToolTipIcon.Info);
                }
                catch { }
            }
        }

        private void ShowMain()
        {
            RevealWindow();
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && _settings.CloseToTray && _settings.ShowTray)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }
            DisposeTray();
        }

        private void RealExit()
        {
            DisposeTray();
            Application.Exit();
        }

        private void DisposeTray()
        {
            if (_timer != null) { _timer.Stop(); _timer.Dispose(); _timer = null; }
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; }
        }

        // ---------------- 辅助 ----------------
        private void RestartSteam()
        {
            if (string.IsNullOrEmpty(_settings.SteamExe) || !File.Exists(_settings.SteamExe))
            {
                Warn("Steam 路径无效。");
                return;
            }
            try
            {
                if (Firewall.IsBlocked())
                {
                    if (!Confirm("当前处于“离线”状态，重启 Steam 后它仍将断网，可能需要重新登录。\n继续？"))
                        return;
                }
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = _settings.SteamExe;
                psi.Arguments = "-shutdown";
                psi.UseShellExecute = true;
                try { Process.Start(psi); } catch { }
                System.Threading.Thread.Sleep(1500);
                ProcessStartInfo p2 = new ProcessStartInfo();
                p2.FileName = _settings.SteamExe;
                p2.UseShellExecute = true;
                Process.Start(p2);
                Info("已请求重启 Steam。");
            }
            catch (Exception ex) { Warn("重启 Steam 失败：" + ex.Message); }
        }

        private static void OpenFolder(string path)
        {
            try
            {
                if (Directory.Exists(path)) Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch { }
        }

        private static void OpenExe(string file)
        {
            try { Process.Start(file); } catch { }
        }

        private bool HasArg(string name)
        {
            for (int i = 0; i < _args.Length; i++)
                if (string.Equals(_args[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private void Warn(string m) { MessageBox.Show(this, m, AppInfo.Title, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        private void Info(string m) { MessageBox.Show(this, m, AppInfo.Title, MessageBoxButtons.OK, MessageBoxIcon.Information); }
        private bool Confirm(string m) { return MessageBox.Show(this, m, AppInfo.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes; }
    }
}
