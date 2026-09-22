using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Data.World;
using Godmist.Runtime.Game;

namespace Godmist.Systems.SaveLoad
{
    public class SaveManager
    {
        private const string QuickSaveFileName = "quicksave.json";
        private const string AutosaveFileName = "autosave.json";
        private const string ManualSaveFilePrefix = "manual_";

        private const string QuickSaveDisplayName = "Szybki zapis";
        private const string AutosaveDisplayName = "Automatyczny zapis";
        private const string ManualSaveDisplayName = "Zapis ręczny";

        private readonly string saveDirectoryPath;
        private readonly GameSaveJsonSerializer serializer = new();
        private readonly GameSessionSaveSnapshotFactory snapshotFactory = new();
        private readonly GameSessionRestoreFactory restoreFactory = new();

        // Cache only summaries, never restored game state. File metadata detects external changes.
        private readonly Dictionary<string, (long Length, DateTime Modified, GameSaveSummary Summary)> summaryCache = new();

        public string SaveDirectoryPath => saveDirectoryPath;

        public SaveManager(string customSaveDirectoryPath = null)
        {
            saveDirectoryPath = string.IsNullOrWhiteSpace(customSaveDirectoryPath)
                ? Path.Combine(UnityEngine.Application.persistentDataPath, "Saves")
                : customSaveDirectoryPath;
        }

        public string GetCampaignDirectoryPath(string campaignId)
        {
            return Path.Combine(saveDirectoryPath, campaignId?.Trim() ?? string.Empty);
        }

        public string GetQuickSavePath(string campaignId)
        {
            return Path.Combine(GetCampaignDirectoryPath(campaignId), QuickSaveFileName);
        }

        public string GetAutosavePath(string campaignId)
        {
            return Path.Combine(GetCampaignDirectoryPath(campaignId), AutosaveFileName);
        }

        public bool TrySaveQuick(GameSession session, out string message)
        {
            return TrySave(
                session,
                QuickSaveFileName,
                QuickSaveDisplayName,
                out _,
                out message);
        }

        public bool TrySaveAutosave(GameSession session, out string message)
        {
            return TrySave(
                session,
                AutosaveFileName,
                AutosaveDisplayName,
                out _,
                out message);
        }

        public bool TrySaveManual(
            GameSession session,
            string displayName,
            out GameSaveSummary summary,
            out string message)
        {
            summary = null;
            message = string.Empty;

            if (session == null)
            {
                message = "Brak aktywnej sesji gry.";
                return false;
            }

            if ((session.HasCombat || session.IsDefeated))
            {
                message = "Nie można zapisać gry podczas walki.";
                return false;
            }

            var normalizedDisplayName = string.IsNullOrWhiteSpace(displayName)
                ? ManualSaveDisplayName
                : displayName.Trim();

            GameSaveData saveData;

            try
            {
                saveData = snapshotFactory.CreateSnapshot(session, normalizedDisplayName);
            }
            catch (Exception exception)
            {
                message = exception.Message;
                return false;
            }

            var fileName = $"{ManualSaveFilePrefix}{saveData.SaveId}.json";

            if (!TryGetCampaignSavePath(
                    session.CampaignId,
                    fileName,
                    out var destinationPath,
                    out message))
            {
                return false;
            }

            var json = serializer.Serialize(saveData);

            if (string.IsNullOrWhiteSpace(json))
            {
                message = "Serializacja zapisu zwróciła pusty wynik.";
                return false;
            }

            summaryCache.Remove(destinationPath);
            if (!TryWriteText(destinationPath, json, out message))
            {
                return false;
            }

            if (!TryCreateSummary(destinationPath, saveData, out summary))
            {
                message = "Zapisano grę, ale nie udało się utworzyć jego podsumowania.";
                return false;
            }

            return true;
        }

        public bool TryGetSaveSummaries(
            out IReadOnlyList<GameSaveSummary> summaries,
            out string message)
        {
            var results = new List<GameSaveSummary>();
            summaries = results;
            message = string.Empty;

            if (!Directory.Exists(saveDirectoryPath))
            {
                return true;
            }

            try
            {
                var paths = Directory.GetFiles(
                    saveDirectoryPath,
                    "*.json",
                    SearchOption.AllDirectories);

                foreach (var path in paths)
                {
                    var file = new FileInfo(path);
                    if (!summaryCache.TryGetValue(path, out var cached)
                        || cached.Length != file.Length || cached.Modified != file.LastWriteTimeUtc)
                    {
                        GameSaveSummary freshSummary = null;
                        if (TryReadSaveData(path, out var saveData, out _))
                            TryCreateSummary(path, saveData, out freshSummary);
                        cached = (file.Length, file.LastWriteTimeUtc, freshSummary);
                        summaryCache[path] = cached;
                    }
                    var summary = cached.Summary;
                    if (summary == null) continue;

                    results.Add(summary);
                }

                var existingPaths = new HashSet<string>(paths);
                foreach (var cachedPath in new List<string>(summaryCache.Keys))
                    if (!existingPaths.Contains(cachedPath)) summaryCache.Remove(cachedPath);

                results.Sort((left, right) =>
                    right.UpdatedAtUtc.CompareTo(left.UpdatedAtUtc));

                return true;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                return false;
            }
        }

