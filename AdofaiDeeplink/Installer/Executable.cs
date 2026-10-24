using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace AdofaiDeeplink.Installer
{
    public class Executable
    {
        internal static Process StartUninstallCleanup(int parentId, bool showResult = true)
        {
            string installDirectory = Path.GetFullPath(Path.Combine(Utils.LocalAppdata, "Pro203S", "AdofaiDeeplink"));
            if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(Utils.AdofaiDeeplinkExe)), installDirectory, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("삭제할 설치 폴더가 올바르지 않습니다.\n\nThe installation directory to remove is invalid.");
            if (parentId <= 0)
                throw new ArgumentOutOfRangeException(nameof(parentId), "부모 프로세스 ID는 0보다 커야 합니다.\n\nThe parent process ID must be greater than zero.");

            string scriptPath = Path.Combine(Path.GetTempPath(), "AdofaiDeeplink-cleanup-" + Guid.NewGuid().ToString("N") + ".vbs");
            string script = @"Option Explicit
On Error Resume Next
Dim fso, service, processes, process, originalCreation, running, attempt, errorNumber
Dim target, parentId, showResult
target = WScript.Arguments(0)
parentId = CLng(WScript.Arguments(1))
showResult = (WScript.Arguments(2) = ""True"")
Set fso = CreateObject(""Scripting.FileSystemObject"")
If Err.Number <> 0 Then Fail Err.Number
Set service = GetObject(""winmgmts:\\.\root\cimv2"")
If Err.Number <> 0 Then Fail Err.Number
originalCreation = """"
Do
    Err.Clear
    Set processes = service.ExecQuery(""SELECT CreationDate FROM Win32_Process WHERE ProcessId = "" & parentId)
    If Err.Number <> 0 Then Fail Err.Number
    running = False
    For Each process In processes
        If originalCreation = """" Then originalCreation = process.CreationDate
        If process.CreationDate = originalCreation Then running = True
    Next
    If Err.Number <> 0 Then Fail Err.Number
    If Not running Then Exit Do
    WScript.Sleep 250
Loop
For attempt = 1 To 30
    Err.Clear
    If fso.FolderExists(target) Then
        CheckFolder fso.GetFolder(target)
        If Err.Number <> 0 Then Fail Err.Number
        fso.DeleteFolder target, True
    End If
    errorNumber = Err.Number
    If errorNumber = 0 And Not fso.FolderExists(target) Then Exit For
    If errorNumber = 0 Then errorNumber = 70
    WScript.Sleep 1000
Next
If errorNumber <> 0 Then Fail errorNumber
fso.DeleteFile WScript.ScriptFullName, True
If showResult Then MsgBox ""삭제가 완료되었습니다."" & vbLf & vbLf & ""Uninstallation complete."", vbOKOnly + vbInformation, ""AdofaiDeeplink""
WScript.Quit 0

Sub CheckFolder(folder)
    Dim child
    If (folder.Attributes And 1024) <> 0 Then Fail 5
    For Each child In folder.SubFolders
        CheckFolder child
    Next
End Sub

Sub Fail(code)
    On Error Resume Next
    If IsObject(fso) Then fso.DeleteFile WScript.ScriptFullName, True
    If showResult Then MsgBox ""프로그램 자동 삭제 중 오류가 발생했어요. ("" & code & "")"" & vbLf & ""[Win+R]을 눌러 """"%localappdata%\Pro203S""""를 입력 후 <AdofaiDeeplink> 폴더를 수동으로 삭제해주세요."" & vbLf & vbLf & ""An error occurred while automatically uninstalling the program. ("" & code & "")"" & vbLf & ""Press [Win+R], enter """"%localappdata%\Pro203S"""", and manually delete the <AdofaiDeeplink> folder."" & vbLf, vbOKOnly + vbCritical, ""AdofaiDeeplink""
    WScript.Quit 1
End Sub
";
            File.WriteAllText(scriptPath, script, Encoding.Unicode);
            try
            {
                return Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wscript.exe"),
                    Arguments = $"//Nologo \"{scriptPath}\" \"{installDirectory}\" {parentId} {showResult}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = Path.GetTempPath()
                }) ?? throw new InvalidOperationException("삭제 작업을 시작할 수 없습니다.\n\nCould not start the uninstall cleanup.");
            }
            catch
            {
                File.Delete(scriptPath);
                throw;
            }
        }

        public static void Install()
        {
            using (var transaction = new Utils())
            {
                Install(transaction);
                transaction.Commit();
            }
        }

        internal static void Install(Utils transaction)
        {
            if (Utils.Installed())
                throw new InvalidOperationException("AdofaiDeeplink가 이미 설치되어 있습니다.\n\nAdofaiDeeplink is already installed.");

            string gamePath = Path.Combine(FindAdofaiPath.GetAdofaiInstallPath(), "A Dance of Fire and Ice.exe");
            transaction.CopyFile(Assembly.GetExecutingAssembly().Location, Utils.AdofaiDeeplinkExe);
            using (Stream notices = Assembly.GetExecutingAssembly().GetManifestResourceStream("AdofaiDeeplink.THIRD-PARTY-NOTICES.txt"))
            {
                if (notices == null)
                    throw new InvalidOperationException("프로그램에 포함된 타사 소프트웨어 고지문을 찾을 수 없습니다.\n\nCould not find the embedded third-party notices.");
                using (var data = new MemoryStream())
                {
                    notices.CopyTo(data);
                    transaction.WriteFile(Path.Combine(Path.GetDirectoryName(Utils.AdofaiDeeplinkExe), "THIRD-PARTY-NOTICES.txt"), data.ToArray());
                }
            }

            RegisterAssociations(Registry.ClassesRoot, gamePath, transaction);
            transaction.SaveRegistryBackup(Path.Combine(Path.GetDirectoryName(Utils.AdofaiDeeplinkExe), "registry-backup.xml"));
        }

        internal static void RegisterAssociations(RegistryKey root, string gamePath, Utils transaction)
        {
            foreach (var value in GetAssociationValues(gamePath))
                transaction.SetRegistryValue(root, value.Item1, value.Item2, value.Item3);
        }

        private static List<Tuple<string, string, string>> GetAssociationValues(string gamePath)
        {
            string command = $"\"{Utils.AdofaiDeeplinkExe}\" \"%1\"";
            string icon = $"\"{gamePath}\",0";
            const string zipMenu = @"SystemFileAssociations\.zip\shell\AdofaiDeeplink";
            var values = new List<Tuple<string, string, string>>
            {
                Tuple.Create(zipMenu, "", "Open with ADOFAI"),
                Tuple.Create(zipMenu, "Icon", icon),
                Tuple.Create(zipMenu + @"\command", "", command),
                Tuple.Create(@"Applications\AdofaiDeeplink.exe", "FriendlyAppName", "A Dance of Fire and Ice"),
                Tuple.Create(@"Applications\AdofaiDeeplink.exe\shell\open\command", "", command),
                Tuple.Create("adofai", "", "A Dance of Fire and Ice"),
                Tuple.Create("adofai", "URL Protocol", ""),
                Tuple.Create(@"adofai\DefaultIcon", "", icon),
                Tuple.Create(@"adofai\shell\open\command", "", command)
            };
            foreach (string extension in new[] { "adofai", "adozip" })
            {
                string progId = extension + "file";
                values.Add(Tuple.Create("." + extension, "", progId));
                values.Add(Tuple.Create("." + extension + @"\OpenWithProgids", progId, ""));
                values.Add(Tuple.Create(progId + @"\DefaultIcon", "", icon));
                values.Add(Tuple.Create(progId + @"\shell\open\command", "", command));
            }
            return values;
        }

        public static void Uninstall()
        {
            Logger.Info($"Removing registries...");

            Uninstall(Registry.ClassesRoot, Path.Combine(Path.GetDirectoryName(Utils.AdofaiDeeplinkExe), "registry-backup.xml"));

            Logger.Info("Done.");
        }

        internal static void Uninstall(RegistryKey root, string backupPath)
        {
            string gameDirectory = FindAdofaiPath.GetAdofaiInstallPath();
            var registrations = GetAssociationValues(gameDirectory == null ? "" : Path.Combine(gameDirectory, "A Dance of Fire and Ice.exe"));
            string ourCommand = $"\"{Utils.AdofaiDeeplinkExe}\" \"%1\"";
            var ownedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Capture ownership before restoring any command, so later values use the original ownership decision.
            foreach (var registration in registrations.Where(item => item.Item1.EndsWith(@"\command", StringComparison.OrdinalIgnoreCase)))
            {
                using (RegistryKey key = root.OpenSubKey(registration.Item1))
                    if (string.Equals(key?.GetValue("") as string, ourCommand, StringComparison.OrdinalIgnoreCase)) ownedGroups.Add(AssociationGroup(registration.Item1));
            }

            var allowedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var registration in registrations)
            {
                string path = registration.Item1;
                while (!string.IsNullOrEmpty(path))
                {
                    allowedKeys.Add(path);
                    int separator = path.LastIndexOf('\\');
                    path = separator < 0 ? "" : path.Substring(0, separator);
                }
            }

            IEnumerable<string> keysToPrune;
            if (File.Exists(backupPath))
            {
                XElement document = XDocument.Load(backupPath).Root;
                foreach (XElement value in document.Element("Values").Elements("Value").Reverse())
                {
                    string path = (string)value.Attribute("path");
                    string name = (string)value.Attribute("name");
                    if (!registrations.Any(item => item.Item1 == path && item.Item2 == name) || !ownedGroups.Contains(AssociationGroup(path))) continue;
                    using (RegistryKey key = root.OpenSubKey(path, true))
                    {
                        if (key == null || !key.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                        RegistryValueKind installedKind = (RegistryValueKind)(int)value.Attribute("installedKind");
                        if (key.GetValueKind(name) != installedKind) continue;
                        object current = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                        if (!XNode.DeepEquals(Utils.EncodeRegistryData(current, installedKind), value.Element("Installed").Element("Data"))) continue;
                        if ((bool)value.Attribute("existed"))
                        {
                            RegistryValueKind previousKind = (RegistryValueKind)(int)value.Attribute("previousKind");
                            key.SetValue(name, Utils.DecodeRegistryData(value.Element("Previous").Element("Data"), previousKind), previousKind);
                        }
                        else key.DeleteValue(name, false);
                    }
                }
                keysToPrune = document.Element("CreatedKeys").Elements("Key").Select(key => key.Value).Reverse().ToArray();
            }
            else
            {
                // Old installations have no original values to restore. Remove only known values with an owned handler.
                foreach (var registration in registrations.AsEnumerable().Reverse())
                {
                    if (!ownedGroups.Contains(AssociationGroup(registration.Item1))) continue;
                    using (RegistryKey key = root.OpenSubKey(registration.Item1, true))
                        if (key?.GetValue(registration.Item2) is string current && current == registration.Item3) key.DeleteValue(registration.Item2, false);
                }
                keysToPrune = allowedKeys.OrderByDescending(path => path.Length).ToArray();
            }
            foreach (string path in keysToPrune)
            {
                if (!allowedKeys.Contains(path)) continue;
                using (RegistryKey key = root.OpenSubKey(path))
                    if (key == null || key.ValueCount != 0 || key.SubKeyCount != 0) continue;
                root.DeleteSubKey(path, false);
            }
        }

        private static string AssociationGroup(string path)
        {
            string first = path.Split('\\')[0];
            if (first == ".adofai" || first == "adofaifile") return "adofaifile";
            if (first == ".adozip" || first == "adozipfile") return "adozipfile";
            if (first == "Applications") return @"Applications\AdofaiDeeplink.exe";
            if (first == "SystemFileAssociations") return @"SystemFileAssociations\.zip\shell\AdofaiDeeplink";
            return first;
        }

        internal static async Task InstallAsync()
        {
            if (Utils.Installed())
                throw new InvalidOperationException("AdofaiDeeplink가 이미 설치되어 있습니다.\n\nAdofaiDeeplink is already installed.");
            string gameDirectory = FindAdofaiPath.GetAdofaiInstallPath();
            string loader = ModLoader.AutoDetect();
            byte[] package = await Utils.GetReleaseAsset(Utils.GetModAssetName(loader));
            InstallDownloadedPackage(package, gameDirectory, loader, Executable.Install);
        }

        internal static void InstallDownloadedPackage(byte[] package, string gameDirectory, string loader, Action<Utils> installExecutable)
        {
            using (var transaction = new Utils())
            {
                Utils.InstallModPackage(package, gameDirectory, loader, transaction);
                installExecutable(transaction);
                transaction.WriteFile(ModLoader.SettingsPath, Encoding.UTF8.GetBytes(loader));
                transaction.Commit();
            }
        }
    }
}
