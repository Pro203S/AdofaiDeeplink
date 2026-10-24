using System;
using System.IO;
using System.Windows.Forms;
using AdofaiDeeplink.MsgBox;

namespace AdofaiDeeplink
{
    public class ModLoader
    {
        internal static string SettingsPath => Path.Combine(Path.GetDirectoryName(Installer.Utils.AdofaiDeeplinkExe), "modLoader.txt");
        public static string getModLoader()
        {
            if (!File.Exists(SettingsPath)) return null;

            string loader = File.ReadAllText(SettingsPath).Trim();
            return loader == "Melon" || loader == "UMM" ? loader : null;
        }

        public static void SetModLoader(string value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            File.WriteAllText(SettingsPath, value);
        }

        internal static bool IsUmmInstalled(string gameDirectory)
        {
            if (string.IsNullOrWhiteSpace(gameDirectory)) return false;
            string managed = Path.Combine(gameDirectory, "A Dance of Fire and Ice_Data", "Managed");
            return File.Exists(Path.Combine(managed, "UnityModManager", "UnityModManager.dll")) || File.Exists(Path.Combine(managed, "UnityModManager.dll"));
        }

        public static string AutoDetect()
        {
            string adofai = FindAdofaiPath.GetAdofaiInstallPath();
            if (string.IsNullOrWhiteSpace(adofai))
                throw new DirectoryNotFoundException("게임 설치 폴더를 찾을 수 없습니다.\n\nCould not find the game installation directory.");

            bool usingMelon = Directory.Exists(Path.Combine(adofai, "MelonLoader"));
            bool usingUMM = IsUmmInstalled(adofai);

            if (usingMelon && usingUMM)
            {
                DialogResult flag = MelonOrUmm.Show();
                if (flag == DialogResult.Yes)
                {
                    return "Melon";
                }

                if (flag == DialogResult.No)
                {
                    return "UMM";
                }

                throw new Exception("모드 로더를 식별할 수 없습니다.\n\nCould not identify the mod loader.");
            }

            if (usingMelon)
            {
                return "Melon";
            }

            if (usingUMM)
            {
                return "UMM";
            }

            throw new Exception("모드 로더를 감지할 수 없습니다.\nMelonLoader, UnityModManager만 지원합니다.\n\nCould not detect the mod loader.\nOnly MelonLoader and UnityModManager are supported.");
        }
    }
}
