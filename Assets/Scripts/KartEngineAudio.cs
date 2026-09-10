using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace PlayGoKart.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class KartEngineAudio : MonoBehaviour
    {
        [Header("Engine Clips")]
        [SerializeField] private AudioClip idleClip;
        [SerializeField] private AudioClip drivingClip;
        [SerializeField] private AudioMixerGroup outputMixerGroup;

        [Header("State")]
        [SerializeField] private bool muteEngineAudio;
        [SerializeField, Min(0f)] private float movingSpeedThreshold = 0.2f;
        [SerializeField, Range(0f, 1f)] private float idleVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float drivingVolume = 0.9f;

        [Header("3D Sound")]
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
        [SerializeField, Min(0.01f)] private float minDistance = 4f;
        [SerializeField, Min(0.01f)] private float maxDistance = 70f;

        private Rigidbody kartBody;
        private RacerProgress racerProgress;
        private AudioSource idleSource;
        private AudioSource drivingSource;
        private Coroutine impactDuckRoutine;
        private float impactVolumeMultiplier = 1f;

        public bool IsMoving { get; private set; }
        public bool IsMuted => muteEngineAudio;
        public bool IsSilencedForFinish { get; private set; }
        public AudioSource IdleSource => idleSource;
        public AudioSource DrivingSource => drivingSource;
        public float ImpactVolumeMultiplier => impactVolumeMultiplier;

        public void DuckForImpact(float volumeMultiplier, float holdDuration, float recoveryDuration)
        {
            if (!isActiveAndEnabled || muteEngineAudio ||
                (racerProgress != null && racerProgress.Finished))
            {
                return;
            }

            if (impactDuckRoutine != null)
            {
                StopCoroutine(impactDuckRoutine);
            }

            impactDuckRoutine = StartCoroutine(ApplyImpactDuck(
                Mathf.Clamp01(volumeMultiplier), Mathf.Max(0f, holdDuration), Mathf.Max(0f, recoveryDuration)));
        }

        private IEnumerator ApplyImpactDuck(float duckedVolume, float holdDuration, float recoveryDuration)
        {
            double recoveryStart = AudioSettings.dspTime + holdDuration;
            double recoveryEnd = recoveryStart + recoveryDuration;
            while (AudioSettings.dspTime < recoveryEnd)
            {
                float recovery = recoveryDuration > 0f
                    ? Mathf.Clamp01((float)(AudioSettings.dspTime - recoveryStart) / recoveryDuration)
                    : 0f;
                impactVolumeMultiplier = Mathf.Lerp(duckedVolume, 1f, Mathf.SmoothStep(0f, 1f, recovery));
                ApplyOutputVolumes();
                yield return null;
            }

            impactVolumeMultiplier = 1f;
            ApplyOutputVolumes();
            impactDuckRoutine = null;
        }

        private void ApplyOutputVolumes()
        {
            bool audible = isActiveAndEnabled && !muteEngineAudio && !IsSilencedForFinish &&
                (racerProgress == null || !racerProgress.Finished);
            if (idleSource != null)
            {
                idleSource.volume = audible && !IsMoving ? idleVolume * impactVolumeMultiplier : 0f;
            }
            if (drivingSource != null)
            {
                drivingSource.volume = audible && IsMoving ? drivingVolume * impactVolumeMultiplier : 0f;
            }
        }

        private void Awake()
        {
            kartBody = GetComponent<Rigidbody>();
            racerProgress = GetComponent<RacerProgress>();

            if (muteEngineAudio)
            {
                enabled = false;
                return;
            }

            if (idleClip == null || drivingClip == null)
            {
                Debug.LogError("KartEngineAudio requires both idle and driving clips.", this);
                enabled = false;
                return;
            }

            idleSource = CreateLoopingSource("Engine Audio - Idle", idleClip);
            drivingSource = CreateLoopingSource("Engine Audio - High On", drivingClip);

            IsMoving = GetPlanarSpeed() > movingSpeedThreshold;
            ApplyPlaybackState();
        }

        private void OnEnable()
        {
            if (muteEngineAudio)
            {
                enabled = false;
                return;
            }

            if (idleSource != null && drivingSource != null)
            {
                UpdateFinishedState();
            }
        }

        private void FixedUpdate()
        {
            if (UpdateFinishedState())
            {
                return;
            }

            bool isMovingNow = GetPlanarSpeed() > movingSpeedThreshold;
            if (isMovingNow == IsMoving)
            {
                return;
            }

            IsMoving = isMovingNow;
            ApplyPlaybackState();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            impactDuckRoutine = null;
            impactVolumeMultiplier = 1f;
            StopPlayback();
        }

        private float GetPlanarSpeed()
        {
            Vector3 velocity = kartBody.linearVelocity;
            velocity.y = 0f;
            return velocity.magnitude;
        }

        private AudioSource CreateLoopingSource(string sourceName, AudioClip clip)
        {
            GameObject sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);

            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.outputAudioMixerGroup = outputMixerGroup;
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0f;
            source.spatialBlend = spatialBlend;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = minDistance;
            source.maxDistance = Mathf.Max(minDistance, maxDistance);
            return source;
        }

        private void ApplyPlaybackState()
        {
            AudioSource activeSource = IsMoving ? drivingSource : idleSource;
            AudioSource inactiveSource = IsMoving ? idleSource : drivingSource;

            inactiveSource.Stop();
            inactiveSource.volume = 0f;
            inactiveSource.enabled = false;

            activeSource.enabled = true;
            ApplyOutputVolumes();
            if (!activeSource.isPlaying)
            {
                activeSource.Play();
            }
        }

        private bool UpdateFinishedState()
        {
            bool shouldSilence = racerProgress != null && racerProgress.Finished;
            if (shouldSilence)
            {
                if (!IsSilencedForFinish)
                {
                    IsSilencedForFinish = true;
                    StopPlayback();
                }

                return true;
            }

            if (IsSilencedForFinish)
            {
                IsSilencedForFinish = false;
                IsMoving = GetPlanarSpeed() > movingSpeedThreshold;
                ApplyPlaybackState();
            }

            return false;
        }

        private void StopPlayback()
        {
            StopSource(idleSource);
            StopSource(drivingSource);
        }

        private static void StopSource(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            source.Stop();
            source.volume = 0f;
            source.enabled = false;
        }
    }
}
