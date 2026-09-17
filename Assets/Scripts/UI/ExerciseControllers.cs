using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using StriveMedical.Data;

namespace StriveMedical.UI
{
    // =========================================================================
    // MODULE DETAIL CONTROLLER  ←  ModuleDetail.tsx
    // =========================================================================
    public class ModuleDetailController : BaseController
    {
        public ModuleDetailController(VisualElement root) : base(root) { Init(); }

        protected override void Initialize()
        {
            InjectNotificationButton("notification-button-slot");
            InjectArmStatus("arm-status-slot");

            On("back-button", () => App.NavigateTo(AppScreen.Dashboard));

            if (!ModuleDatabase.All.TryGetValue(App.ActiveModuleId, out ModuleData data)) return;

            SetLabel("module-name-text",    data.name);
            SetLabel("circular-progress-text", $"{data.progress}%");

            if (data.currentExercise != null)
            {
                var ex = data.currentExercise;
                SetLabel("exercise-number-text", $"Exercise {ex.number}");
                SetLabel("exercise-title-text",  ex.name);
                SetLabel("exercise-desc-text",   ex.description);
                SetLabel("exercise-instr-text",  ex.instructions);
                SetLabel("passing-score-text",   ex.passingScore.ToString());
                SetLabel("reps-text",            ex.reps.ToString());

                var startBtn = Root.Q<Button>("start-exercise-button");
                if (startBtn != null)
                    startBtn.RegisterCallback<ClickEvent>(_ =>
                        App.StartExercise(ex.name, data.id));
            }

            PopulateExerciseList(data);
            PopulateProgressTab(data);

            On("tab-exercises-btn", () => SetTab("exercises"));
            On("tab-progress-btn",  () => SetTab("progress"));
            SetTab("exercises");
        }

        void PopulateExerciseList(ModuleData data)
        {
            var list = Root.Q<VisualElement>("exercise-list");
            if (list == null) return;
            list.Clear();

            var byCategory = new Dictionary<string, List<ExerciseListItem>>();
            foreach (var ex in data.exercises)
            {
                if (!byCategory.ContainsKey(ex.category))
                    byCategory[ex.category] = new List<ExerciseListItem>();
                byCategory[ex.category].Add(ex);
            }

            foreach (var kvp in byCategory)
            {
                var header = new Label(kvp.Key);
                header.style.fontSize = 17;
                header.style.unityFontStyleAndWeight = FontStyle.Bold;
                header.style.marginBottom = 4;
                list.Add(header);

                int done = kvp.Value.FindAll(e => e.completed).Count;
                var sub  = new Label($"{done}/{kvp.Value.Count} exercises");
                sub.style.fontSize    = 13;
                sub.style.color       = new Color(0.42f, 0.45f, 0.50f);
                sub.style.marginBottom = 12;
                list.Add(sub);

                foreach (var ex in kvp.Value)
                {
                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.alignItems    = Align.Center;
                    Pad(row, 16);
                    Radius(row, 8);
                    row.style.marginBottom   = 8;
                    row.style.backgroundColor = ex.completed
                        ? new Color(0.85f, 0.92f, 1f)
                        : new Color(0.96f, 0.96f, 0.96f);
                    Border(row, 1, ex.completed
                        ? new Color(0.69f, 0.81f, 0.98f)
                        : new Color(0.82f, 0.84f, 0.87f));

                    var circle = new VisualElement();
                    circle.style.width = circle.style.height = 32;
                    Radius(circle, 16);
                    circle.style.backgroundColor = ex.completed
                        ? new Color(0.22f, 0.39f, 0.92f)
                        : new Color(0.22f, 0.26f, 0.33f);
                    circle.style.alignItems    = Align.Center;
                    circle.style.justifyContent = Justify.Center;
                    circle.style.marginRight   = 12;

                    var icon = new Label(ex.completed ? "✓" : "");
                    icon.style.color    = Color.white;
                    icon.style.fontSize = 14;
                    circle.Add(icon);

                    var nameLabel = new Label(ex.name);
                    nameLabel.style.fontSize = 14;
                    if (ex.completed)
                        nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

                    row.Add(circle);
                    row.Add(nameLabel);

                    ExerciseListItem captured = ex;
                    row.RegisterCallback<ClickEvent>(_ =>
                        App.StartExercise(captured.name, App.ActiveModuleId));

                    list.Add(row);
                }
            }
        }

