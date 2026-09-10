using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlayGoKart.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(Collider), typeof(RacerProgress))]
    public sealed class RacerCollisionPassThrough : MonoBehaviour
    {
        [Header("Persistent Contact")]
        [SerializeField, Min(0.1f)] private float contactDurationBeforePassThrough = 1.25f;
        [SerializeField, Min(0.1f)] private float passThroughDuration = 2f;

        private readonly Dictionary<Collider, float> contactDurations = new Dictionary<Collider, float>();
        private readonly HashSet<Collider> ignoredColliders = new HashSet<Collider>();
        private Collider ownCollider;

        public int PassThroughActivationCount { get; private set; }
        public bool IsPassingThrough => ignoredColliders.Count > 0;

        private void Awake()
        {
            ownCollider = GetComponent<Collider>();
        }

        private void OnCollisionStay(Collision collision)
        {
            Collider otherCollider = collision.collider;
            Rigidbody otherBody = collision.rigidbody;
            if (otherCollider == null || otherBody == null ||
                otherBody.GetComponent<RacerProgress>() == null ||
                ignoredColliders.Contains(otherCollider) || !OwnsPair(otherBody))
            {
                return;
            }

            contactDurations.TryGetValue(otherCollider, out float duration);
            duration += Time.fixedDeltaTime;
            if (duration < contactDurationBeforePassThrough)
            {
                contactDurations[otherCollider] = duration;
                return;
            }

            contactDurations.Remove(otherCollider);
            StartCoroutine(TemporarilyIgnoreCollision(otherCollider));
        }

        private void OnCollisionExit(Collision collision)
        {
            if (collision.collider != null && !ignoredColliders.Contains(collision.collider))
            {
                contactDurations.Remove(collision.collider);
            }
        }

        private bool OwnsPair(Rigidbody otherBody)
        {
            return string.CompareOrdinal(name, otherBody.name) < 0;
        }

        private IEnumerator TemporarilyIgnoreCollision(Collider otherCollider)
        {
            if (ownCollider == null || otherCollider == null || !ignoredColliders.Add(otherCollider))
            {
                yield break;
            }

            Physics.IgnoreCollision(ownCollider, otherCollider, true);
            PassThroughActivationCount++;

            float remaining = passThroughDuration;
            while (remaining > 0f && ownCollider != null && otherCollider != null)
            {
                remaining -= Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            while (ownCollider != null && otherCollider != null &&
                   ownCollider.bounds.Intersects(otherCollider.bounds))
            {
                yield return new WaitForFixedUpdate();
            }

            RestoreCollision(otherCollider);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            foreach (Collider otherCollider in ignoredColliders)
            {
                if (ownCollider != null && otherCollider != null)
                {
                    Physics.IgnoreCollision(ownCollider, otherCollider, false);
                }
            }

            ignoredColliders.Clear();
            contactDurations.Clear();
        }

        private void RestoreCollision(Collider otherCollider)
        {
            if (ownCollider != null && otherCollider != null)
            {
                Physics.IgnoreCollision(ownCollider, otherCollider, false);
            }

            ignoredColliders.Remove(otherCollider);
            contactDurations.Remove(otherCollider);
        }
    }
}
