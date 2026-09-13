using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DevConsoleViewTests
    {
        [TestCase("createitem item.test characterId=je", "jerzy", "createitem item.test characterId=jerzy")]
        [TestCase("creategalduryte target=", "armor", "creategalduryte target=armor")]
        [TestCase("creategalduryte 1 ar", "armor", "creategalduryte 1 armor")]
        public void ApplySelectedSuggestion_PreservesNamedArgumentKey(string line, string suggestion, string expected)
        {
            var root = new GameObject("SuggestionCompletionTest");
            var inputObject = new GameObject("Input");
            var textObject = new GameObject("Text");
            try
            {
                var view = root.AddComponent<DevConsoleView>();
                var inputType = Type.GetType("TMPro.TMP_InputField, Unity.TextMeshPro");
                var input = inputObject.AddComponent(inputType);
                var text = textObject.AddComponent(Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro"));
                SetPrivateProperty(input, "textComponent", text);
                SetPrivateProperty(input, "text", line);
                SetPrivateField(view, "commandInput", input);
                SetPrivateField(view, "suggestions", new List<string> { suggestion });
                SetPrivateField(view, "selectedSuggestionIndex", 0);
                InvokePrivate(view, "ApplySelectedSuggestion");
                Assert.That(GetInputText(inputType, input), Is.EqualTo(expected));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(textObject);
                UnityEngine.Object.DestroyImmediate(inputObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SuggestionNavigation_UsesFullListAndKeepsFiveVisible()
        {
            var root = new GameObject("DevConsoleViewTest");

            try
            {
                var view = root.AddComponent<DevConsoleView>();
                SetPrivateField(view, "maximumSuggestions", 5);
                SetPrivateField(
                    view,
                    "suggestions",
                    Enumerable.Range(0, 8)
                        .Select(index => $"suggestion.{index}")
                        .ToList());

                for (var index = 0; index < 7; index++)
                {
                    InvokePrivate(view, "SelectNextSuggestion");
                }

                Assert.That(
                    GetPrivateField<int>(view, "selectedSuggestionIndex"),
                    Is.EqualTo(6));
                Assert.That(
                    GetPrivateField<int>(view, "suggestionWindowStartIndex"),
                    Is.EqualTo(2));

                InvokePrivate(view, "SelectNextSuggestion");

                Assert.That(
                    GetPrivateField<int>(view, "selectedSuggestionIndex"),
                    Is.EqualTo(7));
                Assert.That(
                    GetPrivateField<int>(view, "suggestionWindowStartIndex"),
                    Is.EqualTo(3));

                InvokePrivate(view, "SelectPreviousSuggestion");

                Assert.That(
                    GetPrivateField<int>(view, "selectedSuggestionIndex"),
                    Is.EqualTo(6));
                Assert.That(
                    GetPrivateField<int>(view, "suggestionWindowStartIndex"),
                    Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SuggestionNavigation_WrapsFromAlphabeticalFirstToLastSuggestion()
        {
            var root = new GameObject("DevConsoleViewTest");

            try
            {
                var view = root.AddComponent<DevConsoleView>();
                SetPrivateField(view, "maximumSuggestions", 5);
                SetPrivateField(
                    view,
                    "suggestions",
                    new List<string>
                    {
                        "alpha",
                        "bravo",
                        "charlie",
                        "delta",
                        "echo",
                        "foxtrot"
                    });
                SetPrivateField(view, "selectedSuggestionIndex", 0);

                InvokePrivate(view, "SelectPreviousSuggestion");

                Assert.That(
                    GetPrivateField<int>(view, "selectedSuggestionIndex"),
                    Is.EqualTo(5));
                Assert.That(
                    GetPrivateField<int>(view, "suggestionWindowStartIndex"),
                    Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CommandHistory_RetainsMultipleEntriesAndBrowsesWhenInputIsEmpty()
        {
            var root = new GameObject("DevConsoleViewTest");
            var inputObject = new GameObject("CommandInput");
            GameObject textObject = null;

            try
            {
                var view = root.AddComponent<DevConsoleView>();
                var inputType = Type.GetType(
                    "TMPro.TMP_InputField, Unity.TextMeshPro");
                Assert.That(inputType, Is.Not.Null);

                var input = inputObject.AddComponent(inputType);
                textObject = new GameObject("CommandInputText");
                var textType = Type.GetType(
                    "TMPro.TextMeshProUGUI, Unity.TextMeshPro");
                Assert.That(textType, Is.Not.Null);

                var textComponent = textObject.AddComponent(textType);
                SetPrivateProperty(input, "textComponent", textComponent);
                SetPrivateField(view, "commandInput", input);
                SetPrivateField(view, "maximumHistoryEntries", 3);

                InvokePrivate(view, "AddToCommandHistory", "gold 1");
                InvokePrivate(view, "AddToCommandHistory", "gold 2");
                InvokePrivate(view, "AddToCommandHistory", "gold 3");
                InvokePrivate(view, "AddToCommandHistory", "gold 4");

                var history = GetPrivateField<List<string>>(view, "commandHistory");
                Assert.That(history, Is.EqualTo(new[] { "gold 2", "gold 3", "gold 4" }));

                InvokePrivate(view, "BrowseCommandHistory", -1);
                Assert.That(GetInputText(inputType, input), Is.EqualTo("gold 4"));

                InvokePrivate(view, "BrowseCommandHistory", -1);
                Assert.That(GetInputText(inputType, input), Is.EqualTo("gold 3"));

                InvokePrivate(view, "BrowseCommandHistory", 1);
                Assert.That(GetInputText(inputType, input), Is.EqualTo("gold 4"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(textObject);
                UnityEngine.Object.DestroyImmediate(inputObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void InvokePrivate(
            object target,
            string methodName,
            params object[] arguments)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null, $"Nie znaleziono metody {methodName}.");
            method.Invoke(target, arguments);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"Nie znaleziono pola {fieldName}.");
            return (T)field.GetValue(target);
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"Nie znaleziono pola {fieldName}.");
            field.SetValue(target, value);
        }

        private static void SetPrivateProperty(
            object target,
            string propertyName,
            object value)
        {
            var property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);

            Assert.That(property, Is.Not.Null, $"Nie znaleziono właściwości {propertyName}.");
            property.SetValue(target, value);
        }

        private static string GetInputText(Type inputType, object input)
        {
            var property = inputType.GetProperty("text");
            Assert.That(property, Is.Not.Null);
            return (string)property.GetValue(input);
        }
    }
}
