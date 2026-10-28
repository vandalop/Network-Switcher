using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace NetworkSwitcher
{
    public class TrayApplicationContext : ApplicationContext
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _contextMenu;
        private QuickControlsFlyout _flyout;
        private Config _config;
        private System.Windows.Forms.Timer _pollTimer;
        private SynchronizationContext _syncContext;

        // Context menu items
        private ToolStripMenuItem _mnuEthSwitch;
        private ToolStripMenuItem _mnuWifiSwitch;
        private ToolStripMenuItem _mnuUsbSwitch;
        private ToolStripMenuItem _mnuAllOn;
        private ToolStripMenuItem _mnuEthToggle;
        private ToolStripMenuItem _mnuWifiToggle;
        private ToolStripMenuItem _mnuUsbToggle;
        private ToolStripMenuItem _mnuStartup;
        private ToolStripMenuItem _mnuLeftClickMode;

        private NetworkAdapterInfo _cachedEth;
        private NetworkAdapterInfo _cachedWifi;
        private NetworkAdapterInfo _cachedUsb;
        private bool _firstUpdate = true;
        private int _isScanning = 0;

        public TrayApplicationContext()
        {
            _syncContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            _config = Config.Load();

            _flyout = new QuickControlsFlyout(_config, OnFlyoutStateChanged);

            InitializeContextMenu();

            // Set initial placeholder icon immediately so NotifyIcon is never empty
            Icon initialIcon = IconGenerator.CreateTrayIcon(AdapterCategory.Ethernet, false, true);

            _notifyIcon = new NotifyIcon
            {
                Text = "Network Switcher (Starting...)",
                ContextMenuStrip = _contextMenu,
                Icon = initialIcon,
                Visible = true
            };

            _notifyIcon.MouseClick += OnNotifyIconMouseClick;
            _notifyIcon.DoubleClick += delegate
            {
                ToggleFlyout();
            };

            // Event-driven kernel network change detection (ZERO CPU overhead when idle)
            try
            {
                NetworkChange.NetworkAddressChanged += delegate { UpdateStatusAsync(); };
                NetworkChange.NetworkAvailabilityChanged += delegate { UpdateStatusAsync(); };
            }
            catch { }

            // Gentle background heartbeat every 15 seconds (sleeps rest of the time)
            _pollTimer = new System.Windows.Forms.Timer
            {
                Interval = 15000
            };
            _pollTimer.Tick += delegate { UpdateStatusAsync(); };
            _pollTimer.Start();

            // Trigger initial network scan
            UpdateStatusAsync();
        }

        private void InitializeContextMenu()
        {
            _contextMenu = new ContextMenuStrip();
            _contextMenu.Renderer = new FluentContextMenuRenderer();
            _contextMenu.BackColor = Color.FromArgb(30, 30, 30);
            _contextMenu.ForeColor = Color.FromArgb(245, 245, 245);
            _contextMenu.Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
            _contextMenu.ShowImageMargin = true;
            _contextMenu.ShowCheckMargin = true;
            _contextMenu.Padding = new Padding(2, 4, 2, 4);

            _contextMenu.Opened += delegate
            {
                try
                {
                    int val = DWMWCP_ROUND;
                    DwmSetWindowAttribute(_contextMenu.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref val, sizeof(int));
                }
                catch { }
            };

            // Presets
            _mnuEthSwitch = CreateMenuItem("⚡ Switch to Ethernet", delegate { SwitchTo(AdapterCategory.Ethernet); });
            _mnuWifiSwitch = CreateMenuItem("📶 Switch to Wi-Fi", delegate { SwitchTo(AdapterCategory.WiFi); });
            _mnuUsbSwitch = CreateMenuItem("📱 Switch to USB Tethering", delegate { SwitchTo(AdapterCategory.UsbTethering); });
            _mnuAllOn = CreateMenuItem("🌐 Enable All Adapters", delegate { EnableAll(); });

            _contextMenu.Items.Add(_mnuEthSwitch);
            _contextMenu.Items.Add(_mnuWifiSwitch);
            _contextMenu.Items.Add(_mnuUsbSwitch);
            _contextMenu.Items.Add(_mnuAllOn);
            _contextMenu.Items.Add(new ToolStripSeparator());

            // Toggles
            _mnuEthToggle = CreateMenuItem("🔌 Ethernet", delegate { ToggleAdapter(_cachedEth); });
            _mnuWifiToggle = CreateMenuItem("📶 Wi-Fi", delegate { ToggleAdapter(_cachedWifi); });
            _mnuUsbToggle = CreateMenuItem("📱 USB Tethering", delegate { ToggleAdapter(_cachedUsb); });

            _contextMenu.Items.Add(_mnuEthToggle);
            _contextMenu.Items.Add(_mnuWifiToggle);
            _contextMenu.Items.Add(_mnuUsbToggle);
            _contextMenu.Items.Add(new ToolStripSeparator());

            // Tools & Settings
            var mnuFlyout = CreateMenuItem("📋 Open Quick Controls", delegate { ToggleFlyout(); });
            mnuFlyout.Font = new Font(_contextMenu.Font, FontStyle.Bold);

            var mnuRefresh = CreateMenuItem("🔄 Refresh Status", delegate { UpdateStatusAsync(); });

            _mnuStartup = CreateMenuItem("Start with Windows", OnToggleStartup);
            _mnuStartup.Checked = _config.StartWithWindows;

            _mnuLeftClickMode = CreateMenuItem("Left-Click Toggles Ethernet", OnToggleLeftClickMode);
            _mnuLeftClickMode.Checked = _config.LeftClickTogglesEthernet;

            var mnuExit = CreateMenuItem("🚪 Exit", delegate
            {
                _notifyIcon.Visible = false;
                Application.Exit();
            });

            _contextMenu.Items.Add(mnuFlyout);
            _contextMenu.Items.Add(mnuRefresh);
            _contextMenu.Items.Add(_mnuStartup);
            _contextMenu.Items.Add(_mnuLeftClickMode);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(mnuExit);
        }

        private ToolStripMenuItem CreateMenuItem(string text, EventHandler onClick)
        {
            var item = new ToolStripMenuItem(text, null, onClick);
            item.Padding = new Padding(6, 4, 6, 4);
            item.ForeColor = Color.FromArgb(245, 245, 245);
            return item;
        }

        private void OnNotifyIconMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (_config.LeftClickTogglesEthernet)
                {
                    // Directly toggle Ethernet
                    if (_cachedEth != null)
                    {
                        bool newState = !_cachedEth.IsEnabled;
                        ThreadPool.QueueUserWorkItem(delegate
                        {
                            NetworkManager.SetAdapterEnabled(_cachedEth.Name, newState);
                            Thread.Sleep(300);
                            UpdateStatusAsync();
                        });

                        _notifyIcon.ShowBalloonTip(1500, "Network Switcher", 
                            newState ? "Ethernet is being Enabled..." : "Ethernet is being Disabled...", 
                            ToolTipIcon.Info);
                    }
                    else
                    {
                        ToggleFlyout();
                    }
                }
                else
                {
                    ToggleFlyout();
                }
            }
        }

        private void ToggleFlyout()
        {
            if (_flyout.Visible)
            {
                _flyout.Hide();
                MemoryHelper.TrimWorkingSet();
            }
            else
            {
                _flyout.ShowNear(Control.MousePosition);
            }
        }

        private void OnFlyoutStateChanged()
        {
            UpdateStatusAsync();
        }

        private void UpdateStatusAsync()
        {
            // Prevent overlapping scans
            if (Interlocked.CompareExchange(ref _isScanning, 1, 0) != 0) return;

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    var adapters = NetworkManager.GetAdapters();
                    var eth = NetworkManager.GetPrimaryAdapter(AdapterCategory.Ethernet, adapters);
                    var wifi = NetworkManager.GetPrimaryAdapter(AdapterCategory.WiFi, adapters);
                    var usb = NetworkManager.GetPrimaryAdapter(AdapterCategory.UsbTethering, adapters);

                    // Determine active category and connection state
                    AdapterCategory activeCat = AdapterCategory.Ethernet;
                    bool isConnected = false;
                    bool anyEnabled = false;

                    if (eth != null && eth.IsEnabled)
                    {
                        activeCat = AdapterCategory.Ethernet;
                        isConnected = eth.IsConnected;
                        anyEnabled = true;
                    }
                    else if (wifi != null && wifi.IsEnabled)
                    {
                        activeCat = AdapterCategory.WiFi;
                        isConnected = wifi.IsConnected;
                        anyEnabled = true;
                    }
                    else if (usb != null && usb.IsEnabled)
                    {
                        activeCat = AdapterCategory.UsbTethering;
                        isConnected = usb.IsConnected;
                        anyEnabled = true;
                    }

                    // Construct Tooltip
                    string tooltipEth = (eth != null) ? (eth.IsEnabled ? (eth.IsConnected ? "Eth: ON (Connected)" : "Eth: ON (Unplugged)") : "Eth: OFF") : "Eth: None";
                    string tooltipWifi = (wifi != null) ? (wifi.IsEnabled ? (wifi.IsConnected ? "WiFi: ON" : "WiFi: Idle") : "WiFi: OFF") : "WiFi: None";
                    string tooltip = "Network Switcher: " + tooltipEth + " | " + tooltipWifi;
                    if (tooltip.Length >= 63) tooltip = tooltip.Substring(0, 60) + "...";

                    Icon newIcon = IconGenerator.CreateTrayIcon(activeCat, isConnected, anyEnabled);

                    // Post safely to UI thread via SynchronizationContext
                    _syncContext.Post(delegate
                    {
                        try
                        {
                            _cachedEth = eth;
                            _cachedWifi = wifi;
                            _cachedUsb = usb;

                            // Update Tray Icon
                            Icon oldIcon = _notifyIcon.Icon;
                            _notifyIcon.Icon = newIcon;
                            if (oldIcon != null) oldIcon.Dispose();

                            _notifyIcon.Text = tooltip;

                            // Update Context Menu Checkmarks & Texts
                            if (eth != null)
                            {
                                _mnuEthToggle.Enabled = true;
                                _mnuEthToggle.Checked = eth.IsEnabled;
                                _mnuEthToggle.Text = string.Format("🔌 Ethernet: {0}", eth.IsEnabled ? "Enabled" : "Disabled");
                            }
                            else
                            {
                                _mnuEthToggle.Enabled = false;
                                _mnuEthToggle.Text = "🔌 Ethernet: Not Found";
                            }

                            if (wifi != null)
                            {
                                _mnuWifiToggle.Enabled = true;
                                _mnuWifiToggle.Checked = wifi.IsEnabled;
                                _mnuWifiToggle.Text = string.Format("📶 Wi-Fi: {0}", wifi.IsEnabled ? "Enabled" : "Disabled");
                            }
                            else
                            {
                                _mnuWifiToggle.Enabled = false;
                                _mnuWifiToggle.Text = "📶 Wi-Fi: Not Found";
                            }

                            if (usb != null)
                            {
                                _mnuUsbToggle.Enabled = true;
                                _mnuUsbToggle.Checked = usb.IsEnabled;
                                _mnuUsbToggle.Text = string.Format("📱 USB Tether: {0}", usb.IsEnabled ? "Enabled" : "Disabled");
                                _mnuUsbSwitch.Enabled = true;
                            }
                            else
                            {
                                _mnuUsbToggle.Enabled = false;
                                _mnuUsbToggle.Text = "📱 USB Tether: Not Connected";
                                _mnuUsbSwitch.Enabled = false;
                            }

                            // On first successful scan, show a welcome balloon tip
                            if (_firstUpdate)
                            {
                                _firstUpdate = false;
                                _notifyIcon.ShowBalloonTip(2000, "Network Switcher Active", 
                                    "Ready in system tray! Click icon to switch networks.", 
                                    ToolTipIcon.Info);

                                // Trim working set to baseline ~2-4 MB
                                MemoryHelper.TrimWorkingSet();
                            }
                        }
                        catch { }
                        finally
                        {
                            Interlocked.Exchange(ref _isScanning, 0);
                        }
                    }, null);
                }
                catch
                {
                    Interlocked.Exchange(ref _isScanning, 0);
                }
            });
        }

        private void ToggleAdapter(NetworkAdapterInfo adapter)
        {
            if (adapter == null) return;
            bool target = !adapter.IsEnabled;
            ThreadPool.QueueUserWorkItem(delegate
            {
                NetworkManager.SetAdapterEnabled(adapter.Name, target);
                Thread.Sleep(300);
                UpdateStatusAsync();
            });
        }

        private void SwitchTo(AdapterCategory cat)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                NetworkManager.SwitchTo(cat);
                Thread.Sleep(400);
                UpdateStatusAsync();
            });
        }

        private void EnableAll()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                NetworkManager.EnableAll();
                Thread.Sleep(400);
                UpdateStatusAsync();
            });
        }

        private void OnToggleStartup(object sender, EventArgs e)
        {
            bool target = !_mnuStartup.Checked;
            bool ok = StartupManager.SetStartWithWindows(target);
            if (ok)
            {
                _config.StartWithWindows = target;
                _mnuStartup.Checked = target;
            }
            else
            {
                MessageBox.Show("Could not register Windows Scheduled Task. Please verify Administrator privileges.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnToggleLeftClickMode(object sender, EventArgs e)
        {
            _config.LeftClickTogglesEthernet = !_config.LeftClickTogglesEthernet;
            _config.Save();
            _mnuLeftClickMode.Checked = _config.LeftClickTogglesEthernet;
        }

        protected override void ExitThreadCore()
        {
            if (_pollTimer != null)
            {
                _pollTimer.Stop();
                _pollTimer.Dispose();
            }
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            if (_flyout != null && !_flyout.IsDisposed)
            {
                _flyout.Dispose();
            }
            base.ExitThreadCore();
        }
    }

    // Windows 11 Fluent Dark Custom Context Menu Renderer
    public class FluentContextMenuRenderer : ToolStripRenderer
    {
        private static readonly Color BgColor = Color.FromArgb(30, 30, 30);
        private static readonly Color BorderColor = Color.FromArgb(52, 52, 52);
        private static readonly Color HoverColor = Color.FromArgb(50, 50, 50);
        private static readonly Color TextColor = Color.FromArgb(245, 245, 245);
        private static readonly Color TextDisabledColor = Color.FromArgb(120, 120, 120);
        private static readonly Color AccentBlue = Color.FromArgb(0, 120, 215);
        private static readonly Color SeparatorColor = Color.FromArgb(48, 48, 48);

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (SolidBrush brush = new SolidBrush(BgColor))
            {
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (Pen pen = new Pen(BorderColor, 1f))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected && e.Item.Enabled)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(3, 1, e.Item.Width - 6, e.Item.Height - 2);
                using (GraphicsPath path = GetRoundedRectangle(rect, 4))
                using (SolidBrush brush = new SolidBrush(HoverColor))
                {
                    e.Graphics.FillPath(brush, path);
                }
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            Color color = e.Item.Enabled ? TextColor : TextDisabledColor;
            Rectangle rect = e.TextRectangle;
            TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, rect, color, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            ToolStripMenuItem item = e.Item as ToolStripMenuItem;
            if (item != null && item.Checked)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle box = new Rectangle(e.ImageRectangle.X + 2, e.ImageRectangle.Y + 2, e.ImageRectangle.Width - 4, e.ImageRectangle.Height - 4);
                
                // Draw rounded pill/circle
                using (SolidBrush brush = new SolidBrush(AccentBlue))
                {
                    e.Graphics.FillEllipse(brush, box);
                }

                // Draw crisp white checkmark
                using (Pen pen = new Pen(Color.White, 1.8f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    float cx = box.X + box.Width / 2f;
                    float cy = box.Y + box.Height / 2f;
                    e.Graphics.DrawLine(pen, cx - 3.5f, cy, cx - 1f, cy + 2.5f);
                    e.Graphics.DrawLine(pen, cx - 1f, cy + 2.5f, cx + 3.5f, cy - 2.5f);
                }
            }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (Pen pen = new Pen(SeparatorColor, 1f))
            {
                e.Graphics.DrawLine(pen, 12, y, e.Item.Width - 12, y);
            }
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            // Do not paint retro gradient strip
        }

        private static GraphicsPath GetRoundedRectangle(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
