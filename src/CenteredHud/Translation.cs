using System;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace CenteredHud
{
    [HarmonyPatch(typeof(global::Localization), nameof(global::Localization.Initialize))]
    internal static class Translation
    {
        private static void Postfix()
        {
            global::Localization.RegisterForTranslation(typeof(Localization.STRINGS));
            string code = global::Localization.GetLocale()?.Code;
            if (string.IsNullOrEmpty(code)) return;

            // Built-in locales use codes such as ru_klei; community locales can use ru.
            string directory = Path.Combine(Mod.ModDirectory, "translations");
            string filename = Path.Combine(directory, Path.GetFileName(code) + ".po");
            if (!File.Exists(filename) && code.EndsWith("_klei", StringComparison.Ordinal))
                filename = Path.Combine(directory, Path.GetFileName(code.Substring(0, code.Length - 5)) + ".po");
            if (!File.Exists(filename)) return;
            try
            {
                global::Localization.OverloadStrings(global::Localization.LoadStringsFile(filename, false));
            }
            catch (Exception error)
            {
                Debug.LogWarning("[Interface Extended] Cannot load translation: " + error.Message);
            }
        }
    }
}
