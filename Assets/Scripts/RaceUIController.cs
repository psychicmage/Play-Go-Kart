using System.Collections.Generic;
using System.Text;
using PlayGoKart.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace PlayGoKart.UI
{
    [DisallowMultipleComponent]
    public sealed class RaceUIController : MonoBehaviour
    {
        [Header("HUD")]
        [SerializeField] private Text lapText;
        [SerializeField] private Text timerText;
        [SerializeField] private Text positionText;
        [SerializeField] private Text countdownText;
        [SerializeField] private Text leaderboardText;
        [SerializeField] private Text speedText;

        [Header("Panels")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject finishPanel;
        [SerializeField] private Text finalTimeText;
        [SerializeField] private SettingsManager settingsManager;

        private readonly StringBuilder leaderboardBuilder = new StringBuilder(160);
        private string lastLeaderboardText;
        private int lastSpeedKph = int.MinValue;

        public bool SettingsVisible => settingsPanel != null && settingsPanel.activeSelf;

        public void Initialize(int currentLap, int totalLaps, string formattedTime, int position, int racerCount)
        {
            if (leaderboardText != null)
            {
                leaderboardText.supportRichText = true;
            }
            UpdateLap(currentLap, totalLaps);
            UpdateTimer(formattedTime);
            UpdatePosition(position, racerCount);
            SetPanelActive(pausePanel, false);
            SetPanelActive(settingsPanel, false);
            SetPanelActive(finishPanel, false);
            UpdateSpeed(0f);
            HideCenterMessage();
        }

        public void UpdateLeaderboard(IReadOnlyList<RacerProgress> racers)
        {
            if (leaderboardText == null || racers == null)
            {
                return;
            }

            leaderboardBuilder.Clear();
            leaderboardBuilder.AppendLine("RANKING");
            for (int i = 0; i < racers.Count; i++)
            {
                RacerProgress racer = racers[i];
                if (racer.Finished)
                {
                    leaderboardBuilder.Append("<color=#8FC7FF>");
                }
                leaderboardBuilder.Append(racer.IsPlayer ? "> " : "  ")
                    .Append(i + 1).Append(". ")
                    .Append(racer.RacerName);

                if (racer.Finished)
                {
                    leaderboardBuilder.Append("</color>");
                }

                if (i < racers.Count - 1)
                {
                    leaderboardBuilder.AppendLine();
                }
            }

            string text = leaderboardBuilder.ToString();
            if (text != lastLeaderboardText)
            {
                lastLeaderboardText = text;
                leaderboardText.text = text;
            }
        }

        public void UpdateSpeed(float metersPerSecond)
        {
            if (speedText == null)
            {
                return;
            }

            int speedKph = Mathf.Max(0, Mathf.RoundToInt(Mathf.Abs(metersPerSecond) * 3.6f));
            if (speedKph == lastSpeedKph)
            {
                return;
            }

            lastSpeedKph = speedKph;
            speedText.text = $"SPEED\n{speedKph:000} km/h";
        }

        public void UpdatePosition(int position, int racerCount)
        {
            if (positionText != null)
            {
                positionText.text = $"POSITION {Mathf.Max(1, position)} / {Mathf.Max(1, racerCount)}";
            }
        }

        public void UpdateLap(int currentLap, int totalLaps)
        {
            if (lapText != null)
            {
                lapText.text = $"LAP {currentLap} / {totalLaps}";
            }
        }

        public void UpdateTimer(string formattedTime)
        {
            if (timerText != null)
            {
                timerText.text = $"TIME {formattedTime}";
            }
        }

        public void ShowCenterMessage(string message)
        {
            if (countdownText == null)
            {
                return;
            }

            countdownText.text = message;
            countdownText.gameObject.SetActive(true);
        }

        public void HideCenterMessage()
        {
            if (countdownText != null)
            {
                countdownText.gameObject.SetActive(false);
            }
        }

        public void ShowPause()
        {
            SetPanelActive(settingsPanel, false);
            SetPanelActive(pausePanel, true);
        }

        public void HidePauseViews()
        {
            SetPanelActive(pausePanel, false);
            SetPanelActive(settingsPanel, false);
        }

        public void ShowSettings()
        {
            SetPanelActive(pausePanel, false);
            SetPanelActive(settingsPanel, true);
            settingsManager?.OnSettingsOpened();
        }

        public void ShowResults(string resultsText, bool allFinished)
        {
            HidePauseViews();
            if (finalTimeText != null)
            {
                finalTimeText.text = resultsText;
            }

            if (allFinished)
            {
                ShowCenterMessage("RACE FINISHED!");
            }
            else
            {
                ShowCenterMessage("FINISH!");
            }

            SetPanelActive(finishPanel, true);
        }

        public void HideResults()
        {
            SetPanelActive(finishPanel, false);
        }

        private static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
            {
                panel.SetActive(active);
            }
        }
    }
}
