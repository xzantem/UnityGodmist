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

            try
            {
                var view = root.AddComponent<DevConsoleView>();
                var inputType = Type.GetType(
                    "TMPro.TMP_InputField, Unity.TextMeshPro");
                Assert.That(inputType, Is.Not.Null);

                var input = inputObject.AddComponent(inputType);
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
                UnityEngine.Object.DestroyImmediate(inputObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Enter_ExecutesCompleteCommandInsteadOfReapplyingExactSuggestion()
        {
            var root = new GameObject("DevConsoleViewTest");

            try
            {
                var view = root.AddComponent<DevConsoleView>();
                SetPrivateField(
                    view,
                    "suggestions",
                    new List<string> { "help" });

                Assert.That(
                    InvokePrivate<bool>(
                        view,
                        "ShouldAcceptSelectedSuggestion",
                        "help"),
                    Is.False);

                SetPrivateField(
                    view,
                    "suggestions",
                    new List<string> { "gold" });

                Assert.That(
                    InvokePrivate<bool>(
                        view,
                        "ShouldAcceptSelectedSuggestion",
                        "go"),
                    Is.True);
                Assert.That(
                    InvokePrivate<bool>(
                        view,
                        "ShouldAcceptSelectedSuggestion",
                        "gold"),
                    Is.True);
            }
            finally
            {
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

        private static T InvokePrivate<T>(
            object target,
            string methodName,
            params object[] arguments)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null, $"Nie znaleziono metody {methodName}.");
            return (T)method.Invoke(target, arguments);
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

        private static string GetInputText(Type inputType, object input)
        {
            var property = inputType.GetProperty("text");
            Assert.That(property, Is.Not.Null);
            return (string)property.GetValue(input);
        }
    }
}
