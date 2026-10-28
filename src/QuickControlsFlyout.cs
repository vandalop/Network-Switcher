using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace NetworkSwitcher
{
    public class QuickControlsFlyout : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        private Label _lblHeader;
        private Button _btnRefresh;
        private Button _btnClose;

        // Ethernet Card Controls
        private Panel _pnlEthernet;
        private Label _lblEthTitle;
        private Label _lblEthDesc;
        private Label _lblEthStatus;
        private ToggleSwitch _swEthernet;

        // WiFi Card Controls
        private Panel _pnlWiFi;
        private Label _lblWifiTitle;
        private Label _lblWifiDesc;
        private Label _lblWifiStatus;
        private ToggleSwitch _swWiFi;

        // USB Tethering Card Controls
        private Panel _pnlUsb;
        private Label _lblUsbTitle;
        private Label _lblUsbDesc;
        private Label _lblUsbStatus;
        private ToggleSwitch _swUsb;

        // Preset Buttons
        private Button _btnEthOnly;
        private Button _btnWifiOnly;
        private Button _btnUsbOnly;
        private Button _btnAllOn;

        // Footer Controls
        private CheckBox _chkStartup;
        private CheckBox _chkLeftClickToggle;
        private Button _btnExit;

        private Config _config;
        private bool _isUpdatingUi = false;
        private Action _onStateChanged;
        private SynchronizationContext _syncContext;

        private NetworkAdapterInfo _cachedEth;
        private NetworkAdapterInfo _cachedWifi;
        private NetworkAdapterInfo _cachedUsb;

        public QuickControlsFlyout(Config config, Action onStateChanged)
        {
            _syncContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            _config = config;
            _onStateChanged = onStateChanged;

            InitializeComponent();
            ApplyDwmStyling();
        }

        private void InitializeComponent()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true;
            this.ClientSize = new Size(340, 440);
            this.BackColor = Color.FromArgb(28, 28, 28);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            this.DoubleBuffered = true;

            // 1. Header
            _lblHeader = new Label
            {
                Text = "⚡ Network Switcher",
                Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(14, 12),
                Size = new Size(240, 24)
            };

            _btnRefresh = CreateIconButton("↻", new Point(272, 10), new Size(26, 26));
            _btnRefresh.Click += delegate { RefreshAdapters(); };

            _btnClose = CreateIconButton("✕", new Point(302, 10), new Size(26, 26));
            _btnClose.Click += delegate { CloseFlyout(); };

            this.Controls.Add(_lblHeader);
            this.Controls.Add(_btnRefresh);
            this.Controls.Add(_btnClose);

            int yOffset = 44;

            // 2. Ethernet Card
            _pnlEthernet = CreateCardPanel(yOffset, out _lblEthTitle, out _lblEthDesc, out _lblEthStatus, out _swEthernet, "🔌 Ethernet");
            _swEthernet.CheckedChanged += OnEthernetSwitchToggled;
            this.Controls.Add(_pnlEthernet);
            yOffset += 68;

            // 3. Wi-Fi Card
            _pnlWiFi = CreateCardPanel(yOffset, out _lblWifiTitle, out _lblWifiDesc, out _lblWifiStatus, out _swWiFi, "📶 Wi-Fi");
            _swWiFi.CheckedChanged += OnWiFiSwitchToggled;
            this.Controls.Add(_pnlWiFi);
            yOffset += 68;

            // 4. USB Tethering Card
            _pnlUsb = CreateCardPanel(yOffset, out _lblUsbTitle, out _lblUsbDesc, out _lblUsbStatus, out _swUsb, "📱 USB Tethering");
            _swUsb.CheckedChanged += OnUsbSwitchToggled;
            this.Controls.Add(_pnlUsb);
            yOffset += 72;

            // 5. Presets Section
            Label lblPresets = new Label
            {
                Text = "QUICK SWITCH PRESETS",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(140, 140, 140),
                Location = new Point(14, yOffset),
                Size = new Size(200, 14)
            };
            this.Controls.Add(lblPresets);
            yOffset += 18;

            _btnEthOnly = CreatePresetButton("⚡ Ethernet Only", new Point(12, yOffset), new Size(154, 32));
            _btnEthOnly.Click += delegate { SwitchTo(AdapterCategory.Ethernet); };

            _btnWifiOnly = CreatePresetButton("📶 Wi-Fi Only", new Point(174, yOffset), new Size(154, 32));
            _btnWifiOnly.Click += delegate { SwitchTo(AdapterCategory.WiFi); };

            this.Controls.Add(_btnEthOnly);
            this.Controls.Add(_btnWifiOnly);
            yOffset += 38;

            _btnUsbOnly = CreatePresetButton("📱 USB Only", new Point(12, yOffset), new Size(154, 32));
            _btnUsbOnly.Click += delegate { SwitchTo(AdapterCategory.UsbTethering); };

            _btnAllOn = CreatePresetButton("🌐 Enable All", new Point(174, yOffset), new Size(154, 32));
            _btnAllOn.Click += delegate { EnableAll(); };

            this.Controls.Add(_btnUsbOnly);
            this.Controls.Add(_btnAllOn);
            yOffset += 46;

            // 6. Footer Options
            _chkStartup = new CheckBox
            {
                Text = "Start elevated with Windows",
                Location = new Point(14, yOffset),
                Size = new Size(200, 20),
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 8.25f),
                Checked = _config.StartWithWindows
            };
            _chkStartup.CheckedChanged += OnStartupCheckedChanged;

            _btnExit = new Button
            {
                Text = "Exit App",
                Location = new Point(246, yOffset),
                Size = new Size(82, 24),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.FromArgb(240, 90, 90),
                Font = new Font("Segoe UI", 8.25f),
                Cursor = Cursors.Hand
            };
            _btnExit.FlatAppearance.BorderSize = 0;
            _btnExit.Click += delegate { Application.Exit(); };

            this.Controls.Add(_chkStartup);
            this.Controls.Add(_btnExit);
            yOffset += 24;

            _chkLeftClickToggle = new CheckBox
            {
                Text = "Left-click tray directly toggles Ethernet",
                Location = new Point(14, yOffset),
                Size = new Size(310, 20),
                ForeColor = Color.FromArgb(170, 170, 170),
                Font = new Font("Segoe UI", 8.25f),
                Checked = _config.LeftClickTogglesEthernet
            };
            _chkLeftClickToggle.CheckedChanged += delegate
            {
                _config.LeftClickTogglesEthernet = _chkLeftClickToggle.Checked;
                _config.Save();
            };
            this.Controls.Add(_chkLeftClickToggle);
        }

        private Panel CreateCardPanel(int y, out Label lblTitle, out Label lblDesc, out Label lblStatus, out ToggleSwitch sw, string titleText)
        {
            Panel pnl = new Panel
            {
                Location = new Point(12, y),
                Size = new Size(316, 62),
                BackColor = Color.FromArgb(36, 36, 36)
            };
            pnl.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Color.FromArgb(52, 52, 52), 1f))
                {
                    Rectangle rect = new Rectangle(0, 0, pnl.Width - 1, pnl.Height - 1);
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.DrawRectangle(pen, rect);
                }
            };

            lblTitle = new Label
            {
                Text = titleText,
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(10, 8),
                Size = new Size(160, 18)
            };

            lblDesc = new Label
            {
                Text = "Detecting adapter...",
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(160, 160, 160),
                Location = new Point(10, 26),
                Size = new Size(230, 16)
            };

            lblStatus = new Label
            {
                Text = "● Checking",
                Font = new Font("Segoe UI Semibold", 8f),
                ForeColor = Color.FromArgb(180, 180, 180),
                Location = new Point(10, 42),
                Size = new Size(230, 16)
            };

            sw = new ToggleSwitch
            {
                Location = new Point(258, 18),
                Size = new Size(46, 24)
            };

            pnl.Controls.Add(lblTitle);
            pnl.Controls.Add(lblDesc);
            pnl.Controls.Add(lblStatus);
            pnl.Controls.Add(sw);

            return pnl;
        }

        private Button CreateIconButton(string text, Point loc, Size size)
        {
            Button btn = new Button
            {
                Text = text,
                Location = loc,
                Size = size,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(180, 180, 180),
                Font = new Font("Segoe UI", 9f),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 50, 50);
            return btn;
        }

        private Button CreatePresetButton(string text, Point loc, Size size)
        {
            Button btn = new Button
            {
                Text = text,
                Location = loc,
                Size = size,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(42, 42, 42),
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 8.75f),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 60);
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 120, 215);
            return btn;
        }

        private void ApplyDwmStyling()
        {
            try
            {
                int val = DWMWCP_ROUND;
                DwmSetWindowAttribute(this.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref val, sizeof(int));
            }
            catch { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen borderPen = new Pen(Color.FromArgb(60, 60, 60), 1f))
            {
                e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
            }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            CloseFlyout();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                CloseFlyout();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void CloseFlyout()
        {
            this.Hide();
            MemoryHelper.TrimWorkingSet();
        }

        public void ShowNear(Point cursorPosition)
        {
            RefreshAdapters();

            Screen scr = Screen.FromPoint(cursorPosition);
            Rectangle workArea = scr.WorkingArea;

            int targetX = cursorPosition.X - (this.Width / 2);
            int targetY;

            // Determine if taskbar is top or bottom
            if (cursorPosition.Y < workArea.Top + (workArea.Height / 2))
            {
                targetY = workArea.Top + 8; // Top taskbar
            }
            else
            {
                targetY = workArea.Bottom - this.Height - 8; // Bottom taskbar
            }

            if (targetX + this.Width > workArea.Right - 8)
            {
                targetX = workArea.Right - this.Width - 8;
            }
            if (targetX < workArea.Left + 8)
            {
                targetX = workArea.Left + 8;
            }

            this.Location = new Point(targetX, targetY);
            this.Show();
            this.Activate();
        }

        public void RefreshAdapters()
        {
            var adapters = NetworkManager.GetAdapters();
            _cachedEth = NetworkManager.GetPrimaryAdapter(AdapterCategory.Ethernet, adapters);
            _cachedWifi = NetworkManager.GetPrimaryAdapter(AdapterCategory.WiFi, adapters);
            _cachedUsb = NetworkManager.GetPrimaryAdapter(AdapterCategory.UsbTethering, adapters);

            _isUpdatingUi = true;
            try
            {
                // Update Ethernet
                if (_cachedEth != null)
                {
                    _lblEthDesc.Text = _cachedEth.Description;
                    if (_cachedEth.IsEnabled)
                    {
                        if (_cachedEth.IsConnected)
                        {
                            string speed = !string.IsNullOrEmpty(_cachedEth.SpeedDisplay) ? " (" + _cachedEth.SpeedDisplay + ")" : "";
                            _lblEthStatus.Text = "● Connected" + speed;
                            _lblEthStatus.ForeColor = Color.FromArgb(52, 211, 153);
                        }
                        else
                        {
                            _lblEthStatus.Text = "● Cable Unplugged";
                            _lblEthStatus.ForeColor = Color.FromArgb(251, 191, 36);
                        }
                    }
                    else
                    {
                        _lblEthStatus.Text = "● Disabled";
                        _lblEthStatus.ForeColor = Color.FromArgb(156, 163, 175);
                    }
                    _swEthernet.Enabled = true;
                    _swEthernet.Checked = _cachedEth.IsEnabled;
                }
                else
                {
                    _lblEthDesc.Text = "No Ethernet adapter found";
                    _lblEthStatus.Text = "● Not Available";
                    _lblEthStatus.ForeColor = Color.FromArgb(120, 120, 120);
                    _swEthernet.Enabled = false;
                    _swEthernet.Checked = false;
                }

                // Update Wi-Fi
                if (_cachedWifi != null)
                {
                    _lblWifiDesc.Text = _cachedWifi.Description;
                    if (_cachedWifi.IsEnabled)
                    {
                        if (_cachedWifi.IsConnected)
                        {
                            string speed = !string.IsNullOrEmpty(_cachedWifi.SpeedDisplay) ? " (" + _cachedWifi.SpeedDisplay + ")" : "";
                            _lblWifiStatus.Text = "● Connected" + speed;
                            _lblWifiStatus.ForeColor = Color.FromArgb(52, 211, 153);
                        }
                        else
                        {
                            _lblWifiStatus.Text = "● Disconnected";
                            _lblWifiStatus.ForeColor = Color.FromArgb(251, 191, 36);
                        }
                    }
                    else
                    {
                        _lblWifiStatus.Text = "● Disabled";
                        _lblWifiStatus.ForeColor = Color.FromArgb(156, 163, 175);
                    }
                    _swWiFi.Enabled = true;
                    _swWiFi.Checked = _cachedWifi.IsEnabled;
                }
                else
                {
                    _lblWifiDesc.Text = "No Wi-Fi card found";
                    _lblWifiStatus.Text = "● Not Available";
                    _lblWifiStatus.ForeColor = Color.FromArgb(120, 120, 120);
                    _swWiFi.Enabled = false;
                    _swWiFi.Checked = false;
                }

                // Update USB Tethering
                if (_cachedUsb != null)
                {
                    _lblUsbDesc.Text = _cachedUsb.Description;
                    if (_cachedUsb.IsEnabled)
                    {
                        _lblUsbStatus.Text = _cachedUsb.IsConnected ? "● Connected" : "● Plugged In";
                        _lblUsbStatus.ForeColor = _cachedUsb.IsConnected ? Color.FromArgb(52, 211, 153) : Color.FromArgb(251, 191, 36);
                    }
                    else
                    {
                        _lblUsbStatus.Text = "● Disabled";
                        _lblUsbStatus.ForeColor = Color.FromArgb(156, 163, 175);
                    }
                    _swUsb.Enabled = true;
                    _swUsb.Checked = _cachedUsb.IsEnabled;
                    _btnUsbOnly.Enabled = true;
                }
                else
                {
                    _lblUsbDesc.Text = "Plug in phone via USB & turn on USB Tethering";
                    _lblUsbStatus.Text = "● Not Plugged In";
                    _lblUsbStatus.ForeColor = Color.FromArgb(120, 120, 120);
                    _swUsb.Enabled = false;
                    _swUsb.Checked = false;
                    _btnUsbOnly.Enabled = false;
                }
            }
            finally
            {
                _isUpdatingUi = false;
            }

            if (_onStateChanged != null)
            {
                _onStateChanged();
            }
        }

        private void OnEthernetSwitchToggled(object sender, EventArgs e)
        {
            if (_isUpdatingUi || _cachedEth == null) return;
            bool target = _swEthernet.Checked;
            ThreadPool.QueueUserWorkItem(delegate
            {
                NetworkManager.SetAdapterEnabled(_cachedEth.Name, target);
                Thread.Sleep(300);
                _syncContext.Post(delegate { RefreshAdapters(); }, null);
            });
        }

        private void OnWiFiSwitchToggled(object sender, EventArgs e)
        {
            if (_isUpdatingUi || _cachedWifi == null) return;
            bool target = _swWiFi.Checked;
            ThreadPool.QueueUserWorkItem(delegate
            {
                NetworkManager.SetAdapterEnabled(_cachedWifi.Name, target);
                Thread.Sleep(300);
                _syncContext.Post(delegate { RefreshAdapters(); }, null);
            });
        }

        private void OnUsbSwitchToggled(object sender, EventArgs e)
        {
            if (_isUpdatingUi || _cachedUsb == null) return;
            bool target = _swUsb.Checked;
            ThreadPool.QueueUserWorkItem(delegate
            {
                NetworkManager.SetAdapterEnabled(_cachedUsb.Name, target);
                Thread.Sleep(300);
                _syncContext.Post(delegate { RefreshAdapters(); }, null);
            });
        }

        private void SwitchTo(AdapterCategory cat)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                NetworkManager.SwitchTo(cat);
                Thread.Sleep(400);
                _syncContext.Post(delegate { RefreshAdapters(); }, null);
            });
        }

        private void EnableAll()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                NetworkManager.EnableAll();
                Thread.Sleep(400);
                _syncContext.Post(delegate { RefreshAdapters(); }, null);
            });
        }

        private void OnStartupCheckedChanged(object sender, EventArgs e)
        {
            bool enable = _chkStartup.Checked;
            bool ok = StartupManager.SetStartWithWindows(enable);
            if (!ok)
            {
                MessageBox.Show("Could not modify Windows Scheduled Task. Make sure the app is running as Administrator.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            _config.StartWithWindows = StartupManager.IsTaskRegistered();
        }
    }
}