        void PopulateProgressTab(ModuleData data)
        {
            int completed = 0;
            foreach (var e in data.exercises) if (e.completed) completed++;

            SetLabel("overall-progress-text",    $"{data.progress}%");
            SetLabel("exercises-completed-text", $"{completed}/{data.exercises.Count}");
            SetLabel("time-spent-text",  "12h 34m");
            SetLabel("avg-accuracy-text","89%");
            SetLabel("sessions-text",    "18");
            SetLabel("streak-text",      "7 days");

            var oFill = Root.Q<VisualElement>("overall-progress-fill");
            if (oFill != null) oFill.style.width = Length.Percent(data.progress);

            var eFill = Root.Q<VisualElement>("exercises-completed-fill");
            if (eFill != null && data.exercises.Count > 0)
                eFill.style.width = Length.Percent((float)completed / data.exercises.Count * 100f);

            var actList = Root.Q<VisualElement>("activity-list");
            if (actList != null)
            {
                actList.Clear();
                var activities = new (string, int, string, int)[]
                {
                    ("Today",      3, "45 min", 92), ("Yesterday",  5, "62 min", 88),
                    ("2 days ago", 2, "28 min", 95), ("3 days ago", 4, "51 min", 87),
                    ("4 days ago", 3, "39 min", 91),
                };
                foreach (var (date, count, time, acc) in activities)
                {
                    var row = new VisualElement();
                    row.style.flexDirection  = FlexDirection.Row;
                    row.style.justifyContent = Justify.SpaceBetween;
                    Pad(row, 12);
                    Radius(row, 8);
                    row.style.backgroundColor = new Color(0.97f, 0.97f, 0.97f);
                    row.style.marginBottom    = 8;

                    var left  = new VisualElement();
                    var dateL = new Label(date);
                    dateL.style.fontSize = 14;
                    dateL.style.unityFontStyleAndWeight = FontStyle.Bold;
                    var subL  = new Label($"{count} exercises • {time}");
                    subL.style.fontSize = 12;
                    subL.style.color    = new Color(0.42f, 0.45f, 0.50f);
                    left.Add(dateL); left.Add(subL);

                    var right  = new VisualElement();
                    right.style.alignItems = Align.FlexEnd;
                    var accL   = new Label($"{acc}%");
                    accL.style.fontSize = 13;
                    accL.style.unityFontStyleAndWeight = FontStyle.Bold;
                    var accSub = new Label("accuracy");
                    accSub.style.fontSize = 11;
                    accSub.style.color    = new Color(0.42f, 0.45f, 0.50f);
                    right.Add(accL); right.Add(accSub);

                    row.Add(left); row.Add(right);
                    actList.Add(row);
                }
            }

            var breakdownList = Root.Q<VisualElement>("breakdown-list");
            if (breakdownList != null)
            {
                breakdownList.Clear();
                foreach (var ex in data.exercises)
                {
                    var col = new VisualElement();
                    col.style.marginBottom = 12;

                    var row = new VisualElement();
                    row.style.flexDirection  = FlexDirection.Row;
                    row.style.justifyContent = Justify.SpaceBetween;
                    row.style.alignItems     = Align.Center;

                    var left = new VisualElement();
                    left.style.flexDirection = FlexDirection.Row;
                    left.style.alignItems    = Align.Center;

                    var check = new Label(ex.completed ? "✓" : "○");
                    check.style.fontSize    = 18;
                    check.style.color       = ex.completed
                        ? new Color(0.13f, 0.77f, 0.37f)
                        : new Color(0.7f, 0.7f, 0.7f);
                    check.style.marginRight = 8;

                    var exName = new Label(ex.name);
                    exName.style.fontSize = 14;
                    exName.style.unityFontStyleAndWeight = FontStyle.Bold;
                    left.Add(check); left.Add(exName);

                    var status = new Label(ex.completed ? "Completed" : "Not started");
                    status.style.fontSize = 13;
                    status.style.color    = new Color(0.42f, 0.45f, 0.50f);

                    row.Add(left); row.Add(status);
                    col.Add(row);

                    if (ex.completed)
                    {
                        var score = new Label("Best score: 94% • Average time: 3:45");
                        score.style.fontSize   = 12;
                        score.style.color      = new Color(0.42f, 0.45f, 0.50f);
                        score.style.marginLeft = 28;
                        col.Add(score);
                    }

                    breakdownList.Add(col);
                }
            }
        }

        void SetTab(string tab)
        {
            var ep = Root.Q<VisualElement>("exercises-panel");
            var pp = Root.Q<VisualElement>("progress-panel");
            if (ep != null) ep.style.display = tab == "exercises" ? DisplayStyle.Flex : DisplayStyle.None;
            if (pp != null) pp.style.display = tab == "progress"  ? DisplayStyle.Flex : DisplayStyle.None;

            UpdateTabBtn("tab-exercises-btn", tab == "exercises");
            UpdateTabBtn("tab-progress-btn",  tab == "progress");
        }

