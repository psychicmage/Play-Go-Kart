using System.Collections.Generic;
using UnityEngine;

namespace PlayGoKart.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class RacePositionManager : MonoBehaviour
    {
        [SerializeField] private AIWaypointPath path;
        private readonly List<RacerProgress> racers = new List<RacerProgress>(4);

        public IReadOnlyList<RacerProgress> OrderedRacers => racers;

        public void Configure(AIWaypointPath waypointPath, RacerProgress[] raceParticipants)
        {
            path = waypointPath;
            racers.Clear();
            if (raceParticipants != null)
            {
                racers.AddRange(raceParticipants);
            }

            UpdatePositions();
        }

        public int RegisterFinish(RacerProgress racer, double finishTime)
        {
            int position = 1;
            for (int i = 0; i < racers.Count; i++)
            {
                if (racers[i] != racer && racers[i].Finished)
                {
                    position++;
                }
            }

            racer.MarkFinished(finishTime, position);
            UpdatePositions();
            return position;
        }

        public void UpdatePositions()
        {
            racers.Sort(CompareRacers);
            for (int i = 0; i < racers.Count; i++)
            {
                racers[i].SetCurrentPosition(i + 1);
            }
        }

        private int CompareRacers(RacerProgress left, RacerProgress right)
        {
            if (left.Finished || right.Finished)
            {
                if (left.Finished && right.Finished)
                {
                    return left.FinishPosition.CompareTo(right.FinishPosition);
                }

                return left.Finished ? -1 : 1;
            }

            int checkpointComparison = right.CompletedCheckpoints.CompareTo(left.CompletedCheckpoints);
            if (checkpointComparison != 0)
            {
                return checkpointComparison;
            }

            const int CheckpointCount = 9;
            float leftProgress = path == null ? 0f : path.GetCheckpointSegmentProgress(
                left.transform.position, left.CompletedCheckpoints, CheckpointCount);
            float rightProgress = path == null ? 0f : path.GetCheckpointSegmentProgress(
                right.transform.position, right.CompletedCheckpoints, CheckpointCount);
            return rightProgress.CompareTo(leftProgress);
        }
    }
}
