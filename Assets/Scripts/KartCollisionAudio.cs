using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace PlayGoKart.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(Collider), typeof(RacerProgress))]
    public sealed class KartCollisionAudio : MonoBehaviour
    {
        [Header("Collision Sound")]
        [SerializeField] private AudioClip collisionClip;
        [SerializeField] private AudioMixerGroup outputMixerGroup;
        [SerializeField, Range(0f, 1f)] private float volume = 0.95f;
        [SerializeField, Range(0f, 1f)] private float minimumVolumeScale = 0.7f;
        [SerializeField, Min(0.05f)] private float maximumPlaybackDuration = 0.35f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.05f;

        [Header("Impact Filtering")]
        [SerializeField, Min(0f)] private float minimumImpactSpeed = 0.5f;
        [SerializeField, Min(0f)] private float fullVolumeImpactSpeed = 8f;
        [SerializeField, Range(0f, 1f)] private float maximumWallUpDot = 0.65f;
        [SerializeField, Min(0f)] private float replayCooldown = 0.2f;

        [Header("Player Impact Contrast")]
        [SerializeField, Range(0f, 1f)] private float engineDuckedVolume = 0.35f;
        [SerializeField, Min(0f)] private float engineDuckHold = 0.18f;
        [SerializeField, Min(0f)] private float engineDuckRecovery = 0.25f;

        [Header("Spatial Sound")]
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0f;
        [SerializeField, Min(0.01f)] private float minDistance = 3f;
        [SerializeField, Min(0.01f)] private float maxDistance = 45f;

        private RacerProgress racerProgress;
        private KartEngineAudio engineAudio;
        private AudioSource collisionSource;
        private float nextPlaybackTime;

        public int PlaybackCount { get; private set; }
        public AudioSource CollisionSource => collisionSource;

        private void Awake()
        {
            racerProgress = GetComponent<RacerProgress>();
            engineAudio = GetComponent<KartEngineAudio>();
            collisionSource = gameObject.AddComponent<AudioSource>();
            collisionSource.outputAudioMixerGroup = outputMixerGroup;
            collisionSource.playOnAwake = false;
            collisionSource.loop = false;
            collisionSource.spatialBlend = spatialBlend;
            collisionSource.dopplerLevel = 0f;
            collisionSource.rolloffMode = AudioRolloffMode.Logarithmic;
            collisionSource.minDistance = minDistance;
            collisionSource.maxDistance = Mathf.Max(minDistance, maxDistance);
            collisionSource.clip = collisionClip;
            if (collisionClip != null)
            {
                collisionClip.LoadAudioData();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Only the player owns impact playback, even if this component is copied to an AI.
            if (!isActiveAndEnabled || !racerProgress.IsPlayer || AudioListener.pause ||
                collisionClip == null || collisionSource == null || Time.time < nextPlaybackTime)
            {
                return;
            }

            float impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed < minimumImpactSpeed)
            {
                return;
            }

            RacerProgress otherRacer = collision.rigidbody == null
                ? collision.collider.GetComponentInParent<RacerProgress>()
                : collision.rigidbody.GetComponent<RacerProgress>();

            bool hitAnotherKart = otherRacer != null && otherRacer != racerProgress;
            if (otherRacer == racerProgress)
            {
                return;
            }

            if (!hitAnotherKart && !IsWallCollision(collision))
            {
                return;
            }

            float impactVolume = Mathf.InverseLerp(
                minimumImpactSpeed,
                Mathf.Max(minimumImpactSpeed + 0.01f, fullVolumeImpactSpeed),
                impactSpeed);
            // Restart one short voice instead of stacking overlapping full-length crash clips.
            StopAllCoroutines();
            collisionSource.Stop();
            collisionSource.volume = volume * Mathf.Lerp(minimumVolumeScale, 1f, impactVolume);
            collisionSource.Play();
            if (engineAudio != null)
            {
                engineAudio.DuckForImpact(engineDuckedVolume, engineDuckHold, engineDuckRecovery);
            }
            StartCoroutine(FadeAndStop());
            nextPlaybackTime = Time.time + replayCooldown;
            PlaybackCount++;
        }

        private IEnumerator FadeAndStop()
        {
            float duration = Mathf.Min(maximumPlaybackDuration, collisionClip.length);
            float fadeDuration = Mathf.Min(fadeOutDuration, duration);
            float initialVolume = collisionSource.volume;
            double endTime = AudioSettings.dspTime + duration;
            while (AudioSettings.dspTime < endTime)
            {
                float remaining = (float)(endTime - AudioSettings.dspTime);
                collisionSource.volume = initialVolume *
                    (fadeDuration > 0f ? Mathf.Clamp01(remaining / fadeDuration) : 1f);
                yield return null;
                // The DSP clock freezes with AudioListener.pause and ignores a long preceding frame.
            }

            collisionSource.Stop();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (collisionSource != null)
            {
                collisionSource.Stop();
            }
        }

        private bool IsWallCollision(Collision collision)
        {
            for (int i = 0; i < collision.contactCount; i++)
            {
                if (Mathf.Abs(Vector3.Dot(collision.GetContact(i).normal, Vector3.up)) <= maximumWallUpDot)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
