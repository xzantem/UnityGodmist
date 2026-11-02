using System;
using UnityEngine;

namespace Godmist.Views.Exploration
{
    public static class ExplorationKeyboardInput
    {
        private static readonly Func<KeyCode, bool> ReadKey = Input.GetKey;

        // Legacy Horizontal/Vertical axes also include connected joysticks and wheels.
        public static Vector2 ReadMovement() => ReadMovement(ReadKey);
        public static Vector2 ReadGameplayMovement(bool followingLeader = true) => Vector2.ClampMagnitude((followingLeader ? ReadMovement() : Vector2.zero)
            + (Godmist.Views.UI.ControllerInput.Alternate ? Vector2.zero : Godmist.Views.UI.ControllerInput.Movement), 1f);

        public static Vector2 ReadMovement(Func<KeyCode, bool> isPressed)
        {
            var right = isPressed(KeyCode.D) || isPressed(KeyCode.RightArrow);
            var left = isPressed(KeyCode.A) || isPressed(KeyCode.LeftArrow);
            var up = isPressed(KeyCode.W) || isPressed(KeyCode.UpArrow);
            var down = isPressed(KeyCode.S) || isPressed(KeyCode.DownArrow);
            return Vector2.ClampMagnitude(new Vector2(
                (right ? 1f : 0f) - (left ? 1f : 0f),
                (up ? 1f : 0f) - (down ? 1f : 0f)), 1f);
        }
    }
}