        public bool TryLoadLatestValid(GameContentCatalog catalog, out GameSession session, out string message)
        {
            session = null;
            if (catalog == null) { message = "Brak katalogu zawartości gry."; return false; }
            if (!TryGetSaveSummaries(out var summaries, out message)) return false;
            foreach (var summary in summaries)
                if (TryLoadSave(summary, catalog, out var candidate, out _) && !candidate.IsDefeated)
                {
                    session = candidate;
                    message = "Wczytano ostatni poprawny zapis.";
                    return true;
                }
            message = "Brak poprawnego zapisu do kontynuowania. Możesz rozpocząć nową grę.";
            return false;
        }

        public bool TryLoadQuick(
            string campaignId,
            GameContentCatalog contentCatalog,
            out GameSession session,
            out string message)
        {
            session = null;
            message = string.Empty;

            if (!TryGetCampaignSavePath(
                    campaignId,
                    QuickSaveFileName,
                    out var path,
                    out message))
            {
                return false;
            }

            if (!TryReadSaveData(path, out var saveData, out message)
                || !TryCreateSummary(path, saveData, out var summary))
            {
                return false;
            }

            return TryLoadSave(summary, contentCatalog, out session, out message);
        }

        public bool TryLoadSave(
            GameSaveSummary summary,
            GameContentCatalog contentCatalog,
            out GameSession session,
            out string message)
        {
            session = null;
            message = string.Empty;

            if (summary == null)
            {
                message = "Brak wybranego zapisu.";
                return false;
            }

            if (contentCatalog == null)
            {
                message = "Brak GameContentCatalog.";
                return false;
            }

            if (!TryGetSafeSavePath(summary.FilePath, out var path, out message))
            {
                return false;
            }

            if (!TryReadSaveData(path, out var saveData, out message))
            {
                return false;
            }

            if (!string.Equals(
                    saveData.SaveId,
                    summary.SaveId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    saveData.CampaignId,
                    summary.CampaignId,
                    StringComparison.Ordinal))
            {
                message = "Dane zapisu nie odpowiadają wybranemu wpisowi.";
                return false;
            }

            if (!restoreFactory.TryCreate(saveData, contentCatalog, out session))
            {
                session = null;
                message = "Nie udało się odtworzyć GameSession z zapisu.";
                return false;
            }

            message = $"Wczytano zapis: {saveData.DisplayName}.";
            return true;
        }

        public bool TryDeleteManualSave(
            GameSaveSummary summary,
            out string message)
        {
            message = string.Empty;

            if (summary == null)
            {
                message = "Brak wybranego zapisu.";
                return false;
            }

            if (summary.SlotType != SaveSlotType.Manual)
            {
                message = "Można usuwać tylko ręczne zapisy.";
                return false;
            }

            if (!TryGetSafeSavePath(summary.FilePath, out var path, out message))
            {
                return false;
            }

            if (!TryReadSaveData(path, out var saveData, out message))
            {
                return false;
            }

            if (!string.Equals(
                    saveData.SaveId,
                    summary.SaveId,
                    StringComparison.Ordinal))
            {
                message = "Dane zapisu nie odpowiadają wybranemu wpisowi.";
                return false;
            }

            try
            {
                File.Delete(path);
                message = $"Usunięto zapis: {summary.DisplayName}.";
                return true;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                return false;
            }
        }

        private bool TrySave(
            GameSession session,
            string fileName,
            string displayName,
            out GameSaveSummary summary,
            out string message)
        {
            summary = null;
            message = string.Empty;

            if (session == null)
            {
                message = "Brak aktywnej sesji gry.";
                return false;
            }

            if ((session.HasCombat || session.IsDefeated))
            {
                message = "Nie można zapisać gry podczas walki.";
                return false;
            }

            if (!TryGetCampaignSavePath(
                    session.CampaignId,
                    fileName,
                    out var destinationPath,
                    out message))
            {
                return false;
            }

            GameSaveData saveData;

            if (File.Exists(destinationPath))
            {
                if (!TryReadSaveData(destinationPath, out saveData, out message))
                {
                    message = $"Nie można nadpisać zapisu: {message}";
                    return false;
                }

                saveData.DisplayName = displayName;

                if (!snapshotFactory.TryCaptureInto(saveData, session))
                {
                    message = "Nie udało się utworzyć danych zapisu.";
                    return false;
                }
            }
            else
            {
                try
                {
                    saveData = snapshotFactory.CreateSnapshot(session, displayName);
                }
                catch (Exception exception)
                {
                    message = exception.Message;
                    return false;
                }
            }

            var json = serializer.Serialize(saveData);

            if (string.IsNullOrWhiteSpace(json))
            {
                message = "Serializacja zapisu zwróciła pusty wynik.";
                return false;
            }

            summaryCache.Remove(destinationPath);
            if (!TryWriteText(destinationPath, json, out message))
            {
                return false;
            }

            if (!TryCreateSummary(destinationPath, saveData, out summary))
            {
                message = "Zapisano grę, ale nie udało się utworzyć jego podsumowania.";
                return false;
            }

            return true;
        }

