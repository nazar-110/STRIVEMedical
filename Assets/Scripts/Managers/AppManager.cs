using System.Collections.Generic;
using UnityEngine;
using StriveMedical.Data;
using USB;

namespace StriveMedical.Managers
{
    /// <summary>
    /// Central singleton. Owns navigation, shared state, and settings.
    /// Attach to one empty GameObject called "AppManager" in your first scene.
    /// </summary>
    public class AppManager : MonoBehaviour
    {
        public static AppManager Instance { get; private set; }

        // ── Theme ─────────────────────────────────────────────────────────────
        [Header("Theme")]
        public Color PrimaryColor   = new Color(0.204f, 0.298f, 0.239f); // #344C3D
        public Color AccentColor    = new Color(0.557f, 0.643f, 0.549f); // #8EA58C

        // ── Shared navigation state ───────────────────────────────────────────
        // These are the equivalent of React Router's location.state
        public int    ActiveModuleId      { get; private set; } = 1;
        public string ActiveExerciseName  { get; private set; } = "";
        public bool   ArmConnected        { get; private set; } = false;

        // ── Settings ──────────────────────────────────────────────────────────
        public float MovementSensitivity { get; private set; } = 0.5f;
        public float ForceFeedbackLevel  { get; private set; } = 0.6f;

        // ── Navigation stack (for navigate(-1) / back button) ─────────────────
        private Stack<AppScreen> _history = new Stack<AppScreen>();
        public AppScreen CurrentScreen { get; private set; } = AppScreen.Login;

        // ── Events ────────────────────────────────────────────────────────────
        public event System.Action<AppScreen> OnScreenChanged;
        public event System.Action<bool>      OnArmConnectionChanged;

        // ─────────────────────────────────────────────────────────────────────
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
            ArmConnected = USBManager.IsConnected;
            USBManager.OnUSBConnected += HandleUSBConnected;
            USBManager.OnUSBDisconnected += HandleUSBDisconnected;
        }

        void OnDestroy()
        {
            if (Instance != this) return;

            USBManager.OnUSBConnected -= HandleUSBConnected;
            USBManager.OnUSBDisconnected -= HandleUSBDisconnected;
        }

        // ── Navigation ────────────────────────────────────────────────────────

        /// <summary>
        /// Navigate to a screen.
        /// Equivalent to: navigate("/screenName")
        /// </summary>
        public void NavigateTo(AppScreen screen)
        {
            _history.Push(CurrentScreen);
            CurrentScreen = screen;
            OnScreenChanged?.Invoke(screen);
        }

        /// <summary>
        /// Go back to the previous screen.
        /// Equivalent to: navigate(-1)
        /// </summary>
        public void NavigateBack()
        {
            if (_history.Count > 0)
            {
                CurrentScreen = _history.Pop();
                OnScreenChanged?.Invoke(CurrentScreen);
            }
            else
            {
                // Nothing to go back to — go home
                NavigateTo(AppScreen.Home);
            }
        }

        /// <summary>
        /// Navigate to ExerciseWarning and store which exercise to run.
        /// Equivalent to: navigate("/exercise-warning", { state: { exerciseName, moduleId } })
        /// </summary>
        public void StartExercise(string exerciseName, int moduleId)
        {
            ActiveExerciseName = exerciseName;
            ActiveModuleId     = moduleId;
            NavigateTo(AppScreen.ExerciseWarning);
        }

        /// <summary>
        /// Called by ExerciseWarning after the countdown finishes.
        /// </summary>
        public void BeginExerciseInProgress()
        {
            NavigateTo(AppScreen.ExerciseInProgress);
        }

        /// <summary>
        /// Called when the user completes an exercise.
        /// </summary>
        public void CompleteExercise()
        {
            NavigateTo(AppScreen.LessonComplete);
        }

        /// <summary>
        /// Navigate to a module's detail screen.
        /// Equivalent to: navigate(`/module/${moduleId}`, { state: { moduleId } })
        /// </summary>
        public void OpenModuleDetail(int moduleId)
        {
            ActiveModuleId = moduleId;
            NavigateTo(AppScreen.ModuleDetail);
        }

        // ── Device ────────────────────────────────────────────────────────────

        public void SetArmConnected(bool connected)
        {
            if (ArmConnected == connected)
            {
                return;
            }

            ArmConnected = connected;
            OnArmConnectionChanged?.Invoke(connected);
        }

        private void HandleUSBConnected()
        {
            SetArmConnected(true);
        }

        private void HandleUSBDisconnected()
        {
            SetArmConnected(false);
        }

        // ── Settings ──────────────────────────────────────────────────────────

        public void SetMovementSensitivity(float value)
        {
            MovementSensitivity = Mathf.Clamp01(value);
        }

        public void SetForceFeedbackLevel(float value)
        {
            ForceFeedbackLevel = Mathf.Clamp01(value);
        }

        /// <summary>
        /// Save to PlayerPrefs and return to Home.
        /// Equivalent to the Save button in Settings.tsx.
        /// </summary>
        public void SaveSettings()
        {
            PlayerPrefs.SetFloat("MovementSensitivity", MovementSensitivity);
            PlayerPrefs.SetFloat("ForceFeedbackLevel",  ForceFeedbackLevel);
            PlayerPrefs.Save();
            Debug.Log($"[AppManager] Saved — sensitivity:{MovementSensitivity:P0}  force:{ForceFeedbackLevel:P0}");
            NavigateTo(AppScreen.Home);
        }

        void LoadSettings()
        {
            MovementSensitivity = PlayerPrefs.GetFloat("MovementSensitivity", 0.5f);
            ForceFeedbackLevel  = PlayerPrefs.GetFloat("ForceFeedbackLevel",  0.6f);
        }
    }
}
