using System.Collections;
using System.Text;
using PlayGoKart.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace PlayGoKart.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class RaceManager : MonoBehaviour
    {
        [Header("Race")]
        [SerializeField, Min(1)] private int totalLaps = 3;
        [SerializeField, Min(1)] private int intermediateCheckpointCount = 8;
        [SerializeField, Min(0f)] private float preCountdownDelay = 2f;
        [SerializeField, Min(0.1f)] private float countdownDuration = 3f;
        [SerializeField, Min(0.1f)] private float goDisplayDuration = 0.8f;
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("Systems")]
        [SerializeField] private RaceTimer raceTimer;
        [SerializeField] private RaceUIController raceUI;
        [SerializeField] private RacePositionManager positionManager;
        [SerializeField] private AIWaypointPath waypointPath;

        [Header("Racers")]
        [SerializeField] private RacerProgress playerProgress;
        [SerializeField] private KartController playerController;
        [SerializeField] private Rigidbody playerBody;
        [SerializeField] private RacerProgress[] racers;
        [SerializeField] private AIKartController[] aiControllers;
        [SerializeField, Min(0f)] private float resetHeightOffset = 0.15f;
        [SerializeField, Min(0f)] private float finishCoastDuration = 2.5f;
        [SerializeField, Min(0f)] private float finishCoastAcceleration = 12f;

        private RaceState state;
        private RaceState stateBeforePause;
        private float preCountdownRemaining;
        private float countdownRemaining;
        private float goMessageRemaining;
        private float leaderboardRefreshRemaining;
        private int displayedCountdownNumber;
        private bool settingsOpen;

        public RaceState State => state;

        private void Awake()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            ConfigureRacers();
            raceTimer.ResetTimer();
            positionManager.Configure(waypointPath, racers);
            SetAllControls(false);
            raceUI.Initialize(1, totalLaps, raceTimer.FormattedTime, 1, racers.Length);
            raceUI.UpdateLeaderboard(positionManager.OrderedRacers);
        }

        private void Start()
        {
            if (enabled)
            {
                BeginCountdown();
            }
        }

        private void Update()
        {
            HandleKeyboardInput();
            if (state == RaceState.Countdown)
            {
                TickCountdown(Time.unscaledDeltaTime);
            }
            else if (state == RaceState.Racing || state == RaceState.PlayerFinished)
            {
                TickRace(Time.unscaledDeltaTime);
            }
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        public void RegisterCheckpoint(RacerProgress racer, int checkpointIndex, Transform resetTransform)
        {
            if ((state != RaceState.Racing && state != RaceState.PlayerFinished) || racer == null || racer.Finished)
            {
                return;
            }

            RacerCheckpointResult result = racer.TryPassCheckpoint(checkpointIndex, resetTransform, resetHeightOffset);
            if (result == RacerCheckpointResult.Ignored)
            {
                return;
            }

            if (racer.IsPlayer && result == RacerCheckpointResult.LapCompleted)
            {
                raceUI.UpdateLap(racer.CurrentLap, totalLaps);
            }
            else if (result == RacerCheckpointResult.RaceCompleted)
            {
                FinishRacer(racer);
            }

            positionManager.UpdatePositions();
        }

        public void PauseRace()
        {
            if (state == RaceState.Paused || state == RaceState.RaceFinished)
            {
                return;
            }

            stateBeforePause = state;
            state = RaceState.Paused;
            settingsOpen = false;
            SetAllControls(false);
            Time.timeScale = 0f;
            AudioListener.pause = true;
            raceUI.HideResults();
            raceUI.ShowPause();
        }

        public void ResumeRace()
        {
            if (state != RaceState.Paused)
            {
                return;
            }

            settingsOpen = false;
            raceUI.HidePauseViews();
            AudioListener.pause = false;
            Time.timeScale = 1f;
            state = stateBeforePause;
            RestoreControlsForState();
            if (state == RaceState.PlayerFinished)
            {
                raceUI.ShowResults(BuildResultsText(), false);
            }
        }

        public void OpenSettings()
        {
            if (state == RaceState.Paused)
            {
                settingsOpen = true;
                raceUI.ShowSettings();
            }
        }

        public void CloseSettings()
        {
            if (state == RaceState.Paused)
            {
                settingsOpen = false;
                raceUI.ShowPause();
            }
        }

        public void RetryRace()
        {
            AudioListener.pause = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ReturnToMainMenu()
        {
            AudioListener.pause = false;
            Time.timeScale = 1f;
            if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            {
                Debug.LogError($"Main menu scene '{mainMenuSceneName}' is not available in Build Settings.", this);
                return;
            }

            SceneManager.LoadScene(mainMenuSceneName);
        }

        public void ResetKart()
        {
            if (playerProgress.Finished || state == RaceState.RaceFinished)
            {
                return;
            }

            playerBody.position = playerProgress.ResetPosition;
            playerBody.rotation = playerProgress.ResetRotation;
            playerBody.linearVelocity = Vector3.zero;
            playerBody.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        private void ConfigureRacers()
        {
            int aiNumber = 1;
            int checkpointCount = intermediateCheckpointCount + 1;
            for (int i = 0; i < racers.Length; i++)
            {
                bool isPlayer = racers[i] == playerProgress;
                racers[i].Configure(isPlayer ? "PLAYER" : $"AI KART {aiNumber++}", isPlayer, totalLaps, checkpointCount);
            }
        }

        private void BeginCountdown()
        {
            state = RaceState.Countdown;
            preCountdownRemaining = preCountdownDelay;
            countdownRemaining = countdownDuration;
            displayedCountdownNumber = -1;
            goMessageRemaining = 0f;
            SetAllControls(false);
            raceUI.HideCenterMessage();
            if (preCountdownRemaining <= 0f)
            {
                ShowCountdownNumber();
            }
        }

        private void TickCountdown(float unscaledDeltaTime)
        {
            if (preCountdownRemaining > 0f)
            {
                preCountdownRemaining -= unscaledDeltaTime;
                if (preCountdownRemaining > 0f)
                {
                    return;
                }

                countdownRemaining = countdownDuration;
                displayedCountdownNumber = -1;
                ShowCountdownNumber();
                return;
            }

            countdownRemaining -= unscaledDeltaTime;
            if (countdownRemaining <= 0f)
            {
                state = RaceState.Racing;
                raceTimer.StartTimer();
                RestoreControlsForState();
                goMessageRemaining = goDisplayDuration;
                raceUI.ShowCenterMessage("GO!");
                return;
            }

            ShowCountdownNumber();
        }

        private void ShowCountdownNumber()
        {
            int number = Mathf.Clamp(Mathf.CeilToInt(countdownRemaining), 1, 3);
            if (number != displayedCountdownNumber)
            {
                displayedCountdownNumber = number;
                raceUI.ShowCenterMessage(number.ToString());
            }
        }

        private void TickRace(float unscaledDeltaTime)
        {
            raceTimer.Tick(unscaledDeltaTime);
            positionManager.UpdatePositions();
            raceUI.UpdateTimer(raceTimer.FormattedTime);
            raceUI.UpdatePosition(playerProgress.CurrentPosition, racers.Length);
            raceUI.UpdateSpeed(playerController.ForwardSpeed);

            leaderboardRefreshRemaining -= unscaledDeltaTime;
            if (leaderboardRefreshRemaining <= 0f)
            {
                leaderboardRefreshRemaining = 0.2f;
                raceUI.UpdateLeaderboard(positionManager.OrderedRacers);
            }

            if (!playerProgress.Finished)
            {
                raceUI.UpdateLap(playerProgress.CurrentLap, totalLaps);
            }
            else
            {
                raceUI.ShowResults(BuildResultsText(), AllRacersFinished());
            }

            if (goMessageRemaining > 0f)
            {
                goMessageRemaining -= unscaledDeltaTime;
                if (goMessageRemaining <= 0f)
                {
                    raceUI.HideCenterMessage();
                }
            }
        }

        private void FinishRacer(RacerProgress racer)
        {
            positionManager.RegisterFinish(racer, raceTimer.ElapsedSeconds);
            KartController controller = racer.GetComponent<KartController>();
            controller.SetInputEnabled(false);
            controller.SetExternalInput(0f, 0f);

            Rigidbody body = racer.GetComponent<Rigidbody>();
            StartCoroutine(CoastFinishedRacer(body));

            AIKartController ai = racer.GetComponent<AIKartController>();
            if (ai != null)
            {
                ai.SetDrivingEnabled(false);
            }

            if (racer.IsPlayer)
            {
                state = RaceState.PlayerFinished;
                raceUI.ShowResults(BuildResultsText(), false);
            }

            if (AllRacersFinished())
            {
                state = RaceState.RaceFinished;
                raceTimer.StopTimer();
                SetAllControls(false);
                raceUI.ShowResults(BuildResultsText(), true);
            }
        }

        private IEnumerator CoastFinishedRacer(Rigidbody body)
        {
            float remaining = finishCoastDuration;
            while (remaining > 0f && body != null)
            {
                Vector3 forward = Vector3.ProjectOnPlane(body.transform.right, Vector3.up).normalized;
                body.AddForce(forward * finishCoastAcceleration, ForceMode.Acceleration);
                remaining -= Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
        }

        private string BuildResultsText()
        {
            StringBuilder builder = new StringBuilder(160);
            builder.AppendLine("RACE RESULT");
            foreach (RacerProgress racer in positionManager.OrderedRacers)
            {
                int position = racer.Finished ? racer.FinishPosition : racer.CurrentPosition;
                builder.Append(position).Append(GetOrdinalSuffix(position)).Append("  ")
                    .Append(racer.RacerName).Append("   ")
                    .AppendLine(racer.Finished ? RaceTimer.FormatTime(racer.FinishTime) : "RACING...");
            }

            return builder.ToString().TrimEnd();
        }

        private bool AllRacersFinished()
        {
            for (int i = 0; i < racers.Length; i++)
            {
                if (!racers[i].Finished)
                {
                    return false;
                }
            }

            return true;
        }

        private void SetAllControls(bool enabledControls)
        {
            playerController.SetInputEnabled(enabledControls && !playerProgress.Finished);
            for (int i = 0; i < aiControllers.Length; i++)
            {
                RacerProgress progress = aiControllers[i].GetComponent<RacerProgress>();
                aiControllers[i].SetDrivingEnabled(enabledControls && !progress.Finished);
            }
        }

        private void RestoreControlsForState()
        {
            bool raceActive = state == RaceState.Racing || state == RaceState.PlayerFinished;
            SetAllControls(raceActive);
        }

        private void HandleKeyboardInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (state == RaceState.Paused)
                {
                    if (settingsOpen)
                    {
                        CloseSettings();
                    }
                    else
                    {
                        ResumeRace();
                    }
                }
                else if (state != RaceState.RaceFinished)
                {
                    PauseRace();
                }
            }

            if (keyboard.rKey.wasPressedThisFrame || playerController.transform.position.y < -15f)
            {
                ResetKart();
            }
        }

        private bool ValidateReferences()
        {
            if (raceTimer == null || raceUI == null || positionManager == null || waypointPath == null ||
                playerProgress == null || playerController == null || playerBody == null ||
                racers == null || racers.Length != 4 || aiControllers == null || aiControllers.Length != 3)
            {
                Debug.LogError("RaceManager needs the race systems, one player, and exactly three AI racers.", this);
                return false;
            }

            return true;
        }

        private static string GetOrdinalSuffix(int number)
        {
            return number == 1 ? "st" : number == 2 ? "nd" : number == 3 ? "rd" : "th";
        }
    }
}
