using System;
using System.IO;

namespace NetworkSwitcher
{
    public class Config
    {
        private static string ConfigPath
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NetworkSwitcher");
                if (!Directory.Exists(dir))
                {
                    try { Directory.CreateDirectory(dir); } catch { }
                }
                return Path.Combine(dir, "config.ini");
            }
        }

        public bool StartWithWindows { get; set; }
        public bool LeftClickTogglesEthernet { get; set; }

        public Config()
        {
            StartWithWindows = false;
            LeftClickTogglesEthernet = false;
        }

        public static Config Load()
        {
            Config cfg = new Config();
            try
            {
                string path = ConfigPath;
                if (File.Exists(path))
                {
                    string[] lines = File.ReadAllLines(path);
                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim();
                        if (trimmed.StartsWith("#") || !trimmed.Contains("=")) continue;
                        string[] parts = trimmed.Split(new char[] { '=' }, 2);
                        string key = parts[0].Trim();
                        string val = parts[1].Trim();

                        if (string.Equals(key, "LeftClickTogglesEthernet", StringComparison.OrdinalIgnoreCase))
                        {
                            bool b;
                            if (bool.TryParse(val, out b)) cfg.LeftClickTogglesEthernet = b;
                        }
                    }
                }
                // Check task scheduler to accurately sync StartWithWindows
                cfg.StartWithWindows = StartupManager.IsTaskRegistered();
            }
            catch { }
            return cfg;
        }

        public void Save()
        {
            try
            {
                string path = ConfigPath;
                using (StreamWriter sw = new StreamWriter(path, false))
                {
                    sw.WriteLine("# Network Switcher Settings");
                    sw.WriteLine("LeftClickTogglesEthernet=" + LeftClickTogglesEthernet.ToString());
                }
            }
            catch { }
        }
    }
}
