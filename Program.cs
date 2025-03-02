using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using ICSharpCode.SharpZipLib.Zip; // Add reference to a .NET 2.0–compatible SharpZipLib DLL

namespace PkgMgr
{
    // Program entry point and command parser.
    class Program
    {
        public static SettingsManager Settings;

        static void Main(string[] args)
        {
            // Load settings from settings.txt
            Settings = new SettingsManager();

            // (Optional) Debug: uncomment the following line to verify the URL is read correctly.
            // Console.WriteLine("PackageDatabaseUrl: " + Settings.GetSetting("packageDatabaseUrl", "Not Found"));

            if (args.Length == 0)
            {
                ShowUsage();
                return;
            }

            string command = args[0].ToLower();
            try
            {
                switch (command)
                {
                    case "install":
                        ProcessInstall(args);
                        break;
                    case "list":
                        ProcessList(args);
                        break;
                    case "search":
                        ProcessSearch(args);
                        break;
                    case "remove":
                        ProcessRemove(args);
                        break;
                    default:
                        ShowUsage();
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Error in Main command processing: " + ex.Message);
                Console.WriteLine("An unexpected error occurred. See log for details.");
            }
        }

        static void ProcessInstall(string[] args)
        {
            bool isDriver = false;
            string target = "";

            if (args.Length >= 2 && args[1].ToLower() == "-d")
            {
                isDriver = true;
                if (args.Length < 3)
                {
                    Console.WriteLine("Error: Please specify a driver to install.");
                    return;
                }
                target = args[2];
            }
            else if (args.Length >= 2)
            {
                target = args[1];
            }
            else
            {
                Console.WriteLine("Error: Please specify a package to install.");
                return;
            }

            if (isDriver)
            {
                DriverManager dm = new DriverManager();
                Dictionary<string, UpdateInfo> db = dm.LoadRemoteDatabase(Settings.GetSetting("driverDatabaseUrl", ""));
                if (db == null || !db.ContainsKey(target))
                {
                    Console.WriteLine("Driver '{0}' not found in remote database.", target);
                    return;
                }
                UpdateInfo info = db[target];
                if (!info.IsCompatible())
                {
                    Console.WriteLine("Driver '{0}' requires {1} and Windows {2} or later. Your system: {3}, OS {4}.",
                        info.Name, info.Architecture, info.MinOSVersion, CurrentArchitecture(), Environment.OSVersion.Version.ToString());
                    return;
                }
                Console.WriteLine("Installing driver '{0}' (version {1}).", target, info.Version);
                string basePath = Settings.GetSetting("defaultInstallPath", "C:\\Program Files\\MyPkgMgr");
                // Nested Path.Combine calls for .NET 2.0:
                string destZip = Path.Combine(Path.Combine(basePath, "drivers"), target + ".zip");
                if (DownloadManager.DownloadFile(info.DownloadLink, destZip))
                    dm.InstallDriver(target, info.Version);
                else
                    Console.WriteLine("Driver download failed.");
            }
            else
            {
                PackageManager pm = new PackageManager();
                Dictionary<string, UpdateInfo> db = pm.LoadRemoteDatabase(Settings.GetSetting("packageDatabaseUrl", ""));
                if (db == null || !db.ContainsKey(target))
                {
                    Console.WriteLine("Package '{0}' not found in remote database.", target);
                    return;
                }
                UpdateInfo info = db[target];
                if (!info.IsCompatible())
                {
                    Console.WriteLine("Package '{0}' requires {1} and Windows {2} or later. Your system: {3}, OS {4}.",
                        info.Name, info.Architecture, info.MinOSVersion, CurrentArchitecture(), Environment.OSVersion.Version.ToString());
                    return;
                }
                Console.WriteLine("Installing package '{0}' (version {1}).", target, info.Version);
                string basePath = Settings.GetSetting("defaultInstallPath", "C:\\Program Files\\MyPkgMgr");
                string destZip = Path.Combine(Path.Combine(basePath, "packages"), target + ".zip");
                if (DownloadManager.DownloadFile(info.DownloadLink, destZip))
                    pm.Install(target, info.Version);
                else
                    Console.WriteLine("Package download failed.");
            }
        }

        static void ProcessList(string[] args)
        {
            bool listDrivers = false;
            if (args.Length >= 2 && args[1].ToLower() == "-d")
                listDrivers = true;

            if (listDrivers)
            {
                DriverManager dm = new DriverManager();
                dm.ListInstalled();
            }
            else
            {
                PackageManager pm = new PackageManager();
                pm.ListInstalled();
            }
        }

        // "search" command: searches the remote database for items matching the search term.
        static void ProcessSearch(string[] args)
        {
            bool searchDrivers = false;
            string searchTerm = "";
            if (args.Length >= 2 && args[1].ToLower() == "-d")
            {
                searchDrivers = true;
                if (args.Length < 3)
                {
                    Console.WriteLine("Error: Please specify a search term for drivers.");
                    return;
                }
                searchTerm = args[2];
            }
            else if (args.Length >= 2)
            {
                searchTerm = args[1];
            }
            else
            {
                Console.WriteLine("Error: Please specify a search term.");
                return;
            }

            if (searchDrivers)
            {
                DriverManager dm = new DriverManager();
                Dictionary<string, UpdateInfo> db = dm.LoadRemoteDatabase(Settings.GetSetting("driverDatabaseUrl", ""));
                Console.WriteLine("Search results in Drivers for term '{0}':", searchTerm);
                bool found = false;
                if (db != null)
                {
                    foreach (UpdateInfo info in db.Values)
                    {
                        if (info.Name.ToLower().IndexOf(searchTerm.ToLower()) >= 0)
                        {
                            string compat = info.IsCompatible() ? "Compatible" : "Not compatible";
                            Console.WriteLine("  {0} (version {1}) - Download: {2} [{3}]", info.Name, info.Version, info.DownloadLink, compat);
                            found = true;
                        }
                    }
                }
                if (!found)
                    Console.WriteLine("  No drivers found matching '{0}'.", searchTerm);
            }
            else
            {
                PackageManager pm = new PackageManager();
                Dictionary<string, UpdateInfo> db = pm.LoadRemoteDatabase(Settings.GetSetting("packageDatabaseUrl", ""));
                Console.WriteLine("Search results in Packages for term '{0}':", searchTerm);
                bool found = false;
                if (db != null)
                {
                    foreach (UpdateInfo info in db.Values)
                    {
                        if (info.Name.ToLower().IndexOf(searchTerm.ToLower()) >= 0)
                        {
                            string compat = info.IsCompatible() ? "Compatible" : "Not compatible";
                            Console.WriteLine("  {0} (version {1}) - Download: {2} [{3}]", info.Name, info.Version, info.DownloadLink, compat);
                            found = true;
                        }
                    }
                }
                if (!found)
                    Console.WriteLine("  No packages found matching '{0}'.", searchTerm);
            }
        }

        static void ProcessRemove(string[] args)
        {
            bool isDriver = false;
            string target = "";
            if (args.Length >= 2 && args[1].ToLower() == "-d")
            {
                isDriver = true;
                if (args.Length < 3)
                {
                    Console.WriteLine("Error: Please specify a driver to remove.");
                    return;
                }
                target = args[2];
            }
            else if (args.Length >= 2)
            {
                target = args[1];
            }
            else
            {
                Console.WriteLine("Error: Please specify an item to remove.");
                return;
            }

            if (isDriver)
            {
                DriverManager dm = new DriverManager();
                dm.RemoveDriver(target);
            }
            else
            {
                PackageManager pm = new PackageManager();
                pm.Remove(target);
            }
        }

        static void ShowUsage()
        {
            Console.WriteLine("Usage: pkgmgr <command> [-d] [name or search term]");
            Console.WriteLine("Commands:");
            Console.WriteLine("  install [-d] <name>       Download, extract, and install an item from the remote database");
            Console.WriteLine("  list [-d]                 List locally installed items");
            Console.WriteLine("  search [-d] <term>        Search the remote database for items matching the term");
            Console.WriteLine("  remove [-d] <name>        Uninstall a locally installed item");
            Console.WriteLine();
            Console.WriteLine("Settings in settings.txt must include:");
            Console.WriteLine("  packageDatabaseUrl = <URL to remote packages.txt>");
            Console.WriteLine("  driverDatabaseUrl = <URL to remote drivers.txt>");
            Console.WriteLine("  defaultInstallPath = <local install directory>");
        }

        // Helper method to determine current system architecture.
        static string CurrentArchitecture()
        {
            return (IntPtr.Size == 8) ? "x64" : "x86";
        }
    }

