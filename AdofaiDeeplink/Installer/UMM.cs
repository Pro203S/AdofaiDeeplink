using System.IO;
using System.Threading.Tasks;

namespace AdofaiDeeplink.Installer
{
    public class UMM
    {
        public static Task RunAsync()
        {
            return Utils.InstallModAsync("UMM");
        }

        public static void Uninstall()
        {
            Uninstall(FindAdofaiPath.GetAdofaiInstallPath());
        }

        internal static void Uninstall(string gameDirectory)
        {
            if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory)) return;
            string path = Path.Combine(gameDirectory, "Mods", "AdofaiDeeplink");
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }
}
