using UnityEngine;

namespace PlayGoKart.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(KartController), typeof(Rigidbody), typeof(RacerProgress))]
    public sealed class AIKartController : MonoBehaviour
    {
        [Header("Path")]
        [SerializeField] private AIWaypointPath path;
        [SerializeField, Min(1f)] private float waypointReachDistance = 5f;
        [SerializeField] private float pathOffset;
        [SerializeField, Min(0.5f)] private float maximumPathOffset = 3.5f;
        [SerializeField, Min(0.05f)] private float pathOffsetSmoothTime = 0.22f;
        [SerializeField, Min(0f)] private float racingLineOffset = 1.1f;

        [Header("Look Ahead")]
        [SerializeField, Range(1, 6)] private int minimumLookAheadWaypoints = 2;
        [SerializeField, Range(3, 10)] private int maximumLookAheadWaypoints = 7;
        [SerializeField, Range(2, 5)] private int maximumSteeringLookAheadWaypoints = 3;
        [SerializeField, Range(3, 10)] private int cornerAnalysisWaypoints = 7;

        [Header("Driving Profile")]
        [SerializeField, Min(1f)] private float maximumSpeed = 16.5f;
        [SerializeField, Range(0.1f, 1f)] private float throttleStrength = 1f;
        [SerializeField, Min(1f)] private float cornerSpeed = 8f;
        [SerializeField, Range(0.1f, 2f)] private float steeringSensitivity = 1.1f;
        [SerializeField, Range(0f, 1f)] private float brakeStrength = 0.75f;
        [SerializeField, Min(0.1f)] private float straightAccelerationBuildTime = 1.5f;
        [SerializeField, Range(1f, 2f)] private float straightAccelerationMultiplier = 2f;
        [SerializeField, Range(1f, 2f)] private float straightTopSpeedMultiplier = 2f;
        [SerializeField, Min(1f)] private float estimatedBrakeDeceleration = 16f;
        [SerializeField, Min(0f)] private float coastOverspeedMargin = 1.5f;

        [Header("Personality")]
        [SerializeField, Range(0f, 1f)] private float aggression = 0.6f;
        [SerializeField, Range(0.8f, 1.2f)] private float cornerSpeedFactor = 1f;
        [SerializeField, Range(0.5f, 1.5f)] private float brakeAggressiveness = 1f;
        [SerializeField, Range(0.7f, 1.3f)] private float lateBrakeFactor = 1f;
        [SerializeField, Range(0.7f, 1.3f)] private float lookAheadMultiplier = 1f;
        [SerializeField, Range(0.05f, 0.35f)] private float steeringSmoothTime = 0.14f;
        [SerializeField, Range(-1f, 1f)] private float overtakeBias;

        [Header("Per Race Variation")]
        [SerializeField, Min(0f)] private float randomPathOffsetRange = 0.3f;
        [SerializeField, Range(0f, 0.15f)] private float randomPersonalityRange = 0.05f;

        [Header("Traffic")]
        [SerializeField, Min(1f)] private float sensorDistance = 8f;
        [SerializeField, Min(0.1f)] private float sensorRadius = 1.2f;
        [SerializeField, Min(0f)] private float overtakeOffset = 2.5f;
        [SerializeField, Range(-1, 1)] private int overtakeDirection = 1;
        [SerializeField, Min(0.5f)] private float racerSeparationDistance = 4f;
        [SerializeField, Min(0f)] private float racerSeparationOffset = 2f;
        [SerializeField, Min(0.1f)] private float separationLaneChangeSpeed = 8f;

        [Header("Wall Avoidance")]
        [SerializeField, Min(1f)] private float wallSensorDistance = 8f;
        [SerializeField, Range(5f, 45f)] private float wallSensorAngle = 26f;
        [SerializeField, Min(0.1f)] private float wallSensorRadius = 0.55f;
        [SerializeField, Range(0f, 1f)] private float wallAvoidanceStrength = 0.7f;

        [Header("Recovery")]
        [SerializeField, Min(0.5f)] private float stuckTimeout = 3f;
        [SerializeField, Min(0.05f)] private float recoveryHeight = 0.15f;
        [SerializeField, Min(1f)] private float maximumPathDistance = 18f;

        private readonly RaycastHit[] sensorHits = new RaycastHit[8];
        private readonly RaycastHit[] sideSensorHits = new RaycastHit[8];
        private readonly RaycastHit[] wallSensorHits = new RaycastHit[8];
        private readonly Collider[] nearbyRacerHits = new Collider[12];
        private KartController kartController;
        private Rigidbody kartBody;
        private RacerProgress progress;
        private int currentWaypointIndex;
        private float effectivePathOffset;
        private float stoppedDuration;
        private float wrongWayDuration;
        private float waypointStallDuration;
        private float straightAccelerationTimer;
        private float pathOffsetVelocity;
        private float steeringVelocity;
        private float runtimeBasePathOffset;
        private float runtimeAggression;
        private float runtimeCornerSpeedFactor;
        private float runtimeLookAheadMultiplier;
        private float runtimeOvertakeBias;
        private float trafficPathOffset;
        private float frontVehicleSpeed;
        private bool frontVehicleRequiresSpeedMatch;
        private int observedWaypointIndex = -1;
        private bool drivingEnabled;
        private bool tightCorner;

        public bool DrivingEnabled => drivingEnabled;
        public int CurrentWaypointIndex => currentWaypointIndex;
        public float CurrentSpeed => kartBody == null ? 0f : kartBody.linearVelocity.magnitude;
        public float EffectivePathOffset => effectivePathOffset;
        public float DesiredPathOffset { get; private set; }
        public int LookAheadWaypointIndex { get; private set; }
        public float TargetSpeed { get; private set; }
        public float RawSteering { get; private set; }
        public float FinalSteering { get; private set; }
        public float WallAvoidanceSteering { get; private set; }
        public float ThrottleInput { get; private set; }
        public float BrakeInput { get; private set; }
        public float CornerSeverity { get; private set; }
        public float CornerDistance { get; private set; }
        public bool FrontVehicleDetected { get; private set; }
        public bool FrontVehicleRequiresSpeedMatch => frontVehicleRequiresSpeedMatch;
        public bool IsOvertaking => FrontVehicleDetected && Mathf.Abs(trafficPathOffset) > 0.01f;
        public string CurrentAIState { get; private set; } = "Waiting";
        public Vector3 CurrentTargetPoint { get; private set; }
        public Vector3 LookAheadTargetPoint { get; private set; }
        public float RuntimeBasePathOffset => runtimeBasePathOffset;
        public float RuntimeAggression => runtimeAggression;
        public int OvertakeDirection
        {
            get
            {
                if (overtakeDirection != 0)
                {
                    return overtakeDirection < 0 ? -1 : 1;
                }

                return pathOffset < 0f ? -1 : 1;
            }
        }
        public bool IsSeparatingFromRacer { get; private set; }
        public float ClosestAIRacerDistance { get; private set; } = float.PositiveInfinity;
        public float CurrentStraightAccelerationMultiplier { get; private set; } = 1f;
        public float CurrentStraightSpeedMultiplier { get; private set; } = 1f;
        public float PeakStraightAccelerationMultiplier { get; private set; } = 1f;
        public float PeakStraightSpeedMultiplier { get; private set; } = 1f;
        public float MaximumObservedForwardSpeed { get; private set; }
        public int RecoveryCount { get; private set; }

        public void Configure(
            AIWaypointPath waypointPath,
            float speed,
            float throttle,
            float turnSpeed,
            float steering,
            float braking,
            float laneOffset)
        {
            path = waypointPath;
            maximumSpeed = Mathf.Max(1f, speed);
            throttleStrength = Mathf.Clamp(throttle, 0.1f, 1f);
            cornerSpeed = Mathf.Max(1f, turnSpeed);
            steeringSensitivity = Mathf.Clamp(steering, 0.1f, 2f);
            brakeStrength = Mathf.Clamp01(braking);
            pathOffset = laneOffset;
            InitializeRaceVariation();
            PeakStraightAccelerationMultiplier = 1f;
            PeakStraightSpeedMultiplier = 1f;
            MaximumObservedForwardSpeed = 0f;
            waypointReachDistance = 6f;
            stuckTimeout = 5f;
            recoveryHeight = 0.15f;
            maximumPathDistance = 10f;
            if (path != null)
            {
                currentWaypointIndex = (path.FindClosestWaypoint(transform.position) + 1) % path.Count;
            }
        }

        public void SetDrivingEnabled(bool value)
        {
            EnsureReferences();
            drivingEnabled = value;
            kartController.SetInputEnabled(value);
            if (!value)
            {
                kartController.SetExternalInput(0f, 0f);
                straightAccelerationTimer = 0f;
                CurrentStraightAccelerationMultiplier = 1f;
                CurrentStraightSpeedMultiplier = 1f;
                CurrentAIState = progress != null && progress.Finished ? "Finished" : "Waiting";
            }
        }

        public void RecoverToPath()
        {
            EnsureReferences();
            if (path == null || path.Count == 0)
            {
                return;
            }

            CurrentAIState = "Recovering";
            int closestWaypoint = path.FindClosestWaypoint(transform.position);
            int pointsPerCheckpoint = Mathf.Max(1, path.Count / 9);
            int segmentStart = progress == null
                ? closestWaypoint
                : (progress.CompletedCheckpoints % 9) * pointsPerCheckpoint;
            int relativeWaypoint = (closestWaypoint - segmentStart + path.Count) % path.Count;
            if (relativeWaypoint >= pointsPerCheckpoint)
            {
                relativeWaypoint = 0;
            }

            int recoveryStep = Mathf.Clamp(relativeWaypoint + 1, 1, pointsPerCheckpoint - 1);
            int recoveryWaypoint = (segmentStart + recoveryStep) % path.Count;
            currentWaypointIndex = (recoveryWaypoint + 1) % path.Count;
            Vector3 direction = path.GetDirection(recoveryWaypoint);
            kartBody.position = path.GetPosition(recoveryWaypoint, runtimeBasePathOffset) + Vector3.up * recoveryHeight;
            kartBody.rotation = Quaternion.FromToRotation(Vector3.right, direction);
            kartBody.linearVelocity = Vector3.zero;
            kartBody.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
            stoppedDuration = 0f;
            wrongWayDuration = 0f;
            waypointStallDuration = 0f;
            observedWaypointIndex = currentWaypointIndex;
            straightAccelerationTimer = 0f;
            CurrentStraightAccelerationMultiplier = 1f;
            CurrentStraightSpeedMultiplier = 1f;
            RecoveryCount++;
        }

        private void Awake()
        {
            EnsureReferences();
            InitializeRaceVariation();
            kartController.SetExternalControlEnabled(true);
            kartController.SetInputEnabled(false);
        }

        private void Start()
        {
            if (path != null)
            {
                currentWaypointIndex = (path.FindClosestWaypoint(transform.position) + 1) % path.Count;
            }
        }

        private void EnsureReferences()
        {
            if (kartController == null) kartController = GetComponent<KartController>();
            if (kartBody == null) kartBody = GetComponent<Rigidbody>();
            if (progress == null) progress = GetComponent<RacerProgress>();
        }

        private void InitializeRaceVariation()
        {
            int seed = unchecked(name.GetHashCode() * 397 ^ System.Environment.TickCount);
            System.Random random = new System.Random(seed);
            float signed = (float)random.NextDouble() * 2f - 1f;
            runtimeBasePathOffset = Mathf.Clamp(
                pathOffset + signed * randomPathOffsetRange,
                -maximumPathOffset,
                maximumPathOffset);
            runtimeAggression = Mathf.Clamp01(
                aggression + ((float)random.NextDouble() * 2f - 1f) * randomPersonalityRange);
            runtimeCornerSpeedFactor = Mathf.Clamp(
                cornerSpeedFactor + ((float)random.NextDouble() * 2f - 1f) * randomPersonalityRange,
                0.8f,
                1.2f);
            runtimeLookAheadMultiplier = Mathf.Clamp(
                lookAheadMultiplier + ((float)random.NextDouble() * 2f - 1f) * randomPersonalityRange,
                0.7f,
                1.3f);
            runtimeOvertakeBias = Mathf.Clamp(
                overtakeBias + ((float)random.NextDouble() * 2f - 1f) * randomPersonalityRange,
                -1f,
                1f);
            effectivePathOffset = runtimeBasePathOffset;
            DesiredPathOffset = runtimeBasePathOffset;
        }

        private void FixedUpdate()
        {
            if (!drivingEnabled || path == null || path.Count < 2 || progress.Finished)
            {
                kartController.SetExternalInput(0f, 0f);
                ThrottleInput = 0f;
                BrakeInput = 0f;
                CurrentAIState = progress.Finished ? "Finished" : "Waiting";
                return;
            }

            tightCorner = IsNearTightCorner();
            AdvanceWaypoint();
            Vector3 forward = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            float speed = Mathf.Max(0f, Vector3.Dot(kartBody.linearVelocity, forward));
            int lookAhead = CalculateLookAhead(speed);
            int steeringLookAhead = Mathf.Min(
                lookAhead,
                maximumSteeringLookAheadWaypoints);
            if (tightCorner) steeringLookAhead = 1;
            LookAheadWaypointIndex = (currentWaypointIndex + steeringLookAhead) % path.Count;
            AnalyzeCorner(lookAhead, out float cornerDirection);
            UpdateTrafficOffset();
            ApplyRacingLine(cornerDirection);

            CurrentTargetPoint = path.GetPosition(currentWaypointIndex, effectivePathOffset);
            LookAheadTargetPoint = path.GetPosition(LookAheadWaypointIndex, effectivePathOffset);
            Vector3 targetDirection = Vector3.ProjectOnPlane(
                LookAheadTargetPoint - transform.position,
                Vector3.up).normalized;
            float angle = Vector3.SignedAngle(forward, targetDirection, Vector3.up);
            float speedSteeringFactor = Mathf.Lerp(
                1f, 0.76f, Mathf.Clamp01(speed / maximumSpeed));
            float pathSteering =
                angle / 42f * steeringSensitivity * speedSteeringFactor;
            WallAvoidanceSteering = CalculateWallAvoidanceSteering(forward);
            RawSteering = Mathf.Clamp(
                pathSteering + WallAvoidanceSteering,
                -1f, 1f);
            FinalSteering = Mathf.SmoothDamp(
                FinalSteering, RawSteering, ref steeringVelocity,
                steeringSmoothTime, Mathf.Infinity, Time.fixedDeltaTime);

            UpdateStraightAcceleration(CornerSeverity, angle);
            TargetSpeed = CalculateTargetSpeed(speed) + kartController.CatchUpSpeedBoost;
            if (tightCorner) TargetSpeed = Mathf.Min(TargetSpeed, cornerSpeed);
            if (FrontVehicleDetected && frontVehicleRequiresSpeedMatch && !IsOvertaking)
            {
                TargetSpeed = Mathf.Min(TargetSpeed, frontVehicleSpeed + 1f);
            }
            MaximumObservedForwardSpeed = Mathf.Max(MaximumObservedForwardSpeed, Mathf.Abs(speed));
            CalculateControls(speed);
            float throttle = BrakeInput > 0f ? -BrakeInput : ThrottleInput;

            kartController.SetExternalInput(
                FinalSteering,
                throttle,
                CurrentStraightAccelerationMultiplier,
                CurrentStraightSpeedMultiplier);
            CurrentAIState = IsSeparatingFromRacer ? "Avoiding" :
                IsOvertaking ? "Overtaking" :
                CornerSeverity > 0.18f ? "Cornering" : "NormalDriving";
            CheckRecovery(forward, speed);
        }

        private int CalculateLookAhead(float speed)
        {
            float ratio = Mathf.Clamp01(speed / Mathf.Max(maximumSpeed, 0.01f));
            return Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(
                    minimumLookAheadWaypoints,
                    maximumLookAheadWaypoints,
                    ratio) * runtimeLookAheadMultiplier),
                minimumLookAheadWaypoints,
                maximumLookAheadWaypoints);
        }

        private void AnalyzeCorner(int speedLookAhead, out float cornerDirection)
        {
            int samples = Mathf.Clamp(
                Mathf.Max(cornerAnalysisWaypoints, speedLookAhead), 3, 10);
            float distance = Vector3.ProjectOnPlane(
                path.GetPosition(currentWaypointIndex) - transform.position,
                Vector3.up).magnitude;
            CornerDistance = float.PositiveInfinity;
            float maximum = 0f;
            float accumulated = 0f;
            float signed = 0f;

            for (int step = 0; step < samples; step++)
            {
                int index = currentWaypointIndex + step;
                float angle = Vector3.SignedAngle(
                    path.GetDirection(index),
                    path.GetDirection(index + 1),
                    Vector3.up);
                float severity = Mathf.InverseLerp(3f, 34f, Mathf.Abs(angle));
                float weight = Mathf.Lerp(1f, 0.55f, step / Mathf.Max(1f, samples - 1f));
                maximum = Mathf.Max(maximum, severity);
                accumulated += severity * weight;
                signed += angle * weight;
                if (float.IsPositiveInfinity(CornerDistance) && Mathf.Abs(angle) >= 5f)
                {
                    CornerDistance = distance;
                }
                distance += Vector3.Distance(
                    path.GetPosition(index),
                    path.GetPosition(index + 1));
            }

            CornerSeverity = Mathf.Clamp01(maximum * 0.7f + accumulated / samples * 0.65f);
            cornerDirection = Mathf.Abs(signed) < 0.1f ? 0f : Mathf.Sign(signed);
        }

        private void ApplyRacingLine(float cornerDirection)
        {
            DesiredPathOffset = runtimeBasePathOffset + trafficPathOffset;
            if (!Mathf.Approximately(cornerDirection, 0f) && CornerSeverity > 0.1f)
            {
                float apexDistance = Mathf.Lerp(
                    4f, 8f, Mathf.Clamp01(CurrentSpeed / maximumSpeed));
                float amount = racingLineOffset * CornerSeverity;
                float apexBlend = float.IsPositiveInfinity(CornerDistance)
                    ? 0f
                    : 1f - Mathf.InverseLerp(0f, apexDistance * 2f, CornerDistance);
                float lineSide = Mathf.Lerp(
                    -cornerDirection,
                    cornerDirection * 0.45f,
                    Mathf.SmoothStep(0f, 1f, apexBlend));
                DesiredPathOffset += lineSide * amount;
            }

            float separation = CalculateRacerSeparation(
                Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized);
            DesiredPathOffset = Mathf.Clamp(
                DesiredPathOffset + separation,
                -maximumPathOffset,
                maximumPathOffset);
            // A distant target across a tight bend cuts through the inside wall.
            // Follow the local road centre until the bend has been cleared.
            if (tightCorner) DesiredPathOffset = runtimeBasePathOffset * 0.2f;
            float changeSpeed = IsSeparatingFromRacer
                ? separationLaneChangeSpeed
                : separationLaneChangeSpeed * 0.65f;
            effectivePathOffset = Mathf.SmoothDamp(
                effectivePathOffset,
                DesiredPathOffset,
                ref pathOffsetVelocity,
                pathOffsetSmoothTime,
                changeSpeed,
                Time.fixedDeltaTime);
        }

        private float CalculateWallAvoidanceSteering(Vector3 forward)
        {
            if (wallAvoidanceStrength <= 0f)
            {
                return 0f;
            }

            Vector3 origin = transform.position + Vector3.up * 0.5f + forward * 0.5f;
            float strongestAvoidance = 0f;

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 sensorDirection = Quaternion.AngleAxis(
                    side * wallSensorAngle,
                    Vector3.up) * forward;
                int hits = Physics.SphereCastNonAlloc(
                    origin,
                    wallSensorRadius,
                    sensorDirection,
                    wallSensorHits,
                    wallSensorDistance,
                    ~0,
                    QueryTriggerInteraction.Ignore);

                for (int i = 0; i < hits; i++)
                {
                    RaycastHit hit = wallSensorHits[i];
                    if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                    {
                        continue;
                    }

                    if (hit.collider.GetComponentInParent<RacerProgress>() != null)
                    {
                        continue;
                    }

                    // MeshCollider casts can report a zero-distance hit when the sensor
                    // starts inside the road mesh. That is not a wall in front of the kart.
                    if (hit.distance <= 0.05f)
                    {
                        continue;
                    }

                    bool wall = Mathf.Abs(Vector3.Dot(hit.normal, Vector3.up)) < 0.65f;
                    if (!wall)
                    {
                        continue;
                    }

                    float proximity = 1f - Mathf.Clamp01(hit.distance / wallSensorDistance);
                    float avoidance = -side * proximity * wallAvoidanceStrength;
                    if (Mathf.Abs(avoidance) > Mathf.Abs(strongestAvoidance))
                    {
                        strongestAvoidance = avoidance;
                    }
                }
            }

            return Mathf.Clamp(strongestAvoidance, -wallAvoidanceStrength, wallAvoidanceStrength);
        }

        private float CalculateTargetSpeed(float speed)
        {
            float straightTarget = maximumSpeed * Mathf.Lerp(0.98f, 1.02f, runtimeAggression);
            if (CornerSeverity <= 0.03f || float.IsPositiveInfinity(CornerDistance))
            {
                return straightTarget;
            }

            float cornerRatio = Mathf.Lerp(0.62f, 0.72f, runtimeAggression);
            float cornerTarget = Mathf.Max(cornerSpeed, maximumSpeed * cornerRatio) *
                runtimeCornerSpeedFactor;
            cornerTarget = Mathf.Lerp(
                straightTarget,
                Mathf.Min(cornerTarget, straightTarget),
                Mathf.Pow(CornerSeverity, 1.35f));
            float brakingDistance = CalculateBrakingDistance(speed, cornerTarget) /
                Mathf.Max(0.1f, lateBrakeFactor);
            float anticipation = Mathf.Max(5f, speed * 0.7f * runtimeLookAheadMultiplier);
            float influence = 1f - Mathf.InverseLerp(
                brakingDistance + anticipation,
                brakingDistance,
                CornerDistance);
            return Mathf.Lerp(
                straightTarget,
                cornerTarget,
                Mathf.SmoothStep(0f, 1f, influence));
        }

        private float CalculateBrakingDistance(float speed, float target)
        {
            float difference = speed * speed - target * target;
            return difference <= 0f
                ? 0f
                : difference / (2f * Mathf.Max(estimatedBrakeDeceleration, 0.1f));
        }

        private void CalculateControls(float speed)
        {
            float error = TargetSpeed - speed;
            ThrottleInput = 0f;
            BrakeInput = 0f;
            if (error > 1.25f)
            {
                ThrottleInput = throttleStrength;
                return;
            }
            if (error > 0.15f)
            {
                ThrottleInput = Mathf.Lerp(
                    throttleStrength * 0.25f,
                    throttleStrength * 0.75f,
                    Mathf.InverseLerp(0.15f, 1.25f, error));
                return;
            }
            if (error >= -coastOverspeedMargin)
            {
                return;
            }

            float brakingDistance = CalculateBrakingDistance(speed, TargetSpeed) /
                Mathf.Max(0.1f, lateBrakeFactor);
            bool brakingNow = CornerSeverity > 0.22f &&
                CornerDistance <= brakingDistance + Mathf.Max(1.5f, speed * 0.12f);
            bool dangerouslyFast = -error > Mathf.Max(3f, TargetSpeed * 0.25f);
            if (brakingNow || dangerouslyFast)
            {
                BrakeInput = Mathf.Clamp01(
                    Mathf.InverseLerp(coastOverspeedMargin, 5f, -error) *
                    brakeStrength * brakeAggressiveness);
            }
        }

        private void UpdateStraightAcceleration(
            float cornerFactor,
            float steeringAngle)
        {
            bool acceleratingOnStraight = cornerFactor < 0.3f &&
                Mathf.Abs(steeringAngle) < 35f;

            straightAccelerationTimer = acceleratingOnStraight
                ? Mathf.Min(straightAccelerationTimer + Time.fixedDeltaTime, straightAccelerationBuildTime)
                : Mathf.MoveTowards(straightAccelerationTimer, 0f, Time.fixedDeltaTime * 2f);

            float buildRatio = straightAccelerationBuildTime <= 0f
                ? 1f
                : straightAccelerationTimer / straightAccelerationBuildTime;
            CurrentStraightAccelerationMultiplier = Mathf.Lerp(1f, straightAccelerationMultiplier, buildRatio);
            CurrentStraightSpeedMultiplier = Mathf.Lerp(1f, straightTopSpeedMultiplier, buildRatio);
            PeakStraightAccelerationMultiplier = Mathf.Max(
                PeakStraightAccelerationMultiplier,
                CurrentStraightAccelerationMultiplier);
            PeakStraightSpeedMultiplier = Mathf.Max(
                PeakStraightSpeedMultiplier,
                CurrentStraightSpeedMultiplier);
        }

        private void AdvanceWaypoint()
        {
            Vector3 target = path.GetPosition(currentWaypointIndex, effectivePathOffset);
            Vector3 flatDelta = Vector3.ProjectOnPlane(target - transform.position, Vector3.up);
            float reach = tightCorner ? Mathf.Min(waypointReachDistance, 2.5f) : waypointReachDistance;
            Vector3 incoming = path.GetDirection(currentWaypointIndex - 1);
            bool passed = Vector3.Dot(-flatDelta, incoming) > 0f &&
                flatDelta.sqrMagnitude < maximumPathDistance * maximumPathDistance;
            if (flatDelta.sqrMagnitude <= reach * reach || passed)
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % path.Count;
            }
        }

        private bool IsNearTightCorner()
        {
            for (int step = -2; step <= 3; step++)
            {
                int index = currentWaypointIndex + step;
                float turn = Vector3.Angle(path.GetDirection(index - 1), path.GetDirection(index));
                float distance = Vector3.ProjectOnPlane(
                    path.GetPosition(index) - transform.position, Vector3.up).magnitude;
                if (turn >= 25f && distance < 22f) return true;
            }
            return false;
        }

        private bool UpdateTrafficOffset()
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            int hits = Physics.SphereCastNonAlloc(
                origin,
                sensorRadius,
                forward,
                sensorHits,
                sensorDistance,
                ~0,
                QueryTriggerInteraction.Ignore);

            bool blocked = false;
            FrontVehicleDetected = false;
            frontVehicleSpeed = 0f;
            frontVehicleRequiresSpeedMatch = false;
            trafficPathOffset = 0f;
            float closestVehicleDistance = float.PositiveInfinity;
            for (int i = 0; i < hits; i++)
            {
                Collider hitCollider = sensorHits[i].collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                Rigidbody otherBody = sensorHits[i].rigidbody;
                bool otherRacer = otherBody != null && otherBody != kartBody &&
                    otherBody.GetComponent<RacerProgress>() != null;
                bool ghostAIRacer = otherRacer && IsGhostAIRacer(otherBody);
                bool verticalObstacle = Mathf.Abs(Vector3.Dot(sensorHits[i].normal, Vector3.up)) < 0.65f;
                if (otherRacer && sensorHits[i].distance < closestVehicleDistance)
                {
                    FrontVehicleDetected = true;
                    closestVehicleDistance = sensorHits[i].distance;
                    frontVehicleSpeed = Mathf.Max(
                        0f,
                        Vector3.Dot(otherBody.linearVelocity, forward));
                    // AI karts share a non-colliding layer. Keep detecting them for
                    // overtaking and racing-line separation, but never queue behind one.
                    frontVehicleRequiresSpeedMatch = !ghostAIRacer;
                    blocked |= !ghostAIRacer;
                }
                blocked |= !otherRacer && verticalObstacle;
            }

            if (FrontVehicleDetected && CornerSeverity < 0.48f)
            {
                int preferredSide = Mathf.Abs(runtimeOvertakeBias) > 0.15f
                    ? (runtimeOvertakeBias < 0f ? -1 : 1)
                    : OvertakeDirection;
                int selectedSide = IsSideClear(preferredSide, forward)
                    ? preferredSide
                    : IsSideClear(-preferredSide, forward) ? -preferredSide : 0;
                trafficPathOffset = selectedSide * overtakeOffset;
            }

            return blocked;
        }

        private bool IsSideClear(int side, Vector3 forward)
        {
            Vector3 lateral = Vector3.Cross(Vector3.up, forward).normalized * side;
            int hits = Physics.SphereCastNonAlloc(
                transform.position + Vector3.up * 0.5f,
                sensorRadius * 0.75f,
                lateral,
                sideSensorHits,
                overtakeOffset + 1f,
                ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; i++)
            {
                Collider hit = sideSensorHits[i].collider;
                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                Rigidbody body = sideSensorHits[i].rigidbody;
                bool racer = body != null && body != kartBody &&
                    body.GetComponent<RacerProgress>() != null;
                bool wall = Mathf.Abs(Vector3.Dot(sideSensorHits[i].normal, Vector3.up)) < 0.65f;
                if (racer)
                {
                    if (!IsGhostAIRacer(body))
                    {
                        return false;
                    }

                    continue;
                }

                if (wall)
                {
                    return false;
                }
            }
            return true;
        }

        private bool IsGhostAIRacer(Rigidbody otherBody)
        {
            return otherBody != null &&
                otherBody != kartBody &&
                otherBody.GetComponent<AIKartController>() != null &&
                Physics.GetIgnoreLayerCollision(gameObject.layer, otherBody.gameObject.layer);
        }

        private float CalculateRacerSeparation(Vector3 forward)
        {
            IsSeparatingFromRacer = false;
            ClosestAIRacerDistance = float.PositiveInfinity;

            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position + Vector3.up * 0.5f,
                racerSeparationDistance,
                nearbyRacerHits,
                ~0,
                QueryTriggerInteraction.Ignore);

            Vector3 pathRight = Vector3.Cross(Vector3.up, forward).normalized;
            float strongestOffset = 0f;
            float strongestWeight = 0f;
            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = nearbyRacerHits[i];
                Rigidbody otherBody = hitCollider == null ? null : hitCollider.attachedRigidbody;
                AIKartController otherAI = otherBody == null || otherBody == kartBody
                    ? null
                    : otherBody.GetComponent<AIKartController>();
                if (otherAI == null)
                {
                    continue;
                }

                Vector3 delta = Vector3.ProjectOnPlane(
                    otherBody.position - kartBody.position,
                    Vector3.up);
                float distance = delta.magnitude;
                ClosestAIRacerDistance = Mathf.Min(ClosestAIRacerDistance, distance);

                float lateralDelta = Vector3.Dot(delta, pathRight);
                int separationDirection;
                if (Mathf.Abs(lateralDelta) > 0.1f)
                {
                    separationDirection = lateralDelta > 0f ? -1 : 1;
                }
                else if (!Mathf.Approximately(effectivePathOffset, otherAI.EffectivePathOffset))
                {
                    separationDirection = effectivePathOffset > otherAI.EffectivePathOffset ? 1 : -1;
                }
                else
                {
                    separationDirection = OvertakeDirection;
                }

                float weight = 1f - Mathf.Clamp01(distance / racerSeparationDistance);
                if (weight > strongestWeight)
                {
                    strongestWeight = weight;
                    strongestOffset = separationDirection * racerSeparationOffset * weight;
                }
            }

            IsSeparatingFromRacer = strongestWeight > 0f;
            return strongestOffset;
        }

        private void CheckRecovery(Vector3 forward, float speed)
        {
            if (observedWaypointIndex != currentWaypointIndex)
            {
                observedWaypointIndex = currentWaypointIndex;
                waypointStallDuration = 0f;
            }
            else
            {
                waypointStallDuration += Time.fixedDeltaTime;
            }

            bool attemptingToMove = drivingEnabled && !progress.Finished;
            stoppedDuration = attemptingToMove && Mathf.Abs(speed) < 0.6f
                ? stoppedDuration + Time.fixedDeltaTime
                : 0f;

            float directionAlignment = Vector3.Dot(forward, path.GetDirection(currentWaypointIndex));
            wrongWayDuration = directionAlignment < -0.35f
                ? wrongWayDuration + Time.fixedDeltaTime
                : 0f;

            Vector3 pathPoint = path.GetPosition(path.FindClosestWaypoint(transform.position));
            bool tooFarFromPath = Vector3.ProjectOnPlane(transform.position - pathPoint, Vector3.up).magnitude > maximumPathDistance;
            bool upsideDown = Vector3.Dot(transform.up, Vector3.up) < 0.25f;
            bool fellOut = transform.position.y < -10f;

            bool noPathProgress = waypointStallDuration >= stuckTimeout * 2f;
            if (stoppedDuration >= stuckTimeout || wrongWayDuration >= stuckTimeout || noPathProgress ||
                tooFarFromPath || upsideDown || fellOut)
            {
                RecoverToPath();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, CurrentTargetPoint);
            Gizmos.DrawSphere(CurrentTargetPoint, 0.35f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, LookAheadTargetPoint);
            Gizmos.DrawWireSphere(LookAheadTargetPoint, 0.5f);
            Gizmos.color = FrontVehicleDetected ? Color.magenta : Color.green;
            Gizmos.DrawLine(
                transform.position + Vector3.up * 0.5f,
                transform.position + Vector3.up * 0.5f +
                Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized * sensorDistance);
        }
    }
}
