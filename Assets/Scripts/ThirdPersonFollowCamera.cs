using UnityEngine;

namespace PlayGoKart.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class ThirdPersonFollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [Tooltip("The imported kart model faces local +X.")]
        [SerializeField] private Vector3 targetForwardAxis = Vector3.right;
        [SerializeField, Min(0f)] private float followDistance = 6f;
        [SerializeField, Min(0f)] private float followHeight = 3f;
        [SerializeField, Min(0f)] private float lookAhead = 2f;
        [SerializeField, Min(0f)] private float lookHeight = 1f;
        [SerializeField, Min(0.01f)] private float positionSmoothTime = 0.12f;
        [SerializeField, Min(0f)] private float rotationSmoothSpeed = 10f;

        private Vector3 followVelocity;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            SnapToTarget();
        }

        private void Start()
        {
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 forward = GetTargetForward();
            Vector3 desiredPosition = target.position - forward * followDistance + Vector3.up * followHeight;
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref followVelocity,
                positionSmoothTime);

            Vector3 lookTarget = target.position + Vector3.up * lookHeight + forward * lookAhead;
            Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
            float blend = 1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, blend);
        }

        private void SnapToTarget()
        {
            if (target == null)
            {
                return;
            }

            Vector3 forward = GetTargetForward();
            transform.position = target.position - forward * followDistance + Vector3.up * followHeight;
            transform.rotation = Quaternion.LookRotation(
                target.position + Vector3.up * lookHeight + forward * lookAhead - transform.position,
                Vector3.up);
        }

        private Vector3 GetTargetForward()
        {
            Vector3 forward = target.TransformDirection(targetForwardAxis.normalized);
            return Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
        }
    }
}
