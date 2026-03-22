using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
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
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = Assembly.GetExecutingAssembly().Location;
            startInfo.Verb = "runas";
            startInfo.UseShellExecute = true;
            startInfo.Arguments = "installAdofaiDeeplink";

            Process.Start(startInfo);
        }

        public static async Task InstallMod()
        {
            return;
        }

        public static void CopyFiles()
        {
            Directory.CreateDirectory(LocalAppdata + "\\Pro203S");
            Directory.CreateDirectory(LocalAppdata + "\\Pro203S\\AdofaiDeepLink");

            File.Copy(Assembly.GetExecutingAssembly().Location, AdofaiDeeplinkExe);
        }

        public static void EditRegistry()
        {
            RegistryKey classesRoot = Registry.ClassesRoot;

            RegistryKey adofaiExt = classesRoot.CreateSubKey(".adofai", true);
            RegistryKey adofaiFile = classesRoot.CreateSubKey("adofaifile", true);

            adofaiExt.SetValue("", "adofaifile", RegistryValueKind.String);

            string gamePath = FindAdofaiPath.GetAdofaiInstallPath() + "\\A Dance of Fire and Ice.exe";

            adofaiFile
                .CreateSubKey("DefaultIcon", true)
                .SetValue("", $"\"{gamePath}\",0");

            RegistryKey command = adofaiFile
                .CreateSubKey("shell", true)
                .CreateSubKey("open", true)
                .CreateSubKey("command", true);

            command.SetValue("", $"\"{AdofaiDeeplinkExe}\" \"%1\"");
        }
    }
}
