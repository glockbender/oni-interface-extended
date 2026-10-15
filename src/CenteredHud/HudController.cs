using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CenteredHud
{
    public sealed class HudController : MonoBehaviour
    {
        // Direct children of ssOverlayCanvas, inspected in ONI build U59-744825.
        // Changing their anchors keeps the original hierarchy, drawing order and raycasters.
        private static readonly string[] RootNames = {
            "Left", "BottomPanelLeft", "Bottom Center", "TopRight", "Right",
            "BottomPanelRight", "TopRight - InFrontOfCenter", "BuildWatermark", "RestartWarning"
        };

        private readonly List<AnchorState> roots = new List<AnchorState>();
        private RectTransform overlay;
        private Canvas overlayCanvas;
        private RectTransform guideLeft, guideRight;
        private KeyCode cycleKey, settingsKey;
        private float nextDiscovery;
        private bool loggedRoots;
        internal HudSettingsScreen SettingsScreen;
        internal float EffectiveWidth { get; private set; }

        private void Start()
        {
            GameScreenManager manager = GetComponent<GameScreenManager>();
            if (manager == null || manager.ssOverlayCanvas == null) { enabled = false; return; }
            overlay = manager.ssOverlayCanvas.GetComponent<RectTransform>();
            overlayCanvas = manager.ssOverlayCanvas.GetComponent<Canvas>();
            RefreshKeys();
            DiscoverRoots();
            CreateGuides();
        }

        private void RefreshKeys()
        {
            if (!Enum.TryParse(Mod.Settings.CycleKey, true, out cycleKey)) cycleKey = KeyCode.F8;
            if (!Enum.TryParse(Mod.Settings.SettingsKey, true, out settingsKey)) settingsKey = KeyCode.F9;
        }

        private void Update()
        {
            if (overlay == null) return;
            bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (Mod.Settings.EnableShortcuts && control && shift)
            {
                if (Input.GetKeyDown(settingsKey)) ToggleSettings();
                else if (Input.GetKeyDown(cycleKey)) Cycle();
            }
            if (Time.unscaledTime >= nextDiscovery)
            {
                nextDiscovery = Time.unscaledTime + 1f;
                DiscoverRoots();
            }
        }

        private void DiscoverRoots()
        {
            foreach (string rootName in RootNames)
            {
                Transform child = overlay.Find(rootName);
                if (child != null) AddRoot(child as RectTransform);
            }
            // Selection details can be instantiated lazily, outside the named HUD containers.
            if (DetailsScreen.Instance != null)
            {
                Transform child = DetailsScreen.Instance.transform;
                while (child.parent != null && child.parent != overlay) child = child.parent;
                if (child.parent == overlay) AddRoot(child as RectTransform);
            }
            roots.RemoveAll(state => state.Rect == null);
            if (!loggedRoots && roots.Count > 0)
            {
                loggedRoots = true;
                Debug.Log("[CenteredHud] Tracking " + roots.Count + " HUD containers at " + Screen.width + "x" + Screen.height);
            }
        }

        private void AddRoot(RectTransform rect)
        {
            if (rect == null) return;
            foreach (AnchorState state in roots) if (state.Rect == rect) return;
            roots.Add(new AnchorState(rect));
        }

        private void LateUpdate()
        {
            if (overlay == null) return;
            try
            {
                float scale = overlayCanvas != null ? overlayCanvas.scaleFactor : 1f;
                float viewportWidth = overlayCanvas != null ? overlayCanvas.pixelRect.width : Screen.width;
                if (viewportWidth <= 0) return;
                EffectiveWidth = (float)HudGeometry.Width(viewportWidth, Mod.Settings.Enabled ? Mod.Settings.WidthPixels : 0, scale);
                float fraction = EffectiveWidth / viewportWidth;
                foreach (AnchorState state in roots) state.Apply(fraction);
                SetGuides(fraction);
            }
            catch (Exception error)
            {
                Debug.LogWarning("[CenteredHud] Restoring original HUD after layout error: " + error);
                Restore();
                enabled = false;
            }
        }

        internal void Cycle()
        {
            if (!Mod.Settings.Enabled || Mod.Settings.WidthPixels == 0)
            { Mod.Settings.Enabled = true; Mod.Settings.WidthPixels = 2560; }
            else if (Mod.Settings.WidthPixels <= 2560) Mod.Settings.WidthPixels = 3360;
            else { Mod.Settings.Enabled = false; }
            Mod.SaveOptions();
            Debug.Log("[CenteredHud] " + (Mod.Settings.Enabled ? Mod.Settings.WidthPixels + "px" : "Full screen"));
            if (SettingsScreen != null) SettingsScreen.RefreshLabels();
        }

        internal void ToggleSettings()
        {
            if (SettingsScreen != null) SettingsScreen.Deactivate();
            else SettingsScreen = HudSettingsScreen.Open(this, overlay);
        }

        private void CreateGuides()
        {
            guideLeft = CreateGuide("CenteredHudLeftGuide");
            guideRight = CreateGuide("CenteredHudRightGuide");
        }

        private RectTransform CreateGuide(string name)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.SetParent(overlay, false);
            Image image = obj.GetComponent<Image>();
            image.color = new Color(0.3f, 0.85f, 1f, 0.65f);
            image.raycastTarget = false;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(2f, 0f);
            return rect;
        }

        private void SetGuides(float fraction)
        {
            if (guideLeft == null || guideRight == null) return;
            bool show = Mod.Settings.ShowGuides && fraction < 0.9999f;
            guideLeft.gameObject.SetActive(show);
            guideRight.gameObject.SetActive(show);
            if (!show) return;
            float margin = (1f - fraction) * 0.5f;
            guideLeft.anchorMin = new Vector2(margin, 0f);
            guideLeft.anchorMax = new Vector2(margin, 1f);
            guideRight.anchorMin = new Vector2(1f - margin, 0f);
            guideRight.anchorMax = new Vector2(1f - margin, 1f);
        }

        private void Restore()
        {
            foreach (AnchorState state in roots) state.Apply(1f);
            if (guideLeft != null) guideLeft.gameObject.SetActive(false);
            if (guideRight != null) guideRight.gameObject.SetActive(false);
        }

        private void OnDisable() { Restore(); }

        private sealed class AnchorState
        {
            internal readonly RectTransform Rect;
            private float baseMinX, baseMaxX, lastMinX, lastMaxX;

            internal AnchorState(RectTransform rect)
            {
                Rect = rect;
                baseMinX = lastMinX = rect.anchorMin.x;
                baseMaxX = lastMaxX = rect.anchorMax.x;
            }

            internal void Apply(float fraction)
            {
                if (Rect == null) return;
                Vector2 min = Rect.anchorMin, max = Rect.anchorMax;
                // If the game resets anchors, use its new baseline instead of adding offsets.
                if (Math.Abs(min.x - lastMinX) > 0.00001f) baseMinX = min.x;
                if (Math.Abs(max.x - lastMaxX) > 0.00001f) baseMaxX = max.x;
                float nextMin = HudGeometry.MapAnchor(baseMinX, fraction);
                float nextMax = HudGeometry.MapAnchor(baseMaxX, fraction);
                if (Math.Abs(min.x - nextMin) > 0.00001f || Math.Abs(max.x - nextMax) > 0.00001f)
                {
                    // Keep the game's current size/offsets, including ContentSizeFitter updates.
                    Vector2 size = Rect.sizeDelta, position = Rect.anchoredPosition;
                    Rect.anchorMin = new Vector2(nextMin, min.y);
                    Rect.anchorMax = new Vector2(nextMax, max.y);
                    Rect.sizeDelta = size;
                    Rect.anchoredPosition = position;
                }
                lastMinX = nextMin;
                lastMaxX = nextMax;
            }
        }
    }
}
