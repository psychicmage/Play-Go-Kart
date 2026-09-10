using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlayGoKart.UI
{
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private string gameplaySceneName = "InGame";

        [Header("Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject settingsPanel;

        private void Awake()
        {
            ShowMainMenu();
        }

        public void StartGame()
        {
            if (!Application.CanStreamedLevelBeLoaded(gameplaySceneName))
            {
                Debug.LogError(
                    $"Gameplay scene '{gameplaySceneName}' is not available in Build Settings.",
                    this);
                return;
            }

            SceneManager.LoadScene(gameplaySceneName);
        }

        public void OpenSettings()
        {
            SetPanelVisibility(showSettings: true);
        }

        public void CloseSettings()
        {
            ShowMainMenu();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            Debug.Log("QUIT button pressed. Application.Quit() will run in a player build.", this);
#else
            Application.Quit();
#endif
        }

        private void ShowMainMenu()
        {
            SetPanelVisibility(showSettings: false);
        }

        private void SetPanelVisibility(bool showSettings)
        {
            if (mainMenuPanel == null || settingsPanel == null)
            {
                Debug.LogError("Main menu panel references are not assigned.", this);
                return;
            }

            mainMenuPanel.SetActive(!showSettings);
            settingsPanel.SetActive(showSettings);
        }
    }
}
