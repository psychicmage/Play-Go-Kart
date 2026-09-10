using UnityEngine;

namespace PlayGoKart.Gameplay
{
    /// <summary>
    /// Drives the imported kart's wheel meshes from its real Rigidbody speed.
    /// This is presentation-only and never writes to the kart's physics state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class KartWheelAnimator : MonoBehaviour
    {
        [Header("Wheel Meshes")]
        [SerializeField] private Transform rearLeftWheel;
        [SerializeField] private Transform rearRightWheel;
        [SerializeField] private Transform frontLeftWheel;
        [SerializeField] private Transform frontRightWheel;

        [Header("Rolling")]
        [Tooltip("The imported kart model faces local +X.")]
        [SerializeField] private Vector3 localForwardAxis = Vector3.right;
        [Tooltip("The tyre meshes use their local Y axis as the axle.")]
        [SerializeField] private Vector3 localSpinAxis = Vector3.up;
        [SerializeField, Min(0.01f)] private float rearWheelRadius = 0.405f;
        [SerializeField, Min(0.01f)] private float frontWheelRadius = 0.288f;
        [SerializeField, Min(0f)] private float movingSpeedThreshold = 0.03f;
        [SerializeField, Min(0.01f)] private float accelerationSmoothTime = 0.06f;
        [SerializeField, Min(0.01f)] private float decelerationSmoothTime = 0.18f;

        [Header("Visual Steering")]
        [SerializeField] private Vector3 localSteeringAxis = Vector3.up;
        [SerializeField, Range(0f, 45f)] private float maxVisualSteeringAngle = 30f;
        [SerializeField, Min(0.01f)] private float steeringSmoothTime = 0.08f;

        private Rigidbody kartBody;
        private KartController kartController;
        private Quaternion rearLeftBaseRotation;
        private Quaternion rearRightBaseRotation;
        private Quaternion frontLeftBaseRotation;
        private Quaternion frontRightBaseRotation;
        private float smoothedForwardSpeed;
        private float speedSmoothVelocity;
        private float rearRotationDegrees;
        private float frontRotationDegrees;
        private float currentSteeringAngle;
        private float steeringSmoothVelocity;

        public float VisualForwardSpeed => smoothedForwardSpeed;
        public float RearRotationDegrees => rearRotationDegrees;
        public float FrontRotationDegrees => frontRotationDegrees;
        public float CurrentSteeringAngle => currentSteeringAngle;
        public float TargetSteeringAngle => kartController == null
            ? 0f
            : kartController.SteeringInput * maxVisualSteeringAngle;
        public bool HasAllWheelReferences => rearLeftWheel != null && rearRightWheel != null &&
            frontLeftWheel != null && frontRightWheel != null;

        private void Awake()
        {
            kartBody = GetComponent<Rigidbody>();
            kartController = GetComponent<KartController>();

            if (!HasAllWheelReferences)
            {
                Debug.LogError($"{nameof(KartWheelAnimator)} on '{name}' needs all four wheel mesh references.", this);
                enabled = false;
                return;
            }

            rearLeftBaseRotation = rearLeftWheel.localRotation;
            rearRightBaseRotation = rearRightWheel.localRotation;
            frontLeftBaseRotation = frontLeftWheel.localRotation;
            frontRightBaseRotation = frontRightWheel.localRotation;
        }

        private void LateUpdate()
        {
            Vector3 forward = transform.TransformDirection(localForwardAxis.normalized);
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            float targetSpeed = Vector3.Dot(
                Vector3.ProjectOnPlane(kartBody.linearVelocity, Vector3.up),
                forward);

            if (Mathf.Abs(targetSpeed) < movingSpeedThreshold)
            {
                targetSpeed = 0f;
            }

            float smoothTime = Mathf.Abs(targetSpeed) > Mathf.Abs(smoothedForwardSpeed)
                ? accelerationSmoothTime
                : decelerationSmoothTime;
            smoothedForwardSpeed = Mathf.SmoothDamp(
                smoothedForwardSpeed,
                targetSpeed,
                ref speedSmoothVelocity,
                smoothTime,
                Mathf.Infinity,
                Time.deltaTime);

            if (Mathf.Approximately(targetSpeed, 0f) && Mathf.Abs(smoothedForwardSpeed) < 0.01f)
            {
                smoothedForwardSpeed = 0f;
                speedSmoothVelocity = 0f;
            }

            rearRotationDegrees = Mathf.Repeat(
                rearRotationDegrees + SpeedToDegrees(smoothedForwardSpeed, rearWheelRadius) * Time.deltaTime,
                360f);
            frontRotationDegrees = Mathf.Repeat(
                frontRotationDegrees + SpeedToDegrees(smoothedForwardSpeed, frontWheelRadius) * Time.deltaTime,
                360f);

            Quaternion rearSpin = Quaternion.AngleAxis(rearRotationDegrees, localSpinAxis.normalized);
            Quaternion frontSpin = Quaternion.AngleAxis(frontRotationDegrees, localSpinAxis.normalized);
            currentSteeringAngle = Mathf.SmoothDampAngle(
                currentSteeringAngle,
                TargetSteeringAngle,
                ref steeringSmoothVelocity,
                steeringSmoothTime,
                Mathf.Infinity,
                Time.deltaTime);
            Quaternion steering = Quaternion.AngleAxis(
                currentSteeringAngle,
                localSteeringAxis.normalized);
            rearLeftWheel.localRotation = rearLeftBaseRotation * rearSpin;
            rearRightWheel.localRotation = rearRightBaseRotation * rearSpin;
            frontLeftWheel.localRotation = steering * frontLeftBaseRotation * frontSpin;
            frontRightWheel.localRotation = steering * frontRightBaseRotation * frontSpin;
        }

        private void OnDisable()
        {
            if (!HasAllWheelReferences)
            {
                return;
            }

            rearLeftWheel.localRotation = rearLeftBaseRotation;
            rearRightWheel.localRotation = rearRightBaseRotation;
            frontLeftWheel.localRotation = frontLeftBaseRotation;
            frontRightWheel.localRotation = frontRightBaseRotation;
            smoothedForwardSpeed = 0f;
            speedSmoothVelocity = 0f;
            rearRotationDegrees = 0f;
            frontRotationDegrees = 0f;
            currentSteeringAngle = 0f;
            steeringSmoothVelocity = 0f;
        }

        private static float SpeedToDegrees(float speed, float radius)
        {
            return speed / Mathf.Max(radius, 0.01f) * Mathf.Rad2Deg;
        }

        private void OnValidate()
        {
            if (localForwardAxis.sqrMagnitude < 0.001f)
            {
                localForwardAxis = Vector3.right;
            }

            if (localSpinAxis.sqrMagnitude < 0.001f)
            {
                localSpinAxis = Vector3.up;
            }

            if (localSteeringAxis.sqrMagnitude < 0.001f)
            {
                localSteeringAxis = Vector3.up;
            }
        }
    }
}
