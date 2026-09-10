using UnityEngine;
using UnityEngine.InputSystem;

namespace PlayGoKart.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class KartController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string moveActionName = "Player/Move";
        [SerializeField] private bool useExternalControl;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float forwardAcceleration = 18f;
        [SerializeField, Min(0f)] private float reverseAcceleration = 10f;
        [SerializeField, Min(0f)] private float maxForwardSpeed = 16f;
        [SerializeField, Min(0f)] private float maxReverseSpeed = 7f;
        [SerializeField, Min(0f)] private float rollingResistance = 1.5f;
        [SerializeField, Min(0f)] private float catchUpSpeedPerPositionKph = 2f;

        [Header("Steering")]
        [SerializeField, Min(0f)] private float steeringDegreesPerSecond = 95f;
        [SerializeField, Range(0f, 1f)] private float minimumSteeringFactor = 0.25f;
        [SerializeField, Min(0f)] private float lateralGrip = 8f;

        [Header("Body")]
        [Tooltip("The imported kart model faces local +X.")]
        [SerializeField] private Vector3 localForwardAxis = Vector3.right;
        [SerializeField] private Vector3 centerOfMass = new Vector3(0f, 10f, 0f);

        private Rigidbody kartBody;
        private RacerProgress racerProgress;
        private InputAction moveAction;
        private Vector2 moveInput;
        private bool enabledMoveAction;
        private bool inputEnabled = true;
        private Vector2 externalInput;
        private float externalAccelerationMultiplier = 1f;
        private float externalSpeedMultiplier = 1f;

        public bool InputEnabled => inputEnabled;
        public bool UsesExternalControl => useExternalControl;
        public float SteeringInput => inputEnabled
            ? (useExternalControl ? externalInput.x : moveInput.x)
            : 0f;
        public float AppliedAccelerationMultiplier => useExternalControl ? externalAccelerationMultiplier : 1f;
        public float AppliedSpeedMultiplier => useExternalControl ? externalSpeedMultiplier : 1f;
        public float CatchUpSpeedBoostKph => racerProgress == null
            ? 0f
            : Mathf.Max(0, racerProgress.CurrentPosition - 1) * catchUpSpeedPerPositionKph;
        public float CatchUpSpeedBoost => CatchUpSpeedBoostKph / 3.6f;
        public float EffectiveForwardSpeedLimit =>
            maxForwardSpeed * AppliedSpeedMultiplier + CatchUpSpeedBoost;
        public float ForwardSpeed
        {
            get
            {
                Vector3 forward = transform.TransformDirection(localForwardAxis.normalized);
                return kartBody == null ? 0f : Vector3.Dot(kartBody.linearVelocity, forward);
            }
        }

        public void SetInputEnabled(bool value)
        {
            inputEnabled = value;
            if (!inputEnabled)
            {
                moveInput = Vector2.zero;
                externalAccelerationMultiplier = 1f;
                externalSpeedMultiplier = 1f;
            }
        }

        public void SetExternalControlEnabled(bool value)
        {
            useExternalControl = value;
            externalInput = Vector2.zero;
            moveInput = Vector2.zero;
            externalAccelerationMultiplier = 1f;
            externalSpeedMultiplier = 1f;
        }

        public void SetExternalInput(
            float steering,
            float throttle,
            float accelerationMultiplier = 1f,
            float speedMultiplier = 1f)
        {
            externalInput = new Vector2(
                Mathf.Clamp(steering, -1f, 1f),
                Mathf.Clamp(throttle, -1f, 1f));
            externalAccelerationMultiplier = Mathf.Clamp(accelerationMultiplier, 0.1f, 2f);
            externalSpeedMultiplier = Mathf.Clamp(speedMultiplier, 1f, 2f);
        }

        private void Awake()
        {
            kartBody = GetComponent<Rigidbody>();
            racerProgress = GetComponent<RacerProgress>();
            kartBody.interpolation = RigidbodyInterpolation.Interpolate;
            kartBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            kartBody.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            kartBody.centerOfMass = centerOfMass;
        }

        private void OnEnable()
        {
            if (useExternalControl)
            {
                return;
            }

            if (inputActions == null)
            {
                Debug.LogError($"{nameof(KartController)} on '{name}' needs an InputActionAsset.", this);
                enabled = false;
                return;
            }

            moveAction = inputActions.FindAction(moveActionName, false);
            if (moveAction == null)
            {
                Debug.LogError($"Input action '{moveActionName}' was not found.", this);
                enabled = false;
                return;
            }

            enabledMoveAction = !moveAction.enabled;
            if (enabledMoveAction)
            {
                moveAction.Enable();
            }

            moveAction.performed += OnMovePerformed;
        }

        private void OnDisable()
        {
            moveInput = Vector2.zero;
            externalInput = Vector2.zero;
            externalAccelerationMultiplier = 1f;
            externalSpeedMultiplier = 1f;

            if (moveAction != null)
            {
                moveAction.performed -= OnMovePerformed;
            }

            if (enabledMoveAction && moveAction != null)
            {
                moveAction.Disable();
            }

            enabledMoveAction = false;
        }

        private void Update()
        {
            if (!inputEnabled)
            {
                moveInput = Vector2.zero;
            }
            else if (useExternalControl)
            {
                moveInput = externalInput;
            }
            else
            {
                moveInput = moveAction != null
                    ? Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f)
                    : Vector2.zero;
            }
        }

        private void OnMovePerformed(InputAction.CallbackContext context)
        {
            if (inputEnabled && context.ReadValue<Vector2>().sqrMagnitude > 0f)
            {
                Debug.Log("이동!", this);
            }
        }

        private void FixedUpdate()
        {
            Vector3 up = Vector3.up;
            Vector3 forward = transform.TransformDirection(localForwardAxis.normalized);
            forward = Vector3.ProjectOnPlane(forward, up).normalized;

            Vector3 planarVelocity = Vector3.ProjectOnPlane(kartBody.linearVelocity, up);
            float forwardSpeed = Vector3.Dot(planarVelocity, forward);
            float throttle = moveInput.y;

            ApplyDrive(
                forward,
                forwardSpeed,
                throttle,
                AppliedAccelerationMultiplier,
                AppliedSpeedMultiplier);
            ApplySteering(up, forwardSpeed, throttle);
            ApplyLateralGrip(up, forward, planarVelocity);
            LimitForwardSpeed(forward, AppliedSpeedMultiplier);
        }

        private void ApplyDrive(
            Vector3 forward,
            float forwardSpeed,
            float throttle,
            float accelerationMultiplier,
            float speedMultiplier)
        {
            float forwardSpeedLimit = maxForwardSpeed * speedMultiplier + CatchUpSpeedBoost;
            if (throttle > 0f && forwardSpeed < forwardSpeedLimit)
            {
                kartBody.AddForce(
                    forward * (throttle * forwardAcceleration * accelerationMultiplier),
                    ForceMode.Acceleration);
            }
            else if (throttle < 0f && forwardSpeed > -maxReverseSpeed)
            {
                float acceleration = Mathf.Max(reverseAcceleration, forwardAcceleration);
                float targetSpeed = Mathf.MoveTowards(
                    forwardSpeed,
                    -maxReverseSpeed,
                    acceleration * Mathf.Abs(throttle) * Time.fixedDeltaTime);
                kartBody.AddForce(forward * (targetSpeed - forwardSpeed), ForceMode.VelocityChange);
            }
            else if (Mathf.Abs(throttle) < 0.01f)
            {
                kartBody.AddForce(-forward * (forwardSpeed * rollingResistance), ForceMode.Acceleration);
            }
        }

        private void ApplySteering(Vector3 up, float forwardSpeed, float throttle)
        {
            if (Mathf.Abs(moveInput.x) < 0.01f)
            {
                return;
            }

            float movementDirection = Mathf.Abs(forwardSpeed) > 0.2f
                ? Mathf.Sign(forwardSpeed)
                : Mathf.Sign(throttle);

            if (Mathf.Approximately(movementDirection, 0f))
            {
                return;
            }

            float speedRatio = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / Mathf.Max(maxForwardSpeed, 0.01f));
            float steeringFactor = Mathf.Lerp(minimumSteeringFactor, 1f, speedRatio);
            float angle = moveInput.x * movementDirection * steeringDegreesPerSecond
                * steeringFactor * Time.fixedDeltaTime;

            kartBody.MoveRotation(Quaternion.AngleAxis(angle, up) * kartBody.rotation);
        }

        private void ApplyLateralGrip(Vector3 up, Vector3 forward, Vector3 planarVelocity)
        {
            Vector3 sideways = Vector3.Cross(up, forward).normalized;
            float sidewaysSpeed = Vector3.Dot(planarVelocity, sideways);
            kartBody.AddForce(-sideways * (sidewaysSpeed * lateralGrip), ForceMode.Acceleration);
        }

        private void LimitForwardSpeed(Vector3 forward, float speedMultiplier)
        {
            float forwardSpeed = Vector3.Dot(kartBody.linearVelocity, forward);
            float forwardSpeedLimit = maxForwardSpeed * speedMultiplier + CatchUpSpeedBoost;
            float clampedSpeed = Mathf.Clamp(
                forwardSpeed,
                -maxReverseSpeed,
                forwardSpeedLimit);
            kartBody.linearVelocity += forward * (clampedSpeed - forwardSpeed);
        }
    }
}
