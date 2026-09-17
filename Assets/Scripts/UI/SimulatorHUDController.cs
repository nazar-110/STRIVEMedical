using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using StriveMedical.Data;

namespace StriveMedical.UI
{
    /// <summary>
    /// Attach this MonoBehaviour to the UIDocument GameObject in the
    /// NewToolSimulator scene. It reads the active module/exercise from
    /// AppManager and drives the HUD.
    ///
    /// SETUP:
    ///   1. Create an empty GameObject in NewToolSimulator, name it "HUD"
    ///   2. Add Component → UI Document — assign SimulatorHUD.uxml as the
    ///      Source Asset and your Panel Settings asset
    ///   3. Add Component → SimulatorHUDController (this script)
    ///   4. In the Inspector set menuSceneName to the name of your menu scene
    ///      (e.g. "MainMenu" or whatever scene hosts the UI Toolkit menus)
    ///
    /// OBJECTIVE SYSTEM:
    ///   • Call  hud.CompleteCurrentStep()  from ANY script to mark the active
    ///     step done, show the banner, advance to the next step, and update
    ///     the progress bar + task list all at once.
    ///   • The easiest way to wire this up without extra code is to add a
    ///     TrainingObjective component to a trigger-collider GameObject in the
    ///     scene and drag the HUD into its inspector slot — see TrainingObjective.cs.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class SimulatorHUDController : MonoBehaviour
    {
        [Header("Scene Routing")]
        [Tooltip("Name of the scene to return to when the user exits")]
        public string menuSceneName = "SampleScene";

        [Header("Exercise Data (optional override)")]
        [Tooltip("Leave blank to pull from AppManager automatically")]
        public string overrideExerciseName = "";
        public string overrideModuleName   = "";

        [Header("Banner Timing")]
        [Tooltip("How long the banner stays fully visible before fading (seconds)")]
        public float bannerHoldSeconds = 2.5f;
        [Tooltip("How long the fade-out takes (seconds)")]
        public float bannerFadeSeconds = 0.6f;

        // ── Cached UI references ──────────────────────────────────────────
        VisualElement _root;
        VisualElement _hudProgressFill;
        VisualElement _taskListExpanded;
        VisualElement _taskListContainer;
        VisualElement _hintPanel;
        VisualElement _pausedOverlay;
        VisualElement _completionCountdownOverlay;
        VisualElement _armDot;
        VisualElement _stepCompleteBanner;      // NEW — center-top notification

        Label _progressPctLabel;
        Label _moduleNameLabel;
        Label _exerciseNameLabel;
        Label _currentStepLabel;
        Label _currentStepDetail;
        Label _currentStepNum;
        Label _taskCounterLabel;
        Label _armStatusLabel;
        Label _statTimeVal;
        Label _statRepsVal;
        Label _statAccuracyVal;
        Label _completionCountdownLabel;
        Label _stepBannerLabel;                 // NEW — text inside the banner

        Button _homeBtn;
        Button _pauseBtn;
        Button _tasksToggleBtn;
        Button _hintsBtn;
        Button _hintsCloseBtn;
        Button _completeBtn;
        Button _exitBtn;

        // ── State ─────────────────────────────────────────────────────────
        System.Action<ArmOperatingMode> _onArmModeChanged; // stored for clean unsubscription
        bool  _isPaused       = false;
        bool  _tasksExpanded  = false;
        bool  _hintsVisible   = false;
        bool  _isCompleting    = false;
        float _elapsedSeconds = 0f;
        bool  _timerRunning   = true;
        int   _currentStepIndex = 0; // 0-based, starts at step 1 (index 0)

        Coroutine _bannerCoroutine; // track so we can interrupt if steps complete quickly

        // Task list — matches the Demo module objectives.
        // NOTE: NOT readonly so individual elements can be mutated when steps are completed.
        (string label, string detail, bool done)[] _tasks =
        {
            ("Place cube in the box",         "Pick up the small cube and drop it into the open box on the table.",        false),
            ("Remove cube from the box",      "Take the cube back out of the box and place it on the table surface.",      false),
            ("Needle into flesh target",      "Pick up the needle and drive it into the red target block on the table.",   false),
        };

        readonly string[] _hints =
        {
            "Click and hold the left mouse button to grab objects",
            "Release the mouse button to drop the object — position matters!",
            "Use the scroll wheel to adjust the grabber depth",
            "Take your time — accuracy is more important than speed",
        };

        // ── Unity lifecycle ───────────────────────────────────────────────
        void Start()
        {
            var doc = GetComponent<UIDocument>();
            _root = doc.rootVisualElement;

            CacheReferences();
            PopulateData();
            PopulateTaskList();
            PopulateHintList();
            WireButtons();

            // Subscribe to arm mode changes so the pill updates when homing starts/ends.
            _onArmModeChanged = _ => RefreshArmPill();
            var pp = PacketProcessor.PacketProcessor.Instance;
            if (pp != null) pp.OnArmModeChanged += _onArmModeChanged;

            RefreshArmPill();
            RefreshCurrentStep();
            RefreshTaskCounter();
        }

        void OnDestroy()
        {
            var pp = PacketProcessor.PacketProcessor.Instance;
            if (pp != null && _onArmModeChanged != null)
                pp.OnArmModeChanged -= _onArmModeChanged;
        }

        void Update()
        {
            if (_timerRunning && !_isPaused)
            {
                _elapsedSeconds += Time.deltaTime;
                int m = Mathf.FloorToInt(_elapsedSeconds / 60f);
                int s = Mathf.FloorToInt(_elapsedSeconds % 60f);
                if (_statTimeVal != null)
                    _statTimeVal.text = $"{m}:{s:00}";
            }
        }

        // ── Cache ─────────────────────────────────────────────────────────
        void CacheReferences()
        {
            _hudProgressFill    = _root.Q<VisualElement>("hud-progress-fill");
            _taskListExpanded   = _root.Q<VisualElement>("task-list-expanded");
            _taskListContainer  = _root.Q<VisualElement>("task-list-container");
            _hintPanel          = _root.Q<VisualElement>("hint-panel");
            _pausedOverlay      = _root.Q<VisualElement>("paused-overlay");
            _completionCountdownOverlay = _root.Q<VisualElement>("completion-countdown-overlay");
            _armDot             = _root.Q<VisualElement>("arm-dot");
            _stepCompleteBanner = _root.Q<VisualElement>("step-complete-banner"); // NEW

            _progressPctLabel   = _root.Q<Label>("progress-pct-label");
            _moduleNameLabel    = _root.Q<Label>("module-name-label");
            _exerciseNameLabel  = _root.Q<Label>("exercise-name-label");
            _currentStepLabel   = _root.Q<Label>("current-step-label");
            _currentStepDetail  = _root.Q<Label>("current-step-detail");
            _currentStepNum     = _root.Q<Label>("current-step-num");
            _taskCounterLabel   = _root.Q<Label>("task-counter-label");
            _armStatusLabel     = _root.Q<Label>("arm-status-label");
            _statTimeVal        = _root.Q<Label>("stat-time-val");
            _statRepsVal        = _root.Q<Label>("stat-reps-val");
            _statAccuracyVal    = _root.Q<Label>("stat-accuracy-val");
            _completionCountdownLabel = _root.Q<Label>("completion-countdown-label");
            _stepBannerLabel    = _root.Q<Label>("step-banner-label");            // NEW

            _homeBtn            = _root.Q<Button>("home-btn");
            _pauseBtn           = _root.Q<Button>("pause-btn");
            _tasksToggleBtn     = _root.Q<Button>("tasks-toggle-btn");
            _hintsBtn           = _root.Q<Button>("hints-btn");
            _hintsCloseBtn      = _root.Q<Button>("hints-close-btn");
            _completeBtn        = _root.Q<Button>("complete-btn");
            _exitBtn            = _root.Q<Button>("exit-btn");
        }

        // ── Data population ───────────────────────────────────────────────
        void PopulateData()
        {
            var app = Managers.AppManager.Instance;

            string exerciseName = !string.IsNullOrEmpty(overrideExerciseName)
                ? overrideExerciseName
                : (app != null ? app.ActiveExerciseName : "Exercise");

            string moduleName = !string.IsNullOrEmpty(overrideModuleName)
                ? overrideModuleName
                : (app != null && ModuleDatabase.All.TryGetValue(app.ActiveModuleId, out var mod)
                    ? mod.name : "Training Module");

            int progress = app != null && ModuleDatabase.All.TryGetValue(app.ActiveModuleId, out var modData)
                ? modData.progress : 45;

            if (_exerciseNameLabel != null) _exerciseNameLabel.text = exerciseName;
            if (_moduleNameLabel   != null) _moduleNameLabel.text   = moduleName;
            if (_progressPctLabel  != null) _progressPctLabel.text  = $"{progress}%";
            if (_hudProgressFill   != null) _hudProgressFill.style.width = Length.Percent(progress);

            // Arm status
            if (app != null)
                app.OnArmConnectionChanged += _ => RefreshArmPill();
            RefreshArmPill();
        }

        void PopulateTaskList()
        {
            if (_taskListContainer == null) return;
            _taskListContainer.Clear();

            for (int i = 0; i < _tasks.Length; i++)
            {
                var (label, detail, done) = _tasks[i];
                bool isCurrent = i == _currentStepIndex;

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems    = Align.FlexStart;
                row.style.paddingTop    = row.style.paddingBottom = 10;
                row.style.borderBottomWidth = 1;
                row.style.borderBottomColor = new Color(1f, 1f, 1f, 0.06f);

                // Badge
                var badge = new VisualElement();
                badge.style.width  = badge.style.height = 24;
                badge.style.borderTopLeftRadius     = badge.style.borderTopRightRadius =
                badge.style.borderBottomLeftRadius  = badge.style.borderBottomRightRadius = 12;
                badge.style.alignItems    = Align.Center;
                badge.style.justifyContent = Justify.Center;
                badge.style.marginRight   = 10;
                badge.style.flexShrink    = 0;
                badge.style.marginTop     = 2;

                var badgeLabel = new Label(done ? "✓" : (i + 1).ToString());
                badgeLabel.style.fontSize = 11;
                badgeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                badgeLabel.style.color = Color.white;

                if (done)
                    badge.style.backgroundColor = new Color(0.133f, 0.773f, 0.369f);
                else if (isCurrent)
                    badge.style.backgroundColor = new Color(0.204f, 0.298f, 0.239f);
                else
                    badge.style.backgroundColor = new Color(0.25f, 0.28f, 0.33f);

                badge.Add(badgeLabel);

                // Text
                var textCol = new VisualElement();
                textCol.style.flexGrow = 1;

                var taskLabel = new Label(label);
                taskLabel.style.fontSize   = 13;
                taskLabel.style.whiteSpace = WhiteSpace.Normal;
                taskLabel.style.color      = done
                    ? new Color(0.42f, 0.45f, 0.50f)
                    : isCurrent
                        ? new Color(0.953f, 0.953f, 0.953f)
                        : new Color(0.65f, 0.68f, 0.72f);
                if (done)
                {
                    taskLabel.text = $"<s>{label}</s>";
                    taskLabel.enableRichText = true;
                }
                if (isCurrent) taskLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                textCol.Add(taskLabel);

                if (isCurrent)
                {
                    var detailLabel = new Label(detail);
                    detailLabel.style.fontSize   = 11;
                    detailLabel.style.whiteSpace = WhiteSpace.Normal;
                    detailLabel.style.color      = new Color(0.612f, 0.639f, 0.686f);
                    detailLabel.style.marginTop  = 3;
                    textCol.Add(detailLabel);
                }

                row.Add(badge);
                row.Add(textCol);
                _taskListContainer.Add(row);
            }
        }

        void PopulateHintList()
        {
            var container = _root.Q<VisualElement>("hint-list-container");
            if (container == null) return;
            container.Clear();

            int i = 1;
            foreach (var hint in _hints)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems    = Align.FlexStart;
                row.style.marginBottom  = 10;

                var numCircle = new VisualElement();
                numCircle.style.width  = numCircle.style.height = 22;
                numCircle.style.borderTopLeftRadius     = numCircle.style.borderTopRightRadius =
                numCircle.style.borderBottomLeftRadius  = numCircle.style.borderBottomRightRadius = 11;
                numCircle.style.backgroundColor = new Color(0.204f, 0.298f, 0.239f);
                numCircle.style.alignItems      = Align.Center;
                numCircle.style.justifyContent  = Justify.Center;
                numCircle.style.marginRight     = 10;
                numCircle.style.flexShrink      = 0;

                var numLabel = new Label(i.ToString());
                numLabel.style.fontSize = 10;
                numLabel.style.color    = Color.white;
                numLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                numCircle.Add(numLabel);

                var hintLabel = new Label(hint);
                hintLabel.style.fontSize   = 13;
                hintLabel.style.color      = new Color(0.612f, 0.639f, 0.686f);
                hintLabel.style.whiteSpace = WhiteSpace.Normal;
                hintLabel.style.flexGrow   = 1;

                row.Add(numCircle);
                row.Add(hintLabel);
                container.Add(row);
                i++;
            }
        }