    // PackageManager class: handles local registry and loading remote package database.
    class PackageManager
    {
        private Dictionary<string, string> installedPackages;
        private string registryFile = "installedPackages.txt";

        public PackageManager()
        {
            installedPackages = new Dictionary<string, string>();
            LoadRegistry();
        }

        private void LoadRegistry()
        {
            try
            {
                if (File.Exists(registryFile))
                {
                    string[] lines = File.ReadAllLines(registryFile);
                    foreach (string line in lines)
                    {
                        if (!string.IsNullOrEmpty(line))
                        {
                            string[] parts = line.Split(':');
                            if (parts.Length == 2)
                                installedPackages[parts[0].Trim()] = parts[1].Trim();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Error loading installed packages: " + ex.Message);
            }
        }

        private void SaveRegistry()
        {
            try
            {
                List<string> lines = new List<string>();
                foreach (KeyValuePair<string, string> kvp in installedPackages)
                    lines.Add(kvp.Key + ":" + kvp.Value);
                File.WriteAllLines(registryFile, lines.ToArray());
            }
            catch (Exception ex)
            {
                Logger.Log("Error saving installed packages: " + ex.Message);
            }
        }

        // Install by extracting the downloaded ZIP.
        public void Install(string name, string version)
        {
            if (installedPackages.ContainsKey(name))
            {
                Console.WriteLine("Package '{0}' is already installed (version {1}).", name, installedPackages[name]);
                return;
            }
            string basePath = Program.Settings.GetSetting("defaultInstallPath", "C:\\Program Files\\MyPkgMgr");
            string zipPath = Path.Combine(Path.Combine(basePath, "packages"), name + ".zip");
            string installFolder = Path.Combine(Path.Combine(basePath, "packages"), name);
            PackageInstaller.ExtractZip(zipPath, installFolder);
            installedPackages[name] = version;
            SaveRegistry();
            Console.WriteLine("Package '{0}' installed (version {1}).", name, version);
            Logger.Log("Installed package: " + name);
        }

        public void Remove(string name)
        {
            if (installedPackages.ContainsKey(name))
            {
                string basePath = Program.Settings.GetSetting("defaultInstallPath", "C:\\Program Files\\MyPkgMgr");
                string installFolder = Path.Combine(Path.Combine(basePath, "packages"), name);
                PackageInstaller.Uninstall(installFolder);
                installedPackages.Remove(name);
                SaveRegistry();
                Console.WriteLine("Package '{0}' removed.", name);
                Logger.Log("Removed package: " + name);
            }
            else
                Console.WriteLine("Package '{0}' is not installed.", name);
        }

        public void ListInstalled()
        {
            Console.WriteLine("Installed Packages:");
            if (installedPackages.Count == 0)
                Console.WriteLine("  (none)");
            else
            {
                foreach (KeyValuePair<string, string> kvp in installedPackages)
                    Console.WriteLine("  {0} (version {1})", kvp.Key, kvp.Value);
            }
        }

        // Loads the remote package database from the given URL.
        // Expected file format per line: Architecture WindowsVersion PackageName PackageVersion PackageURL
        public Dictionary<string, UpdateInfo> LoadRemoteDatabase(string url)
        {
            Dictionary<string, UpdateInfo> db = new Dictionary<string, UpdateInfo>();
            try
            {
                string data = DownloadManager.DownloadText(url);
                if (data == null)
                    return null;
                string[] lines = data.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0) continue;
                    string[] parts = trimmed.Split(' ');
                    if (parts.Length >= 5)
                    {
                        string arch = parts[0].Trim();
                        string minOS = parts[1].Trim();
                        string name = parts[2].Trim();
                        string version = parts[3].Trim();
                        StringBuilder sb = new StringBuilder();
                        for (int j = 4; j < parts.Length; j++)
                        {
                            if (j > 4) sb.Append(" ");
                            sb.Append(parts[j].Trim());
                        }
                        string link = sb.ToString();
                        UpdateInfo info = new UpdateInfo(version, link);
                        info.Name = name;
                        info.MinOSVersion = minOS;
                        info.Architecture = arch;
                        db[name] = info;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Error loading remote package database: " + ex.Message);
            }
            return db;
        }
    }

    // DriverManager class: similar to PackageManager, for drivers.
    class DriverManager
    {
        private Dictionary<string, string> installedDrivers;
        private string registryFile = "installedDrivers.txt";

        public DriverManager()
        {
            installedDrivers = new Dictionary<string, string>();
            LoadRegistry();
        }

        private void LoadRegistry()
        {
            try
            {
                if (File.Exists(registryFile))
                {
                    string[] lines = File.ReadAllLines(registryFile);
                    foreach (string line in lines)
                    {
                        if (!string.IsNullOrEmpty(line))
                        {
                            string[] parts = line.Split(':');
                            if (parts.Length == 2)
                                installedDrivers[parts[0].Trim()] = parts[1].Trim();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Error loading installed drivers: " + ex.Message);
            }
        }

        private void SaveRegistry()
        {
            try
            {
                List<string> lines = new List<string>();
                foreach (KeyValuePair<string, string> kvp in installedDrivers)
                    lines.Add(kvp.Key + ":" + kvp.Value);
                File.WriteAllLines(registryFile, lines.ToArray());
            }
            catch (Exception ex)
            {
                Logger.Log("Error saving installed drivers: " + ex.Message);
            }
        }

        public void InstallDriver(string name, string version)
        {
            if (installedDrivers.ContainsKey(name))
            {
                Console.WriteLine("Driver '{0}' is already installed (version {1}).", name, installedDrivers[name]);
                return;
            }
            string basePath = Program.Settings.GetSetting("defaultInstallPath", "C:\\Program Files\\MyPkgMgr");
            string zipPath = Path.Combine(Path.Combine(basePath, "drivers"), name + ".zip");
            string installFolder = Path.Combine(Path.Combine(basePath, "drivers"), name);
            PackageInstaller.ExtractZip(zipPath, installFolder);
            installedDrivers[name] = version;
            SaveRegistry();
            Console.WriteLine("Driver '{0}' installed (version {1}).", name, version);
            Logger.Log("Installed driver: " + name);
        }

        public void RemoveDriver(string name)
        {
            if (installedDrivers.ContainsKey(name))
            {
                string basePath = Program.Settings.GetSetting("defaultInstallPath", "C:\\Program Files\\MyPkgMgr");
                string installFolder = Path.Combine(Path.Combine(basePath, "drivers"), name);
                PackageInstaller.Uninstall(installFolder);
                installedDrivers.Remove(name);
                SaveRegistry();
                Console.WriteLine("Driver '{0}' removed.", name);
                Logger.Log("Removed driver: " + name);
            }
            else
                Console.WriteLine("Driver '{0}' is not installed.", name);
        }

        public void ListInstalled()
        {
            Console.WriteLine("Installed Drivers:");
            if (installedDrivers.Count == 0)
                Console.WriteLine("  (none)");
            else
            {
                foreach (KeyValuePair<string, string> kvp in installedDrivers)
                    Console.WriteLine("  {0} (version {1})", kvp.Key, kvp.Value);
            }
        }

        // Loads the remote driver database from the given URL.
        // Expected file format per line: Architecture WindowsVersion DriverName DriverVersion PackageURL
        public Dictionary<string, UpdateInfo> LoadRemoteDatabase(string url)
        {
            Dictionary<string, UpdateInfo> db = new Dictionary<string, UpdateInfo>();
            try
            {
                string data = DownloadManager.DownloadText(url);
                if (data == null)
                    return null;
                string[] lines = data.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0) continue;
                    string[] parts = trimmed.Split(' ');
                    if (parts.Length >= 5)
                    {
                        string arch = parts[0].Trim();
                        string minOS = parts[1].Trim();
                        string name = parts[2].Trim();
                        string version = parts[3].Trim();
                        StringBuilder sb = new StringBuilder();
                        for (int j = 4; j < parts.Length; j++)
                        {
                            if (j > 4) sb.Append(" ");
                            sb.Append(parts[j].Trim());
                        }
                        string link = sb.ToString();
                        UpdateInfo info = new UpdateInfo(version, link);
                        info.Name = name;
                        info.MinOSVersion = minOS;
                        info.Architecture = arch;
                        db[name] = info;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Error loading remote driver database: " + ex.Message);
            }
            return db;
        }
    }

    // UpdateInfo holds version, download link, minimum OS version, and architecture.
    class UpdateInfo
    {
        public string Name;
        public string Version;
        public string DownloadLink;
        public string MinOSVersion; // e.g., "5.1" means Windows XP or later
        public string Architecture; // e.g., "x86" or "x64"

        public UpdateInfo(string version, string downloadLink)
        {
            Version = version;
            DownloadLink = downloadLink;
            MinOSVersion = "0.0"; // default: compatible with all OS versions
            Architecture = "x86"; // default to x86
        }

        // Checks if the current system's architecture and OS version meet the requirements.
        public bool IsCompatible()
        {
            try
            {
                string currentArch = (IntPtr.Size == 8) ? "x64" : "x86";
                if (!Architecture.Equals(currentArch, StringComparison.InvariantCultureIgnoreCase))
                    return false;
                Version current = Environment.OSVersion.Version;
                Version required = new Version(MinOSVersion);
                return current.CompareTo(required) >= 0;
            }
            catch
            {
                return true;
            }
        }
    }

    // SettingsManager reads settings from settings.txt.
    class SettingsManager
    {
        private Dictionary<string, string> settings;
        private string settingsFile = "settings.txt";

        public SettingsManager()
        {
            settings = new Dictionary<string, string>();
            LoadSettings();
        }

        public void LoadSettings()
        {
            try
            {
                if (File.Exists(settingsFile))
                {
                    string[] lines = File.ReadAllLines(settingsFile);
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrEmpty(line)) continue;
                        string[] parts = line.Split('=');
                        if (parts.Length == 2)
                            settings[parts[0].Trim()] = parts[1].Trim();
                    }
                }
                else
                {
                    settings["packageDatabaseUrl"] = "https://raw.githubusercontent.com/YourUsername/YourRepo/main/repo/packages.txt";
                    settings["driverDatabaseUrl"] = "https://raw.githubusercontent.com/YourUsername/YourRepo/main/repo/drivers.txt";
                    settings["defaultInstallPath"] = "C:\\Program Files\\MyPkgMgr";
                    settings["logFile"] = "pkgmgr.log";
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Error loading settings: " + ex.Message);
            }
        }

        public void SaveSettings()
        {
            try
            {
                List<string> lines = new List<string>();
                foreach (KeyValuePair<string, string> kvp in settings)
                    lines.Add(kvp.Key + "=" + kvp.Value);
                File.WriteAllLines(settingsFile, lines.ToArray());
            }
            catch (Exception ex)
            {
                Logger.Log("Error saving settings: " + ex.Message);
            }
        }

        public string GetSetting(string key, string defaultValue)
        {
            if (settings.ContainsKey(key))
                return settings[key];
            else
                return defaultValue;
        }

        public void PrintSettings()
        {
            if (settings.Count == 0)
                Console.WriteLine("  (none)");
            else
            {
                foreach (KeyValuePair<string, string> kvp in settings)
                    Console.WriteLine("  {0} = {1}", kvp.Key, kvp.Value);
            }
        }
    }

    // Logger writes messages to a log file.
    class Logger
    {
        public static void Log(string message)
        {
            try
            {
                string logFile = Program.Settings.GetSetting("logFile", "pkgmgr.log");
                string logEntry = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - " + message;
                File.AppendAllText(logFile, logEntry + Environment.NewLine);
            }
            catch { }
        }
    }

    // DownloadManager handles file and text downloads.
    class DownloadManager
    {
        public static bool DownloadFile(string url, string destinationPath)
        {
            Console.WriteLine("Downloading from: " + url);
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Timeout = 15000;
                // Add User-Agent for GitHub compatibility.
                request.UserAgent = "Mozilla/5.0 (compatible; PackageManager/1.0)";
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                string destDir = Path.GetDirectoryName(destinationPath);
                if (!Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);
                using (Stream responseStream = response.GetResponseStream())
                using (FileStream fileStream = File.Create(destinationPath))
                {
                    byte[] buffer = new byte[4096];
                    int bytesRead = 0;
                    while ((bytesRead = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        fileStream.Write(buffer, 0, bytesRead);
                    }
                }
                Console.WriteLine("Download completed: " + destinationPath);
                Logger.Log("Downloaded file from " + url + " to " + destinationPath);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log("Download failed from " + url + ": " + ex.Message);
                Console.WriteLine("Download error: " + ex.Message);
                return false;
            }
        }

        public static string DownloadText(string url)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Timeout = 15000;
                // Add User-Agent header
                request.UserAgent = "Mozilla/5.0 (compatible; PackageManager/1.0)";
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                Logger.Log("DownloadText failed from " + url + ": " + ex.Message);
                Console.WriteLine("Error downloading text from {0}: {1}", url, ex.Message);
                return null;
            }
        }
    }

    // PackageInstaller handles ZIP extraction and uninstallation using SharpZipLib.
    class PackageInstaller
    {
        public static void ExtractZip(string zipPath, string outFolder)
        {
            try
            {
                using (FileStream fs = File.OpenRead(zipPath))
                using (ZipFile zf = new ZipFile(fs))
                {
                    foreach (ZipEntry zipEntry in zf)
                    {
                        if (!zipEntry.IsFile)
                            continue;
                        string entryFileName = zipEntry.Name;
                        string fullPath = Path.Combine(outFolder, entryFileName);
                        string directoryName = Path.GetDirectoryName(fullPath);
                        if (directoryName.Length > 0 && !Directory.Exists(directoryName))
                            Directory.CreateDirectory(directoryName);
                        using (FileStream streamWriter = File.Create(fullPath))
                        {
                            Stream zipStream = zf.GetInputStream(zipEntry);
                            byte[] buffer = new byte[4096];
                            int size = 0;
                            while ((size = zipStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                streamWriter.Write(buffer, 0, size);
                            }
                        }
                    }
                }
                Console.WriteLine("Extraction completed to: " + outFolder);
                Logger.Log("Extracted ZIP " + zipPath + " to " + outFolder);
            }
            catch (Exception ex)
            {
                Logger.Log("Error extracting ZIP " + zipPath + ": " + ex.Message);
                Console.WriteLine("Extraction error: " + ex.Message);
            }
        }

        public static void Uninstall(string folderPath)
        {
            try
            {
                if (Directory.Exists(folderPath))
                {
                    Directory.Delete(folderPath, true);
                    Console.WriteLine("Uninstallation: Deleted folder " + folderPath);
                    Logger.Log("Uninstalled package from " + folderPath);
                }
                else
                    Console.WriteLine("Folder " + folderPath + " does not exist.");
            }
            catch (Exception ex)
            {
                Logger.Log("Error uninstalling folder " + folderPath + ": " + ex.Message);
                Console.WriteLine("Uninstallation error: " + ex.Message);
            }
        }
    }

    // Utility class provides helper methods.
    static class Utility
    {
        public static string JoinStrings(string separator, string[] values)
        {
            if (values == null || values.Length == 0)
                return "";
            StringBuilder sb = new StringBuilder();
            sb.Append(values[0]);
            for (int i = 1; i < values.Length; i++)
            {
                sb.Append(separator);
                sb.Append(values[i]);
            }
            return sb.ToString();
        }
    }
}
