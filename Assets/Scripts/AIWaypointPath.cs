using System.Collections.Generic;
using UnityEngine;

namespace PlayGoKart.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class AIWaypointPath : MonoBehaviour
    {
        [SerializeField] private List<Transform> waypoints = new List<Transform>();

        public int Count => waypoints.Count;

        public void SetWaypoints(List<Transform> value)
        {
            waypoints = value ?? new List<Transform>();
        }

        public Vector3 GetPosition(int index, float lateralOffset = 0f)
        {
            if (waypoints.Count == 0)
            {
                return transform.position;
            }

            int wrapped = Wrap(index);
            Vector3 position = waypoints[wrapped].position;
            Vector3 direction = GetDirection(wrapped);
            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            return position + right * lateralOffset;
        }

        public Vector3 GetDirection(int index)
        {
            if (waypoints.Count < 2)
            {
                return transform.right;
            }

            Vector3 direction = waypoints[Wrap(index + 1)].position - waypoints[Wrap(index)].position;
            return Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        }

        public int FindClosestWaypoint(Vector3 position)
        {
            int closest = 0;
            float closestDistance = float.MaxValue;
            for (int i = 0; i < waypoints.Count; i++)
            {
                float distance = (waypoints[i].position - position).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = i;
                }
            }

            return closest;
        }

        public int FindClosestWaypointInCheckpointSegment(
            Vector3 position,
            int completedCheckpoints,
            int checkpointCount,
            int padding = 2)
        {
            if (waypoints.Count == 0 || checkpointCount < 1)
            {
                return 0;
            }

            int pointsPerSegment = Mathf.Max(1, waypoints.Count / checkpointCount);
            int segment = (completedCheckpoints % checkpointCount + checkpointCount) % checkpointCount;
            int segmentStart = segment * pointsPerSegment;
            int clampedPadding = Mathf.Clamp(padding, 0, pointsPerSegment);
            int closest = Wrap(segmentStart);
            float closestDistance = float.MaxValue;

            for (int step = -clampedPadding; step < pointsPerSegment + clampedPadding; step++)
            {
                int index = Wrap(segmentStart + step);
                Vector3 delta = Vector3.ProjectOnPlane(waypoints[index].position - position, Vector3.up);
                float distance = delta.sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = index;
                }
            }

            return closest;
        }

        public float GetNearestProgress(Vector3 position)
        {
            if (waypoints.Count < 2)
            {
                return 0f;
            }

            int nearest = FindClosestWaypoint(position);
            int previous = Wrap(nearest - 1);
            Vector3 start = waypoints[previous].position;
            Vector3 segment = waypoints[nearest].position - start;
            float interpolation = segment.sqrMagnitude < 0.001f
                ? 0f
                : Mathf.Clamp01(Vector3.Dot(position - start, segment) / segment.sqrMagnitude);
            return previous + interpolation;
        }

        public float GetCheckpointSegmentProgress(Vector3 position, int completedCheckpoints, int checkpointCount)
        {
            if (waypoints.Count < 2 || checkpointCount < 1)
            {
                return 0f;
            }

            int pointsPerSegment = Mathf.Max(1, waypoints.Count / checkpointCount);
            int segment = (completedCheckpoints % checkpointCount + checkpointCount) % checkpointCount;
            int segmentStart = segment * pointsPerSegment;
            float bestDistance = float.MaxValue;
            float bestProgress = 0f;

            for (int step = 0; step < pointsPerSegment; step++)
            {
                int startIndex = Wrap(segmentStart + step);
                int endIndex = Wrap(startIndex + 1);
                Vector3 start = waypoints[startIndex].position;
                Vector3 edge = waypoints[endIndex].position - start;
                float t = edge.sqrMagnitude < 0.001f
                    ? 0f
                    : Mathf.Clamp01(Vector3.Dot(position - start, edge) / edge.sqrMagnitude);
                Vector3 nearest = start + edge * t;
                float distance = (position - nearest).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestProgress = step + t;
                }
            }

            return bestProgress / pointsPerSegment;
        }

        private int Wrap(int index)
        {
            return (index % waypoints.Count + waypoints.Count) % waypoints.Count;
        }

        private void OnDrawGizmosSelected()
        {
            if (waypoints == null || waypoints.Count < 2)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] == null || waypoints[(i + 1) % waypoints.Count] == null)
                {
                    continue;
                }

                Gizmos.DrawSphere(waypoints[i].position, 0.4f);
                Gizmos.DrawLine(waypoints[i].position, waypoints[(i + 1) % waypoints.Count].position);
            }
        }
    }
}
