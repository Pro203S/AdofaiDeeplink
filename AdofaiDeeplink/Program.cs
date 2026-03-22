using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AdofaiDeeplink
{
    internal static class Program
    {
        public static string ExtractUrl(string input)
        {
            if (string.IsNullOrEmpty(input))
                return null;

            // 정규식: adofai://open/?url= 또는 adofai://open?url= 모두 지원
            var match = Regex.Match(input, @"^adofai://open/?\?url=(.+)$", RegexOptions.IgnoreCase);

            if (match.Success)
                return match.Groups[1].Value;

            // 혹시 위 패턴이 안 맞는 경우, fallback으로 수동 파싱
            int idx = input.IndexOf("url=");
            if (idx >= 0)
                return input.Substring(idx + 4);

            return null;
        }

        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                bool install = false;
                if (Installer.Installed())
                {
                    install = MessageBox.Show(
                        "이미 AdofaiDeeplink가 설치되어있습니다." +
                        "\n새로 설치하시겠습니까?" +
                        "\n" +
                        "\nAdofaiDeeplink is already installed." +
                        "\nDo you want to install a new installation?",
                        "AdofaiDeeplink",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question) == DialogResult.Yes;
                }
                else
                {
                    install = MessageBox.Show(
                        "AdofaiDeeplink를 설치하시겠습니까?" +
                        "\n" +
                        "\nAre you sure you want to install Adofai Deeplink?",
                        "AdofaiDeeplink",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question) == DialogResult.Yes;
                }

                if (!install) Environment.Exit(0);

                Installer.RunAdminTask();
                Environment.Exit(0);
            }

            if (args[0] == "installAdofaiDeeplink")
            {
                Installer.CopyFiles();
                Installer.EditRegistry();
                await Installer.InstallMod();

                MessageBox.Show("설치가 완료되었습니다.\n\nInstallation complete!", "AdofaiDeeplink", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string levelPath = args[0];
            string gamePath = FindAdofaiPath.GetAdofaiInstallPath();
            string deeplinkPath = gamePath + "\\A Dance of Fire and Ice_Data\\deeplink.txt";
            
            if (levelPath.StartsWith("adofai://"))
            {
                string extracted = ExtractUrl(levelPath);
                if (!string.IsNullOrEmpty(extracted))
                {
                    File.WriteAllText(deeplinkPath, $"0{extracted}");
                }
            }
            else
            {
                File.WriteAllText(deeplinkPath, $"1{levelPath}");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = gamePath + "\\A Dance of Fire and Ice.exe",
                UseShellExecute = true
            });
        }
    }
}
