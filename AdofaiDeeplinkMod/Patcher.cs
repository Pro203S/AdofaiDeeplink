using HarmonyLib;
using System;
using System.Collections;
using System.Data;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AdofaiDeeplinkMod
{
    [HarmonyPatch(typeof(scnLevelSelect), "Start")]
    public static class DetectDeeplink
    {
        public static void Postfix()
        {
            string deeplinkPath = Application.dataPath + "\\deeplink.txt";
            if (!File.Exists(deeplinkPath)) return;

            Main.Logger.Log("deeplink.txt found.");

            GCS.sceneToLoad = GCNS.sceneEditor;
            scrController.instance.StartLoadingScene();
            return;
        }
    }

    [HarmonyPatch(typeof(scnEditor), "Start")]
    public static class EditorWork
    {
        public static void Postfix(scnEditor __instance)
        {
            string deeplinkPath = Application.dataPath + "\\deeplink.txt";
            if (!File.Exists(deeplinkPath)) return;

            string data = File.ReadAllText(deeplinkPath);
            File.Delete(deeplinkPath);
            if (string.IsNullOrEmpty(data)) return;

            Main.Logger.Log($"File Data: {data}");

            if (data.StartsWith("0"))
            {
                // URL

                __instance.levelLinkInput.text = data.Remove(0, 1);
                __instance.buttonOpenURL.onClick.Invoke();
                __instance.popupURLDownload.onClick.Invoke();
            }
            else if (data.StartsWith("1"))
            {
                // 파일 경로

                string filePath = data.Remove(0, 1);
                string extension = Path.GetExtension(filePath).Remove(0, 1);

                Main.Logger.Log($"filePath: {filePath}, extension: {extension}");

                if (GCS.levelTextExtensions.Contains(extension))
                {
                    var method = AccessTools.Method(typeof(scnEditor), "OpenLevelCo");
                    IEnumerator routine = (IEnumerator)method.Invoke(__instance, new object[] { filePath });
                    __instance.StartCoroutine(routine);
                    return;
                }

                if (GCS.levelZipExtensions.Contains(extension))
                {
                    string availableDir = RDUtils.GetAvailableDirectoryName(Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath)));
                    Directory.CreateDirectory(availableDir);
                    Main.Logger.Log("availableDir: " + availableDir);

                    try
                    {
                        ZipUtils.Unzip(filePath, availableDir);
                    }
                    catch (Exception ex)
                    {
                        __instance.ShowNotificationPopup("Failed to extract the level archive.");
                        Main.Logger.Error($"Failed to unzip: " + ex.Message + "\n" + ex.StackTrace);
                        Directory.Delete(availableDir, true);
                        return;
                    }

                    string[] files = Directory.GetFiles(availableDir);
                    Main.Logger.Log("files: " + string.Join(" ", files));
                    string adofaiPath = null;
                    foreach (string file in files)
                    {
                        if (Path.GetExtension(file) != ".adofai") continue;

                        adofaiPath = file;
                    }
                    Main.Logger.Log("adofaiPath: " + adofaiPath);

                    if (adofaiPath == null)
                    {
                        __instance.ShowNotificationPopup("Level not found.");
                        Directory.Delete(availableDir, true);
                        return;
                    }

                    var method = AccessTools.Method(typeof(scnEditor), "OpenLevelCo");
                    IEnumerator routine = (IEnumerator)method.Invoke(__instance, new object[] { adofaiPath });
                    __instance.StartCoroutine(routine);
                    return;
                }

                __instance.ShowNotificationPopup("Unable to load the level.");
            }

            return;
        }
    }
}
