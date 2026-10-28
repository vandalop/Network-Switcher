using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace NetworkSwitcher
{
    static class Program
    {
        private const string MutexName = "Global\\NetworkSwitcher_SingleInstance_Mutex_2026";
        private static Mutex _mutex;

        [STAThread]
        static void Main(string[] args)
        {
            // Clean up any temporary update files
            try
            {
                string oldPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NetworkSwitcher.old");
                if (File.Exists(oldPath)) File.Delete(oldPath);
            }
            catch { }

            // Global Exception Handling
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                LogCrash("AppDomain: " + e.ExceptionObject.ToString());
            };
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                LogCrash("ThreadException: " + e.Exception.ToString());
            };

            // 1. Administrator Elevation Check FIRST (before locking mutex)
            if (!IsAdministrator())
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath)
                    {
                        Verb = "runas",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Network Switcher requires Administrator privileges to toggle network adapters.\n" + ex.Message,
                        "Administrator Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            // 2. Single Instance Check (After Elevation)
            bool createdNew;
            try
            {
                _mutex = new Mutex(true, MutexName, out createdNew);
            }
            catch
            {
                createdNew = false;
            }

            if (!createdNew)
            {
                MessageBox.Show("Network Switcher is already running in your System Tray.\nCheck your hidden icons (^) or taskbar.",
                    "Network Switcher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApplicationContext());
            }
            catch (Exception ex)
            {
                LogCrash("Application.Run: " + ex.ToString());
                MessageBox.Show("Error starting Network Switcher:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (_mutex != null)
                {
                    try { _mutex.ReleaseMutex(); } catch { }
                    _mutex.Close();
                }
            }
        }

        private static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private static void LogCrash(string message)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NetworkSwitcher");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "crash.log");
                File.AppendAllText(path, string.Format("[{0}] {1}\r\n", DateTime.Now, message));
            }
            catch { }
        }
    }
}
