using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Win32;

namespace NetworkSwitcher
{
    public enum AdapterCategory
    {
        Ethernet,
        WiFi,
        UsbTethering,
        Bluetooth,
        VpnOrVirtual,
        Other
    }

    public class NetworkAdapterInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public AdapterCategory Category { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsConnected { get; set; }
        public string PnpInstanceId { get; set; }
        public string IpAddress { get; set; }
        public string SpeedDisplay { get; set; }

        public override string ToString()
        {
            return string.Format("[{0}] '{1}' ({2}) - {3}, {4}", 
                Category, Name, Description, IsEnabled ? "Enabled" : "Disabled", IsConnected ? "Connected" : "Disconnected");
        }
    }

    public static class NetworkManager
    {
        private const string NetRegPath = @"SYSTEM\CurrentControlSet\Control\Network\{4D36E972-E325-11CE-BFC1-08002BE10318}";

        public static List<NetworkAdapterInfo> GetAdapters()
        {
            var list = new List<NetworkAdapterInfo>();
            var statusMap = new Dictionary<string, Tuple<bool, bool>>(StringComparer.OrdinalIgnoreCase);

            // Step 1: Query netsh interface show interface for real-time Admin State & Operational State
            try
            {
                var psi = new ProcessStartInfo("netsh.exe", "interface show interface")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(psi))
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();

                    string[] lines = output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    int namePos = -1;
                    foreach (var line in lines)
                    {
                        if (line.Contains("Interface Name"))
                        {
                            namePos = line.IndexOf("Interface Name");
                            continue;
                        }
                        if (namePos > 0 && line.Length >= namePos && !line.StartsWith("---"))
                        {
                            string adminStr = line.Substring(0, Math.Min(14, line.Length)).Trim();
                            string stateStr = line.Length > 15 ? line.Substring(15, Math.Min(14, line.Length - 15)).Trim() : "";
                            string ifName = line.Substring(namePos).Trim();

                            bool isEnabled = adminStr.Equals("Enabled", StringComparison.OrdinalIgnoreCase);
                            bool isConnected = stateStr.Equals("Connected", StringComparison.OrdinalIgnoreCase);
                            statusMap[ifName] = Tuple.Create(isEnabled, isConnected);
                        }
                    }
                }
            }
            catch { }

