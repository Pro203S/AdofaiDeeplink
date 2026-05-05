using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

// 바이브코드는좀나이스한거가틈

namespace AdofaiDeeplink
{
    public class FindAdofaiPath
    {
        public static string GetAdofaiInstallPath()
        {
            return FindGameInstallFolder(977950);
        }

        public static string FindGameInstallFolder(int appId)
        {
            string steamRoot = GetSteamInstallPath();
            if (string.IsNullOrEmpty(steamRoot)) return null;

            var steamAppsRoots = GetSteamAppsRoots(steamRoot);

            foreach (var root in steamAppsRoots)
            {
                var manifest = Path.Combine(root, $"appmanifest_{appId}.acf");
                if (!File.Exists(manifest)) continue;

                var installdir = TryParseInstallDirFromManifest(manifest);
                if (string.IsNullOrEmpty(installdir)) continue;

                var gamePath = Path.Combine(root, "common", installdir);
                if (Directory.Exists(gamePath))
                    return gamePath;
            }

            return null;
        }

        public static string GetSteamInstallPath()
        {
            string[] keys =
            {
                @"SOFTWARE\Wow6432Node\Valve\Steam",
                @"SOFTWARE\Valve\Steam"
            };

            foreach (var keyPath in keys)
            {
                using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
                {
                    var v = key?.GetValue("InstallPath") as string;
                    if (!string.IsNullOrEmpty(v) && Directory.Exists(v)) return v;
                }
            }

            var guess = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
            return Directory.Exists(guess) ? guess : null;
        }

        private static List<string> GetSteamAppsRoots(string steamRoot)
        {
            var results = new List<string>();
            var defaultSteamApps = Path.Combine(steamRoot, "steamapps");
            if (Directory.Exists(defaultSteamApps)) results.Add(defaultSteamApps);

            var vdfPath = Path.Combine(defaultSteamApps, "libraryfolders.vdf");
            if (!File.Exists(vdfPath)) return results;

            var text = File.ReadAllText(vdfPath, new UTF8Encoding(false));

            var pathMatches = Regex.Matches(text, "\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
            foreach (Match m in pathMatches)
            {
                var lib = NormalizeLibraryPath(m.Groups[1].Value);
                if (lib != null) results.Add(Path.Combine(lib, "steamapps"));
            }

            var legacyMatches = Regex.Matches(text, "\"\\d+\"\\s*\"([^\"]+)\"");
            foreach (Match m in legacyMatches)
            {
                var candidate = m.Groups[1].Value;
                if (!candidate.Contains(":\\") && !candidate.StartsWith(@"\\"))
                    continue;

                var lib = NormalizeLibraryPath(candidate);
                if (lib != null) results.Add(Path.Combine(lib, "steamapps"));
            }

            var dedup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var final = new List<string>();
            foreach (var p in results)
            {
                var norm = Path.GetFullPath(p);
                if (dedup.Add(norm) && Directory.Exists(norm))
                    final.Add(norm);
            }
            return final;
        }

        private static string NormalizeLibraryPath(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            var path = raw.Replace(@"\\", @"\").Trim();
            return Directory.Exists(path) ? path : null;
        }

        private static string TryParseInstallDirFromManifest(string manifestPath)
        {
            var text = File.ReadAllText(manifestPath, new UTF8Encoding(false));
            var m = Regex.Match(text, "\"installdir\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value.Trim();
            return null;
        }
    }
}
