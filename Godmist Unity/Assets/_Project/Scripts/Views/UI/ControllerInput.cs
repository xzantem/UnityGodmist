using UnityEngine;
using UnityEngine.InputSystem;

namespace Godmist.Views.UI
{
    /// <summary>
    /// Stateless access to the explicitly configured project action asset. Context/lifecycle
    /// belongs to ControllerInputView; gameplay systems never depend on this adapter.
    /// </summary>
    public static class ControllerInput
    {
        public const string WorldMap = "GodmistWorld";
        public const string UiMap = "GodmistUI";
        public const string GlobalMap = "GodmistController";

        public static InputAction Action(string map, string name) => Action(InputSystem.actions, map, name);
        public static InputAction Action(InputActionAsset asset, string map, string name)
        {
            if (map == WorldMap || map == UiMap)
            {
                var shared = name switch
                {
                    "Confirm" or "Cancel" or "Context" or "Next" or "Previous" or "Skills" or "Map" or "Center" or "Cursor" or "NextTarget" or "PreviousTarget" => name,
                    "CompleteTargets" or "Details" => "Auxiliary",
                    "Rotate" => "Next",
                    "RotateBack" => "Previous",
                    "StackModifier" => "RightTrigger",
                    "Alternate" => "Cursor",
                    _ => null
                };
                if (shared != null) return asset?.FindAction(GlobalMap + "/" + shared, false);
            }
            return asset?.FindAction(map + "/" + name, false);
        }
        private static bool Enabled(InputActionAsset asset, string map) => asset?.FindActionMap(map, false)?.enabled == true;
        public static bool Pressed(string map, string name)
            => Pressed(InputSystem.actions, map, name);
        public static bool Pressed(InputActionAsset asset, string map, string name)
        {
            var action = Action(asset, map, name);
            return Enabled(asset, map) && action is { enabled: true } && action.WasPressedThisFrame();
        }
        public static bool Held(string map, string name) => Enabled(InputSystem.actions, map) && Action(map, name) is { enabled: true } action && action.IsPressed();
        public static Vector2 Vector(string map, string name) => Action(map, name) is { enabled: true } action ? action.ReadValue<Vector2>() : Vector2.zero;
        public static bool Active => InputSystem.actions?.FindActionMap(WorldMap, false)?.enabled == true
                                  || InputSystem.actions?.FindActionMap(UiMap, false)?.enabled == true;
        public static bool WorldActive => InputSystem.actions?.FindActionMap(WorldMap, false)?.enabled == true;
        public static bool UiActive => InputSystem.actions?.FindActionMap(UiMap, false)?.enabled == true;
        public static bool PointerPressed => UiActive && PrecisePointer && Action("UI", "Click") is { enabled: true } click && click.WasPressedThisFrame();
        public static bool PointerSecondaryPressed => UiActive && PrecisePointer && Action("UI", "RightClick") is { enabled: true } click && click.WasPressedThisFrame();
        public static bool Confirm => Action(WorldMap, "DirectInteraction")?.enabled != true && Pressed(WorldMap, "Confirm");
        public static bool Context => Pressed(WorldMap, "Context");
        public static bool Cancel => Pressed(WorldMap, "Cancel");
        public static bool Rotate => Pressed(WorldMap, "Rotate");
        public static bool RotateBack => Pressed(WorldMap, "RotateBack");
        public static bool CompleteTargets => Pressed(WorldMap, "CompleteTargets");
        public static bool PrecisePointer => Active && Action(WorldActive ? WorldMap : UiMap, "PointerMove") is { enabled: true };
        public static bool Alternate => WorldActive && PrecisePointer || Held(UiMap, "StackModifier");
        public static Vector2 Movement => WorldActive && !PrecisePointer ? Vector(WorldMap, "Move") : Vector2.zero;
        public static Vector2 Camera => Vector(WorldMap, "Camera");
        public static float Zoom => Action(WorldMap, "Zoom") is { enabled: true } zoom ? zoom.ReadValue<float>() : 0;
        public static Vector3 PointerPosition => Active && Action("UI", "Point") is { enabled: true } point
            ? (Vector3)point.ReadValue<Vector2>() : Input.mousePosition;
    }

    /// <summary>A hold must start in its owning context; carrying a held button across contexts cannot arm it.</summary>
    public sealed class ControllerHoldGesture
    {
        private bool armed;
        private float elapsed;
        public void Reset() { armed = false; elapsed = 0; }
        public bool Tick(bool eligible, bool pressed, bool held, float delta, float duration = .65f)
        {
            if (!eligible || !held) { Reset(); return false; }
            if (pressed) { armed = true; elapsed = 0; }
            if (!armed) return false;
            elapsed += Mathf.Max(0, delta);
            if (elapsed < duration) return false;
            Reset();
            return true;
        }
    }
}