        void UpdateTabBtn(string btnName, bool active)
        {
            var btn = Root.Q<Button>(btnName);
            if (btn == null) return;
            if (active)
            {
                btn.style.backgroundColor = new UnityEngine.Color(0.145f, 0.388f, 0.922f); // blue
                btn.style.color           = UnityEngine.Color.white;
            }
            else
            {
                btn.style.backgroundColor = UnityEngine.Color.clear;
                btn.style.color           = new UnityEngine.Color(0.612f, 0.639f, 0.686f); // gray
            }
        }

        void SetLabel(string name, string text)
        {
            var el = Root.Q<Label>(name);
            if (el != null) el.text = text;
        }
    }


    // =========================================================================
    // EXERCISE WARNING CONTROLLER  ←  ExerciseWarning.tsx
    // =========================================================================
    public class ExerciseWarningController : BaseController
    {
        readonly ScreenRouter _router;

        public ExerciseWarningController(VisualElement root, ScreenRouter router) : base(root)
        {
            _router = router;
            Init();
        }

        protected override void Initialize()
        {
            InjectNotificationButton("notification-button-slot");
            InjectArmStatus("arm-status-slot");

            On("instr-next-btn",    () => SetStep("confirm"));
            On("instr-cancel-btn",  HandleCancel);
            On("confirm-homed-btn", StartCountdown);
            On("confirm-back-btn",  () => SetStep("instruction"));

            SetStep("instruction");
        }

        void SetStep(string step)
        {
            Show("instruction-panel", step == "instruction");
            Show("confirm-panel",     step == "confirm");
            Show("countdown-panel",   step == "countdown");
        }

        void Show(string name, bool visible)
        {
            var el = Root.Q<VisualElement>(name);
            if (el != null) el.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void StartCountdown()
        {
            PacketProcessor.PacketProcessor.Instance?.SendConfirmHome();
            SetStep("countdown");
            _router.StartCoroutineOnRouter(RunCountdown());
        }

        IEnumerator RunCountdown()
        {
            var countdownText = Root.Q<Label>("countdown-text");
            var startingText  = Root.Q<Label>("starting-text");

            if (startingText != null) startingText.style.display = DisplayStyle.None;
            if (countdownText != null) countdownText.style.display = DisplayStyle.Flex;

            for (int i = 3; i > 0; i--)
            {
                if (countdownText != null) countdownText.text = i.ToString();
                yield return new WaitForSeconds(1f);
            }

            if (countdownText != null) countdownText.style.display = DisplayStyle.None;
            if (startingText  != null) startingText .style.display = DisplayStyle.Flex;

            yield return new WaitForSeconds(0.5f);
            /// App.BeginExerciseInProgress(); Instead of opening a new page in the UI for the simulation, we are switching to a different scene
            UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");
        }

        void HandleCancel()
        {
            PacketProcessor.PacketProcessor.Instance?.SendStartHoming();
            _router.StopAllOnRouter();
            App.NavigateBack();
        }
    }


    // =========================================================================
    // EXERCISE IN PROGRESS CONTROLLER  ←  ExerciseInProgress.tsx
    // =========================================================================
    public class ExerciseInProgressController : BaseController
    {
        bool  _isPaused  = false;
        bool  _isZoomed  = false;
        bool  _showTasks = false;
        bool  _showInfo  = false;
        float _progress  = 0.45f;

        readonly (string text, bool completed)[] _tasks =
        {
            ("Position arm at starting position",     true),
            ("Slowly raise arm to 90 degrees",        true),
            ("Hold position for 5 seconds",           false),
            ("Lower arm slowly to starting position", false),
            ("Repeat 10 times",                       false),
        };

        readonly string[] _hints =
        {
            "Keep your elbow straight throughout the movement",
            "Move slowly and controlled - quality over speed",
            "Stop if you feel pain - discomfort is normal, pain is not",
            "Breathe steadily - don't hold your breath",
        };

        public ExerciseInProgressController(VisualElement root) : base(root) { Init(); }

        protected override void Initialize()
        {
            InjectNotificationButton("notification-button-slot");
            InjectArmStatus("arm-status-slot");

            SetLabel("exercise-name-text", App.ActiveExerciseName);
            SetLabel("module-label",       $"Module {App.ActiveModuleId}");
            RefreshProgress();

            On("pause-play-button", TogglePause);
            On("zoom-button",       ToggleZoom);
            On("task-list-button",  ToggleTasks);
            On("info-button",       ToggleInfo);
            On("tasks-close-btn",   () => SetPanels(false, false));
            On("info-close-btn",    () => SetPanels(false, false));
            On("exit-button",       () =>
            {
                PacketProcessor.PacketProcessor.Instance?.SendStartHoming();
                App.NavigateBack();
            });
            On("complete-button",   () => App.CompleteExercise());

            PopulateTasks();
            PopulateHints();
            RefreshPanels();
        }

