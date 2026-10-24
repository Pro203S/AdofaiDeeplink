using System.IO;
using MelonLoader;
using System.Reflection;
using System.Threading;
using MelonLoader.Utils;

[assembly: MelonInfo(typeof(AdofaiDeeplinkMelonMod.Main), "AdofaiDeeplink", "0.1.0", "Pro203S", "https://github.com/Pro203S/AdofaiDeeplink")]
[assembly: MelonGame("7th Beat Games", "A Dance of Fire and Ice")]

namespace AdofaiDeeplinkMelonMod
{
    public class Main : MelonMod
    {
        public static MelonLogger.Instance Logger;
        public FileSystemWatcher watcher = null;
        public HarmonyLib.Harmony harmony;
        private int pendingDeeplink;

        public override void OnInitializeMelon()
        {
            Logger = LoggerInstance;
            LoggerInstance.Msg("Initializing...");
            harmony = new HarmonyLib.Harmony("AdofaiDeeplink");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            watcher = new FileSystemWatcher()
            {
                Path = MelonEnvironment.UnityGameDataDirectory,
                Filter = "deeplink.txt",
                NotifyFilter = NotifyFilters.FileName
            };
            watcher.Created += new FileSystemEventHandler((o, ev) =>
            {
                LoggerInstance.Msg("deeplink.txt created");

                Interlocked.Exchange(ref pendingDeeplink, 1);
            });
            watcher.EnableRaisingEvents = true;
        }

        public override void OnUpdate()
        {
            if (scrController.instance == null || Interlocked.Exchange(ref pendingDeeplink, 0) == 0)
                return;
            if (!File.Exists(Path.Combine(MelonEnvironment.UnityGameDataDirectory, "deeplink.txt")))
                return;

            GCS.sceneToLoad = GCNS.sceneEditor;
            scrController.instance.StartLoadingScene();
        }

        public override void OnDeinitializeMelon()
        {
            LoggerInstance.Msg("Disposing...");
            watcher?.Dispose();
            Interlocked.Exchange(ref pendingDeeplink, 0);
            harmony?.UnpatchSelf();
        }
    }
}
