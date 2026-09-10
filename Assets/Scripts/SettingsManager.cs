using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlayGoKart.UI
{
    [DisallowMultipleComponent]
    public sealed class SettingsManager : MonoBehaviour
    {
        private const string MasterVolumeKey = "Settings.MasterVolume";
        private const string BgmVolumeKey = "Settings.BGMVolume";
        private const string SfxVolumeKey = "Settings.SFXVolume";
        private const string ResolutionWidthKey = "Settings.ResolutionWidth";
        private const string ResolutionHeightKey = "Settings.ResolutionHeight";
        private const string FullscreenKey = "Settings.Fullscreen";

        private const string MasterVolumeParameter = "MasterVolume";
        private const string BgmVolumeParameter = "BGMVolume";
        private const string SfxVolumeParameter = "SFXVolume";
        private const float MinimumDecibels = -80f;

        [Header("Audio")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider bgmVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Text masterVolumeLabel;
        [SerializeField] private Text bgmVolumeLabel;
        [SerializeField] private Text sfxVolumeLabel;

        [Header("Display")]
        [SerializeField] private Dropdown resolutionDropdown;
        [SerializeField] private Dropdown screenModeDropdown;

        private readonly List<ResolutionOption> availableResolutions = new List<ResolutionOption>();
        private bool initialized;

        private void Awake()
        {
            ConfigureVolumeSliders();
            BuildResolutionOptions();
            BuildScreenModeOptions();
            LoadSavedSettings();
            initialized = true;
        }

        private void OnEnable()
        {
            if (initialized)
            {
                RefreshVolumeLabels();
            }
        }

        public void OnMasterVolumeChanged(float value)
        {
            ApplyMixerVolume(MasterVolumeParameter, value, true);
            UpdateVolumeLabel(masterVolumeLabel, "MASTER VOLUME", value);
        }

        public void OnSettingsOpened()
        {
            RefreshVolumeLabels();

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(masterVolumeSlider.gameObject);
            }
        }

        public void OnBgmVolumeChanged(float value)
        {
            ApplyMixerVolume(BgmVolumeParameter, value, false);
            UpdateVolumeLabel(bgmVolumeLabel, "BGM VOLUME", value);
        }

        public void OnSfxVolumeChanged(float value)
        {
            ApplyMixerVolume(SfxVolumeParameter, value, false);
            UpdateVolumeLabel(sfxVolumeLabel, "SFX VOLUME", value);
        }

        public void ApplySettings()
        {
            ApplyCurrentAudioSettings();
            ApplyCurrentDisplaySettings();

            PlayerPrefs.SetFloat(MasterVolumeKey, masterVolumeSlider.value);
            PlayerPrefs.SetFloat(BgmVolumeKey, bgmVolumeSlider.value);
            PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolumeSlider.value);

            ResolutionOption resolution = GetSelectedResolution();
            PlayerPrefs.SetInt(ResolutionWidthKey, resolution.width);
            PlayerPrefs.SetInt(ResolutionHeightKey, resolution.height);
            PlayerPrefs.SetInt(FullscreenKey, IsFullscreenSelected() ? 1 : 0);
            PlayerPrefs.Save();

            Debug.Log("Settings applied and saved.", this);
        }

        public void RestoreDefaults()
        {
            SetSliderValue(masterVolumeSlider, 100f);
            SetSliderValue(bgmVolumeSlider, 80f);
            SetSliderValue(sfxVolumeSlider, 80f);

            ResolutionOption defaultResolution = GetDefaultResolution();
            resolutionDropdown.SetValueWithoutNotify(
                Mathf.Max(0, FindResolutionIndex(defaultResolution.width, defaultResolution.height)));
            screenModeDropdown.SetValueWithoutNotify(0);

            ApplyCurrentAudioSettings();
            RefreshVolumeLabels();
        }

        private void ConfigureVolumeSliders()
        {
            ConfigureSlider(masterVolumeSlider);
            ConfigureSlider(bgmVolumeSlider);
            ConfigureSlider(sfxVolumeSlider);
        }

        private static void ConfigureSlider(Slider slider)
        {
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
        }

        private void BuildResolutionOptions()
        {
            availableResolutions.Clear();
            HashSet<long> uniqueResolutions = new HashSet<long>();

            // Keep the two supported gameplay baselines available even when the
            // monitor/driver does not enumerate them through Screen.resolutions.
            AddResolutionIfUnique(1280, 720, uniqueResolutions);
            AddResolutionIfUnique(1920, 1080, uniqueResolutions);

            foreach (Resolution resolution in Screen.resolutions)
            {
                if (resolution.width < 1280 || resolution.height < 720)
                {
                    continue;
                }

                AddResolutionIfUnique(resolution.width, resolution.height, uniqueResolutions);
            }

            AddResolutionIfUnique(Screen.currentResolution.width, Screen.currentResolution.height, uniqueResolutions);

            availableResolutions.Sort((left, right) =>
            {
                int widthComparison = left.width.CompareTo(right.width);
                return widthComparison != 0 ? widthComparison : left.height.CompareTo(right.height);
            });

            List<string> labels = new List<string>(availableResolutions.Count);
            foreach (ResolutionOption resolution in availableResolutions)
            {
                labels.Add($"{resolution.width} x {resolution.height}");
            }

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(labels);
        }

        private void AddResolutionIfUnique(int width, int height, HashSet<long> uniqueResolutions)
        {
            long key = ((long)width << 32) | (uint)height;
            if (uniqueResolutions.Add(key))
            {
                availableResolutions.Add(new ResolutionOption(width, height));
            }
        }

        private void BuildScreenModeOptions()
        {
            screenModeDropdown.ClearOptions();
            screenModeDropdown.AddOptions(new List<string> { "FULLSCREEN", "WINDOWED" });
        }

        private void LoadSavedSettings()
        {
            float masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 100f);
            float bgmVolume = PlayerPrefs.GetFloat(BgmVolumeKey, 80f);
            float sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 80f);

            SetSliderValue(masterVolumeSlider, masterVolume);
            SetSliderValue(bgmVolumeSlider, bgmVolume);
            SetSliderValue(sfxVolumeSlider, sfxVolume);

            ResolutionOption defaultResolution = GetDefaultResolution();
            int savedWidth = PlayerPrefs.GetInt(ResolutionWidthKey, defaultResolution.width);
            int savedHeight = PlayerPrefs.GetInt(ResolutionHeightKey, defaultResolution.height);
            int savedResolutionIndex = FindResolutionIndex(savedWidth, savedHeight);
            if (savedResolutionIndex < 0)
            {
                savedResolutionIndex = FindResolutionIndex(defaultResolution.width, defaultResolution.height);
            }

            resolutionDropdown.SetValueWithoutNotify(savedResolutionIndex);

            int fullscreen = PlayerPrefs.GetInt(FullscreenKey, 1);
            screenModeDropdown.SetValueWithoutNotify(fullscreen == 1 ? 0 : 1);

            ApplyCurrentAudioSettings();
            ApplyCurrentDisplaySettings();
            RefreshVolumeLabels();
        }

        private void ApplyCurrentAudioSettings()
        {
            ApplyMixerVolume(MasterVolumeParameter, masterVolumeSlider.value, true);
            ApplyMixerVolume(BgmVolumeParameter, bgmVolumeSlider.value, false);
            ApplyMixerVolume(SfxVolumeParameter, sfxVolumeSlider.value, false);
        }

        private void ApplyMixerVolume(string parameterName, float percentage, bool masterFallback)
        {
            float decibels = percentage <= 0f
                ? MinimumDecibels
                : Mathf.Log10(percentage / 100f) * 20f;

            bool appliedToMixer = audioMixer != null && audioMixer.SetFloat(parameterName, decibels);
            if (masterFallback)
            {
                AudioListener.volume = appliedToMixer ? 1f : percentage / 100f;
            }
        }

        private void ApplyCurrentDisplaySettings()
        {
            ResolutionOption resolution = GetSelectedResolution();
            FullScreenMode screenMode = IsFullscreenSelected()
                ? FullScreenMode.ExclusiveFullScreen
                : FullScreenMode.Windowed;

            Screen.SetResolution(resolution.width, resolution.height, screenMode);
        }

        private ResolutionOption GetSelectedResolution()
        {
            int index = Mathf.Clamp(resolutionDropdown.value, 0, availableResolutions.Count - 1);
            return availableResolutions[index];
        }

        private bool IsFullscreenSelected()
        {
            return screenModeDropdown.value == 0;
        }

        private ResolutionOption GetDefaultResolution()
        {
            int preferredIndex = FindResolutionIndex(1920, 1080);
            if (preferredIndex >= 0 && preferredIndex < availableResolutions.Count)
            {
                return availableResolutions[preferredIndex];
            }

            int currentIndex = FindResolutionIndex(Screen.currentResolution.width, Screen.currentResolution.height);
            if (currentIndex >= 0 && currentIndex < availableResolutions.Count)
            {
                return availableResolutions[currentIndex];
            }

            return availableResolutions[availableResolutions.Count - 1];
        }

        private int FindResolutionIndex(int width, int height)
        {
            for (int i = 0; i < availableResolutions.Count; i++)
            {
                if (availableResolutions[i].width == width && availableResolutions[i].height == height)
                {
                    return i;
                }
            }

            return -1;
        }

        private void RefreshVolumeLabels()
        {
            UpdateVolumeLabel(masterVolumeLabel, "MASTER VOLUME", masterVolumeSlider.value);
            UpdateVolumeLabel(bgmVolumeLabel, "BGM VOLUME", bgmVolumeSlider.value);
            UpdateVolumeLabel(sfxVolumeLabel, "SFX VOLUME", sfxVolumeSlider.value);
        }

        private static void UpdateVolumeLabel(Text label, string title, float value)
        {
            label.text = $"{title}  {Mathf.RoundToInt(value)}";
        }

        private static void SetSliderValue(Slider slider, float value)
        {
            slider.SetValueWithoutNotify(Mathf.Clamp(value, 0f, 100f));
        }

        [Serializable]
        private struct ResolutionOption
        {
            public readonly int width;
            public readonly int height;

            public ResolutionOption(int width, int height)
            {
                this.width = width;
                this.height = height;
            }
        }
    }
}
