using System;
using UnityEngine;

namespace PlayGoKart.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class RaceTimer : MonoBehaviour
    {
        private double elapsedSeconds;
        private bool running;

        public double ElapsedSeconds => elapsedSeconds;
        public string FormattedTime => FormatTime(elapsedSeconds);

        public void ResetTimer()
        {
            elapsedSeconds = 0d;
            running = false;
        }

        public void StartTimer()
        {
            running = true;
        }

        public void StopTimer()
        {
            running = false;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (running)
            {
                elapsedSeconds += Math.Max(0f, unscaledDeltaTime);
            }
        }

        public static string FormatTime(double seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(Math.Max(0d, seconds));
            return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}.{time.Milliseconds:000}";
        }
    }
}
