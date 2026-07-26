using System.Collections;
using Godmist.Core.Enums;
using Godmist.Runtime.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Godmist.Views.Game
{
    public class SceneLoader : MonoBehaviour
    {
        private GameSession gameSession;
        private bool isLoading;

        public bool IsLoading => isLoading;

        public void Initialize(GameSession session)
        {
            gameSession = session;
        }

        public bool TryLoadScene(string sceneName, GameModeState destinationMode)
        {
            if (isLoading) return false;

            if (gameSession == null)
            {
                Debug.LogError("SceneLoader: brak GameSession.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("SceneLoader: brak nazwy sceny.");
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError(
                    $"SceneLoader: scena '{sceneName}' nie jest dodana do Build Settings.");
                return false;
            }

            StartCoroutine(LoadSceneRoutine(sceneName, destinationMode));
            return true;
        }

        private IEnumerator LoadSceneRoutine(
            string sceneName,
            GameModeState destinationMode)
        {
            isLoading = true;
            gameSession.TrySetMode(GameModeState.Loading);

            var operation = SceneManager.LoadSceneAsync(
                sceneName,
                LoadSceneMode.Single);

            while (!operation.isDone)
            {
                yield return null;
            }

            gameSession.TrySetMode(destinationMode);
            isLoading = false;
        }
    }
}