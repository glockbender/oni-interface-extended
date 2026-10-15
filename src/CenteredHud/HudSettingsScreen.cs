using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Text = CenteredHud.Localization.STRINGS;

namespace CenteredHud
{
    // Reuse ONI's visual prefabs: fonts, sprites, hover states and sounds.
    public sealed class HudSettingsScreen : KScreen
    {
        internal static string MenuTitle { get { return Text.TITLE; } }
        private HudController owner;
        private LocText status;
        private KInputTextField widthInput;
        private GameObject guidesCheck;
        private GameObject shortcutsCheck;
        private LocText help;
        private System.Action onClosed;
        private bool ownsPause;
        private OptionsMenuScreen templates;
        private Transform graphics;
        private Transform metrics;
        private LocText labelTemplate;
        private float lastEffectiveWidth = -1;
        private int lastScreenWidth, lastScreenHeight;

        internal static HudSettingsScreen Open(HudController controller, Transform parent, System.Action onClosed = null)
        {
            GameObject obj = new GameObject("InterfaceExtendedSettings", typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Canvas canvas = obj.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
            obj.AddComponent<GraphicRaycaster>();
            Image blocker = obj.AddComponent<Image>();
            // The native Options menu already owns a modal backdrop.
            blocker.color = new Color(0f, 0f, 0f, onClosed != null ? 0f : 0.5f);
            HudSettingsScreen screen = obj.AddComponent<HudSettingsScreen>();
            screen.owner = controller;
            screen.onClosed = onClosed;
            screen.ConsumeMouseScroll = true;
            try
            {
                screen.Build();
                screen.Activate();
                return screen;
            }
            catch
            {
                Destroy(obj);
                throw;
            }
        }

        public override float GetSortKey() { return MODAL_SCREEN_SORT_KEY + 1f; }
        public override bool IsModal() { return true; }

        protected override void OnActivate()
        {
            base.OnActivate();
            if (SpeedControlScreen.Instance != null)
            {
                SpeedControlScreen.Instance.Pause(false);
                ownsPause = true;
            }
        }

        protected override void OnDeactivate()
        {
            if (owner != null) owner.SettingsScreen = null;
            if (ownsPause && SpeedControlScreen.Instance != null)
                SpeedControlScreen.Instance.Unpause(false);
            ownsPause = false;
            System.Action callback = onClosed;
            onClosed = null;
            if (callback != null) callback();
            base.OnDeactivate();
        }

        public override void OnKeyDown(KButtonEvent e) { e.Consumed = true; }
        public override void OnKeyUp(KButtonEvent e) { e.Consumed = true; }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Deactivate();
            float effective = owner != null ? owner.EffectiveWidth : 0;
            if (effective != lastEffectiveWidth || Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
                RefreshStatus();
        }

        private void RefreshStatus()
        {
            lastEffectiveWidth = owner != null ? owner.EffectiveWidth : 0;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            if (status != null)
            {
                string mode = Mod.Settings.Enabled && Mod.Settings.WidthPixels > 0 ? Text.CENTERED : Text.FULLSCREEN;
                status.text = owner != null
                    ? string.Format(Text.LIVE_STATUS, mode, lastEffectiveWidth, Screen.width, Screen.height)
                    : string.Format(Text.MENU_STATUS, mode + (Mod.Settings.Enabled && Mod.Settings.WidthPixels > 0
                        ? " · " + string.Format(Text.PIXELS, Mod.Settings.WidthPixels) : ""));
            }
        }

        private void Build()
        {
            templates = ScreenPrefabs.Instance.OptionsScreen;
            graphics = ((Component)AccessTools.Field(typeof(OptionsMenuScreen), "graphicsOptionsScreenPrefab")
                .GetValue(templates)).transform;
            metrics = ((Component)AccessTools.Field(typeof(OptionsMenuScreen), "metricsScreenPrefab")
                .GetValue(templates)).transform;
            labelTemplate = graphics.Find("Content/DisplaySettings/Resolution/Resolution/Label").GetComponent<LocText>();

            RectTransform panel = Rect("Panel", transform, 0, 0, 620, 426);
            CopyImage(templates.transform.Find("CenterContent/Content/BG").GetComponent<Image>(), panel.gameObject);
            RectTransform titleBar = Clone(templates.transform.Find("CenterContent/Content/BG/TitleBar").gameObject,
                panel, 0, 225, 620, 24);
            SetLabel(titleBar.GetComponentInChildren<LocText>(true), MenuTitle.ToUpperInvariant());
            KButton close = titleBar.GetComponentInChildren<KButton>(true);
            close.ClearOnClick();
            close.onClick += Deactivate;

            RectTransform layout = Section(panel, "Layout", 47, 590, 280);
            Label(layout, Text.LAYOUT, 0, 117, 558, 26, true);
            Label(layout, Text.DESCRIPTION, 0, 82, 558, 32);
            status = Label(layout, "", 0, 43, 558, 42);
            status.name = "Status";
            Button(layout, "Preset2560", string.Format(Text.PIXELS, 2560), -186, -8, 176, () => SetWidth(2560));
            Button(layout, "Preset3360", string.Format(Text.PIXELS, 3360), 0, -8, 176, () => SetWidth(3360));
            Button(layout, "FullWidth", Text.FULLSCREEN, 186, -8, 176, () => SetWidth(0));
            Label(layout, Text.WIDTH, -166, -57, 218, 32);
            KInputTextField fieldTemplate = ScreenPrefabs.Instance.FileNameDialog.GetComponentInChildren<KInputTextField>(true);
            RectTransform field = Clone(fieldTemplate.gameObject, layout, 28, -57, 142, 32);
            field.name = "Width";
            widthInput = field.GetComponent<KInputTextField>();
            widthInput.onEndEdit.RemoveAllListeners();
            widthInput.onValueChanged.RemoveAllListeners();
            widthInput.OnValueChangesPaused = null;
            widthInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            widthInput.characterLimit = 5;
            widthInput.onEndEdit.AddListener(value => {
                int width;
                if (int.TryParse(value, out width)) SetWidth(width);
                else RefreshLabels();
            });
            Button(layout, "DecreaseWidth", "−", 146, -57, 62, () => StepWidth(-160));
            Button(layout, "IncreaseWidth", "+", 219, -57, 62, () => StepWidth(160));
            guidesCheck = Toggle(layout, "Guides", Text.GUIDES, 0, -108, () => {
                Mod.Settings.ShowGuides = !Mod.Settings.ShowGuides;
                Mod.SaveOptions(); RefreshLabels();
            });

            RectTransform controls = Section(panel, "Controls", -123, 590, 48);
            shortcutsCheck = Toggle(controls, "Shortcuts", Text.SHORTCUTS, 0, 0, () => {
                Mod.Settings.EnableShortcuts = !Mod.Settings.EnableShortcuts;
                Mod.SaveOptions(); RefreshLabels();
            });
            help = Label(panel, "", 0, -161, 558, 26);
            Button(panel, "Done", Text.DONE, 0, -191, 256, () => Deactivate(), true);
            RefreshLabels();
        }

        private RectTransform Section(Transform parent, string name, float y, float w, float h)
        {
            RectTransform rect = Rect(name, parent, 0, y, w, h);
            CopyImage(graphics.Find("Content/DisplaySettings/BG").GetComponent<Image>(), rect.gameObject);
            return rect;
        }

        private void SetWidth(int width)
        {
            width = Math.Max(0, Math.Min(Options.MaximumWidth, width));
            Mod.Settings.WidthPixels = width;
            Mod.Settings.Enabled = width > 0;
            Mod.SaveOptions();
            RefreshLabels();
        }

        private void StepWidth(int delta)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Canvas root = canvas != null ? canvas.rootCanvas : null;
            double scale = root != null ? root.scaleFactor : 1;
            int viewport = root != null ? (int)root.pixelRect.width : Screen.width;
            SetWidth(HudGeometry.StepWidth(viewport, Mod.Settings.Enabled ? Mod.Settings.WidthPixels : 0, scale, delta));
        }

