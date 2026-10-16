using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InterfaceExtended.TestAgent
{
    public sealed class Scenario : MonoBehaviour
    {
        private string bridge, lastId, output;
        private float nextPoll;
        private bool running, loaded, pauseOpened, releasedBaselinePause;
        private JObject request, step, manifest;
        private readonly JArray steps = new JArray();
        private readonly List<string> logs = new List<string>();
        private readonly List<string> errors = new List<string>();
        private Type modType, settingsType;
        private string configPath, settingsBackup;
        private byte[] configBackup;
        private bool configExisted, saveFailedBackup;
        private OptionsMenuScreen options;
        private KScreen settingsScreen;
        private GameObject blocker;
        private readonly Dictionary<string, BaselineRect> baseline = new Dictionary<string, BaselineRect>();
        private static readonly string[] Roots = { "Left", "BottomPanelLeft", "Bottom Center", "TopRight", "Right",
            "BottomPanelRight", "TopRight - InFrontOfCenter", "BuildWatermark", "RestartWarning" };

        public void Initialize(string directory) { bridge = directory; Directory.CreateDirectory(bridge); }

        private void Update()
        {
            if (running || bridge == null || Time.realtimeSinceStartup < nextPoll) return;
            nextPoll = Time.realtimeSinceStartup + 0.5f;
            string path = Path.Combine(bridge, "request.json");
            if (!File.Exists(path)) return;
            try
            {
                Files.Require(new FileInfo(path).Length < 65536, "Request is too large");
                JObject candidate = JObject.Parse(File.ReadAllText(path));
                string id = (string)candidate["id"];
                Guid parsed;
                Files.Require(Guid.TryParseExact(id, "D", out parsed) && parsed.ToString() == id, "Invalid request ID");
                if (id == lastId) return;
                string response = Path.Combine(bridge, "response.json");
                if (File.Exists(response) && (string)JObject.Parse(File.ReadAllText(response))["id"] == id)
                { lastId = id; return; }
                lastId = id;
                string claims = Path.Combine(bridge, "claims");
                Directory.CreateDirectory(claims);
                string claim = Path.Combine(claims, id);
                Files.Require(!File.Exists(claim), "An interrupted request is already claimed; inspect its recovery record before retrying");
                using (File.Open(claim, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
                request = candidate;
                running = true;
                StartCoroutine(Guard(Run()));
            }
            catch (Exception error)
            {
                Debug.LogWarning("[Interface Extended Test Agent] Request rejected: " + error.Message);
            }
        }

        // Flatten nested iterators so every action/assertion failure reaches the same cleanup path.
        private IEnumerator Guard(IEnumerator scenario)
        {
            Exception failure = null;
            var stack = new Stack<IEnumerator>();
            stack.Push(scenario);
            try
            {
                while (stack.Count > 0)
                {
                    object current = null;
                    bool moved = false;
                    try
                    {
                        moved = stack.Peek().MoveNext();
                        if (moved) current = stack.Peek().Current;
                        else (stack.Pop() as IDisposable)?.Dispose();
                    }
                    catch (Exception error) { failure = error; }
                    if (failure != null) break;
                    if (!moved) continue;
                    IEnumerator nested = current as IEnumerator;
                    if (nested != null) stack.Push(nested);
                    else yield return current;
                }
                if (failure != null)
                {
                    RecordFailure("failure", failure);
                    // Capture yields only frame waits; all capture errors remain inside this guard.
                    IEnumerator capture = Capture("failure-" + steps.Count.ToString("00") + ".png");
                    try
                    {
                        while (true)
                        {
                            object current = null;
                            bool moved = false;
                            try { moved = capture.MoveNext(); if (moved) current = capture.Current; }
                            catch (Exception error) { step["captureError"] = error.Message; break; }
                            if (!moved) { step["screenshot"] = "failure-" + steps.Count.ToString("00") + ".png"; break; }
                            yield return current;
                        }
                    }
                    finally { (capture as IDisposable)?.Dispose(); }
                }
                try { Cleanup(); }
                catch (Exception error)
                {
                    failure = Combine(failure, error);
                    RecordFailure("cleanup", error);
                }
                yield return null;
                if (errors.Count > 0)
                {
                    var error = new InvalidOperationException("Game logged errors during run: " + string.Join(" | ", errors));
                    failure = Combine(failure, error);
                    RecordFailure("game-errors", error);
                }
                WriteTerminal(failure);
            }
            finally
            {
                Application.logMessageReceived -= Log;
                running = false;
                while (stack.Count > 0)
                {
                    try { (stack.Pop() as IDisposable)?.Dispose(); }
                    catch (Exception error) { Debug.LogWarning("[Interface Extended Test Agent] Iterator disposal: " + error.Message); }
                }
            }
        }

        private void OnDisable()
        {
            bool interrupted = running;
            StopAllCoroutines();
            Application.logMessageReceived -= Log;
            if (!interrupted) return;
            running = false;
            Exception failure = new InvalidOperationException("Test agent was disabled before the run finished.");
            try { Cleanup(); }
            catch (Exception error) { failure = Combine(failure, error); }
            RecordFailure("interrupted", failure);
            WriteTerminal(failure);
        }

        private void RecordFailure(string name, Exception error)
        {
            // Failure recording must not depend on a writable status file before cleanup.
            if (step == null || (bool?)step["ok"] == true || name != "failure")
            {
                step = new JObject { ["name"] = name, ["ok"] = false, ["startedUtc"] = Files.Utc() };
                steps.Add(step);
            }
            step["ok"] = false;
            step["error"] = error.ToString();
            step["finishedUtc"] = Files.Utc();
        }

        private void WriteTerminal(Exception failure)
        {
            var writeErrors = new List<Exception>();
            if (output != null)
            {
                if (manifest != null)
                    Attempt(() => Files.Json(Path.Combine(output, "manifest-game.json"), manifest), writeErrors);
                Attempt(() => File.WriteAllLines(Path.Combine(output, "player-log.txt"), logs), writeErrors);
                foreach (Exception error in writeErrors) failure = Combine(failure, error);
                writeErrors.Clear();
                Attempt(() => Files.Json(Path.Combine(output, "results.json"), new { schemaVersion = 1, id = lastId,
                    ok = failure == null, error = failure?.Message, steps }), writeErrors);
                foreach (Exception error in writeErrors) failure = Combine(failure, error);
                writeErrors.Clear();
            }
            Attempt(() => Files.Json(Path.Combine(bridge, "response.json"), new { schemaVersion = 1, id = lastId,
                ok = failure == null, error = failure?.Message,
                resultsPath = output == null ? null : Path.Combine(output, "results.json") }), writeErrors);
            foreach (Exception error in writeErrors)
                Debug.LogWarning("[Interface Extended Test Agent] Terminal response could not be written: " + error.Message);
        }

        private static Exception Combine(Exception previous, Exception next)
        { return previous == null ? next : new AggregateException("Scenario or recovery failed", previous, next); }

        private static void Attempt(System.Action action, List<Exception> failures)
        {
            try { action(); }
            catch (Exception error) { failures.Add(error); }
        }

        private IEnumerator Run()
        {
            steps.Clear(); logs.Clear(); errors.Clear(); baseline.Clear();
            step = null; output = null; manifest = null; settingsBackup = null;
            loaded = pauseOpened = releasedBaselinePause = false;
            Application.logMessageReceived += Log;
            Files.Require((int?)request["schemaVersion"] == 1 && (string)request["command"] == "run", "Unsupported request");
            string profile = (string)request["profile"];
            Files.Require(profile == "default" || profile == "local", "Unknown profile");
            Files.Require(profile == "local" || !(bool)request["allowModDifferenceDialog"], "Dialog gate requires local profile");
            Files.Require(new[] { "none", "wrong-width", "blocked-control" }.Contains((string)request["negativeControl"]), "Unknown negative control");
            string target = (string)request["outputDirectory"];
            Files.Require(Path.IsPathRooted(target) && Directory.Exists(target), "Output directory must exist and be absolute");
            output = Path.GetFullPath(target);
            string save = Path.GetFullPath((string)request["savePath"]);
            Files.Require(save == Path.Combine(output, "fixture", "InterfaceExtended-TestBaseline.sav"), "Only the run's disposable fixture may be loaded");
            Files.Require(string.Equals(Files.Hash(save), (string)request["expectedSaveSha256"], StringComparison.OrdinalIgnoreCase), "Working save hash mismatch");
            Files.Require(Game.Instance == null && MainMenu.Instance != null && MainMenu.Instance.isActiveAndEnabled,
                "Start ONI at the main menu with no colony loaded");
            Files.Require(Convert.ToUInt32(Field(typeof(KleiVersion), "ChangeList").GetRawConstantValue()) == 744825,
                "This prototype requires inspected game build 744825; review the game contract after updating ONI");
            Files.Require(Screen.width == (int)request["expectedViewportWidth"] && Screen.height == (int)request["expectedViewportHeight"], "Unexpected viewport");
            Files.Require(Active<ConfirmDialogScreen>().Length == 0 && Active<OptionsMenuScreen>().Length == 0, "Close existing dialogs first");
            modType = AccessTools.TypeByName("CenteredHud.Mod");
            Files.Require(modType != null, "Interface Extended is not loaded");
            object settings = Field(modType, "Settings").GetValue(null);
            settingsType = settings.GetType();
            configPath = (string)Field(modType, "ConfigPath").GetValue(null);
            configExisted = File.Exists(configPath);
            Files.Require(!File.Exists(Path.Combine(output, "config-recovery.json")), "Run directory already has config recovery data; use a fresh directory");
            configBackup = configExisted ? File.ReadAllBytes(configPath) : null;
            settingsBackup = JsonConvert.SerializeObject(settings);
            saveFailedBackup = (bool)Field(modType, "SaveFailed").GetValue(null);
            // Keep recovery data even if ONI exits before the coroutine can restore the configuration.
            if (configExisted) File.WriteAllBytes(Path.Combine(output, "config-before.bin"), configBackup);
            Files.Json(Path.Combine(output, "config-recovery.json"), new { configPath, configExisted,
                sha256 = configExisted ? Files.Hash(configPath) : null, restored = false });
            manifest = new JObject {
                ["scenarioVersion"] = 1, ["startedUtc"] = Files.Utc(), ["profile"] = profile,
                ["unityVersion"] = Application.unityVersion,
                ["gameBuild"] = BuildWatermark.GetBuildText(),
                ["viewport"] = new JObject { ["width"] = Screen.width, ["height"] = Screen.height },
                ["productionDll"] = modType.Assembly.Location, ["productionDllSha256"] = Files.Hash(modType.Assembly.Location),
                ["agentDllSha256"] = Files.Hash(typeof(Scenario).Assembly.Location),
                ["settingsBefore"] = JObject.Parse(settingsBackup), ["activeMods"] = ActiveMods(),
                ["locale"] = Convert.ToString(Localization.GetLocale()?.Code),
                ["inputMethod"] = "Unity UI raycast and pointer dispatch; numeric field component commit",
                ["modDifferenceDialog"] = false
            };

            Begin("main-menu-settings");
            // Navigation setup uses the native action; controls under test are reached by raycast clicks.
            AccessTools.Method(typeof(MainMenu), "Options").Invoke(MainMenu.Instance, null);
            yield return Wait(() => Active<OptionsMenuScreen>().Length == 1 && Active<OptionsMenuScreen>()[0].IsActive(),
                10, "Native Options did not become ready");
            options = UiDriver.FindActive<OptionsMenuScreen>();
            yield return Stable();
            yield return OpenSettings();
            yield return Finish("main-menu-settings.png");
            Begin("main-menu-close-titlebar");
            KButton titleClose = settingsScreen.transform.Find("Panel").GetComponentsInChildren<KButton>(true)
                .Single(b => AncestorNamed(b.transform, "TitleBar"));
            step["interaction"] = UiDriver.Click(titleClose);
            yield return Wait(() => settingsScreen == null, 5, "Title-bar close did not destroy settings");
            AssertParentVisible();
            yield return Finish("main-menu-options-restored.png");
            CloseOptions(); yield return null;

            Begin("load-baseline");
            SaveGame.Header header;
            SaveGame.GameInfo info = SaveLoader.LoadHeader(save, out header);
            HashSet<string> enable, disable;
            Files.Require(info.dlcIds != null && info.IsCompatableWithCurrentDlcConfiguration(out enable, out disable), "Save DLC configuration is incompatible");
            Files.Require(header.buildVersion <= 744825 && info.saveMajorVersion == 7 && info.saveMinorVersion <= 38,
                "Save version is outside the inspected U59-744825 contract");
            manifest["saveHeader"] = JObject.FromObject(info);
            manifest["saveBuild"] = header.buildVersion;
            AgentMod.SaveMods = null;
            AgentMod.ModEvents = null; AgentMod.ModLoadError = false;
            LoadingOverlay.Load(() => LoadScreen.DoLoad(save));
            float deadline = Time.realtimeSinceStartup + 90;
            bool sawHud = false;
            while (!sawHud)
            {
                Files.Require(Time.realtimeSinceStartup < deadline, "Timed out loading the baseline");
                manifest["modEvents"] = AgentMod.ModEvents;
                Files.Require(!AgentMod.ModLoadError, "Native mod manager reported a mod load error");
                ConfirmDialogScreen[] dialogs = Active<ConfirmDialogScreen>();
                if (dialogs.Length > 0)
                {
                    Files.Require(dialogs.Length == 1, "Multiple load dialogs");
                    ConfirmDialogScreen dialog = dialogs[0];
                    string title = ((LocText)Field(typeof(ConfirmDialogScreen), "titleText").GetValue(dialog)).text;
                    Files.Require(title == STRINGS.UI.FRONTEND.MOD_DIALOGS.SAVE_GAME_MODS_DIFFER.TITLE.ToString(), "Unknown load dialog: " + title);
                    manifest["modDifferenceDialog"] = true;
                    manifest["modDifferenceMessage"] = ((LocText)Field(typeof(ConfirmDialogScreen), "popupMessage").GetValue(dialog)).text;
                    manifest["savedMods"] = AgentMod.SaveMods;
                    yield return Capture("mod-differences.png");
                    step["dialogScreenshot"] = "mod-differences.png";
                    Files.Require(profile == "local" && (bool)request["allowModDifferenceDialog"], "Mod differences require explicit local-profile gate");
                    foreach (string callback in new[] { "confirmAction", "cancelAction", "configurableAction", "onDeactivateCB" })
                        Files.Require(Field(typeof(ConfirmDialogScreen), callback).GetValue(dialog) == null, "Dialog has an unexpected action: " + callback);
                    foreach (string button in new[] { "cancelButton", "configurableButton" })
                        Files.Require(!((GameObject)Field(typeof(ConfirmDialogScreen), button).GetValue(dialog)).activeSelf, "Unexpected dialog action");
                    step["dialogInteraction"] = UiDriver.Click(((GameObject)Field(typeof(ConfirmDialogScreen), "confirmButton").GetValue(dialog)).GetComponent<KButton>());
                    yield return Wait(() => dialog == null || !dialog.isActiveAndEnabled, 5, "Mod-difference dialog did not close");
                }
                sawHud = Game.Instance != null && GameScreenManager.Instance != null && SpeedControlScreen.Instance != null
                    && PauseScreen.Instance != null && GameScreenManager.Instance.ssOverlayCanvas != null;
                yield return null;
            }
            loaded = true;
            Files.Require(Path.GetFullPath(SaveLoader.GetActiveSaveFilePath()) == save, "Game loaded another save");
            manifest["savedMods"] = AgentMod.SaveMods;
            manifest["activeMods"] = ActiveMods();
            manifest["canvasScale"] = Canvas.scaleFactor;
            manifest["settingsCoordinate"] = CustomGameSettings.Instance.GetSettingsCoordinate();
            yield return Stable();
            Files.Require(Active<ConfirmDialogScreen>().Length == 0, "A late load dialog is still active");
            Files.Require(PauseCount == 1, "Baseline must be paused with exactly one pause owner");
            yield return Finish("loaded-baseline.png");

            Begin("full-width");
            yield return OpenFromColony();
            Click("Panel/Layout/FullWidth"); yield return Stable();
            AssertConfig(0);
            foreach (string name in Roots) baseline.Add(name, new BaselineRect(Root(name)));
            step["baselineRoots"] = new JObject(Roots.Select(name => new JProperty(name, UiDriver.Rect(Root(name)))));
            yield return Finish("full-width.png");
            Begin("hud-full-width");
            yield return CloseColonyMenus(); AssertLayout(0);
            yield return Finish("hud-full-width.png");
            foreach (int width in new[] { 2560, 3360 })
            {
                Begin("preset-" + width);
                yield return OpenFromColony();
                if ((string)request["negativeControl"] == "blocked-control" && width == 2560)
                {
                    blocker = UiDriver.CreateBlocker(settingsScreen.transform);
                    yield return Stable();
                }
                Click("Panel/Layout/Preset" + width); yield return Stable();
                AssertConfig(width);
                AssertLayout(width + ((string)request["negativeControl"] == "wrong-width" && width == 2560 ? 80 : 0));
                yield return Finish("preset-" + width + ".png");
                Begin("hud-" + width);
                yield return CloseColonyMenus(); AssertLayout(width);
                yield return Finish("hud-" + width + ".png");
            }
            Begin("restore-full-width");
            yield return OpenFromColony();
            Click("Panel/Layout/FullWidth"); yield return Stable(); AssertConfig(0); AssertLayout(0);
            yield return Finish("restored-full-width.png");
            Begin("numeric-width");
            TMP_InputField input = settingsScreen.transform.Find("Panel/Layout/Width").GetComponent<TMP_InputField>();
            step["interaction"] = UiDriver.ClickField(input);
            yield return Wait(() => input.isFocused, 5, "Numeric field did not receive focus");
            input.text = "2720";
            // Component commit, intentionally not claimed as hardware keyboard input coverage.
            input.onEndEdit.Invoke(input.text);
            input.DeactivateInputField();
            yield return Stable();
            AssertConfig(2720); AssertLayout(2720);
            var text = input.textComponent;
            text.ForceMeshUpdate();
            Vector3 glyphMin = input.textViewport.InverseTransformPoint(text.rectTransform.TransformPoint(text.textBounds.min));
            Vector3 glyphMax = input.textViewport.InverseTransformPoint(text.rectTransform.TransformPoint(text.textBounds.max));
            UnityEngine.Rect viewportRect = input.textViewport.rect;
            step["inputTextBounds"] = new JObject { ["value"] = input.text, ["renderText"] = text.text,
                ["glyphMinX"] = glyphMin.x, ["glyphMinY"] = glyphMin.y, ["glyphMaxX"] = glyphMax.x, ["glyphMaxY"] = glyphMax.y,
                ["viewportMinX"] = viewportRect.xMin, ["viewportMinY"] = viewportRect.yMin,
                ["viewportMaxX"] = viewportRect.xMax, ["viewportMaxY"] = viewportRect.yMax };
            // TMP appends a zero-width sentinel to its display text; it is not part of the entered value.
            Files.Require(input.text == "2720" && text.text.TrimEnd('\u200b') == "2720" && text.textBounds.size.x > 0 && text.textBounds.size.y > 0 &&
                glyphMin.x >= viewportRect.xMin - 1 && glyphMin.y >= viewportRect.yMin - 1 &&
                glyphMax.x <= viewportRect.xMax + 1 && glyphMax.y <= viewportRect.yMax + 1,
                "Numeric glyph bounds are outside the input viewport or not rendered");
            yield return Finish("numeric-width.png");

            Begin("minus-from-full-width");
            Click("Panel/Layout/FullWidth"); yield return Stable();
            Click("Panel/Layout/DecreaseWidth"); yield return Stable();
            int stepped = (int)Math.Round(Math.Max(Math.Min(Screen.width, 1280 * Canvas.scaleFactor), Screen.width - 160));
            AssertConfig(stepped); AssertLayout(stepped);
            yield return Finish("minus-from-full-width.png");
            Begin("plus-to-full-width");
            Click("Panel/Layout/IncreaseWidth"); yield return Stable();
            AssertConfig(Screen.width); AssertLayout(Screen.width);
            yield return Finish("plus-to-full-width.png");

            Begin("guides-on");
            Click("Panel/Layout/Preset2560"); yield return Stable();
            bool before = (bool)Settings()["ShowGuides"];
            if (before) { ClickToggle("Panel/Layout/Guides"); yield return Stable(); }
            ClickToggle("Panel/Layout/Guides"); yield return Stable();
            AssertGuides(true); AssertLayout(2560);
            yield return Finish("guides-on.png");
            Begin("guides-off");
            ClickToggle("Panel/Layout/Guides"); yield return Stable(); AssertGuides(false);
            yield return Finish("guides-off.png");
            Begin("shortcut-setting");
            bool shortcutsBefore = (bool)Settings()["EnableShortcuts"];
            ClickToggle("Panel/Controls/Shortcuts"); yield return Stable();
            Files.Require((bool)Settings()["EnableShortcuts"] != shortcutsBefore, "Shortcut toggle did not change");
            ClickToggle("Panel/Controls/Shortcuts"); yield return Stable();
            Files.Require((bool)Settings()["EnableShortcuts"] == shortcutsBefore, "Shortcut toggle did not restore");
            AssertPersisted();
            yield return Finish("shortcut-setting.png");

            Begin("close-paused");
            yield return CloseColonyMenus();
            Files.Require(PauseCount == 1, "Paused colony lost or leaked a pause owner");
            AssertLayout(2560);
            yield return Finish("hud-2560-paused.png");
            Begin("reopen-paused");
            yield return OpenFromColony(); yield return CloseColonyMenus();
            Files.Require(PauseCount == 1, "Repeated opening leaked a pause owner"); AssertLayout(2560);
            yield return Finish("reopen-paused.png");
            Begin("open-close-playing");
            SpeedControlScreen.Instance.Unpause(false); releasedBaselinePause = true;
            Files.Require(PauseCount == 0, "Could not establish playing case");
            yield return OpenFromColony(); yield return CloseColonyMenus();
            Files.Require(PauseCount == 0, "Playing colony remained paused after closing menus");
            SpeedControlScreen.Instance.Pause(false); releasedBaselinePause = false;
            yield return Finish("playing-case-restored-to-pause.png");
            Files.Require(errors.Count == 0, "Game logged errors during run: " + string.Join(" | ", errors));
            Begin("restore-original-settings");
            RestoreSettings(); yield return Stable();
            Files.Require(JsonConvert.SerializeObject(Field(modType, "Settings").GetValue(null)) == settingsBackup, "Settings restoration mismatch");
            Files.Require(File.Exists(configPath) == configExisted && (!configExisted || File.ReadAllBytes(configPath).SequenceEqual(configBackup)), "Config restoration mismatch");
            manifest["finishedUtc"] = Files.Utc();
            yield return Finish("restored-original-settings.png");
        }

        private IEnumerator OpenSettings()
        {
            KButton entry = UiDriver.MenuButton(options, b => Equals(b.userData, "InterfaceExtended.Options"));
            step["openInteraction"] = UiDriver.Click(entry);
            yield return Wait(() => ActiveSettings().Length == 1 && ActiveSettings()[0].IsActive(),
                5, "Expected exactly one ready Interface Extended window");
            settingsScreen = ActiveSettings().Single();
            yield return Stable();
        }

        private IEnumerator OpenFromColony()
        {
            Files.Require(!pauseOpened && Active<OptionsMenuScreen>().Length == 0 && ActiveSettings().Length == 0, "Unexpected pre-existing menus");
            PauseScreen.Instance.Show(true); pauseOpened = true;
            yield return Stable();
            step["pauseOptionsInteraction"] = UiDriver.Click(UiDriver.MenuButton(PauseScreen.Instance, b => b.onClick?.Method.Name == "OnOptions"));
            yield return Wait(() => Active<OptionsMenuScreen>().Length == 1 && Active<OptionsMenuScreen>()[0].IsActive(),
                5, "Colony Options did not become ready");
            options = UiDriver.FindActive<OptionsMenuScreen>();
            yield return Stable();
            yield return OpenSettings();
        }

        private IEnumerator CloseColonyMenus()
        {
            Click("Panel/Done");
            yield return Wait(() => settingsScreen == null, 5, "Done did not close settings");
            AssertParentVisible(); CloseOptions(); yield return Stable();
            step["resumeInteraction"] = UiDriver.Click(UiDriver.MenuButton(PauseScreen.Instance, b => b.onClick?.Method.Name == "OnResume"));
            pauseOpened = false; yield return Stable();
            Files.Require(ActiveSettings().Length == 0 && Active<OptionsMenuScreen>().Length == 0, "A settings modal remains active");
        }

        private void CloseOptions()
        {
            step["closeOptionsInteraction"] = UiDriver.Click((KButton)Field(typeof(OptionsMenuScreen), "closeButton").GetValue(options));
        }

        private void AssertParentVisible()
        {
            GameObject panel = (GameObject)Field(typeof(KModalButtonMenu), "panelRoot").GetValue(options);
            Files.Require(panel.activeInHierarchy && panel.transform.lossyScale.x > 0 && panel.transform.lossyScale.y > 0, "Parent Options panel was not restored");
        }

        private void Click(string path) { step["interaction"] = UiDriver.Click(settingsScreen.transform.Find(path).GetComponent<KButton>()); }
        private void ClickToggle(string path) { step["interaction"] = UiDriver.Click(settingsScreen.transform.Find(path).GetComponentInChildren<KButton>(true)); }
        private Canvas Canvas { get { return GameScreenManager.Instance.ssOverlayCanvas.GetComponent<Canvas>(); } }
        private RectTransform Root(string name)
        {
            Transform found = GameScreenManager.Instance.ssOverlayCanvas.transform.Find(name);
            Files.Require(found != null, "Missing HUD root: " + name);
            return found.GetComponent<RectTransform>();
        }
        private int PauseCount { get { return (int)Field(typeof(SpeedControlScreen), "pauseCount").GetValue(SpeedControlScreen.Instance); } }
        private JObject Settings() { return JObject.FromObject(Field(modType, "Settings").GetValue(null)); }
        private void AssertConfig(int width)
        {
            JObject settings = Settings();
            Files.Require((int)settings["WidthPixels"] == width && (bool)settings["Enabled"] == (width > 0), "Requested width/mode mismatch");
            AssertPersisted();
        }
        private void AssertPersisted()
        {
            Files.Require(!(bool)Field(modType, "SaveFailed").GetValue(null), "Production mod reports a save error");
            Files.Require(JToken.DeepEquals(JObject.Parse(File.ReadAllText(configPath)), Settings()), "In-memory settings differ from persisted config");
        }
        private void AssertLayout(int width)
        {
            float viewport = Canvas.pixelRect.width;
            Files.Require(Math.Abs(viewport - Screen.width) < 1, "Overlay viewport differs from the screen");
            double expectedWidth = width == 0 ? viewport : Math.Min(viewport, Math.Max(width, Math.Min(viewport, 1280 * Canvas.scaleFactor)));
            double fraction = expectedWidth / viewport, margin = (1 - fraction) / 2;
            var measured = new JObject();
            foreach (var item in baseline)
            {
                RectTransform rect = Root(item.Key);
                BaselineRect original = item.Value;
                Near(rect.anchorMin.x, margin + fraction * original.Min.x, 0.00001, item.Key + " left anchor");
                Near(rect.anchorMax.x, margin + fraction * original.Max.x, 0.00001, item.Key + " right anchor");
                Near(rect.anchorMin.y, original.Min.y, 0.00001, item.Key + " bottom anchor");
                Near(rect.anchorMax.y, original.Max.y, 0.00001, item.Key + " top anchor");
                Near(Vector2.Distance(rect.sizeDelta, original.Size), 0, 0.01, item.Key + " sizeDelta");
                Near(Vector2.Distance(rect.anchoredPosition, original.Position), 0, 0.01, item.Key + " anchoredPosition");
                Vector2[] corners = ScreenCorners(rect);
                Near(corners[0].x, original.Corners[0].x + viewport * margin * (1 - 2 * original.Min.x), 1, item.Key + " left screen edge");
                Near(corners[2].x, original.Corners[2].x + viewport * margin * (1 - 2 * original.Max.x), 1, item.Key + " right screen edge");
                measured[item.Key] = UiDriver.Rect(rect);
            }
            step["measurements"] = new JObject { ["requestedWidth"] = width, ["expectedEffectiveWidth"] = expectedWidth,
                ["marginPixels"] = viewport * margin, ["canvasScale"] = Canvas.scaleFactor, ["roots"] = measured, ["pauseCount"] = PauseCount };
            Files.Json(Path.Combine(output, "layout-" + steps.Count.ToString("00") + ".json"), step["measurements"]);
        }
        private void AssertGuides(bool visible)
        {
            Files.Require((bool)Settings()["ShowGuides"] == visible, "Guide setting mismatch"); AssertPersisted();
            foreach (string name in new[] { "CenteredHudLeftGuide", "CenteredHudRightGuide" })
            {
                var rect = Root(name);
                Files.Require(rect.gameObject.activeSelf == visible && !rect.GetComponent<Image>().raycastTarget, "Guide visibility/raycast mismatch");
                if (visible)
                {
                    double fraction = Math.Min(Canvas.pixelRect.width, Math.Max(2560, Math.Min(Canvas.pixelRect.width, 1280 * Canvas.scaleFactor))) / Canvas.pixelRect.width;
                    Near(rect.anchorMin.x, name.Contains("Left") ? (1 - fraction) / 2 : (1 + fraction) / 2, 0.00001, name + " position");
                }
            }
        }
        private IEnumerator Stable()
        {
            string previous = null; int stable = 0;
            float deadline = Time.realtimeSinceStartup + 10;
            while (stable < 3)
            {
                yield return new WaitForEndOfFrame();
                Files.Require(Time.realtimeSinceStartup < deadline, "Layout did not settle");
                Canvas.ForceUpdateCanvases();
                string signature = loaded ? string.Join("|", Roots.Select(n => JsonConvert.SerializeObject(UiDriver.Rect(Root(n)))))
                    : Screen.width + "x" + Screen.height;
                stable = signature == previous ? stable + 1 : 0; previous = signature;
            }
        }
        private static IEnumerator Wait(Func<bool> predicate, float seconds, string error)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!predicate()) { Files.Require(Time.realtimeSinceStartup < deadline, error); yield return null; }
        }
        private void Begin(string name)
        {
            step = new JObject { ["name"] = name, ["ok"] = false, ["startedUtc"] = Files.Utc() };
            steps.Add(step);
            Files.Json(Path.Combine(bridge, "status.json"), new { id = lastId, step = name, message = "Running " + name });
        }
        private IEnumerator Finish(string screenshot)
        {
            yield return Capture(screenshot);
            step["screenshot"] = screenshot; step["finishedUtc"] = Files.Utc(); step["ok"] = true;
        }
        private IEnumerator Capture(string name)
        {
            Files.Require(output != null, "No validated output directory for capture");
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(output, name);
            if (File.Exists(path)) File.Delete(path);
            int expectedWidth = Screen.width, expectedHeight = Screen.height;
            ScreenCapture.CaptureScreenshot(path);
            float deadline = Time.realtimeSinceStartup + 10;
            long previousLength = -1;
            byte[] complete = null;
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            byte[] end = { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
            while (complete == null)
            {
                yield return null;
                Files.Require(Time.realtimeSinceStartup < deadline, "Screenshot was not completely written");
                try
                {
                    if (!File.Exists(path)) continue;
                    long length = new FileInfo(path).Length;
                    bool stable = length > 32 && length == previousLength;
                    previousLength = length;
                    if (!stable) continue;
                    byte[] candidate = File.ReadAllBytes(path);
                    if (candidate.LongLength == length && candidate.Take(signature.Length).SequenceEqual(signature) &&
                        candidate.Skip(candidate.Length - end.Length).SequenceEqual(end)) complete = candidate;
                }
                catch (IOException) { previousLength = -1; }
            }
            // Decode only a complete PNG; the external runner also verifies chunk CRCs and zlib.
            var texture = new Texture2D(2, 2);
            try
            {
                Files.Require(ImageConversion.LoadImage(texture, complete), "Screenshot is not decodable");
                Files.Require(texture.width == expectedWidth && texture.height == expectedHeight, "Screenshot size mismatch");
            }
            finally { Destroy(texture); }
        }
        private void RestoreSettings()
        {
            if (settingsBackup == null) return;
            try
            {
                Field(modType, "Settings").SetValue(null, JsonConvert.DeserializeObject(settingsBackup, settingsType));
                Field(modType, "SaveFailed").SetValue(null, saveFailedBackup);
            }
            finally
            {
                if (configExisted)
                {
                    string temporary = configPath + ".test-agent-restore.tmp";
                    File.WriteAllBytes(temporary, configBackup);
                    if (File.Exists(configPath)) File.Replace(temporary, configPath, null);
                    else File.Move(temporary, configPath);
                }
                else if (File.Exists(configPath)) File.Delete(configPath);
                Files.Json(Path.Combine(output, "config-recovery.json"), new { configPath, configExisted,
                    sha256 = configExisted ? Files.Hash(configPath) : null, restored = true });
            }
        }
        private void Cleanup()
        {
            var failures = new List<Exception>();
            try
            {
                Attempt(() => { if (blocker != null) Destroy(blocker); blocker = null; }, failures);
                Attempt(() => { if (settingsScreen != null && settingsScreen.IsActive()) settingsScreen.Deactivate(); settingsScreen = null; }, failures);
                Attempt(() => { if (options != null && options.IsActive()) options.Deactivate(); options = null; }, failures);
                Attempt(() => {
                    if (pauseOpened && PauseScreen.Instance != null) PauseScreen.Instance.Show(false);
                    pauseOpened = false;
                }, failures);
                Attempt(() => {
                    if (releasedBaselinePause && SpeedControlScreen.Instance != null) SpeedControlScreen.Instance.Pause(false);
                    releasedBaselinePause = false;
                }, failures);
            }
            finally { Attempt(RestoreSettings, failures); }
            if (failures.Count > 0) throw new AggregateException("Agent cleanup could not fully restore the session", failures);
        }
        private void Log(string message, string trace, LogType type)
        {
            logs.Add(Files.Utc() + " [" + type + "] " + message + (string.IsNullOrEmpty(trace) ? "" : "\n" + trace));
            bool layoutFailure = type == LogType.Warning && message != null &&
                message.StartsWith("[CenteredHud] Restoring original HUD after layout error:", StringComparison.Ordinal);
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert || layoutFailure)
                errors.Add(message);
        }
        private static FieldInfo Field(Type type, string name)
        {
            FieldInfo field = AccessTools.Field(type, name);
            Files.Require(field != null, "Missing game/mod field: " + type.Name + "." + name); return field;
        }
        private static T[] Active<T>() where T : Behaviour
        { return Resources.FindObjectsOfTypeAll<T>().Where(c => c != null && c.gameObject.scene.IsValid() && c.isActiveAndEnabled && c.gameObject.activeInHierarchy).ToArray(); }
        private static KScreen[] ActiveSettings()
        { return Active<KScreen>().Where(s => s.GetType().FullName == "CenteredHud.HudSettingsScreen").ToArray(); }
        private static bool AncestorNamed(Transform transform, string name)
        {
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
                if (parent.name == name || parent.name == name + "(Clone)") return true;
            return false;
        }
        private static JArray ActiveMods()
        {
            return new JArray(Global.Instance.modManager.mods.Where(m => m.IsEnabledForActiveDlc()).Select(m =>
                new JObject { ["label"] = JObject.FromObject(m.label), ["staticID"] = m.staticID }));
        }
        private static void Near(double actual, double expected, double tolerance, string label)
        { Files.Require(!double.IsNaN(actual) && Math.Abs(actual - expected) <= tolerance, label + ": actual " + actual + ", expected " + expected); }
        private static Vector2[] ScreenCorners(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Canvas canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return corners.Select(p => RectTransformUtility.WorldToScreenPoint(camera, p)).ToArray();
        }
        private sealed class BaselineRect
        {
            internal readonly Vector2 Min, Max, Size, Position;
            internal readonly Vector2[] Corners;
            internal BaselineRect(RectTransform rect)
            { Min = rect.anchorMin; Max = rect.anchorMax; Size = rect.sizeDelta; Position = rect.anchoredPosition; Corners = ScreenCorners(rect); }
        }
    }
}