            // Step 2: Query IP and speed from System.Net.NetworkInformation
            var ipMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var speedMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up)
                    {
                        IPInterfaceProperties prop = ni.GetIPProperties();
                        foreach (UnicastIPAddressInformation uip in prop.UnicastAddresses)
                        {
                            if (uip.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                ipMap[ni.Name] = uip.Address.ToString();
                                break;
                            }
                        }
                        if (ni.Speed > 0)
                        {
                            if (ni.Speed >= 1000000000)
                                speedMap[ni.Name] = string.Format("{0:0.#} Gbps", (double)ni.Speed / 1000000000.0);
                            else if (ni.Speed >= 1000000)
                                speedMap[ni.Name] = string.Format("{0:0} Mbps", (double)ni.Speed / 1000000.0);
                        }
                    }
                }
            }
            catch { }

            // Step 3: Fast Registry enumeration (0 MB RAM overhead)
            try
            {
                using (RegistryKey baseKey = Registry.LocalMachine.OpenSubKey(NetRegPath))
                {
                    if (baseKey != null)
                    {
                        foreach (string subKeyName in baseKey.GetSubKeyNames())
                        {
                            if (!subKeyName.StartsWith("{")) continue;
                            using (RegistryKey connKey = baseKey.OpenSubKey(subKeyName + @"\Connection"))
                            {
                                if (connKey == null) continue;

                                string connName = connKey.GetValue("Name") as string;
                                if (string.IsNullOrEmpty(connName)) continue;

                                Tuple<bool, bool> statusTuple;
                                bool hasNetshStatus = statusMap.TryGetValue(connName, out statusTuple);
                                if (!hasNetshStatus)
                                {
                                    continue;
                                }

                                string pnp = connKey.GetValue("PnpInstanceID") as string ?? "";
                                int mediaSubType = 0;
                                object subObj = connKey.GetValue("MediaSubType");
                                if (subObj is int) mediaSubType = (int)subObj;

                                string desc = GetDeviceFriendlyDescription(pnp, connName);
                                AdapterCategory cat = Classify(connName, desc, pnp, mediaSubType);

                                bool enabled = statusTuple != null ? statusTuple.Item1 : true;
                                bool connected = statusTuple != null ? statusTuple.Item2 : false;

                                string ip = "";
                                ipMap.TryGetValue(connName, out ip);

                                string speed = "";
                                speedMap.TryGetValue(connName, out speed);

                                list.Add(new NetworkAdapterInfo
                                {
                                    Name = connName,
                                    Description = desc,
                                    Category = cat,
                                    IsEnabled = enabled,
                                    IsConnected = connected,
                                    PnpInstanceId = pnp,
                                    IpAddress = ip,
                                    SpeedDisplay = speed
                                });
                            }
                        }
                    }
                }
            }
            catch { }

            return list;
        }

        private static string GetDeviceFriendlyDescription(string pnp, string fallback)
        {
            if (string.IsNullOrEmpty(pnp)) return fallback;
            try
            {
                string enumKeyPath = @"SYSTEM\CurrentControlSet\Enum\" + pnp;
                using (RegistryKey enumKey = Registry.LocalMachine.OpenSubKey(enumKeyPath))
                {
                    if (enumKey != null)
                    {
                        string friendly = enumKey.GetValue("FriendlyName") as string;
                        if (!string.IsNullOrEmpty(friendly)) return friendly;

                        string deviceDesc = enumKey.GetValue("DeviceDesc") as string;
                        if (!string.IsNullOrEmpty(deviceDesc))
                        {
                            int idx = deviceDesc.LastIndexOf(';');
                            if (idx >= 0 && idx < deviceDesc.Length - 1)
                            {
                                return deviceDesc.Substring(idx + 1).Trim();
                            }
                            return deviceDesc;
                        }
                    }
                }
            }
            catch { }
            return fallback;
        }

        public static AdapterCategory Classify(string connId, string desc, string pnp, int mediaSubType)
        {
            string pnpUpper = (pnp ?? "").ToUpperInvariant();
            string descUpper = (desc ?? "").ToUpperInvariant();
            string connUpper = (connId ?? "").ToUpperInvariant();

            // 1. USB Tethering (RNDIS / Apple / Samsung / USB Gadget)
            if (pnpUpper.StartsWith("USB\\") || 
                descUpper.Contains("REMOTE NDIS") || 
                descUpper.Contains("RNDIS") || 
                descUpper.Contains("APPLE MOBILE") || 
                descUpper.Contains("SAMSUNG MOBILE") || 
                descUpper.Contains("TETHER") ||
                connUpper.Contains("TETHER"))
            {
                return AdapterCategory.UsbTethering;
            }

            // 2. Wi-Fi (MediaSubType 2 is Native 802.11)
            if (mediaSubType == 2 || 
                descUpper.Contains("WI-FI") || 
                descUpper.Contains("WIRELESS") || 
                descUpper.Contains("802.11") || 
                descUpper.Contains("WLAN") ||
                connUpper.Contains("WIFI") || 
                connUpper.Contains("WI-FI"))
            {
                return AdapterCategory.WiFi;
            }

            // 3. Bluetooth
            if (mediaSubType == 7 || descUpper.Contains("BLUETOOTH") || connUpper.Contains("BLUETOOTH") || pnpUpper.StartsWith("BTH\\"))
            {
                return AdapterCategory.Bluetooth;
            }

            // 4. Virtual / VPN
            if (descUpper.Contains("OPENVPN") || 
                descUpper.Contains("WIREGUARD") || 
                descUpper.Contains("TAP") || 
                descUpper.Contains("VIRTUAL") || 
                descUpper.Contains("VPN") ||
                descUpper.Contains("HYPER-V") ||
                descUpper.Contains("VMWARE") ||
                pnpUpper.StartsWith("ROOT\\") ||
                pnpUpper.StartsWith("SWD\\"))
            {
                return AdapterCategory.VpnOrVirtual;
            }

            // 5. Ethernet
            if (pnpUpper.StartsWith("PCI\\") ||
                descUpper.Contains("ETHERNET") || 
                descUpper.Contains("GBE") || 
                descUpper.Contains("GIGABIT") || 
                descUpper.Contains("REALTEK") || 
                descUpper.Contains("INTEL(R)") ||
                connUpper.Contains("ETHERNET"))
            {
                return AdapterCategory.Ethernet;
            }

            return AdapterCategory.Other;
        }

        public static bool SetAdapterEnabled(string adapterName, bool enable)
        {
            try
            {
                var psi = new ProcessStartInfo("netsh.exe", string.Format("interface set interface name=\"{0}\" admin={1}", adapterName, enable ? "enable" : "disable"))
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var proc = Process.Start(psi))
                {
                    proc.WaitForExit();
                    return proc.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void SwitchTo(AdapterCategory target)
        {
            var adapters = GetAdapters();
            foreach (var a in adapters)
            {
                if (a.Category == AdapterCategory.Ethernet || a.Category == AdapterCategory.WiFi || a.Category == AdapterCategory.UsbTethering)
                {
                    bool shouldEnable = (a.Category == target);
                    if (a.IsEnabled != shouldEnable)
                    {
                        SetAdapterEnabled(a.Name, shouldEnable);
                    }
                }
            }
        }

        public static void EnableAll()
        {
            var adapters = GetAdapters();
            foreach (var a in adapters)
            {
                if (a.Category == AdapterCategory.Ethernet || a.Category == AdapterCategory.WiFi || a.Category == AdapterCategory.UsbTethering)
                {
                    if (!a.IsEnabled)
                    {
                        SetAdapterEnabled(a.Name, true);
                    }
                }
            }
        }

        public static NetworkAdapterInfo GetPrimaryAdapter(AdapterCategory category, List<NetworkAdapterInfo> list = null)
        {
            var adapters = list ?? GetAdapters();
            foreach (var a in adapters)
            {
                if (a.Category == category) return a;
            }
            return null;
        }
    }
}
