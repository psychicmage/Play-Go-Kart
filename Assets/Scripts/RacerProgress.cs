using UnityEngine;

namespace PlayGoKart.Gameplay
{
    public enum RacerCheckpointResult
    {
        Ignored,
        CheckpointPassed,
        LapCompleted,
        RaceCompleted
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RacerProgress : MonoBehaviour
    {
        [SerializeField] private string racerName = "RACER";
        [SerializeField] private bool isPlayer;

        public string RacerName => racerName;
        public bool IsPlayer => isPlayer;
        public int CurrentLap { get; private set; } = 1;
        public int ExpectedCheckpoint { get; private set; } = 1;
        public int CompletedCheckpoints { get; private set; }
        public bool Finished { get; private set; }
        public double FinishTime { get; private set; }
        public int FinishPosition { get; private set; }
        public int CurrentPosition { get; private set; } = 1;
        public int CheckpointCount => checkpointCount;
        public Vector3 ResetPosition { get; private set; }
        public Quaternion ResetRotation { get; private set; }

        private int totalLaps = 3;
        private int checkpointCount = 9;

        public void Configure(string displayName, bool player, int laps, int checkpoints)
        {
            racerName = string.IsNullOrWhiteSpace(displayName) ? name : displayName;
            isPlayer = player;
            totalLaps = Mathf.Max(1, laps);
            checkpointCount = Mathf.Max(2, checkpoints);
            ResetProgress();
        }

        public void ResetProgress()
        {
            CurrentLap = 1;
            ExpectedCheckpoint = 1;
            CompletedCheckpoints = 0;
            Finished = false;
            FinishTime = 0d;
            FinishPosition = 0;
            CurrentPosition = 1;
            ResetPosition = transform.position;
            ResetRotation = transform.rotation;
        }

        public RacerCheckpointResult TryPassCheckpoint(int checkpointIndex, Transform checkpointTransform, float resetHeight)
        {
            if (Finished || checkpointIndex != ExpectedCheckpoint)
            {
                return RacerCheckpointResult.Ignored;
            }

            if (checkpointTransform != null)
            {
                ResetPosition = checkpointTransform.position + Vector3.up * resetHeight;
                ResetRotation = checkpointTransform.rotation;
            }

            if (checkpointIndex == 0)
            {
                CompletedCheckpoints++;
                if (CurrentLap >= totalLaps)
                {
                    return RacerCheckpointResult.RaceCompleted;
                }

                CurrentLap++;
                ExpectedCheckpoint = 1;
                return RacerCheckpointResult.LapCompleted;
            }

            CompletedCheckpoints++;
            ExpectedCheckpoint = checkpointIndex == checkpointCount - 1 ? 0 : checkpointIndex + 1;
            return RacerCheckpointResult.CheckpointPassed;
        }

        public void MarkFinished(double time, int position)
        {
            Finished = true;
            FinishTime = time;
            FinishPosition = position;
            CurrentPosition = position;
        }

        public void SetCurrentPosition(int position)
        {
            if (!Finished)
            {
                CurrentPosition = position;
            }
        }
    }
}
