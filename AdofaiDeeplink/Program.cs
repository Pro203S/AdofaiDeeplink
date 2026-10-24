using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using AdofaiDeeplink.Installer;
using AdofaiDeeplink.MsgBox;

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
            Logger.Info("Application started.");

            try
            {
                await RunAsync(args);
            }
            catch (Exception ex)
            {
                Logger.Error("Application failed.", ex);
                MessageBox.Show(ex.Message + "\n\n--- StackTrace ---\n" + ex.StackTrace, "AdofaiDeeplink", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }

        private static async Task RunAsync(string[] args)
        {
            Logger.Info("Received Args: " + string.Join(" ", args));

            if (RequiresGame(args, Utils.Installed()) && string.IsNullOrEmpty(FindAdofaiPath.GetAdofaiInstallPath()))
            {
                MessageBox.Show(
                    "AdofaiDeeplink는 Steam으로 설치된 A Dance of Fire and Ice에서만 지원합니다." +
                    "\nSteam에서 설치 경로를 찾을 수 없어 사용할 수 없습니다." +
                    "\nA Dance of Fire and Ice를 Steam에서 재설치 해주세요." +
                    "\n" +
                    "\nAdofaiDeeplink only supports A Dance of Fire and Ice installed through Steam." +
                    "\nUnable to locate the game's installation directory through Steam." +
                    "\nPlease reinstall A Dance of Fire and Ice through Steam.",
                    "AdofaiDeeplink",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                Environment.Exit(0);
            }

            if (args.Length == 0)
            {
                if (!Utils.Installed())
                {
                    if (MessageBox.Show(
                        "AdofaiDeeplink를 설치하시겠습니까?" +
                        "\n" +
                        "\nAre you sure you want to install AdofaiDeeplink?",
                        "AdofaiDeeplink",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question) != DialogResult.Yes)
                        Environment.Exit(0);

                    Utils.RunAdminTask("install");
                    Environment.Exit(0);
                }

                DialogResult flag = RemoveOrUpdate.Show();

                if (flag == DialogResult.Yes)
                {
                    Utils.RunAdminTask("uninstall");
                    Environment.Exit(0);
                }

                if (flag == DialogResult.No)
                {
                    Utils.RunAdminTask("update");
                    Environment.Exit(0);
                }

                Environment.Exit(0);
            }

            if ((args[0] == "install" || args[0] == "uninstall" || args[0] == "update") && !Utils.IsAdministrator())
            {
                Utils.RunAdminTask(args[0]);
                return;
            }

            if (args[0] == "install")
            {
                Logger.Info("Installing AdofaiDeeplink...");

                await Executable.InstallAsync();

                MessageBox.Show(
                    "설치가 완료되었습니다!\n\nInstallation complete!",
                    "AdofaiDeeplink",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                return;
            }

            if (args[0] == "uninstall")
            {
                Logger.Info("Uninstalling AdofaiDeeplink...");

                Logger.Info("--------- Mod ----------");
                string installedGameDirectory = FindAdofaiPath.GetAdofaiInstallPath();
                Melon.Uninstall(installedGameDirectory);
                UMM.Uninstall(installedGameDirectory);

                Logger.Info("------ Executable ------");
                Executable.Uninstall();

                using (Process cleanup = Executable.StartUninstallCleanup(Process.GetCurrentProcess().Id)) { }
                Logger.Info("Uninstall cleanup started; installation files will be removed after this process exits.");

                return;
            }

            if (args[0] == "update")
            {
                await Update.RunAsync();
                return;
            }

            if (args[0] == "adofai://updateMod")
            {
                bool flag = MessageBox.Show("AdofaiDeeplink를 업데이트 하시겠습니까?", "AdofaiDeeplink", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
                if (!flag) Environment.Exit(0);

                Utils.RunAdminTask("update");
                Environment.Exit(0);
            }

            if (args[0] == "adofai://uninstall")
            {
                bool flag = MessageBox.Show("AdofaiDeeplink를 삭제하시겠습니까?\n\nWould you like to uninstall AdofaiDeeplink?", "AdofaiDeeplink", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
                if (!flag) Environment.Exit(0);

                Utils.RunAdminTask("uninstall");
                Environment.Exit(0);
            }

            string levelPath = args[0];
            Logger.Info("Processing level open request.");
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

            if (Process.GetProcessesByName("A Dance of Fire and Ice").Length > 0)
            {
                Logger.Info("Found ADOFAI Process. Skipped starting game.");
                Environment.Exit(0);
            }

            Logger.Info("Starting A Dance of Fire and Ice via Steam.");
            Process.Start(new ProcessStartInfo
            {
                FileName = "steam://rungameid/977950"
            });
        }

        internal static bool RequiresGame(string[] args, bool installed)
        {
            if (args.Length == 0) return !installed;
            return args[0] != "uninstall" && args[0] != "adofai://uninstall";
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
