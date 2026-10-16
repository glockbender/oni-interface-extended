using System;
using HarmonyLib;
using UnityEngine;

namespace CenteredHud
{
    public sealed class Mod : KMod.UserMod2
    {
        internal const string Version = "1.4.0";
        internal static string ModDirectory;
        internal static string ConfigPath;
        private static ConfigStore config;
        internal static Options Settings;
        internal static bool SaveFailed;

        public override void OnLoad(Harmony harmony)
        {
            ModDirectory = path;
            config = new ConfigStore(KMod.Manager.GetDirectory(), path);
            ConfigPath = config.ConfigPath;
            Settings = ReadOptions();
            base.OnLoad(harmony);
            Debug.Log("[Interface Extended] Loaded " + Version);
        }

        internal static Options ReadOptions()
        {
            try
            {
                Exception migrationError;
                Options result = config.Read(out migrationError);
                SaveFailed = migrationError != null;
                if (migrationError != null)
                    Debug.LogWarning("[Interface Extended] Using legacy config; migration failed: " + migrationError.Message);
                result.CycleKey = NormalizeKey(result.CycleKey, KeyCode.F8);
                result.SettingsKey = NormalizeKey(result.SettingsKey, KeyCode.F9);
                return result;
            }
            catch (Exception error)
            {
                Debug.LogWarning("[Interface Extended] Cannot read config; using defaults. " + error.Message);
            }
            return new Options();
        }

        private static string NormalizeKey(string value, KeyCode fallback)
        {
            KeyCode key;
            if (!Enum.TryParse(value, true, out key) || !Enum.IsDefined(typeof(KeyCode), key)) key = fallback;
            return key.ToString();
        }

        internal static void SaveOptions()
        {
            try
            {
                config.Write(Settings);
                SaveFailed = false;
            }
            catch (Exception error)
            {
                SaveFailed = true;
                Debug.LogWarning("[Interface Extended] Cannot save config: " + error.Message);
            }
        }
    }

    [HarmonyPatch(typeof(GameScreenManager), "OnSpawn")]
    internal static class InstallHudController
    {
        private static void Postfix(GameScreenManager __instance)
        {
            if (__instance.GetComponent<HudController>() == null)
                __instance.gameObject.AddComponent<HudController>();
        }
    }
}