        // ── Button wiring ─────────────────────────────────────────────────
        void WireButtons()
        {
            _homeBtn?.RegisterCallback<ClickEvent>(_ => HomeArmFromLesson());
            _pauseBtn?.RegisterCallback<ClickEvent>(_ => TogglePause());
            _pausedOverlay?.RegisterCallback<ClickEvent>(_ => TogglePause());
            _tasksToggleBtn?.RegisterCallback<ClickEvent>(_ => ToggleTaskList());
            _hintsBtn?.RegisterCallback<ClickEvent>(_ => ToggleHints(true));
            _hintsCloseBtn?.RegisterCallback<ClickEvent>(_ => ToggleHints(false));
            _completeBtn?.RegisterCallback<ClickEvent>(_ => BeginCompleteExercise());
            _exitBtn?.RegisterCallback<ClickEvent>(_ => ExitToMenu());
        }

        // ── Actions ───────────────────────────────────────────────────────
        void TogglePause()
        {
            _isPaused = !_isPaused;
            if (_pauseBtn != null) _pauseBtn.text = _isPaused ? "▶" : "⏸";
            if (_pausedOverlay != null)
                _pausedOverlay.style.display = _isPaused ? DisplayStyle.Flex : DisplayStyle.None;

            if (_pauseBtn != null)
            {
                _pauseBtn.style.backgroundColor = _isPaused
                    ? new Color(0.204f, 0.298f, 0.239f)
                    : new Color(1f, 1f, 1f, 0.06f);
            }
        }

