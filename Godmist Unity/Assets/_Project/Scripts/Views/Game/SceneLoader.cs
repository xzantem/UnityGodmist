using System;
using System.Collections;
using Godmist.Core.Enums;
using Godmist.Runtime.Game;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Godmist.Views.Game
{
    public class SceneLoader : MonoBehaviour
    {
        [SerializeField] private UIWorkspaceStylePalette loadingStyle;
        private GameSession gameSession;
        private bool isLoading;
        private GameObject loadingOverlay;
        private TextMeshProUGUI loadingLabel;
        private Slider loadingProgress;
        public bool IsLoading => isLoading;

        public void Initialize(GameSession session) => gameSession = session;

        // True means the transition was accepted. Preparation runs only after
        // the persistent overlay has had a full frame to reach the screen.
        public bool TryLoadScene(string sceneName, GameModeState destinationMode,
            Func<bool> prepare = null, string message = "Wczytywanie…",
            Action onLoaded = null, Action onFailed = null)
        {
            if (isLoading) return false;
            if (gameSession == null)
            { Debug.LogError("SceneLoader: brak GameSession."); return false; }
            if (string.IsNullOrWhiteSpace(sceneName))
            { Debug.LogError("SceneLoader: brak nazwy sceny."); return false; }
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            { Debug.LogError($"SceneLoader: scena '{sceneName}' nie jest dodana do Build Settings."); return false; }
            StartCoroutine(LoadSceneRoutine(sceneName, destinationMode, prepare, message, onLoaded, onFailed));
            return true;
        }

        private IEnumerator LoadSceneRoutine(string sceneName, GameModeState destinationMode,
            Func<bool> prepare, string message, Action onLoaded, Action onFailed)
        {
            isLoading = true;
            var previousMode = gameSession.CurrentMode;
            var completed = false;
            try
            {
                gameSession.TrySetMode(GameModeState.Loading);
                ShowLoading(message);
                // Two player-loop boundaries ensure a rendered overlay before
                // synchronous generation, even when called from a UI callback.
                yield return null;
                yield return null;
                if (!TryPrepare(prepare))
                {
                    ShowLoading("Nie udało się przygotować wyprawy.");
                    loadingProgress.gameObject.SetActive(false);
                    yield return new WaitForSecondsRealtime(2f);
                    yield break;
                }
                ReportLoadingProgress(.1f);
                var operation = StartSceneLoad(sceneName);
                if (operation == null)
                {
                    ShowLoading("Nie udało się wczytać sceny.");
                    loadingProgress.gameObject.SetActive(false);
                    yield return new WaitForSecondsRealtime(2f);
                    yield break;
                }
                while (!operation.isDone)
                {
                    // Unity reports scene data loading in the 0..0.9 range.
                    // Leave the final portion for scene activation and startup.
                    ReportLoadingProgress(.1f + .85f * Mathf.Clamp01(operation.progress / .9f));
                    yield return null;
                }
                ReportLoadingProgress(.95f);
                // Keep the overlay through scene Awake/Start and initial layout.
                yield return null;
                yield return null;
                ReportLoadingProgress(1f);
                yield return null;
                gameSession.TrySetMode(destinationMode);
                completed = true;
                onLoaded?.Invoke();
            }
            finally
            {
                try
                {
                    if (!completed)
                    {
                        try { onFailed?.Invoke(); }
                        finally { gameSession.TrySetMode(previousMode); }
                    }
                }
                finally
                {
                    if (loadingOverlay != null) loadingOverlay.SetActive(false);
                    isLoading = false;
                }
            }
        }

        private static bool TryPrepare(Func<bool> prepare)
        {
            try { return prepare?.Invoke() ?? true; }
            catch (Exception exception) { Debug.LogException(exception); return false; }
        }

        private static AsyncOperation StartSceneLoad(string sceneName)
        {
            try { return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single); }
            catch (Exception exception) { Debug.LogException(exception); return null; }
        }

        private void ShowLoading(string message)
        {
            if (loadingOverlay == null)
            {
                // Transient UI owned by the persistent scene loader, rather
                // than a scene/prefab reference which disappears during loading.
                loadingOverlay = new GameObject("Scene Loading Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                loadingOverlay.transform.SetParent(transform, false);
                var canvas = loadingOverlay.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32760;
                var scaler = loadingOverlay.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = .5f;
                var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
                background.transform.SetParent(loadingOverlay.transform, false);
                var backgroundRect = background.GetComponent<RectTransform>();
                backgroundRect.anchorMin = Vector2.zero; backgroundRect.anchorMax = Vector2.one;
                backgroundRect.offsetMin = Vector2.zero; backgroundRect.offsetMax = Vector2.zero;
                background.GetComponent<Image>().color = new Color(.025f, .03f, .04f, 1);
                var label = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
                label.transform.SetParent(background.transform, false);
                var rect = label.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(.08f, .5f); rect.anchorMax = new Vector2(.92f, .5f);
                rect.anchoredPosition = new Vector2(0, 32); rect.sizeDelta = new Vector2(0, 64);
                loadingLabel = label.GetComponent<TextMeshProUGUI>();
                loadingLabel.font = loadingStyle != null && loadingStyle.Font != null ? loadingStyle.Font : TMP_Settings.defaultFontAsset;
                loadingLabel.fontSize = 36;
                loadingLabel.alignment = TextAlignmentOptions.Center;
                loadingLabel.color = new Color(.9f, .88f, .8f, 1);
                loadingLabel.raycastTarget = false;
                CreateProgressBar(background.transform);
            }
            loadingLabel.text = message;
            loadingProgress.SetValueWithoutNotify(0);
            loadingProgress.gameObject.SetActive(true);
            loadingOverlay.SetActive(true);
        }

        private void ReportLoadingProgress(float value)
        {
            if (loadingProgress != null)
                loadingProgress.SetValueWithoutNotify(Mathf.Max(loadingProgress.value, Mathf.Clamp01(value)));
        }

        private void CreateProgressBar(Transform parent)
        {
            var bar = new GameObject("Loading Progress", typeof(RectTransform), typeof(Slider));
            bar.transform.SetParent(parent, false);
            var rect = (RectTransform)bar.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(0, -32); rect.sizeDelta = new Vector2(520, 16);
            loadingProgress = bar.GetComponent<Slider>();
            loadingProgress.interactable = false;
            loadingProgress.transition = Selectable.Transition.None;
            loadingProgress.navigation = new Navigation { mode = Navigation.Mode.None };
            loadingProgress.minValue = 0; loadingProgress.maxValue = 1;
            var track = ProgressImage("Track", bar.transform, "slider_track", Color.white);
            track.type = Image.Type.Sliced;
            var area = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(bar.transform, false);
            area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(6, 3); area.offsetMax = new Vector2(-6, -3);
            var fill = ProgressImage("Fill", area, "fill_runtime", new Color(.8f, .64f, .28f));
            loadingProgress.fillRect = fill.rectTransform;
            loadingProgress.SetValueWithoutNotify(0);
        }

        private Image ProgressImage(string name, Transform parent, string artwork, Color tint)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.rectTransform.anchorMin = Vector2.zero; image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            image.sprite = loadingStyle != null ? loadingStyle.GetArtwork(artwork) : null;
            image.color = image.sprite == null && artwork == "slider_track" ? new Color(.12f, .13f, .15f) : tint;
            image.raycastTarget = false;
            return image;
        }
    }
}
