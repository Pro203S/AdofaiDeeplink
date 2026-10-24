using System;
using System.Collections.Generic;
using System.IO.Compression;
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Text;
using System.Xml.Linq;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace AdofaiDeeplink.Installer
{
    public class Utils : IDisposable
    {
        public static string LocalAppdata = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public static string AdofaiDeeplinkExe = LocalAppdata + "\\Pro203S\\AdofaiDeeplink\\AdofaiDeeplink.exe";

        public static bool Installed()
        {
            return File.Exists(AdofaiDeeplinkExe);
        }

        public static void RunAdminTask(string args)
        {
            Logger.Info("Requesting administrator privileges for installation.");
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = Assembly.GetExecutingAssembly().Location,
                Verb = "runas",
                UseShellExecute = true,
                Arguments = args
            };

            try
            {
                Process.Start(startInfo);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                Logger.Info("Installation cancelled at the UAC prompt.");
            }
        }

        public static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public static async Task<byte[]> GetReleaseAsset(string name)
        {
            using (var wc = new WebClient())
            {
                wc.Headers.Set("User-Agent", "AdofaiDeeplink-Installer");
                string json = await wc.DownloadStringTaskAsync("https://api.github.com/repos/Pro203S/AdofaiDeeplink/releases");
                JArray releases = JArray.Parse(json);
                JArray assets = (JArray)(releases.FirstOrDefault()?["assets"]) ?? throw new Exception("GitHub에서 릴리즈 에셋을 찾을 수 없습니다.\n\nCould not find release assets on GitHub.");
                JObject asset = (JObject)assets.FirstOrDefault(v => (string)v["name"] == name) ?? throw new ArgumentException($"{name} 에셋을 찾을 수 없습니다.\n\nCould not find the {name} asset.");

                string url = (string)asset["browser_download_url"];

                wc.Headers.Set("User-Agent", "AdofaiDeeplink-Installer");
                return await wc.DownloadDataTaskAsync(url);
            }
        }

        private readonly List<Action> undo = new List<Action>();
        private readonly List<string> backups = new List<string>();
        internal readonly List<Tuple<string, string>> UpdatedFiles = new List<Tuple<string, string>>();
        internal readonly List<string> CreatedDirectories = new List<string>();
        private readonly List<string> createdRegistryKeys = new List<string>();
        private readonly List<XElement> registryValues = new List<XElement>();
        private bool committed;
        private bool deferred;

        internal void CreateDirectory(string path)
        {
            path = Path.GetFullPath(path);
            if (Directory.Exists(path)) return;
            string parent = Path.GetDirectoryName(path);
            if (parent != null) CreateDirectory(parent);
            Directory.CreateDirectory(path);
            CreatedDirectories.Add(path);
            undo.Add(() => { if (Directory.Exists(path)) Directory.Delete(path); });
        }

        internal void CopyFile(string source, string destination)
        {
            WriteFile(destination, File.ReadAllBytes(source));
        }

        internal void WriteFile(string destination, byte[] data)
        {
            destination = Path.GetFullPath(destination);
            CreateDirectory(Path.GetDirectoryName(destination));
            string staged = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(staged, data);
                if (File.Exists(destination))
                {
                    string backup = destination + "." + Guid.NewGuid().ToString("N") + ".bak";
                    File.Replace(staged, destination, backup);
                    backups.Add(backup);
                    UpdatedFiles.Add(Tuple.Create(destination, backup));
                    undo.Add(() =>
                    {
                        if (File.Exists(destination)) File.Replace(backup, destination, null);
                        else File.Move(backup, destination);
                    });
                }
                else
                {
                    File.Move(staged, destination);
                    UpdatedFiles.Add(Tuple.Create(destination, (string)null));
                    undo.Add(() => File.Delete(destination));
                }
            }
            finally
            {
                if (File.Exists(staged)) File.Delete(staged);
            }
        }

        internal void SetRegistryValue(RegistryKey root, string path, string name, object value, RegistryValueKind kind = RegistryValueKind.String)
        {
            string current = "";
            foreach (string part in path.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries))
            {
                current = current.Length == 0 ? part : current + "\\" + part;
                using (RegistryKey existing = root.OpenSubKey(current))
                {
                    if (existing != null) continue;
                }
                string createdPath = current;
                using (root.CreateSubKey(createdPath)) { }
                createdRegistryKeys.Add(createdPath);
                undo.Add(() => root.DeleteSubKeyTree(createdPath, false));
            }
            using (RegistryKey key = root.OpenSubKey(path, true))
            {
                bool existed = Array.Exists(key.GetValueNames(), item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase));
                object previous = existed ? key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) : null;
                RegistryValueKind previousKind = existed ? key.GetValueKind(name) : RegistryValueKind.String;
                registryValues.Add(new XElement("Value", new XAttribute("path", path), new XAttribute("name", name), new XAttribute("existed", existed), new XAttribute("previousKind", (int)previousKind), new XAttribute("installedKind", (int)kind), new XElement("Previous", existed ? EncodeRegistryData(previous, previousKind) : null), new XElement("Installed", EncodeRegistryData(value, kind))));
                undo.Add(() =>
                {
                    using (RegistryKey restored = root.OpenSubKey(path, true))
                    {
                        if (existed) restored.SetValue(name, previous, previousKind);
                        else restored.DeleteValue(name, false);
                    }
                });
                key.SetValue(name, value, kind);
            }
        }

        internal void Commit()
        {
            committed = true;
        }

        internal void CommitDeferred()
        {
            // The replacement process now owns these backups and must either commit or roll them back.
            deferred = true;
            committed = true;
        }

        internal void SaveRegistryBackup(string path)
        {
            var document = new XDocument(new XElement("RegistryBackup", new XElement("Values", registryValues), new XElement("CreatedKeys", createdRegistryKeys.Select(key => new XElement("Key", key)))));
            WriteFile(path, Encoding.UTF8.GetBytes(document.ToString()));
        }

        internal static XElement EncodeRegistryData(object value, RegistryValueKind kind)
        {
            if (kind == RegistryValueKind.Binary || kind == RegistryValueKind.None) return new XElement("Data", Convert.ToBase64String((byte[])value));
            if (kind == RegistryValueKind.MultiString) return new XElement("Data", ((string[])value).Select(item => new XElement("Item", Convert.ToBase64String(Encoding.UTF8.GetBytes(item)))));
            if (kind == RegistryValueKind.DWord || kind == RegistryValueKind.QWord) return new XElement("Data", Convert.ToString(value, CultureInfo.InvariantCulture));
            return new XElement("Data", Convert.ToBase64String(Encoding.UTF8.GetBytes((string)value)));
        }

        internal static object DecodeRegistryData(XElement data, RegistryValueKind kind)
        {
            if (kind == RegistryValueKind.Binary || kind == RegistryValueKind.None) return Convert.FromBase64String(data.Value);
            if (kind == RegistryValueKind.MultiString) return data.Elements("Item").Select(item => Encoding.UTF8.GetString(Convert.FromBase64String(item.Value))).ToArray();
            if (kind == RegistryValueKind.DWord) return int.Parse(data.Value, CultureInfo.InvariantCulture);
            if (kind == RegistryValueKind.QWord) return long.Parse(data.Value, CultureInfo.InvariantCulture);
            return Encoding.UTF8.GetString(Convert.FromBase64String(data.Value));
        }

        public void Dispose()
        {
            if (deferred) return;
            if (!committed)
            {
                var failures = new List<Exception>();
                for (int index = undo.Count - 1; index >= 0; index--)
                {
                    try { undo[index](); }
                    catch (Exception ex) { failures.Add(ex); }
                }
                if (failures.Count != 0)
                    throw new AggregateException("설치 실패 후 일부 항목을 복구하지 못했습니다. 남아 있는 .bak 파일을 확인해주세요.\n\nSome changes could not be rolled back after installation failed. Check the remaining .bak files.", failures);
            }
            foreach (string backup in backups)
            {
                try { File.Delete(backup); }
                catch (Exception ex) { Logger.Warn("Could not remove installation backup: " + backup + ": " + ex.Message); }
            }
        }

        internal static string GetModAssetName(string loader)
        {
            if (loader == "Melon") return "AdofaiDeeplinkMelonMod.zip";
            if (loader == "UMM") return "AdofaiDeeplinkMod.zip";
            throw new ArgumentException("지원하지 않는 모드 로더입니다.\n\nThe mod loader is not supported.");
        }

        internal static string GetModDestination(string gameDirectory, string loader)
        {
            GetModAssetName(loader);
            if (string.IsNullOrWhiteSpace(gameDirectory))
                throw new DirectoryNotFoundException("게임 설치 폴더를 찾을 수 없습니다.\n\nCould not find the game installation directory.");
            return loader == "Melon" ? Path.Combine(gameDirectory, "Mods") : Path.Combine(gameDirectory, "Mods", "AdofaiDeeplink");
        }

        internal static async Task InstallModAsync(string loader)
        {
            byte[] package = await Utils.GetReleaseAsset(GetModAssetName(loader));
            using (var transaction = new Utils())
            {
                InstallModPackage(package, FindAdofaiPath.GetAdofaiInstallPath(), loader, transaction);
                transaction.Commit();
            }
        }

        internal static void InstallModPackage(byte[] package, string gameDirectory, string loader, Utils transaction)
        {
            string destination = GetModDestination(gameDirectory, loader);
            string temporary = Path.Combine(Path.GetTempPath(), "AdofaiDeeplink-package-" + Guid.NewGuid().ToString("N"));
            string extraction = Path.Combine(temporary, "extracted");
            Directory.CreateDirectory(temporary);
            try
            {
                string archivePath = Path.Combine(temporary, "package.zip");
                File.WriteAllBytes(archivePath, package);
                // Extraction completes before any installed files are changed; ZipFile rejects paths outside the destination.
                ZipFile.ExtractToDirectory(archivePath, extraction);
                string requiredDll = loader == "Melon" ? "AdofaiDeeplinkMelonMod.dll" : "AdofaiDeeplinkMod.dll";
                if (!File.Exists(Path.Combine(extraction, requiredDll)) || (loader == "UMM" && !File.Exists(Path.Combine(extraction, "Info.json"))))
                    throw new InvalidDataException("다운로드한 모드 압축 파일에 필요한 파일이 없습니다.\n\nThe downloaded mod archive is missing required files.");
                if (loader == "Melon")
                {
                    // Only deploy this mod, never game/loader assemblies from the archive into the shared Mods directory.
                    transaction.CopyFile(Path.Combine(extraction, requiredDll), Path.Combine(destination, requiredDll));
                }
                else
                {
                    foreach (string source in Directory.GetFiles(extraction, "*", SearchOption.AllDirectories))
                        transaction.CopyFile(source, Path.Combine(destination, source.Substring(extraction.Length + 1)));
                }
            }
            finally
            {
                if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
            }
        }
    }
}
