using System.IO;
using System.Threading.Tasks;

namespace AdofaiDeeplink.Installer
{
    public class Melon
    {
        public static Task RunAsync()
        {
            return Utils.InstallModAsync("Melon");
        }

        public static void Uninstall()
        {
            Uninstall(FindAdofaiPath.GetAdofaiInstallPath());
        }

        internal static void Uninstall(string gameDirectory)
        {
            if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory)) return;
            string path = Path.Combine(gameDirectory, "Mods", "AdofaiDeeplinkMelonMod.dll");
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
