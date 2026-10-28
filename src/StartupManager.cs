using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace NetworkSwitcher
{
    public static class StartupManager
    {
        private const string TaskName = "NetworkSwitcher";

        public static bool IsTaskRegistered()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("schtasks.exe", "/Query /TN \"" + TaskName + "\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (Process proc = Process.Start(psi))
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

        public static bool SetStartWithWindows(bool enable)
        {
            try
            {
                string exePath = Application.ExecutablePath;

                if (enable)
                {
                    string args = string.Format("/Create /TN \"{0}\" /TR \"\\\"{1}\\\"\" /SC ONLOGON /RL HIGHEST /F", TaskName, exePath);
                    ProcessStartInfo psi = new ProcessStartInfo("schtasks.exe", args)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using (Process proc = Process.Start(psi))
                    {
                        proc.WaitForExit();
                        return proc.ExitCode == 0;
                    }
                }
                else
                {
                    ProcessStartInfo psi = new ProcessStartInfo("schtasks.exe", "/Delete /TN \"" + TaskName + "\" /F")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using (Process proc = Process.Start(psi))
                    {
                        proc.WaitForExit();
                        return proc.ExitCode == 0;
                    }
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
