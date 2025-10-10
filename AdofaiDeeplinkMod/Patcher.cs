using HarmonyLib;
using System.IO;
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

            ADOBase.LoadScene("scnEditor");
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

            Debug.Log($"File Data: {data}");

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

                __instance.OpenLevel(data.Remove(0, 1));
            }
            else
            {
                File.Delete(deeplinkPath);
                return;
            }

            return;
        }
    }
}
