using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace vrchat_screenshots_with_world_id
{
    class Program
    {
        private const string AutostartRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AutostartRegistryValue = "VRChatScreenshotRenamer";

        private static string logFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low", "VRChat", "VRChat");
        private static NotifyIcon trayIcon;
        private static FileSystemWatcher watcher;
        private static CancellationTokenSource cancellationTokenSource;
        private static string lastWorld = "";
        private static List<string> recentScreenshots = new List<string>();

        private static ContextMenuStrip contextMenu;
        private static ToolStripMenuItem autostartMenuItem;
        private static ToolStripMenuItem currentWorldMenuItem;
        private static ToolStripMenuItem recentScreenshotsMenuItem;

        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            InitializeTrayIcon();
            InitializeContextMenu();
            InitializeFileSystemWatcher();

            Application.Run();
        }

        private static void InitializeTrayIcon()
        {
            trayIcon = new NotifyIcon();
            trayIcon.Icon = System.Drawing.SystemIcons.Application;
            trayIcon.Text = "VRChat Screenshot Renamer";
            trayIcon.Visible = true;
        }

        private static void InitializeContextMenu()
        {
            contextMenu = new ContextMenuStrip();

            autostartMenuItem = new ToolStripMenuItem("Autostart on Login");
            autostartMenuItem.Checked = IsAutostartEnabled();
            autostartMenuItem.Click += AutostartMenuItemClick;

            currentWorldMenuItem = new ToolStripMenuItem("Current World: None");
            currentWorldMenuItem.Click += CurrentWorldMenuItemClick;

            recentScreenshotsMenuItem = new ToolStripMenuItem("Recent Screenshots");

            ToolStripMenuItem exitMenuItem = new ToolStripMenuItem("Exit");
            exitMenuItem.Click += ExitMenuItemClick;

            contextMenu.Items.AddRange(new ToolStripItem[]
            {
                autostartMenuItem,
                currentWorldMenuItem,
                recentScreenshotsMenuItem,
                new ToolStripSeparator(),
                exitMenuItem
            });

            trayIcon.ContextMenuStrip = contextMenu;
        }

        private static void InitializeFileSystemWatcher()
        {
            watcher = new FileSystemWatcher(logFolder, "output_log_*.txt");
            watcher.Created += OnNewLogFileCreated;
            watcher.EnableRaisingEvents = true;
        }

        /// <summary>
        /// Event handler for when a new VRChat log file is created.
        /// </summary>
        private static void OnNewLogFileCreated(object sender, FileSystemEventArgs e)
        {
            // cancel previous watcher if it's still running.
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
            }

            string logFile = e.FullPath;
            cancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            Task.Run(() => ProcessLogFile(logFile, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Tails the specified log file and yields each new line asynchronously.
        /// </summary>
        static async IAsyncEnumerable<string> TailAsync(string file, CancellationToken cancellationToken)
        {
            using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fileStream))
            {
                reader.BaseStream.Seek(0, SeekOrigin.End);
                while (!cancellationToken.IsCancellationRequested)
                {
                    string line = await reader.ReadLineAsync();
                    if (line != null) yield return line;
                    else await Task.Delay(1000, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Processes the specified VRChat log file.
        /// </summary>
        private static async Task ProcessLogFile(string logFile, CancellationToken cancellationToken)
        {
            await foreach (string line in TailAsync(logFile, cancellationToken))
            {
                if (line.Contains("Joining wrld_"))
                {
                    lastWorld = Regex.Match(line, @"Joining wrld_(\w+-\w+-\w+-\w+-\w+)").Groups[1].Value;
                    Console.WriteLine($"joined world {lastWorld}");
                    UpdateCurrentWorldMenuItem(lastWorld);
                }
                else if (line.Contains("Took screenshot to: "))
                {
                    string screenshotPath = Regex.Match(line, @"Took screenshot to: (.+)").Groups[1].Value;
                    Console.WriteLine($"screenshot taken to {screenshotPath}");
                    string newName = RenameScreenshotFile(screenshotPath, lastWorld);
                    if (newName != null)
                    {
                        recentScreenshots.Add(newName);
                        if (recentScreenshots.Count > 5)
                        {
                            recentScreenshots.RemoveAt(0);
                        }
                        UpdateRecentScreenshotsMenu();
                    }
                }
            }
        }

        /// <summary>
        /// Renames the specified screenshot file with the world ID appended.
        /// </summary>
        private static string RenameScreenshotFile(string screenshotPath, string worldId)
        {
            string newName = Regex.Replace(screenshotPath, @"(.+)(\.\w+)$", $"$1_wrld_{worldId}$2");
            if (File.Exists(screenshotPath))
            {
                Console.WriteLine($"Moving {screenshotPath} to {newName}");
                File.Move(screenshotPath, newName);
                return newName;
            }
            return null;
        }

        /// <summary>
        /// Updates the "Current World" menu item with the specified world ID.
        /// </summary>
        private static void UpdateCurrentWorldMenuItem(string worldId)
        {
            InvokeIfRequired(() =>
            {
                currentWorldMenuItem.Text = $"Current World: {worldId}";
                currentWorldMenuItem.Tag = worldId;
            });
        }

        /// <summary>
        /// open vrchat world page with "Current World" menu item when clicked.
        /// </summary>
        private static void CurrentWorldMenuItemClick(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = (ToolStripMenuItem)sender;
            string worldId = (string)menuItem.Tag;
            if (worldId != null && worldId != "")
            {
                string url = $"https://vrchat.com/home/world/wrld_{worldId}";
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        }

        /// <summary>
        /// Updates the "Recent Screenshots" submenu with the recent screenshots.
        /// </summary>
        private static void UpdateRecentScreenshotsMenu()
        {
            InvokeIfRequired(() =>
            {
                recentScreenshotsMenuItem.DropDownItems.Clear();

                foreach (string screenshot in recentScreenshots)
                {
                    ToolStripMenuItem screenshotMenuItem = new ToolStripMenuItem(Path.GetFileName(screenshot));
                    screenshotMenuItem.Click += (sender, e) => OpenScreenshotFolder(screenshot);
                    recentScreenshotsMenuItem.DropDownItems.Add(screenshotMenuItem);
                }
            });
        }

        /// <summary>
        /// Opens the folder containing the specified screenshot file in Windows Explorer.
        /// </summary>
        private static void OpenScreenshotFolder(string screenshotPath)
        {
            string folderPath = Path.GetDirectoryName(screenshotPath);
            Process.Start("explorer.exe", folderPath);
        }

        /// <summary>
        /// Event handler for when the "Autostart on Login" menu item is clicked.
        /// </summary>
        private static void AutostartMenuItemClick(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = (ToolStripMenuItem)sender;
            menuItem.Checked = !menuItem.Checked;
            SetAutostart(menuItem.Checked);
        }

        private static void ExitMenuItemClick(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            Application.Exit();
        }

        /// <summary>
        /// Checks if the application is set to run on startup.
        /// </summary>
        private static bool IsAutostartEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(AutostartRegistryKey, true))
            {
                return key.GetValue(AutostartRegistryValue) != null;
            }
        }

        /// <summary>
        /// Sets whether the application should run on startup.
        /// </summary>
        private static void SetAutostart(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(AutostartRegistryKey, true))
            {
                if (enabled)
                {
                    key.SetValue(AutostartRegistryValue, Application.ExecutablePath);
                }
                else
                {
                    key.DeleteValue(AutostartRegistryValue, false);
                }
            }
        }

        /// <summary>
        /// Invokes the specified action on the UI thread if required.
        /// </summary>
        private static void InvokeIfRequired(Action action)
        {
            if (trayIcon.ContextMenuStrip.InvokeRequired)
            {
                trayIcon.ContextMenuStrip.BeginInvoke(action);
            }
            else
            {
                action.Invoke();
            }
        }
    }
}