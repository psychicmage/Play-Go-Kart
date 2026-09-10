using UnityEngine;

namespace PlayGoKart.Gameplay
{
    public enum CheckpointPassResult
    {
        Ignored,
        CheckpointAccepted,
        LapCompleted,
        RaceCompleted
    }

    [DisallowMultipleComponent]
    public sealed class LapManager : MonoBehaviour
    {
        [SerializeField, Min(1)] private int totalLaps = 3;
        [SerializeField, Min(1)] private int checkpointCount = 8;

        private int currentLap;
        private int expectedCheckpoint;
        private bool startLineRegistered;

        public int CurrentLap => currentLap;
        public int TotalLaps => totalLaps;
        public int ExpectedCheckpoint => expectedCheckpoint;
        public bool StartLineRegistered => startLineRegistered;

        public void Configure(int laps, int intermediateCheckpointCount)
        {
            totalLaps = Mathf.Max(1, laps);
            checkpointCount = Mathf.Max(1, intermediateCheckpointCount);
            ResetProgress();
        }

        public void ResetProgress()
        {
            currentLap = 1;
            expectedCheckpoint = 1;
            startLineRegistered = false;
        }

        public CheckpointPassResult TryPassCheckpoint(int checkpointIndex, bool distanceRequirementMet = false)
        {
            if (checkpointIndex == expectedCheckpoint && checkpointIndex >= 1 && checkpointIndex <= checkpointCount)
            {
                expectedCheckpoint++;
                return CheckpointPassResult.CheckpointAccepted;
            }

            if (checkpointIndex != 0)
            {
                return CheckpointPassResult.Ignored;
            }

            if (!startLineRegistered)
            {
                startLineRegistered = true;
                expectedCheckpoint = 1;
                return CheckpointPassResult.CheckpointAccepted;
            }

            bool allCheckpointsPassed = expectedCheckpoint == checkpointCount + 1;
            if (!allCheckpointsPassed && !distanceRequirementMet)
            {
                return CheckpointPassResult.Ignored;
            }

            expectedCheckpoint = 1;
            if (currentLap >= totalLaps)
            {
                return CheckpointPassResult.RaceCompleted;
            }

            currentLap++;
            return CheckpointPassResult.LapCompleted;
        }
    }
}
