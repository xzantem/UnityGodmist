using System.Linq;
using Godmist.Editor;
using Godmist.Views.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Godmist.Tests.EditMode
{
    public sealed class ControllerSupportTests : InputTestFixture
    {
        private InputActionAsset asset;
        private Gamepad pad;
        private GameObject fieldObject, keyboardObject;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControllerSupportSetup.ActionsPath);
            Assert.That(source, Is.Not.Null);
            asset = InputActionAsset.FromJson(source.ToJson());
            ControllerSupportSetup.ConfigureActions(asset);
            asset.Disable();
            pad = InputSystem.AddDevice<Gamepad>();
            foreach (var map in asset.actionMaps) map.devices = new InputDevice[] { pad };
            asset.FindActionMap(ControllerInput.GlobalMap, true).Enable();
        }

        [TearDown]
        public override void TearDown()
        {
            try
            {
                if (asset != null) { asset.Disable(); Object.DestroyImmediate(asset); }
                if (pad is { added: true }) InputSystem.RemoveDevice(pad);
                if (keyboardObject != null) Object.DestroyImmediate(keyboardObject);
                if (fieldObject != null) Object.DestroyImmediate(fieldObject);
            }
            finally { base.TearDown(); }
        }

        private void State(GamepadState state)
        {
            InputSystem.QueueStateEvent(pad, state);
            InputSystem.Update();
        }

        [Test]
        public void ActionAuthoringIsIdempotentAndPreservesExistingMapsAndIds()
        {
            var before = asset.ToJson();
            var ids = asset.actionMaps.SelectMany(m => m.actions).ToDictionary(a => a.actionMap.name + "/" + a.name, a => a.id);
            ControllerSupportSetup.ConfigureActions(asset);
            Assert.That(asset.ToJson(), Is.EqualTo(before));
            foreach (var entry in ids) Assert.That(asset.FindAction(entry.Key, true).id, Is.EqualTo(entry.Value));
        }

        [Test]
        public void NativeUiSubmitHasNoGamepadBinding()
        {
            var submit = asset.FindAction("UI/KeyboardSubmit", true);
            Assert.That(submit.bindings.Count, Is.EqualTo(2));
            Assert.That(submit.bindings.All(b => b.path.StartsWith("<Keyboard>/")), Is.True);
        }

        [Test]
        public void NativeUiNavigationIgnoresGamepadAndStillReadsKeyboard()
        {
            var map = asset.FindActionMap("UI", true);
            var navigation = map.FindAction("KeyboardNavigate", true);
            var keys = InputSystem.AddDevice<Keyboard>();
            try
            {
                map.devices = new InputDevice[] { pad, keys };
                navigation.Enable();
                Assert.That(navigation.bindings.All(binding => binding.isComposite || binding.path.StartsWith("<Keyboard>/")), Is.True);
                State(new GamepadState { leftStick = Vector2.one, rightStick = Vector2.one });
                Assert.That(navigation.ReadValue<Vector2>(), Is.EqualTo(Vector2.zero));
                Press(keys.rightArrowKey);
                Assert.That(navigation.ReadValue<Vector2>(), Is.EqualTo(Vector2.right));
                Release(keys.rightArrowKey);
            }
            finally { InputSystem.RemoveDevice(keys); }
        }

        [Test]
        public void ContextSwitchDoesNotRepeatTheHeldConfirm()
        {
            var world = asset.FindActionMap(ControllerInput.WorldMap, true);
            var ui = asset.FindActionMap(ControllerInput.UiMap, true);
            world.Enable();
            State(new GamepadState().WithButton(GamepadButton.South));
            Assert.That(ControllerInput.Pressed(asset, ControllerInput.WorldMap, "Confirm"), Is.True);
            Assert.That(ControllerInput.Pressed(asset, ControllerInput.UiMap, "Confirm"), Is.False);
            world.Disable(); ui.Enable();
            State(new GamepadState().WithButton(GamepadButton.South));
            Assert.That(ControllerInput.Pressed(asset, ControllerInput.UiMap, "Confirm"), Is.False, "Holding A across a modal transition must not confirm the new dialog.");
            State(new GamepadState());
            State(new GamepadState().WithButton(GamepadButton.South));
            Assert.That(ControllerInput.Pressed(asset, ControllerInput.UiMap, "Confirm"), Is.True);
            Assert.That(ControllerInput.Pressed(asset, ControllerInput.WorldMap, "Confirm"), Is.False);
        }

        [Test]
        public void DriftDoesNotMoveAndCameraStickDoesNotMoveTheParty()
        {
            var world = asset.FindActionMap(ControllerInput.WorldMap, true); world.Enable();
            State(new GamepadState { leftStick = new Vector2(.03f, -.02f), rightStick = Vector2.right });
            Assert.That(world.FindAction("Move", true).ReadValue<Vector2>(), Is.EqualTo(Vector2.zero));
            Assert.That(world.FindAction("Camera", true).ReadValue<Vector2>().x, Is.GreaterThan(.9f));
            State(new GamepadState { leftStick = Vector2.one });
            Assert.That(world.FindAction("Move", true).ReadValue<Vector2>().magnitude, Is.LessThanOrEqualTo(1.001f));
        }

        [Test]
        public void CursorToggleIsL3AndWholeStackModifierRemainsRightTrigger()
        {
            var ui = asset.FindActionMap(ControllerInput.UiMap, true); ui.Enable();
            State(new GamepadState().WithButton(GamepadButton.LeftStick));
            Assert.That(ControllerInput.Action(asset, ControllerInput.UiMap, "Alternate").IsPressed(), Is.True);
            Assert.That(ControllerInput.Action(asset, ControllerInput.UiMap, "StackModifier").IsPressed(), Is.False);
            State(new GamepadState { rightTrigger = 1 });
            Assert.That(ControllerInput.Action(asset, ControllerInput.UiMap, "Alternate").IsPressed(), Is.False);
            Assert.That(ControllerInput.Action(asset, ControllerInput.UiMap, "StackModifier").IsPressed(), Is.True);
        }

        [Test]
        public void RemappedWorldBindingsKeepTargetsSeparateFromZoomAndCameraCenter()
        {
            asset.FindActionMap(ControllerInput.WorldMap, true).Enable();
            State(new GamepadState().WithButton(GamepadButton.DpadRight));
            Assert.That(ControllerInput.Pressed(asset, ControllerInput.WorldMap, "NextTarget"), Is.True);
            Assert.That(asset.FindAction("GodmistWorld/Zoom", true).ReadValue<float>(), Is.Zero);
            State(new GamepadState().WithButton(GamepadButton.DpadUp));
            Assert.That(asset.FindAction("GodmistWorld/Zoom", true).ReadValue<float>(), Is.EqualTo(1));
            Assert.That(ControllerInput.Pressed(asset, ControllerInput.WorldMap, "Skills"), Is.False);
            State(new GamepadState().WithButton(GamepadButton.Select));
            Assert.That(ControllerInput.Pressed(asset, ControllerInput.WorldMap, "Map"), Is.True);
            Assert.That(ControllerInput.Pressed(asset, ControllerInput.GlobalMap, "Panels"), Is.False);
            State(new GamepadState().WithButton(GamepadButton.RightStick));
            Assert.That(ControllerInput.Pressed(asset, ControllerInput.WorldMap, "Center"), Is.True);
        }

        [Test]
        public void HoldCannotArmFromTargetingOrUiAndFiresOnlyOnceUntilRelease()
        {
            var hold = new ControllerHoldGesture();
            Assert.That(hold.Tick(false, true, true, 1), Is.False);
            Assert.That(hold.Tick(true, false, true, 1), Is.False);
            Assert.That(hold.Tick(true, false, false, .1f), Is.False);
            Assert.That(hold.Tick(true, true, true, .3f), Is.False);
            Assert.That(hold.Tick(true, false, true, .4f), Is.True);
            Assert.That(hold.Tick(true, false, true, 2), Is.False);
            hold.Reset();
            Assert.That(hold.Tick(true, false, true, 2), Is.False, "An actor/context change also requires a new press.");
        }

        [Test]
        public void LosingEligibilityCancelsAnAlreadyStartedHold()
        {
            var hold = new ControllerHoldGesture();
            Assert.That(hold.Tick(true, true, true, .4f), Is.False);
            Assert.That(hold.Tick(false, false, true, .4f), Is.False);
            Assert.That(hold.Tick(true, false, true, 1), Is.False);
        }

        private (TMP_InputField field, ControllerKeyboardView keyboard) Keyboard(string value)
        {
            fieldObject = new GameObject("Input", typeof(RectTransform), typeof(TMP_InputField));
            var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)); text.transform.SetParent(fieldObject.transform, false);
            var field = fieldObject.GetComponent<TMP_InputField>(); field.textComponent = text.GetComponent<TextMeshProUGUI>();
            field.textViewport = (RectTransform)fieldObject.transform; field.text = value;
            keyboardObject = new GameObject("Keyboard", typeof(RectTransform), typeof(ControllerKeyboardView));
            var keyboard = keyboardObject.GetComponent<ControllerKeyboardView>(); keyboard.Open(field);
            return (field, keyboard);
        }

        [Test]
        public void KeyboardCancelRestoresOriginalTextAndNotifiesTheForm()
        {
            var (field, keyboard) = Keyboard("Miecz");
            var updates = 0; field.onValueChanged.AddListener(_ => updates++);
            keyboard.Enter("clear"); keyboard.Enter("ł"); keyboard.Enter("shift"); keyboard.Enter("a");
            Assert.That(field.text, Is.EqualTo("łA"));
            keyboard.Cancel();
            Assert.That(field.text, Is.EqualTo("Miecz"));
            Assert.That(updates, Is.GreaterThanOrEqualTo(4));
        }

        [Test]
        public void KeyboardHonorsNumericValidationAndCharacterLimit()
        {
            var (field, keyboard) = Keyboard("1");
            field.characterLimit = 2; field.characterValidation = TMP_InputField.CharacterValidation.Integer;
            keyboard.Enter("a"); Assert.That(field.text, Is.EqualTo("1"));
            keyboard.Enter("2"); keyboard.Enter("3"); Assert.That(field.text, Is.EqualTo("12"));
            keyboard.Enter("backspace"); Assert.That(field.text, Is.EqualTo("1"));
            keyboard.Accept(); Assert.That(field.text, Is.EqualTo("1"));
        }
    }
}
