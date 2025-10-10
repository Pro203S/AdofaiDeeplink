using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
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

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Environment.Exit(0);
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