        void ToggleTaskList()
        {
            _tasksExpanded = !_tasksExpanded;
            if (_taskListExpanded != null)
                _taskListExpanded.style.display = _tasksExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            if (_tasksToggleBtn != null)
                _tasksToggleBtn.text = _tasksExpanded ? "▲" : "▼";
        }

        void ToggleHints(bool show)
        {
            _hintsVisible = show;
            if (_hintPanel != null)
                _hintPanel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;

            if (_hintsBtn != null)
            {
                _hintsBtn.style.backgroundColor = show
                    ? new Color(0.204f, 0.298f, 0.239f)
                    : new Color(1f, 1f, 1f, 0.06f);
            }
        }

        void HomeArmFromLesson()
        {
            SendStartHomingCommand("lesson home button");
            ResetVisualArmHome();
        }

        void BeginCompleteExercise()
        {
            if (_isCompleting)
                return;

            StartCoroutine(CompleteExerciseAfterCountdown());
        }

        IEnumerator CompleteExerciseAfterCountdown()
        {
            _isCompleting = true;
            _timerRunning = false;

            if (_completeBtn != null)
                _completeBtn.SetEnabled(false);
            if (_homeBtn != null)
                _homeBtn.SetEnabled(false);

            if (_completionCountdownOverlay != null)
                _completionCountdownOverlay.style.display = DisplayStyle.Flex;

            for (int i = 3; i >= 1; i--)
            {
                if (_completionCountdownLabel != null)
                    _completionCountdownLabel.text = i.ToString();
                yield return new WaitForSeconds(1f);
            }

            if (_completionCountdownLabel != null)
                _completionCountdownLabel.text = "Returning home...";

            SendStartHomingCommand("lesson completion");
            ResetVisualArmHome();
            yield return new WaitForSeconds(0.25f);

            CompleteExercise();
        }

