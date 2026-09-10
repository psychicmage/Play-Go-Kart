using UnityEngine;

namespace PlayGoKart.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RaceCheckpoint : MonoBehaviour
    {
        [SerializeField, Min(0)] private int checkpointIndex;
        [SerializeField] private RaceManager raceManager;

        public int CheckpointIndex => checkpointIndex;

        public void Configure(int index, RaceManager manager)
        {
            checkpointIndex = Mathf.Max(0, index);
            raceManager = manager;
            BoxCollider trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            Rigidbody body = other.attachedRigidbody;
            RacerProgress racer = body == null ? null : body.GetComponent<RacerProgress>();
            if (racer == null)
            {
                return;
            }

            raceManager?.RegisterCheckpoint(racer, checkpointIndex, transform);
        }
    }
}
