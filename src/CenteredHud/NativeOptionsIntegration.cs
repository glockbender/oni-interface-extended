using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace CenteredHud
{
    [HarmonyPatch(typeof(OptionsMenuScreen), "OnPrefabInit")]
    internal static class NativeOptionsIntegration
    {
        private const string EntryId = "prvz.CenteredHud.Options";

        private static void Postfix(OptionsMenuScreen __instance,
            ref IList<KButtonMenu.ButtonInfo> ___buttons, GameObject ___panelRoot)
        {
            if (___buttons == null) return;
            foreach (KButtonMenu.ButtonInfo button in ___buttons)
                if (Equals(button.userData, EntryId)) return;

            // Copy the list so fixed-size collections and other mods remain supported.
            var buttons = new List<KButtonMenu.ButtonInfo>(___buttons);
            buttons.Insert(System.Math.Min(1, buttons.Count), new KButtonMenu.ButtonInfo(
                HudSettingsScreen.MenuTitle, Action.NumActions, () => {
                    if (__instance == null) return;
                    HudController controller = GameScreenManager.Instance != null
                        ? GameScreenManager.Instance.GetComponent<HudController>() : null;
                    if (controller != null && controller.SettingsScreen != null) return;

                    // Keep ONI's parent dialog and its pause state alive while hiding its panel.
                    // Our modal sits above it; closing restores the exact original panel scale.
                    Vector3 previousScale = ___panelRoot != null ? ___panelRoot.transform.localScale : Vector3.one;
                    HudSettingsScreen screen = HudSettingsScreen.Open(controller, __instance.transform.parent, () => {
                        if (___panelRoot != null) ___panelRoot.transform.localScale = previousScale;
                    });
                    if (controller != null) controller.SettingsScreen = screen;
                    if (___panelRoot != null) ___panelRoot.transform.localScale = Vector3.zero;
                }, userData: EntryId));
            ___buttons = buttons;
            Debug.Log("[CenteredHud] Added native settings menu entry.");
        }
    }
}
