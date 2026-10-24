using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AdofaiDeeplink.Installer
{
    public class Update
    {
        public static async Task RunAsync()
        {
            if (!Utils.Installed()) throw new InvalidOperationException("설치된 AdofaiDeeplink 실행 파일을 찾을 수 없습니다.\n\nCould not find the installed AdofaiDeeplink executable.");

            string modLoader = ModLoader.getModLoader() ?? ModLoader.AutoDetect();
            // Download both assets before changing either the mod or the executable.
            byte[] package = await Utils.GetReleaseAsset(Utils.GetModAssetName(modLoader));
            byte[] data = await Utils.GetReleaseAsset("AdofaiDeeplink.exe");
            if (data.Length < 2 || data[0] != 'M' || data[1] != 'Z')
                throw new InvalidDataException("다운로드한 파일이 EXE 형식이 아닙니다.\n\nThe downloaded file is not in EXE format.");

            string stagedPath = Utils.AdofaiDeeplinkExe + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (var transaction = new Utils())
            {
                Utils.InstallModPackage(package, FindAdofaiPath.GetAdofaiInstallPath(), modLoader, transaction);
                transaction.WriteFile(ModLoader.SettingsPath, Encoding.UTF8.GetBytes(modLoader));
                transaction.WriteFile(stagedPath, data);
                using (Process replacement = StartUpdateReplacement(stagedPath, Utils.AdofaiDeeplinkExe, Process.GetCurrentProcess().Id, transaction)) { }
                transaction.CommitDeferred();
                Logger.Info("Update downloaded; replacement will run after this process exits.");
            }
        }

        internal static Process StartUpdateReplacement(string stagedPath, string targetPath, int parentId, Utils transaction = null, int maxAttempts = 30)
        {
            stagedPath = Path.GetFullPath(stagedPath);
            targetPath = Path.GetFullPath(targetPath);
            if (!string.Equals(Path.GetDirectoryName(stagedPath), Path.GetDirectoryName(targetPath), StringComparison.OrdinalIgnoreCase) || string.Equals(stagedPath, targetPath, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("교체할 파일은 서로 다른 파일이어야 하며 같은 폴더에 있어야 합니다.\n\nReplacement files must be distinct and in the same directory.");
            if (!File.Exists(stagedPath))
                throw new FileNotFoundException("다운로드한 업데이트 파일을 찾을 수 없습니다.\n\nCould not find the downloaded update file.", stagedPath);
            if (parentId <= 0)
                throw new ArgumentOutOfRangeException(nameof(parentId), "부모 프로세스 ID는 0보다 커야 합니다.\n\nThe parent process ID must be greater than zero.");
            if (maxAttempts <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxAttempts), "재시도 횟수는 0보다 커야 합니다.\n\nThe number of attempts must be greater than zero.");

            var rollback = new StringBuilder();
            var cleanup = new StringBuilder();
            var environment = new System.Collections.Generic.Dictionary<string, string>();
            if (transaction != null)
            {
                for (int index = transaction.UpdatedFiles.Count - 1; index >= 0; index--)
                {
                    var file = transaction.UpdatedFiles[index];
                    string destinationVariable = "ADOFAI_UPDATE_FILE_" + index;
                    string backupVariable = "ADOFAI_UPDATE_BACKUP_" + index;
                    environment[destinationVariable] = file.Item1;
                    if (file.Item2 == null)
                    {
                        rollback.AppendLine($"if exist \"%{destinationVariable}%\" del /F /Q \"%{destinationVariable}%\" >nul 2>&1");
                        rollback.AppendLine($"if exist \"%{destinationVariable}%\" set \"rollbackFailed=1\"");
                    }
                    else
                    {
                        environment[backupVariable] = file.Item2;
                        rollback.AppendLine($"if not exist \"%{backupVariable}%\" set \"rollbackFailed=1\"");
                        rollback.AppendLine($"if exist \"%{backupVariable}%\" (");
                        rollback.AppendLine($"    move /Y \"%{backupVariable}%\" \"%{destinationVariable}%\" >nul 2>&1");
                        rollback.AppendLine("    if errorlevel 1 set \"rollbackFailed=1\"");
                        rollback.AppendLine(")");
                        cleanup.AppendLine($"if exist \"%{backupVariable}%\" del /F /Q \"%{backupVariable}%\" >nul 2>&1");
                    }
                }
                for (int index = transaction.CreatedDirectories.Count - 1; index >= 0; index--)
                {
                    string variable = "ADOFAI_UPDATE_DIRECTORY_" + index;
                    environment[variable] = transaction.CreatedDirectories[index];
                    // Only remove empty directories created by this update; never recursively remove shared Mods content.
                    rollback.AppendLine($"if exist \"%{variable}%\" rd \"%{variable}%\" >nul 2>&1");
                }
            }

            string scriptPath = Path.Combine(Path.GetTempPath(), "AdofaiDeeplink-update-" + Guid.NewGuid().ToString("N") + ".cmd");
            // Paths travel through environment variables so CMD does not reinterpret their contents as script text.
            string script = @"@echo off
setlocal DisableDelayedExpansion
:wait
""%SystemRoot%\System32\tasklist.exe"" /FI ""PID eq %ADOFAI_UPDATE_PID%"" /NH | ""%SystemRoot%\System32\findstr.exe"" /R /C:""[ ]%ADOFAI_UPDATE_PID%[ ]"" >nul
if errorlevel 1 goto replace
""%SystemRoot%\System32\ping.exe"" -n 2 127.0.0.1 >nul
goto wait
:replace
set attempts=0
:retry
move /Y ""%ADOFAI_UPDATE_SOURCE%"" ""%ADOFAI_UPDATE_TARGET%"" >nul 2>&1
if not errorlevel 1 goto success
set /a attempts+=1
if %attempts% GEQ __MAX_ATTEMPTS__ goto failure
""%SystemRoot%\System32\ping.exe"" -n 2 127.0.0.1 >nul
goto retry
:failure
set ""rollbackFailed=0""
__ROLLBACK__
if ""%rollbackFailed%""==""0"" (
    >""%ADOFAI_UPDATE_TARGET%.update-error.log"" echo Update replacement failed. Previous mod files and settings were restored.
) else (
    >""%ADOFAI_UPDATE_TARGET%.update-error.log"" echo Update replacement failed and rollback was incomplete. Remaining .bak files were preserved for recovery.
)
del ""%~f0"" & exit 1
:success
__CLEANUP__
del ""%~f0"" & exit 0
";
            script = script.Replace("__MAX_ATTEMPTS__", maxAttempts.ToString(System.Globalization.CultureInfo.InvariantCulture)).Replace("__ROLLBACK__", rollback.ToString()).Replace("__CLEANUP__", cleanup.ToString());
            File.WriteAllText(scriptPath, script.Replace("\r\n", "\n").Replace("\n", "\r\n"), Encoding.ASCII);
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
                    Arguments = "/D /V:OFF /S /C \"\"%ADOFAI_UPDATE_SCRIPT%\"\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = Path.GetTempPath()
                };
                startInfo.EnvironmentVariables["ADOFAI_UPDATE_SCRIPT"] = scriptPath;
                startInfo.EnvironmentVariables["ADOFAI_UPDATE_SOURCE"] = stagedPath;
                startInfo.EnvironmentVariables["ADOFAI_UPDATE_TARGET"] = targetPath;
                startInfo.EnvironmentVariables["ADOFAI_UPDATE_PID"] = parentId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                foreach (var entry in environment) startInfo.EnvironmentVariables[entry.Key] = entry.Value;
                return Process.Start(startInfo) ?? throw new InvalidOperationException("업데이트 파일 교체 작업을 시작할 수 없습니다.\n\nCould not start the update file replacement.");
            }
            catch
            {
                File.Delete(scriptPath);
                throw;
            }
        }
    }
}