        private bool TryGetCampaignSavePath(
            string campaignId,
            string fileName,
            out string path,
            out string message)
        {
            path = string.Empty;
            message = string.Empty;

            if (string.IsNullOrWhiteSpace(campaignId))
            {
                message = "Brak CampaignId.";
                return false;
            }

            if (campaignId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || campaignId.Contains("/")
                || campaignId.Contains("\\"))
            {
                message = "CampaignId zawiera niedozwolone znaki.";
                return false;
            }

            path = Path.Combine(
                GetCampaignDirectoryPath(campaignId),
                fileName);

            return true;
        }

        private bool TryGetSafeSavePath(
            string sourcePath,
            out string path,
            out string message)
        {
            path = string.Empty;
            message = string.Empty;

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                message = "Brak ścieżki zapisu.";
                return false;
            }

            try
            {
                var rootPath = Path.GetFullPath(saveDirectoryPath)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);

                var candidatePath = Path.GetFullPath(sourcePath);
                var allowedPrefix = rootPath + Path.DirectorySeparatorChar;

                if (!candidatePath.StartsWith(
                        allowedPrefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    message = "Ścieżka zapisu znajduje się poza katalogiem zapisów.";
                    return false;
                }

                if (!string.Equals(
                        Path.GetExtension(candidatePath),
                        ".json",
                        StringComparison.OrdinalIgnoreCase))
                {
                    message = "Plik zapisu musi mieć rozszerzenie .json.";
                    return false;
                }

                path = candidatePath;
                return true;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                return false;
            }
        }

        private bool TryCreateSummary(
            string path,
            GameSaveData saveData,
            out GameSaveSummary summary)
        {
            summary = null;

            if (saveData == null
                || string.IsNullOrWhiteSpace(saveData.SaveId)
                || string.IsNullOrWhiteSpace(saveData.CampaignId))
            {
                return false;
            }

            var directoryInfo = Directory.GetParent(path);

            if (directoryInfo == null
                || !string.Equals(
                    directoryInfo.Name,
                    saveData.CampaignId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            var createdAtUtc = ParseSaveDate(saveData.CreatedAtUtc);
            var updatedAtUtc = ParseSaveDate(saveData.UpdatedAtUtc);

            summary = new GameSaveSummary(
                path,
                saveData.SaveId,
                saveData.CampaignId,
                saveData.DisplayName,
                createdAtUtc,
                updatedAtUtc,
                GetSlotType(path));

            return true;
        }

        private static DateTime ParseSaveDate(string value)
        {
            return DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsedValue)
                ? parsedValue
                : DateTime.MinValue;
        }

        private static SaveSlotType GetSlotType(string path)
        {
            var fileName = Path.GetFileName(path);

            if (string.Equals(
                    fileName,
                    QuickSaveFileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return SaveSlotType.Quick;
            }

            if (string.Equals(
                    fileName,
                    AutosaveFileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return SaveSlotType.Autosave;
            }

            return SaveSlotType.Manual;
        }

        private bool TryReadSaveData(
            string path,
            out GameSaveData saveData,
            out string message)
        {
            saveData = null;
            message = string.Empty;

            if (!File.Exists(path))
            {
                message = $"Nie znaleziono pliku zapisu: {path}";
                return false;
            }

            try
            {
                var json = File.ReadAllText(path);

                if (!serializer.TryDeserialize(json, out saveData))
                {
                    message = "Plik zapisu ma nieprawidłową strukturę.";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                return false;
            }
        }

        private static bool TryWriteText(
            string path,
            string content,
            out string message)
        {
            message = string.Empty;
            var temporaryPath = $"{path}.tmp";

            try
            {
                var directoryPath = Path.GetDirectoryName(path);

                if (string.IsNullOrWhiteSpace(directoryPath))
                {
                    message = "Nieprawidłowa ścieżka katalogu zapisów.";
                    return false;
                }

                Directory.CreateDirectory(directoryPath);

                File.WriteAllText(
                    temporaryPath,
                    content,
                    new UTF8Encoding(false));

                File.Copy(temporaryPath, path, true);
                File.Delete(temporaryPath);

                message = $"Zapisano grę: {path}";
                return true;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                return false;
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }
}