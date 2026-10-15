using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace InterfaceExtended.TestAgent
{
    // Development-only mod. Loading it opens no socket and performs no scenario without a request.
    public sealed class AgentMod : KMod.UserMod2
    {
        internal static JArray SaveMods;
        internal static JArray ModEvents;
        internal static bool ModLoadError;

        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            harmony.Patch(AccessTools.Method(typeof(KMod.Manager), "MatchFootprint"),
                prefix: new HarmonyMethod(typeof(AgentMod), nameof(RecordFootprint)),
                postfix: new HarmonyMethod(typeof(AgentMod), nameof(RecordDifferences)));
            var host = new GameObject("InterfaceExtendedTestAgent");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<Scenario>().Initialize(Path.Combine(path, "bridge"));
        }

        private static void RecordFootprint(List<KMod.Label> __0)
        {
            SaveMods = __0 == null ? new JArray() : JArray.FromObject(__0);
        }

        private static void RecordDifferences(KMod.Manager __instance)
        {
            ModEvents = new JArray();
            ModLoadError = false;
            foreach (var item in __instance.events)
            {
                ModEvents.Add(new JObject { ["type"] = item.event_type.ToString(),
                    ["mod"] = JObject.FromObject(item.mod), ["details"] = item.details });
                if (item.event_type == KMod.EventType.LoadError) ModLoadError = true;
            }
        }
    }

    internal static class Files
    {
        internal static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        internal static void Json(string path, object value)
        {
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonConvert.SerializeObject(value, Formatting.Indented));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }

        internal static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        internal static string Utc() { return System.DateTime.UtcNow.ToString("o"); }
    }
}
