using System;
using System.Collections.Generic;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InterfaceExtended.TestAgent
{
    internal static class UiDriver
    {
        internal static T FindActive<T>() where T : Component
        {
            T match = null;
            int count = 0;
            foreach (T component in Resources.FindObjectsOfTypeAll<T>())
            {
                if (!component.gameObject.scene.IsValid() || !component.gameObject.activeInHierarchy) continue;
                Behaviour behaviour = component as Behaviour;
                if (behaviour != null && !behaviour.isActiveAndEnabled) continue;
                match = component;
                count++;
            }
            if (count != 1)
                throw new InvalidOperationException("Expected one active " + typeof(T).FullName + "; found " + count + ".");
            return match;
        }

        internal static KButton MenuButton(KButtonMenu menu, Func<KButtonMenu.ButtonInfo, bool> predicate)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            var field = AccessTools.Field(typeof(KButtonMenu), "buttons");
            var entries = field != null ? field.GetValue(menu) as IList<KButtonMenu.ButtonInfo> : null;
            if (entries == null) throw new InvalidOperationException("Native menu button list is unavailable: " + Path(menu.transform));
            KButtonMenu.ButtonInfo match = null;
            int count = 0;
            foreach (KButtonMenu.ButtonInfo entry in entries)
            {
                if (entry == null || !predicate(entry)) continue;
                match = entry;
                count++;
            }
            if (count != 1)
                throw new InvalidOperationException("Expected one matching button in " + Path(menu.transform) + "; found " + count + ".");
            if (match.uibutton == null)
                throw new InvalidOperationException("Native menu has not created its matching button: " + Path(menu.transform));
            return match.uibutton;
        }

        internal static JObject Click(KButton button)
        {
            if (button == null) throw new ArgumentNullException(nameof(button));
            if (!button.isInteractable) throw new InvalidOperationException("Button is not interactable: " + Path(button.transform));
            return ClickTarget(button);
        }

        internal static JObject ClickField(TMP_InputField field)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            if (!field.IsInteractable()) throw new InvalidOperationException("Input is not interactable: " + Path(field.transform));
            return ClickTarget(field);
        }

        private static JObject ClickTarget(Behaviour target)
        {
            if (!KInputManager.isFocused) throw new InvalidOperationException("ONI does not have input focus.");
            if (!target.isActiveAndEnabled) throw new InvalidOperationException("Target is inactive: " + Path(target.transform));
            UnityEngine.EventSystems.EventSystem events = UnityEngine.EventSystems.EventSystem.current;
            if (events == null || !events.isActiveAndEnabled) throw new InvalidOperationException("No active Unity EventSystem.");
            RectTransform rect = target.transform as RectTransform;
            if (rect == null || rect.rect.width <= 0 || rect.rect.height <= 0)
                throw new InvalidOperationException("Target has no usable rectangle: " + Path(target.transform));
            Camera camera = CameraFor(rect);
            Vector3 center = rect.TransformPoint(rect.rect.center);
            if (camera != null && camera.WorldToScreenPoint(center).z <= 0)
                throw new InvalidOperationException("Target is behind its UI camera: " + Path(rect));
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, center);
            if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.x) || float.IsInfinity(point.y) ||
                point.x < 0 || point.y < 0 || point.x >= Screen.width || point.y >= Screen.height)
                throw new InvalidOperationException("Target center is outside the viewport: " + Path(rect));

            var pointer = new PointerEventData(events) {
                pointerId = -1,
                button = PointerEventData.InputButton.Left,
                position = point,
                pressPosition = point,
                clickCount = 1,
                clickTime = Time.unscaledTime,
                eligibleForClick = true,
                useDragThreshold = true
            };
            var hits = new List<RaycastResult>();
            events.RaycastAll(pointer, hits);
            if (hits.Count == 0) throw new InvalidOperationException("Nothing receives input at " + Path(rect));
            RaycastResult hit = hits[0];
            GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject);
            if (handler != target.gameObject)
                throw new InvalidOperationException("Target " + Path(rect) + " is blocked by " + Path(hit.gameObject.transform) +
                    "; click handler: " + (handler != null ? Path(handler.transform) : "none"));

            var result = new JObject {
                ["target"] = Path(rect),
                ["hit"] = Path(hit.gameObject.transform),
                ["handler"] = Path(handler.transform),
                ["point"] = Point(point),
                ["hitCount"] = hits.Count,
                ["rect"] = Rect(rect)
            };
            pointer.pointerCurrentRaycast = hit;
            pointer.pointerPressRaycast = hit;
            pointer.pointerEnter = hit.gameObject;
            pointer.rawPointerPress = hit.gameObject;
            pointer.pointerPress = target.gameObject;
            // Match Unity's selection change before a press, committing a previously edited input.
            GameObject selectable = ExecuteEvents.GetEventHandler<ISelectHandler>(hit.gameObject);
            if (selectable != events.currentSelectedGameObject) events.SetSelectedGameObject(null, pointer);
            ExecuteEvents.ExecuteHierarchy(hit.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            GameObject pressed = ExecuteEvents.ExecuteHierarchy(hit.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            if (pressed != target.gameObject)
                throw new InvalidOperationException("Pointer-down handler differs from target: " + Path(rect));
            if (target == null || !target.isActiveAndEnabled)
                throw new InvalidOperationException("Target disappeared during pointer down: " + (string)result["target"]);
            ExecuteEvents.Execute(target.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            return result;
        }

        internal static string Path(Transform transform)
        {
            if (transform == null) return null;
            var names = new List<string>();
            for (Transform item = transform; item != null; item = item.parent) names.Add(item.name);
            names.Reverse();
            return "/" + string.Join("/", names);
        }

        internal static JObject Rect(RectTransform rect)
        {
            if (rect == null) throw new ArgumentNullException(nameof(rect));
            Camera camera = CameraFor(rect);
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var worldCorners = new JArray();
            var screenCorners = new JArray();
            foreach (Vector3 corner in corners)
            {
                worldCorners.Add(new JObject { ["x"] = corner.x, ["y"] = corner.y, ["z"] = corner.z });
                screenCorners.Add(Point(RectTransformUtility.WorldToScreenPoint(camera, corner)));
            }
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Canvas root = canvas != null ? canvas.rootCanvas : null;
            return new JObject {
                ["path"] = Path(rect),
                ["screenCorners"] = screenCorners,
                ["worldCorners"] = worldCorners,
                ["anchorMin"] = Point(rect.anchorMin),
                ["anchorMax"] = Point(rect.anchorMax),
                ["sizeDelta"] = Point(rect.sizeDelta),
                ["anchoredPosition"] = Point(rect.anchoredPosition),
                ["pivot"] = Point(rect.pivot),
                ["size"] = Point(rect.rect.size),
                ["canvas"] = root != null ? Path(root.transform) : null,
                ["renderMode"] = root != null ? root.renderMode.ToString() : null,
                ["camera"] = camera != null ? camera.name : null
            };
        }

        private static Camera CameraFor(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            if (canvas == null) throw new InvalidOperationException("UI target has no Canvas: " + Path(rect));
            Canvas root = canvas.rootCanvas;
            if (!canvas.isActiveAndEnabled || !root.isActiveAndEnabled)
                throw new InvalidOperationException("UI target Canvas is inactive: " + Path(rect));
            if (root.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            Camera camera = canvas.worldCamera != null ? canvas.worldCamera : root.worldCamera;
            if (camera == null && root.renderMode == RenderMode.WorldSpace) camera = Camera.main;
            return camera;
        }

        private static JObject Point(Vector2 point)
        {
            return new JObject { ["x"] = point.x, ["y"] = point.y };
        }

        internal static GameObject CreateBlocker(Transform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var blocker = new GameObject("InterfaceExtendedTestBlocker", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasRenderer), typeof(Image), typeof(GraphicRaycaster));
            blocker.layer = parent.gameObject.layer;
            RectTransform rect = blocker.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Canvas canvas = blocker.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = short.MaxValue;
            Image image = blocker.GetComponent<Image>();
            image.color = new Color(1, 0, 0, 0.15f);
            image.raycastTarget = true;
            rect.SetAsLastSibling();
            return blocker;
        }
    }
}