        internal void RefreshLabels()
        {
            if (widthInput != null) widthInput.SetTextWithoutNotify(Mod.Settings.WidthPixels.ToString());
            if (guidesCheck != null) guidesCheck.SetActive(Mod.Settings.ShowGuides);
            if (shortcutsCheck != null) shortcutsCheck.SetActive(Mod.Settings.EnableShortcuts);
            if (help != null) help.text = Mod.SaveFailed ? Text.SAVE_ERROR : Mod.Settings.EnableShortcuts
                ? string.Format(Text.SHORTCUT_HINT, Mod.Settings.CycleKey, Mod.Settings.SettingsKey)
                : Text.SAVE_HINT;
            RefreshStatus();
        }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            Position(rect, x, y, w, h);
            return rect;
        }

        private static void Position(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(w, h);
            rect.localScale = Vector3.one;
            foreach (LayoutGroup layout in rect.GetComponents<LayoutGroup>()) layout.enabled = false;
            foreach (ContentSizeFitter fitter in rect.GetComponents<ContentSizeFitter>()) fitter.enabled = false;
        }

        private static RectTransform Clone(GameObject template, Transform parent, float x, float y, float w, float h)
        {
            GameObject obj = Instantiate(template, parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            Position(rect, x, y, w, h);
            obj.SetActive(true);
            // Captions explain these controls; inherited tooltip canvases draw behind this modal.
            foreach (ToolTip tip in obj.GetComponentsInChildren<ToolTip>(true)) tip.enabled = false;
            return rect;
        }

        private static void CopyImage(Image template, GameObject target)
        {
            Image image = target.AddComponent<Image>();
            image.sprite = template.sprite;
            image.type = template.type;
            image.color = template.color;
            image.material = template.material;
            image.pixelsPerUnitMultiplier = template.pixelsPerUnitMultiplier;
        }

        private static void SetLabel(LocText label, string value)
        {
            label.key = "";
            label.text = value;
            label.raycastTarget = false;
        }

        private LocText Label(Transform parent, string value, float x, float y, float w, float h, bool heading = false)
        {
            LocText template = heading ? graphics.Find("Content/DisplaySettings/Header").GetComponent<LocText>() : labelTemplate;
            RectTransform rect = Clone(template.gameObject, parent, x, y, w, h);
            LocText label = rect.GetComponent<LocText>();
            SetLabel(label, value);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            return label;
        }

        private void Button(Transform parent, string name, string caption, float x, float y, float w, System.Action click, bool primary = false)
        {
            GameObject template = primary ? templates.transform.Find("CenterContent/Content/BottomButtons/DoneButton").gameObject
                : templates.buttonPrefab;
            RectTransform rect = Clone(template, parent, x, y, w, 32);
            rect.name = name;
            KButton button = rect.GetComponent<KButton>();
            button.ClearOnClick();
            button.onClick += click;
            foreach (LocText label in rect.GetComponentsInChildren<LocText>(true))
                SetLabel(label, label.name == "Hotkey" ? "" : caption);
        }

        private GameObject Toggle(Transform parent, string name, string caption, float x, float y, System.Action click)
        {
            RectTransform rect = Clone(metrics.Find("Content/Buttons/MetricsToggle").gameObject, parent, x, y, 558, 32);
            rect.name = name;
            KButton button = rect.GetComponentInChildren<KButton>(true);
            Position(button.GetComponent<RectTransform>(), -261, 0, 28, 28);
            button.ClearOnClick();
            button.onClick += click;
            LocText label = rect.GetComponentInChildren<LocText>(true);
            Position(label.GetComponent<RectTransform>(), 19, 0, 490, 32);
            SetLabel(label, caption);
            return button.transform.Find("CheckMark").gameObject;
        }
    }
}