        void CompleteExercise()
        {
            _timerRunning = false;
            var app = Managers.AppManager.Instance;
            if (app != null)
                app.CompleteExercise(); // fires OnScreenChanged → LessonComplete
            SceneManager.LoadScene(menuSceneName);
        }

        void SendStartHomingCommand(string source)
        {
            var pp = PacketProcessor.PacketProcessor.Instance;
            if (pp == null)
            {
                Debug.LogWarning($"SimulatorHUDController: Cannot home arm from {source}; PacketProcessor is unavailable.");
                return;
            }

            Debug.Log($"SimulatorHUDController: Sending Start Homing from {source}.");
            pp.SendStartHoming();
        }

        void ResetVisualArmHome()
        {
            var follower = FindFirstObjectByType<KinematicsFollower>();
            if (follower == null)
            {
                Debug.LogWarning("SimulatorHUDController: Cannot reset visual arm; KinematicsFollower was not found.");
                return;
            }

            follower.ResetVisualToHome();
        }

        void ExitToMenu()
        {
            _timerRunning = false;
            // Return arm to home before leaving the simulator. The guard in
            // SendStartHoming() prevents a duplicate send if the home button
            // was already pressed moments earlier.
            SendStartHomingCommand("lesson exit");
            var app = Managers.AppManager.Instance;
            if (app != null) app.NavigateBack();
            SceneManager.LoadScene(menuSceneName);
        }

