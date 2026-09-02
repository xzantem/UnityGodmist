using System;
using System.Globalization;
using UnityEngine;

namespace Godmist.Data.Configuration
{
    [Serializable]
    public sealed class GameDefineEntry
    {
        public string Id = string.Empty;
        public string DisplayName = string.Empty;
        [TextArea(2, 4)] public string Description = string.Empty;
        public GameDefineCategory Category;
        public string Unit = string.Empty;
        public GameDefineValueType ValueType;

        public int DefaultIntegerValue;
        public int CurrentIntegerValue;
        public int MinimumIntegerValue;
        public int MaximumIntegerValue;

        public float DefaultFloatValue;
        public float CurrentFloatValue;
        public float MinimumFloatValue;
        public float MaximumFloatValue;

        public bool DefaultBooleanValue;
        public bool CurrentBooleanValue;

        public string DefaultStringValue = string.Empty;
        public string CurrentStringValue = string.Empty;

        public string GetDisplayValue()
        {
            return ValueType switch
            {
                GameDefineValueType.Integer => CurrentIntegerValue.ToString(CultureInfo.InvariantCulture),
                GameDefineValueType.Float => CurrentFloatValue.ToString("G9", CultureInfo.InvariantCulture),
                GameDefineValueType.Boolean => CurrentBooleanValue.ToString(),
                _ => CurrentStringValue ?? string.Empty
            };
        }

        public bool TryValidate(out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(Id))
            {
                error = "Brak stabilnego identyfikatora.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                error = "Brak nazwy parametru.";
                return false;
            }

            if (!Enum.IsDefined(typeof(GameDefineCategory), Category))
            {
                error = "Nieprawidłowa kategoria.";
                return false;
            }

            switch (ValueType)
            {
                case GameDefineValueType.Integer:
                    if (MinimumIntegerValue > MaximumIntegerValue)
                    {
                        error = "Minimum integer jest większe od maksimum.";
                        return false;
                    }

                    if (DefaultIntegerValue < MinimumIntegerValue
                        || DefaultIntegerValue > MaximumIntegerValue
                        || CurrentIntegerValue < MinimumIntegerValue
                        || CurrentIntegerValue > MaximumIntegerValue)
                    {
                        error = "Wartość integer jest poza zakresem.";
                        return false;
                    }
                    break;
                case GameDefineValueType.Float:
                    if (float.IsNaN(MinimumFloatValue)
                        || float.IsInfinity(MinimumFloatValue)
                        || float.IsNaN(MaximumFloatValue)
                        || float.IsInfinity(MaximumFloatValue)
                        || MinimumFloatValue > MaximumFloatValue)
                    {
                        error = "Nieprawidłowy zakres float.";
                        return false;
                    }

                    if (float.IsNaN(DefaultFloatValue)
                        || float.IsInfinity(DefaultFloatValue)
                        || float.IsNaN(CurrentFloatValue)
                        || float.IsInfinity(CurrentFloatValue)
                        || DefaultFloatValue < MinimumFloatValue
                        || DefaultFloatValue > MaximumFloatValue
                        || CurrentFloatValue < MinimumFloatValue
                        || CurrentFloatValue > MaximumFloatValue)
                    {
                        error = "Wartość float jest poza zakresem.";
                        return false;
                    }
                    break;
                case GameDefineValueType.Boolean:
                    break;
                case GameDefineValueType.String:
                    if (CurrentStringValue == null || DefaultStringValue == null)
                    {
                        error = "Wartość tekstowa nie może być nullem.";
                        return false;
                    }
                    break;
                default:
                    error = "Nieprawidłowy typ wartości.";
                    return false;
            }

            return true;
        }

        public void Normalize()
        {
            Id = Id?.Trim() ?? string.Empty;
            DisplayName = DisplayName?.Trim() ?? string.Empty;
            Unit ??= string.Empty;

            if (ValueType == GameDefineValueType.Integer)
            {
                if (MinimumIntegerValue > MaximumIntegerValue)
                {
                    (MinimumIntegerValue, MaximumIntegerValue) =
                        (MaximumIntegerValue, MinimumIntegerValue);
                }
                DefaultIntegerValue = Mathf.Clamp(
                    DefaultIntegerValue, MinimumIntegerValue, MaximumIntegerValue);
                CurrentIntegerValue = Mathf.Clamp(
                    CurrentIntegerValue, MinimumIntegerValue, MaximumIntegerValue);
            }
            else if (ValueType == GameDefineValueType.Float)
            {
                if (MinimumFloatValue > MaximumFloatValue)
                {
                    (MinimumFloatValue, MaximumFloatValue) =
                        (MaximumFloatValue, MinimumFloatValue);
                }
                DefaultFloatValue = Mathf.Clamp(
                    DefaultFloatValue, MinimumFloatValue, MaximumFloatValue);
                CurrentFloatValue = Mathf.Clamp(
                    CurrentFloatValue, MinimumFloatValue, MaximumFloatValue);
            }
            else if (ValueType == GameDefineValueType.String)
            {
                DefaultStringValue ??= string.Empty;
                CurrentStringValue ??= string.Empty;
            }
        }
    }
}
