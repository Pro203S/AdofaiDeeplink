using HarmonyLib;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;

namespace AdofaiDeeplinkMod
{
    public class Main
    {
        public static UnityModManager.ModEntry.ModLogger Logger;
        public static Harmony harmony;
        public static bool IsEnabled = false;
        public static FileSystemWatcher watcher;

        public static void Startup(UnityModManager.ModEntry modEntry)
        {
            Logger = modEntry.Logger;
            modEntry.OnToggle = OnToggle;
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            IsEnabled = value;

            if (value)
            {
                // 모드 활성화
                harmony = new Harmony(modEntry.Info.Id);
                harmony.PatchAll(Assembly.GetExecutingAssembly());

                watcher = new FileSystemWatcher()
                {
                    Path = Application.dataPath,
                    Filter = "deeplink.txt"
                };
                watcher.Created += new FileSystemEventHandler((o, ev) =>
                {
                    Logger.Log("deeplink.txt created");

                    GCS.sceneToLoad = GCNS.sceneEditor;
                    scrController.instance.StartLoadingScene();
                });
                watcher.EnableRaisingEvents = true;
                watcher.NotifyFilter = (NotifyFilters)375;
            }
            else
            {
                // 비활성화
                harmony.UnpatchAll(modEntry.Info.Id);
                watcher?.Dispose();
            }
            return true;
        }
    }
}
