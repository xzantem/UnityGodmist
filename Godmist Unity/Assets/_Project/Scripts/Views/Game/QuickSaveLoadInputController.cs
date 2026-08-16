using Godmist.Views.UI;
using UnityEngine;

namespace Godmist.Views.Game
{
    public class QuickSaveLoadInputController : MonoBehaviour
    {
        [SerializeField] private KeyCode quickSaveKey = KeyCode.F5;
        [SerializeField] private KeyCode quickLoadKey = KeyCode.F9;

        private GameBootstrap gameBootstrap;

        private void Awake()
        {
            gameBootstrap = GetComponent<GameBootstrap>();

            if (gameBootstrap == null)
            {
                Debug.LogError(
                    "QuickSaveLoadInputController: brak GameBootstrap.");
            }
        }

        private void Update()
        {
            if (DevConsoleView.IsOpen) return;
            
            if (gameBootstrap == null)
            {
                return;
            }

            if (Input.GetKeyDown(quickSaveKey))
            {
                if (gameBootstrap.TrySaveQuick(out var saveMessage))
                {
                    Debug.Log(saveMessage);
                }
                else
                {
                    Debug.LogWarning(saveMessage);
                }
            }

            if (Input.GetKeyDown(quickLoadKey))
            {
                if (gameBootstrap.TryLoadQuick(out var loadMessage))
                {
                    Debug.Log(loadMessage);
                }
                else
                {
                    Debug.LogWarning(loadMessage);
                }
            }
        }
    }
}