        // ── Helpers ───────────────────────────────────────────────────────
        void RefreshArmPill()
        {
            var app = Managers.AppManager.Instance;
            bool connected = app == null || app.ArmConnected;
            var mode = PacketProcessor.PacketProcessor.Instance?.ArmMode ?? ArmOperatingMode.Unknown;

            string statusText;
            Color  dotColor;

            if (!connected)
            {
                statusText = "Arm Disconnected";
                dotColor   = new Color(0.878f, 0.235f, 0.235f);
            }
            else if (mode == ArmOperatingMode.AutoReturnHome ||
                     mode == ArmOperatingMode.ManualHomeAssist)
            {
                statusText = "Arm Homing...";
                dotColor   = new Color(0.91f, 0.70f, 0.13f); // amber
            }
            else if (mode == ArmOperatingMode.AssistActive ||
                     mode == ArmOperatingMode.RestoreAssist)
            {
                statusText = "Arm Ready";
                dotColor   = new Color(0.478f, 0.667f, 0.431f);
            }
            else
            {
                statusText = "Arm Connected";
                dotColor   = new Color(0.478f, 0.667f, 0.431f);
            }

            if (_armStatusLabel != null) _armStatusLabel.text = statusText;
            if (_armDot != null)        _armDot.style.backgroundColor = dotColor;
        }

        void RefreshCurrentStep()
        {
            if (_currentStepIndex >= _tasks.Length)
            {
                // All steps complete — show a finished state in the task panel
                if (_currentStepLabel  != null) _currentStepLabel.text  = "All steps complete!";
                if (_currentStepDetail != null) _currentStepDetail.text = "Press Complete to finish the exercise.";
                if (_currentStepNum    != null) _currentStepNum.text    = "✓";
                return;
            }
            var (label, detail, _) = _tasks[_currentStepIndex];
            if (_currentStepLabel  != null) _currentStepLabel.text  = label;
            if (_currentStepDetail != null) _currentStepDetail.text = detail;
            if (_currentStepNum    != null) _currentStepNum.text    = (_currentStepIndex + 1).ToString();
        }

        void RefreshTaskCounter()
        {
            int done = 0;
            foreach (var (_, _, d) in _tasks) if (d) done++;
            if (_taskCounterLabel != null)
                _taskCounterLabel.text = $"{done} / {_tasks.Length}";
        }

        // ── Banner coroutine ──────────────────────────────────────────────
        IEnumerator ShowBannerCoroutine(string stepLabel, bool persistent = false)
        {
            if (_stepCompleteBanner == null) yield break;

            // Set text and make fully visible
            if (_stepBannerLabel != null)
                _stepBannerLabel.text = stepLabel;

            _stepCompleteBanner.style.display = DisplayStyle.Flex;
            _stepCompleteBanner.style.opacity = 1f;

            // If persistent (module complete), never fade
            if (persistent)
            {
                _bannerCoroutine = null;
                yield break;
            }

            // Hold
            yield return new WaitForSeconds(bannerHoldSeconds);

            // Fade out
            float elapsed = 0f;
            while (elapsed < bannerFadeSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / bannerFadeSeconds);
                _stepCompleteBanner.style.opacity = 1f - t;
                yield return null;
            }

