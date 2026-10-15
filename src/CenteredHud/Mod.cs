using System;
using System.IO;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace CenteredHud
{
    public sealed class Mod : KMod.UserMod2
    {
        internal const string Version = "1.3.0";
        internal static string ConfigPath;
        internal static Options Settings;
        internal static bool SaveFailed;

        public override void OnLoad(Harmony harmony)
        {
            ConfigPath = Path.Combine(path, "config.json");
            Settings = ReadOptions();
            base.OnLoad(harmony);
            Debug.Log("[Interface Extended] Loaded " + Version);
        }

        internal static Options ReadOptions()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    Options result = JsonConvert.DeserializeObject<Options>(File.ReadAllText(ConfigPath));
                    if (result == null) throw new InvalidDataException("Empty settings");
                    result.Normalize();
                    result.CycleKey = NormalizeKey(result.CycleKey, KeyCode.F8);
                    result.SettingsKey = NormalizeKey(result.SettingsKey, KeyCode.F9);
                    return result;
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning("[CenteredHud] Cannot read config; using defaults. " + error.Message);
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
                // Write a temporary file first, so a failed write does not truncate the configuration.
                string temporary = ConfigPath + ".tmp";
                File.WriteAllText(temporary, JsonConvert.SerializeObject(Settings, Formatting.Indented));
                if (File.Exists(ConfigPath)) File.Replace(temporary, ConfigPath, null);
                else File.Move(temporary, ConfigPath);
                SaveFailed = false;
            }
            catch (Exception error)
            {
                SaveFailed = true;
                Debug.LogWarning("[CenteredHud] Cannot save config: " + error.Message);
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
