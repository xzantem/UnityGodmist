using System;
using System.IO;
using System.Text;
using Godmist.Core.Serialization;
using Godmist.Data.World;
using Godmist.Runtime.Game;

namespace Godmist.Systems.SaveLoad
{
    public class SaveManager
    {
        private const string QuickSaveFileName = "quicksave.json";
        private const string AutosaveFileName = "autosave.json";

        private const string QuickSaveDisplayName = "Szybki zapis";
        private const string AutosaveDisplayName = "Automatyczny zapis";

        private readonly string saveDirectoryPath;
        private readonly GameSaveJsonSerializer serializer = new();
        private readonly GameSessionSaveSnapshotFactory snapshotFactory = new();
        private readonly GameSessionRestoreFactory restoreFactory = new();

        public string SaveDirectoryPath => saveDirectoryPath;

        public SaveManager(string customSaveDirectoryPath = null)
        {
            saveDirectoryPath = string.IsNullOrWhiteSpace(
                customSaveDirectoryPath)
                ? Path.Combine(
                    UnityEngine.Application.persistentDataPath,
                    "Saves")
                : customSaveDirectoryPath;
        }

        public string GetCampaignDirectoryPath(string campaignId)
        {
            return Path.Combine(
                saveDirectoryPath,
                campaignId?.Trim() ?? string.Empty);
        }

        public string GetQuickSavePath(string campaignId)
        {
            return Path.Combine(
                GetCampaignDirectoryPath(campaignId),
                QuickSaveFileName);
        }

        public string GetAutosavePath(string campaignId)
        {
            return Path.Combine(
                GetCampaignDirectoryPath(campaignId),
                AutosaveFileName);
        }

        public bool TrySaveQuick(
            GameSession session,
            out string message)
        {
            return TrySave(
                session,
                QuickSaveFileName,
                QuickSaveDisplayName,
                out message);
        }

        public bool TrySaveAutosave(
            GameSession session,
            out string message)
        {
            return TrySave(
                session,
                AutosaveFileName,
                AutosaveDisplayName,
                out message);
        }

        public bool TryLoadQuick(
            string campaignId,
            GameContentCatalog contentCatalog,
            out GameSession session,
            out string message)
        {
            session = null;
            message = string.Empty;

            if (contentCatalog == null)
            {
                message = "Brak GameContentCatalog.";
                return false;
            }

            if (!TryGetCampaignSavePath(
                    campaignId,
                    QuickSaveFileName,
                    out var path,
                    out message))
            {
                return false;
            }

            if (!TryReadSaveData(path, out var saveData, out message))
            {
                return false;
            }

            if (!restoreFactory.TryCreate(
                    saveData,
                    contentCatalog,
                    out session))
            {
                session = null;
                message = "Nie udało się odtworzyć GameSession z zapisu.";
                return false;
            }

            message = $"Wczytano zapis: {saveData.DisplayName}.";
            return true;
        }

        private bool TrySave(
            GameSession session,
            string fileName,
            string displayName,
            out string message)
        {
            message = string.Empty;

            if (session == null)
            {
                message = "Brak aktywnej sesji gry.";
                return false;
            }

            if (session.ActiveCombat != null)
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
                if (!TryReadSaveData(
                        destinationPath,
                        out saveData,
                        out message))
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
                    saveData = snapshotFactory.CreateSnapshot(
                        session,
                        displayName);
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

            return TryWriteText(
                destinationPath,
                json,
                out message);
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

            if (campaignId.IndexOfAny(
                    Path.GetInvalidFileNameChars()) >= 0
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