            // Hide completely, reset opacity for next time
            _stepCompleteBanner.style.display = DisplayStyle.None;
            _stepCompleteBanner.style.opacity = 1f;
            _bannerCoroutine = null;
        }

        // ══════════════════════════════════════════════════════════════════
        //  PUBLIC API — call from hardware / simulation / objective scripts
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// THE MAIN OBJECTIVE HOOK.
        ///
        /// Call this whenever the trainee completes the current step.
        /// It will:
        ///   1. Mark the current task as done (green ✓ badge, strikethrough)
        ///   2. Show the center-top banner with the completed step name, then fade it out
        ///   3. Advance _currentStepIndex to the next incomplete step
        ///   4. Update the progress bar proportionally
        ///   5. Refresh the task counter, current-step box, and expanded list
        ///   6. On the LAST step, show a persistent "Module Complete!" banner
        ///
        /// Example — call from another MonoBehaviour:
        ///   GetComponent&lt;SimulatorHUDController&gt;().CompleteCurrentStep();
        ///   // or cache a reference in the Inspector and call hud.CompleteCurrentStep();
        /// </summary>
        public void CompleteCurrentStep()
        {
            if (_currentStepIndex >= _tasks.Length) return;

            // 1. Mark done
            var (label, detail, _) = _tasks[_currentStepIndex];
            _tasks[_currentStepIndex] = (label, detail, true);

            // 2. Check if this was the last step
            int doneCount = 0;
            foreach (var (_, _, d) in _tasks) if (d) doneCount++;
            bool allComplete = doneCount >= _tasks.Length;

            // 3. Show banner (interrupt previous one if still running)
            if (_bannerCoroutine != null) StopCoroutine(_bannerCoroutine);
            if (allComplete)
                _bannerCoroutine = StartCoroutine(ShowBannerCoroutine("✓ Module Complete!", persistent: true));
            else
                _bannerCoroutine = StartCoroutine(ShowBannerCoroutine(label));

            // 4. Advance index
            if (_currentStepIndex < _tasks.Length - 1)
                _currentStepIndex++;
            // else we've finished the last step; RefreshCurrentStep handles that

            // 5. Recalculate and push progress
            float pct = (float)doneCount / _tasks.Length * 100f;
            SetProgress(pct);

            // 6. Refresh all task-related UI
            RefreshCurrentStep();
            RefreshTaskCounter();
            PopulateTaskList();
        }

        /// <summary>Update the live accuracy display from your simulation logic.</summary>
        public void SetAccuracy(float pct)
        {
            if (_statAccuracyVal != null) _statAccuracyVal.text = $"{Mathf.RoundToInt(pct)}%";
        }

        /// <summary>Update the repetition counter.</summary>
        public void SetReps(int current, int total)
        {
            if (_statRepsVal != null) _statRepsVal.text = $"{current} / {total}";
        }

        /// <summary>Update the module progress bar (0-100).</summary>
        public void SetProgress(float pct)
        {
            pct = Mathf.Clamp(pct, 0f, 100f);
            if (_hudProgressFill   != null) _hudProgressFill.style.width = Length.Percent(pct);
            if (_progressPctLabel  != null) _progressPctLabel.text = $"{Mathf.RoundToInt(pct)}%";
        }

        /// <summary>
        /// Advance to the next step WITHOUT marking the current one done or showing the banner.
        /// Use CompleteCurrentStep() for the full objective-complete flow.
        /// This method is kept for backwards compatibility / manual scripting.
        /// </summary>
        public void AdvanceStep()
        {
            if (_currentStepIndex < _tasks.Length - 1)
            {
                _currentStepIndex++;
                RefreshCurrentStep();
                RefreshTaskCounter();
                PopulateTaskList();
            }
        }

        /// <summary>Mark the arm as connected or disconnected.</summary>
        public void SetArmConnected(bool connected)
        {
            var app = Managers.AppManager.Instance;
            if (app != null) app.SetArmConnected(connected);
            RefreshArmPill();
        }
    }
}