        void TogglePause()
        {
            _isPaused = !_isPaused;
            var btn     = Root.Q<Button>("pause-play-button");
            if (btn != null) btn.text = _isPaused ? "▶" : "⏸";
            var overlay = Root.Q<VisualElement>("paused-overlay");
            if (overlay != null)
                overlay.style.display = _isPaused ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void ToggleZoom()
        {
            _isZoomed = !_isZoomed;
            var area  = Root.Q<VisualElement>("visualization-area");
            if (area != null) area.style.height = _isZoomed ? 600 : 400;
        }

        void ToggleTasks() => SetPanels(!_showTasks, false);
        void ToggleInfo()  => SetPanels(false, !_showInfo);

        void SetPanels(bool tasks, bool info)
        {
            _showTasks = tasks;
            _showInfo  = info;
            RefreshPanels();
        }

        void RefreshPanels()
        {
            void Show(string name, bool v)
            {
                var el = Root.Q<VisualElement>(name);
                if (el != null) el.style.display = v ? DisplayStyle.Flex : DisplayStyle.None;
            }
            Show("tasks-panel",   _showTasks);
            Show("info-panel",    _showInfo);
            Show("default-panel", !_showTasks && !_showInfo);
        }

        void RefreshProgress()
        {
            var fill = Root.Q<VisualElement>("progress-fill");
            if (fill != null) fill.style.width = Length.Percent(_progress * 100f);
            SetLabel("progress-text", $"Progress: {Mathf.RoundToInt(_progress * 100)}%");
        }

        void PopulateTasks()
        {
            var list = Root.Q<VisualElement>("task-list");
            if (list == null) return;
            list.Clear();

            foreach (var (text, completed) in _tasks)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems    = Align.Center;
                Pad(row, 12);
                Radius(row, 8);
                row.style.marginBottom   = 8;
                row.style.backgroundColor = completed
                    ? new Color(0.94f, 1f, 0.96f)
                    : new Color(0.97f, 0.97f, 0.97f);

                var icon = new Label(completed ? "✓" : "○");
                icon.style.fontSize    = 18;
                icon.style.color       = completed
                    ? new Color(0.13f, 0.77f, 0.37f)
                    : new Color(0.7f, 0.7f, 0.7f);
                icon.style.marginRight = 12;

                var lbl = new Label(completed ? $"<s>{text}</s>" : text);
                lbl.style.fontSize   = 13;
                lbl.style.whiteSpace = WhiteSpace.Normal;
                if (completed) lbl.style.color = new Color(0.5f, 0.5f, 0.5f);

                row.Add(icon); row.Add(lbl);
                list.Add(row);
            }
        }

        void PopulateHints()
        {
            var list = Root.Q<VisualElement>("hint-list");
            if (list == null) return;
            list.Clear();

            int i = 1;
            foreach (var hint in _hints)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                Pad(row, 12);
                Radius(row, 8);
                row.style.backgroundColor = new Color(0.93f, 0.96f, 1f);
                row.style.marginBottom    = 8;

                var circle = new VisualElement();
                circle.style.width = circle.style.height = 24;
                Radius(circle, 12);
                circle.style.backgroundColor = new Color(0.22f, 0.39f, 0.92f);
                circle.style.alignItems      = Align.Center;
                circle.style.justifyContent  = Justify.Center;
                circle.style.marginRight     = 12;
                circle.style.flexShrink      = 0;

                var num = new Label(i.ToString());
                num.style.fontSize = 11;
                num.style.color    = Color.white;
                num.style.unityFontStyleAndWeight = FontStyle.Bold;
                circle.Add(num);

                var hintLabel = new Label(hint);
                hintLabel.style.fontSize   = 13;
                hintLabel.style.color      = new Color(0.27f, 0.33f, 0.40f);
                hintLabel.style.whiteSpace = WhiteSpace.Normal;
                hintLabel.style.flexGrow   = 1;

                row.Add(circle); row.Add(hintLabel);
                list.Add(row);
                i++;
            }
        }

        void SetLabel(string name, string text)
        {
            var el = Root.Q<Label>(name);
            if (el != null) el.text = text;
        }

        /// <summary>Called from hardware layer to update live progress.</summary>
        public void SetProgress(float normalised)
        {
            _progress = Mathf.Clamp01(normalised);
            RefreshProgress();
        }
    }
}
