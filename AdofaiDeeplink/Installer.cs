using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AdofaiDeeplink
{
    public class Installer
    {
        public static string LocalAppdata = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public static string AdofaiDeeplinkExe = LocalAppdata + "\\Pro203S\\AdofaiDeepLink\\A Dance of Fire and Ice.exe";
        public static bool Installed()
        {
            return File.Exists(AdofaiDeeplinkExe);
        }

        public static void RunAdminTask()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = Assembly.GetExecutingAssembly().Location,
                Verb = "runas",
                UseShellExecute = true,
                Arguments = "installAdofaiDeeplink"
            };

            Process.Start(startInfo);
        }

        public static async Task<Task> InstallMod()
        {
            WebClient wc = new WebClient();
            wc.Headers.Set("User-Agent", "AdofaiDeeplink-Installer");

            string rawJson = await wc.DownloadStringTaskAsync("https://api.github.com/repos/Pro203S/AdofaiDeeplink/tags");
            MessageBox.Show(rawJson);

            
            return Task.CompletedTask;
        }

        public static void CopyFiles()
        {
            Directory.CreateDirectory(LocalAppdata + "\\Pro203S");
            Directory.CreateDirectory(LocalAppdata + "\\Pro203S\\AdofaiDeepLink");

            if (File.Exists(AdofaiDeeplinkExe))
                File.Delete(AdofaiDeeplinkExe);

            File.Copy(Assembly.GetExecutingAssembly().Location, AdofaiDeeplinkExe);
        }

        public static void EditRegistry()
        {
            string gamePath = FindAdofaiPath.GetAdofaiInstallPath() + "\\A Dance of Fire and Ice.exe";

            RegistryKey classesRoot = Registry.ClassesRoot;

            RegistryKey adofaiExt = classesRoot.CreateSubKey(".adofai", true);
            RegistryKey adofaiFile = classesRoot.CreateSubKey("adofaifile", true);
            RegistryKey adofaiLink = classesRoot.CreateSubKey("adofailink", true);

            // adofai://
            adofaiLink.SetValue("", "URL:A Dance of Fire and Ice Protocol");
            adofaiLink
                .CreateSubKey("DefaultIcon", true)
                .SetValue("", $"\"{gamePath}\",0");

            RegistryKey linkcmd = adofaiLink
                .CreateSubKey("shell", true)
                .CreateSubKey("open", true)
                .CreateSubKey("command", true);

            linkcmd.SetValue("", $"\"{AdofaiDeeplinkExe}\" \"%1\"");

            // *.adofai
            adofaiExt.SetValue("", "adofaifile", RegistryValueKind.String);
            adofaiExt.CreateSubKey("OpenWithProgids", true).SetValue("adofaifile", "", RegistryValueKind.String);

            adofaiFile
                .CreateSubKey("DefaultIcon", true)
                .SetValue("", $"\"{gamePath}\",0");

            RegistryKey extcmd = adofaiFile
                .CreateSubKey("shell", true)
                .CreateSubKey("open", true)
                .CreateSubKey("command", true);

            extcmd.SetValue("", $"\"{AdofaiDeeplinkExe}\" \"%1\"");
        }

        public static void Uninstall()
        {

        }
    }
}
