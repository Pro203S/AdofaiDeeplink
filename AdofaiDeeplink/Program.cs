using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;

            if (args.Length == 0)
            {
                if (!Installer.Installed())
                {
                    if (MessageBox.Show(
                        "AdofaiDeeplink를 설치하시겠습니까?" +
                        "\n" +
                        "\nAre you sure you want to install Adofai Deeplink?",
                        "AdofaiDeeplink",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question) != DialogResult.Yes)
                        Environment.Exit(0);

                    Installer.RunAdminTask();
                    Environment.Exit(0);
                }

                // Yes = Reinstall, No = remove, Cancel = cancel
                DialogResult flag = CustomMsgBox.Show();

                if (flag == DialogResult.Yes)
                {
                    Installer.RunAdminTask();
                    Environment.Exit(0);
                }
                else if (flag == DialogResult.No)
                {
                    Environment.Exit(0);
                }

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

            // 얼불춤 실행되어있을때 Exit
            if (Process.GetProcessesByName("A Dance of Fire and Ice").Any())
                Environment.Exit(0);
            
            Process.Start(new ProcessStartInfo
            {
                FileName = "steam://rungameid/977950"
            });
        }

        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            var thisAssembly = Assembly.GetExecutingAssembly();
            var name = args.Name.Substring(0, args.Name.IndexOf(',')) + ".dll";

            var resources = thisAssembly.GetManifestResourceNames().Where(s => s.EndsWith(name));
            var enumerable = resources.ToList();
            if (!enumerable.Any()) return null;
            var resourceName = enumerable.First();
            var stream = thisAssembly.GetManifestResourceStream(resourceName);

            if (stream == null) return null;
            var assembly = new byte[stream.Length];
            stream.Read(assembly, 0, assembly.Length);
            stream.Dispose();
            return Assembly.Load(assembly);
        }
    }
}
