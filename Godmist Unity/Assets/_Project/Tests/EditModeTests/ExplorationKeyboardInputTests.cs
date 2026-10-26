using System.Collections.Generic;
using Godmist.Views.Exploration;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class ExplorationKeyboardInputTests
    {
        private static Vector2 Read(params KeyCode[] keys)
        {
            var pressed = new HashSet<KeyCode>(keys);
            return ExplorationKeyboardInput.ReadMovement(pressed.Contains);
        }

        [Test]
        public void NoKeyboardMovementIgnoresUnrelatedAndJoystickButtons()
        {
            Assert.That(Read(), Is.EqualTo(Vector2.zero));
            Assert.That(Read(KeyCode.JoystickButton0, KeyCode.Space, KeyCode.F), Is.EqualTo(Vector2.zero));
        }

        [TestCase(KeyCode.W, 0f, 1f)]
        [TestCase(KeyCode.UpArrow, 0f, 1f)]
        [TestCase(KeyCode.S, 0f, -1f)]
        [TestCase(KeyCode.DownArrow, 0f, -1f)]
        [TestCase(KeyCode.A, -1f, 0f)]
        [TestCase(KeyCode.LeftArrow, -1f, 0f)]
        [TestCase(KeyCode.D, 1f, 0f)]
        [TestCase(KeyCode.RightArrow, 1f, 0f)]
        public void WasdAndArrowKeysMoveInExpectedDirection(KeyCode key, float x, float y)
            => Assert.That(Read(key), Is.EqualTo(new Vector2(x, y)));

        [Test]
        public void DiagonalMovementDoesNotIncreaseSpeed()
        {
            var direction = Read(KeyCode.W, KeyCode.D);
            Assert.That(direction.magnitude, Is.EqualTo(1f).Within(.00001f));
            Assert.That(direction.x, Is.EqualTo(direction.y));
        }

        [Test]
        public void DuplicateBindingsDoNotOutweighOppositeDirection()
        {
            Assert.That(Read(KeyCode.W, KeyCode.UpArrow), Is.EqualTo(Vector2.up));
            Assert.That(Read(KeyCode.W, KeyCode.UpArrow, KeyCode.S), Is.EqualTo(Vector2.zero));
            Assert.That(Read(KeyCode.A, KeyCode.LeftArrow, KeyCode.D), Is.EqualTo(Vector2.zero));
            Assert.That(Read(KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D), Is.EqualTo(Vector2.zero));
        }
    }
}
