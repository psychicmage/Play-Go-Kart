using System.Collections;
using UnityEngine;

namespace PlayGoKart.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(KartController), typeof(RacerProgress))]
    public sealed class PlayerWrongWayRecovery : MonoBehaviour
    {
        [Header("Track")]
        [SerializeField] private AIWaypointPath waypointPath;
        [SerializeField] private Vector3 localForwardAxis = Vector3.right;
        [SerializeField, Min(0f)] private float maximumTrackDistance = 8f;
        [SerializeField, Range(0, 5)] private int checkpointSegmentPadding = 2;

        [Header("Wrong Way Detection")]
        [SerializeField, Min(0f)] private float minimumDetectionSpeed = 3f;
        [SerializeField, Range(-1f, 0f)] private float wrongWayMovementDotThreshold = -0.55f;
        [SerializeField, Range(-1f, 0f)] private float wrongWayFacingDotThreshold = -0.25f;
        [SerializeField, Min(0.1f)] private float wrongWayDetectionTime = 1.5f;

        [Header("Safe Position")]
        [SerializeField, Min(0.05f)] private float safePositionInterval = 0.35f;
        [SerializeField, Min(0f)] private float minimumSafeSpeed = 2f;
        [SerializeField, Range(0f, 1f)] private float safeMovementDotThreshold = 0.55f;
        [SerializeField, Range(0f, 1f)] private float safeFacingDotThreshold = 0.25f;
        [SerializeField, Range(0f, 1f)] private float minimumUprightDot = 0.65f;
        [SerializeField, Min(0.1f)] private float groundCheckDistance = 4f;
        [SerializeField, Min(0f)] private float respawnHeight = 0.15f;
        [SerializeField, Min(0f)] private float inputLockDuration = 0.3f;
        [SerializeField, Min(1f)] private float teleportResetDistance = 10f;

        private readonly RaycastHit[] groundHits = new RaycastHit[8];
        private Rigidbody kartBody;
        private KartController kartController;
        private RacerProgress racerProgress;
        private Vector3 lastObservedPosition;
        private float safePositionTimer;
        private float wrongWayTimer;
        private bool isRespawning;

        public Vector3 LastSafePosition { get; private set; }
        public Quaternion LastSafeRotation { get; private set; }
        public int LastSafeCheckpoint { get; private set; }
        public float WrongWayTimer => wrongWayTimer;
        public bool IsWrongWay => wrongWayTimer > 0f;
        public bool IsRespawning => isRespawning;
        public int RespawnCount { get; private set; }
        public int CurrentTrackWaypoint { get; private set; }
        public float CurrentMovementDot { get; private set; }
        public float CurrentFacingDot { get; private set; }

        private void Awake()
        {
            kartBody = GetComponent<Rigidbody>();
            kartController = GetComponent<KartController>();
            racerProgress = GetComponent<RacerProgress>();

            if (waypointPath == null || waypointPath.Count < 2)
            {
                Debug.LogError($"{nameof(PlayerWrongWayRecovery)} on '{name}' needs a waypoint path.", this);
                enabled = false;
                return;
            }

            RecordCurrentPositionAsSafe();
            lastObservedPosition = kartBody.position;
        }

        private void FixedUpdate()
        {
            if (isRespawning)
            {
                return;
            }

            if (Vector3.Distance(kartBody.position, lastObservedPosition) >= teleportResetDistance)
            {
                RecordCurrentPositionAsSafe();
                ResetWrongWayTimer();
            }
            lastObservedPosition = kartBody.position;

            if (!kartController.InputEnabled || racerProgress.Finished)
            {
                ResetWrongWayTimer();
                return;
            }

            Vector3 trackDirection;
            float trackDistance;
            SampleTrack(out trackDirection, out trackDistance);

            Vector3 planarVelocity = Vector3.ProjectOnPlane(kartBody.linearVelocity, Vector3.up);
            float speed = planarVelocity.magnitude;
            Vector3 kartForward = Vector3.ProjectOnPlane(
                transform.TransformDirection(localForwardAxis.normalized),
                Vector3.up).normalized;
            CurrentFacingDot = Vector3.Dot(kartForward, trackDirection);
            CurrentMovementDot = speed > 0.01f
                ? Vector3.Dot(planarVelocity / speed, trackDirection)
                : 0f;

            bool wrongWay = speed >= minimumDetectionSpeed &&
                trackDistance <= maximumTrackDistance &&
                CurrentMovementDot <= wrongWayMovementDotThreshold &&
                CurrentFacingDot <= wrongWayFacingDotThreshold;

            if (wrongWay)
            {
                wrongWayTimer += Time.fixedDeltaTime;
                safePositionTimer = 0f;
                if (wrongWayTimer >= wrongWayDetectionTime)
                {
                    StartCoroutine(RespawnAtLastSafePosition());
                }
                return;
            }

            wrongWayTimer = Mathf.MoveTowards(wrongWayTimer, 0f, Time.fixedDeltaTime * 2f);
            TryRecordSafePosition(trackDirection, trackDistance, speed);
        }

        private void TryRecordSafePosition(Vector3 trackDirection, float trackDistance, float speed)
        {
            safePositionTimer += Time.fixedDeltaTime;
            if (safePositionTimer < safePositionInterval ||
                speed < minimumSafeSpeed ||
                trackDistance > maximumTrackDistance ||
                CurrentMovementDot < safeMovementDotThreshold ||
                CurrentFacingDot < safeFacingDotThreshold ||
                Vector3.Dot(transform.up, Vector3.up) < minimumUprightDot ||
                !HasGroundBelow())
            {
                return;
            }

            LastSafePosition = kartBody.position + Vector3.up * respawnHeight;
            LastSafeRotation = Quaternion.FromToRotation(localForwardAxis.normalized, trackDirection);
            LastSafeCheckpoint = racerProgress.CompletedCheckpoints;
            safePositionTimer = 0f;
        }

        private bool HasGroundBelow()
        {
            Vector3 origin = kartBody.position + Vector3.up * 1.5f;
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                groundHits,
                groundCheckDistance,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = groundHits[i].collider;
                if (hit != null && !hit.transform.IsChildOf(transform) &&
                    Vector3.Dot(groundHits[i].normal, Vector3.up) >= 0.45f)
                {
                    return true;
                }
            }

            return false;
        }

        private void SampleTrack(out Vector3 direction, out float distance)
        {
            CurrentTrackWaypoint = waypointPath.FindClosestWaypointInCheckpointSegment(
                kartBody.position,
                racerProgress.CompletedCheckpoints,
                racerProgress.CheckpointCount,
                checkpointSegmentPadding);
            Vector3 point = waypointPath.GetPosition(CurrentTrackWaypoint);
            direction = waypointPath.GetDirection(CurrentTrackWaypoint);
            distance = Vector3.ProjectOnPlane(kartBody.position - point, Vector3.up).magnitude;
        }

        private void RecordCurrentPositionAsSafe()
        {
            Vector3 direction;
            float unusedDistance;
            SampleTrack(out direction, out unusedDistance);
            LastSafePosition = kartBody.position + Vector3.up * respawnHeight;
            LastSafeRotation = Quaternion.FromToRotation(localForwardAxis.normalized, direction);
            LastSafeCheckpoint = racerProgress.CompletedCheckpoints;
            safePositionTimer = 0f;
        }

        private IEnumerator RespawnAtLastSafePosition()
        {
            isRespawning = true;
            kartController.SetInputEnabled(false);
            kartBody.position = LastSafePosition;
            kartBody.rotation = LastSafeRotation;
            kartBody.linearVelocity = Vector3.zero;
            kartBody.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
            RespawnCount++;
            ResetWrongWayTimer();
            lastObservedPosition = kartBody.position;

            if (inputLockDuration > 0f)
            {
                yield return new WaitForSeconds(inputLockDuration);
            }

            if (!racerProgress.Finished)
            {
                kartController.SetInputEnabled(true);
            }
            isRespawning = false;
        }

        private void ResetWrongWayTimer()
        {
            wrongWayTimer = 0f;
            CurrentMovementDot = 0f;
            CurrentFacingDot = 0f;
        }

        private void OnValidate()
        {
            if (localForwardAxis.sqrMagnitude < 0.001f)
            {
                localForwardAxis = Vector3.right;
            }
        }
    }
}
