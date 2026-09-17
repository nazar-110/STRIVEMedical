using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using StriveMedical.Data;

namespace StriveMedical.UI
{
    // =========================================================================
    // LESSON COMPLETE CONTROLLER  ←  LessonComplete.tsx
    // =========================================================================
    public class LessonCompleteController : BaseController
    {
        VisualElement _homeCountdownPanel;
        Label _homeCountdownText;
        IVisualElementScheduledItem _homeCountdownItem;
        int _homeCountdownValue;

        public LessonCompleteController(VisualElement root) : base(root) { Init(); }

        protected override void Initialize()
        {
            InjectNotificationButton("notification-button-slot");
            InjectArmStatus("arm-status-slot");

            On("back-button",           () => App.NavigateTo(AppScreen.Dashboard));
            On("back-to-dashboard-btn", () => App.NavigateTo(AppScreen.Dashboard));

            SetLabel("accuracy-text", "95%");
            SetLabel("time-text",     "2:30");
            SetLabel("score-text",    "985");
            SetLabel("rank-text",     "#3");

            PopulateLeaderboard();
            StartReturnHomeCountdown();
        }

        void StartReturnHomeCountdown()
        {
            _homeCountdownValue = 3;

            _homeCountdownPanel = new VisualElement();
            _homeCountdownPanel.style.position = Position.Absolute;
            _homeCountdownPanel.style.top = 88;
            _homeCountdownPanel.style.left = 0;
            _homeCountdownPanel.style.right = 0;
            _homeCountdownPanel.style.alignItems = Align.Center;
            _homeCountdownPanel.pickingMode = PickingMode.Ignore;

            var pill = new VisualElement();
            pill.style.flexDirection = FlexDirection.Row;
            pill.style.alignItems = Align.Center;
            pill.style.backgroundColor = new Color(0.204f, 0.298f, 0.239f);
            pill.style.borderTopLeftRadius = 18;
            pill.style.borderTopRightRadius = 18;
            pill.style.borderBottomLeftRadius = 18;
            pill.style.borderBottomRightRadius = 18;
            pill.style.paddingLeft = 18;
            pill.style.paddingRight = 18;
            pill.style.paddingTop = 10;
            pill.style.paddingBottom = 10;

            _homeCountdownText = new Label();
            _homeCountdownText.style.fontSize = 14;
            _homeCountdownText.style.color = Color.white;
            _homeCountdownText.style.unityFontStyleAndWeight = FontStyle.Bold;
            pill.Add(_homeCountdownText);

            _homeCountdownPanel.Add(pill);
            Root.Add(_homeCountdownPanel);

            UpdateHomeCountdownText();
            _homeCountdownItem = _homeCountdownPanel.schedule.Execute(TickReturnHomeCountdown).StartingIn(1000).Every(1000);
        }

        void TickReturnHomeCountdown()
        {
            _homeCountdownValue--;

            if (_homeCountdownValue > 0)
            {
                UpdateHomeCountdownText();
                return;
            }

            _homeCountdownItem?.Pause();
            var processor = PacketProcessor.PacketProcessor.Instance;
            if (processor != null)
            {
                processor.SendStartHoming();
            }
            else
            {
                Debug.LogWarning("[LessonComplete] Could not send Start Homing: PacketProcessor instance is missing.");
            }

            if (_homeCountdownText != null)
                _homeCountdownText.text = "Moving arm home...";
        }

        void UpdateHomeCountdownText()
        {
            if (_homeCountdownText != null)
                _homeCountdownText.text = $"Returning arm home in {_homeCountdownValue}";
        }

        void PopulateLeaderboard()
        {
            var list = Root.Q<VisualElement>("leaderboard-list");
            if (list == null) return;
            list.Clear();
            foreach (var entry in LeaderboardData.Global)
                list.Add(DashboardController.BuildLeaderboardRow(entry));
        }

        void SetLabel(string name, string text)
        {
            var el = Root.Q<Label>(name);
            if (el != null) el.text = text;
        }
    }


    // =========================================================================
    // SETTINGS CONTROLLER  ←  Settings.tsx
    // =========================================================================
    public class SettingsController : BaseController
    {
        public SettingsController(VisualElement root) : base(root) { Init(); }

        protected override void Initialize()
        {
            InjectNotificationButton("notification-button-slot");
            InjectArmStatus("arm-status-slot");

            On("back-button", () => App.NavigateBack());

            var sensitivitySlider = Root.Q<Slider>("sensitivity-slider");
            var forceSlider       = Root.Q<Slider>("force-slider");
            var sensitivityLabel  = Root.Q<Label>("sensitivity-value-text");
            var forceLabel        = Root.Q<Label>("force-value-text");

            // Restore saved values
            if (sensitivitySlider != null) sensitivitySlider.value = App.MovementSensitivity;
            if (forceSlider       != null) forceSlider.value       = App.ForceFeedbackLevel;
            if (sensitivityLabel  != null) sensitivityLabel.text   = $"{Mathf.RoundToInt(App.MovementSensitivity * 100)}%";
            if (forceLabel        != null) forceLabel.text         = $"{Mathf.RoundToInt(App.ForceFeedbackLevel * 100)}%";

            // Live label update
            sensitivitySlider?.RegisterValueChangedCallback(e =>
            {
                if (sensitivityLabel != null)
                    sensitivityLabel.text = $"{Mathf.RoundToInt(e.newValue * 100)}%";
            });

            forceSlider?.RegisterValueChangedCallback(e =>
            {
                if (forceLabel != null)
                    forceLabel.text = $"{Mathf.RoundToInt(e.newValue * 100)}%";
            });

            // Preset buttons — use On() helper to avoid ?. += issue
            On("sens-low-btn",  () => { if (sensitivitySlider != null) sensitivitySlider.value = 0.25f; });
            On("sens-med-btn",  () => { if (sensitivitySlider != null) sensitivitySlider.value = 0.50f; });
            On("sens-high-btn", () => { if (sensitivitySlider != null) sensitivitySlider.value = 0.75f; });

            On("force-gentle-btn",   () => { if (forceSlider != null) forceSlider.value = 0.30f; });
            On("force-moderate-btn", () => { if (forceSlider != null) forceSlider.value = 0.60f; });
            On("force-strong-btn",   () => { if (forceSlider != null) forceSlider.value = 0.90f; });

            On("save-button", () =>
            {
                App.SetMovementSensitivity(sensitivitySlider?.value ?? 0.5f);
                App.SetForceFeedbackLevel(forceSlider?.value ?? 0.6f);
                App.SaveSettings();
            });

            On("engineering-btn", () => App.NavigateTo(AppScreen.EngineeringUI));
            On("logout-button",   () => App.NavigateTo(AppScreen.Login));
        }
    }


    // =========================================================================
    // NOTIFICATIONS CONTROLLER  ←  Notifications.tsx
    // =========================================================================
    public class NotificationsController : BaseController
    {
        public NotificationsController(VisualElement root) : base(root) { Init(); }

        protected override void Initialize()
        {
            On("back-button", () => App.NavigateBack());
            PopulateList();
        }

        void PopulateList()
        {
            var list = Root.Q<VisualElement>("notification-list");
            if (list == null) return;
            list.Clear();

            foreach (var item in NotificationsData.All)
                list.Add(BuildRow(item));
        }

        VisualElement BuildRow(NotificationItem item)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems    = Align.Center;
            Pad(row, 16);
            Radius(row, 8);
            row.style.marginBottom  = 8;
            row.style.backgroundColor = Color.white;

            // Unread = thicker left border in primary color
            if (!item.read)
            {
                row.style.borderLeftWidth  = 4;
                row.style.borderLeftColor  = new Color(0.204f, 0.298f, 0.239f);
                row.style.borderRightWidth = row.style.borderTopWidth = row.style.borderBottomWidth = 1;
                row.style.borderRightColor = row.style.borderTopColor = row.style.borderBottomColor
                    = new Color(0.9f, 0.9f, 0.9f);
            }
            else
            {
                Border(row, 1, new Color(0.9f, 0.9f, 0.9f));
            }

            // Icon circle
            string iconText = item.type switch
            {
                NotificationType.Success => "✓",
                NotificationType.Info    => "ℹ",
                NotificationType.Alert   => "⚠",
                _                        => "🔔",
            };
            Color iconColor = item.type switch
            {
                NotificationType.Success => new Color(0.13f, 0.77f, 0.37f),
                NotificationType.Info    => new Color(0.22f, 0.39f, 0.92f),
                NotificationType.Alert   => new Color(0.91f, 0.34f, 0.05f),
                _                        => new Color(0.5f, 0.5f, 0.5f),
            };

            var iconCircle = new VisualElement();
            iconCircle.style.width = iconCircle.style.height = 36;
            Radius(iconCircle, 18);
            iconCircle.style.backgroundColor = new Color(iconColor.r, iconColor.g, iconColor.b, 0.15f);
            iconCircle.style.alignItems      = Align.Center;
            iconCircle.style.justifyContent  = Justify.Center;
            iconCircle.style.marginRight     = 12;
            iconCircle.style.flexShrink      = 0;

            var iconLabel = new Label(iconText);
            iconLabel.style.fontSize = 16;
            iconLabel.style.color    = iconColor;
            iconCircle.Add(iconLabel);

            // Content
            var content = new VisualElement();
            content.style.flexGrow = 1;

            var titleLabel = new Label(item.title);
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = item.read ? FontStyle.Normal : FontStyle.Bold;
            titleLabel.style.marginBottom = 4;

            var msgLabel = new Label(item.message);
            msgLabel.style.fontSize   = 13;
            msgLabel.style.color      = new Color(0.42f, 0.45f, 0.50f);
            msgLabel.style.whiteSpace = WhiteSpace.Normal;

            content.Add(titleLabel);
            content.Add(msgLabel);

            // Time
            var timeLabel = new Label(item.time);
            timeLabel.style.fontSize   = 11;
            timeLabel.style.color      = new Color(0.60f, 0.63f, 0.67f);
            timeLabel.style.marginLeft = 12;
            timeLabel.style.flexShrink = 0;
            timeLabel.style.alignSelf  = Align.FlexStart;

            row.Add(iconCircle);
            row.Add(content);
            row.Add(timeLabel);
            return row;
        }
    }


    // =========================================================================
    // ENGINEERING UI CONTROLLER  ←  EngineeringUI.tsx
    // =========================================================================
    public class EngineeringUIController : BaseController
    {
        public EngineeringUIController(VisualElement root) : base(root) { Init(); }

        protected override void Initialize()
        {
            InjectNotificationButton("notification-button-slot");
            InjectArmStatus("arm-status-slot");

            On("back-button",          () => App.NavigateBack());
            On("back-to-settings-btn", () => App.NavigateBack());

            On("tab-diagnostics-btn", () => SetTab("diagnostics"));
            On("tab-calibration-btn", () => SetTab("calibration"));
            On("tab-logs-btn",        () => SetTab("logs"));

            PopulateEncoders();
            PopulateMotors();
            PopulateLogs();

            On("reset-torque-btn", () => Debug.Log("[EngineeringUI] Reset Torque Limits"));
            On("perf-test-btn",    () => Debug.Log("[EngineeringUI] Run Performance Test"));
            On("motor-cal-btn",    () => Debug.Log("[EngineeringUI] Motor Calibration"));
            On("sensor-cal-btn",   () => Debug.Log("[EngineeringUI] Sensor Calibration"));
            On("zero-pos-btn",     () => Debug.Log("[EngineeringUI] Zero Position"));
            On("clear-logs-btn",   ClearLogs);
            On("refresh-btn",      PopulateLogs);
            On("export-logs-btn",  () => Debug.Log("[EngineeringUI] Export Logs"));

            // Command Testing Bindings
            On("test-ping-btn",         () => PacketProcessor.PacketProcessor.Instance?.SendPing());
            On("test-reset-btn",        () => PacketProcessor.PacketProcessor.Instance?.SendReset());
            On("test-telem-btn",        () => PacketProcessor.PacketProcessor.Instance?.SendRequestTelem());
            On("test-homing-btn",       () => PacketProcessor.PacketProcessor.Instance?.SendStartHoming());
            On("test-confirm-home-btn", () => PacketProcessor.PacketProcessor.Instance?.SendConfirmHome());
            On("test-estop-btn",        () => PacketProcessor.PacketProcessor.Instance?.SendEStop());

            On("test-state-btn", () => {
                var payload = new Payload_SetODriveState { JointMask = 0x01, AxisState = 1 }; // 1 = IDLE state
                PacketProcessor.PacketProcessor.Instance?.SendSetODState(payload);
            });

            On("test-targets-btn", () => {
                var payload = new Payload_SetJointTargets(); // All zeros by default (safe)
                PacketProcessor.PacketProcessor.Instance?.SendSetJointTargets(payload);
            });

            On("test-param-btn", () => {
                var payload = new Payload_SetJointParameter { JointMask = 0x01, ParameterID = 0, Value = 0f };
                PacketProcessor.PacketProcessor.Instance?.SendSetJointParams(payload);
            });

            SetTab("diagnostics");
        }

        void PopulateEncoders()
        {
            var list = Root.Q<VisualElement>("encoder-list");
            if (list == null) return;
            list.Clear();

            var encoders = new (string name, int pos, float deg, int range)[]
            {
                ("Shoulder Encoder", 1024, 45.2f, 4096),
                ("Elbow Encoder",    2048, 90.0f, 4096),
                ("Wrist Encoder",    3072, 135.5f, 4096),
                ("Grip Encoder",     512,  22.5f,  2048),
            };

            foreach (var (name, pos, deg, range) in encoders)
            {
                var card = new VisualElement();
                card.style.backgroundColor = new Color(0.97f, 0.97f, 0.97f);
                Radius(card, 8);
                Pad(card, 12);
                card.style.marginBottom = 12;

                var topRow = new VisualElement();
                topRow.style.flexDirection  = FlexDirection.Row;
                topRow.style.justifyContent = Justify.SpaceBetween;
                topRow.style.alignItems     = Align.Center;
                topRow.style.marginBottom   = 8;

                var nameLabel = new Label(name);
                nameLabel.style.fontSize = 13;
                nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

                var degLabel = new Label($"{deg:F1}°");
                degLabel.style.fontSize = 18;
                degLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                degLabel.style.color = new Color(0.557f, 0.643f, 0.549f);

                topRow.Add(nameLabel); topRow.Add(degLabel);

                var infoRow = new VisualElement();
                infoRow.style.flexDirection  = FlexDirection.Row;
                infoRow.style.justifyContent = Justify.SpaceBetween;
                infoRow.style.marginBottom   = 6;

                var posLabel   = new Label($"Position: {pos}");
                posLabel.style.fontSize = 11;
                posLabel.style.color    = new Color(0.42f, 0.45f, 0.50f);
                var rangeLabel = new Label($"Range: 0-{range}");
                rangeLabel.style.fontSize = 11;
                rangeLabel.style.color    = new Color(0.42f, 0.45f, 0.50f);
                infoRow.Add(posLabel); infoRow.Add(rangeLabel);

                var track = new VisualElement();
                track.AddToClassList("progress-track");
                var fill = new VisualElement();
                fill.AddToClassList("progress-fill");
                fill.style.width = Length.Percent((float)pos / range * 100f);
                track.Add(fill);

                card.Add(topRow); card.Add(infoRow); card.Add(track);
                list.Add(card);
            }
        }

        void PopulateMotors()
        {
            var list = Root.Q<VisualElement>("motor-list");
            if (list == null) return;
            list.Clear();

            var motors = new (string name, float torque, float max, float current, int temp)[]
            {
                ("Motor 1 (Shoulder)", 8.2f, 15.0f, 1.2f, 42),
                ("Motor 2 (Elbow)",    5.4f, 12.0f, 0.9f, 38),
                ("Motor 3 (Wrist)",    3.8f,  8.0f, 1.1f, 40),
            };

            foreach (var (name, torque, max, current, temp) in motors)
            {
                var card = new VisualElement();
                card.style.backgroundColor = new Color(0.97f, 0.97f, 0.97f);
                Radius(card, 8);
                Pad(card, 12);
                card.style.marginBottom = 12;

                var topRow = new VisualElement();
                topRow.style.flexDirection  = FlexDirection.Row;
                topRow.style.justifyContent = Justify.SpaceBetween;
                topRow.style.alignItems     = Align.Center;
                topRow.style.marginBottom   = 8;

                var nameLabel = new Label(name);
                nameLabel.style.fontSize = 13;
                nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

                var right = new VisualElement();
                right.style.alignItems = Align.FlexEnd;
                var tLabel = new Label($"{torque} Nm");
                tLabel.style.fontSize = 18;
                tLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                tLabel.style.color = new Color(0.557f, 0.643f, 0.549f);
                var mLabel = new Label($"Max: {max} Nm");
                mLabel.style.fontSize = 11;
                mLabel.style.color    = new Color(0.5f, 0.5f, 0.5f);
                right.Add(tLabel); right.Add(mLabel);

                topRow.Add(nameLabel); topRow.Add(right);

                float pct   = torque / max;
                Color color = pct > 0.8f
                    ? new Color(0.937f, 0.267f, 0.267f)
                    : pct > 0.6f
                        ? new Color(0.97f, 0.60f, 0.13f)
                        : new Color(0.557f, 0.643f, 0.549f);

                var track = new VisualElement();
                track.AddToClassList("progress-track");
                track.style.marginBottom = 8;
                var fill = new VisualElement();
                fill.AddToClassList("progress-fill");
                fill.style.width           = Length.Percent(pct * 100f);
                fill.style.backgroundColor = color;
                track.Add(fill);

                var statsRow = new VisualElement();
                statsRow.style.flexDirection = FlexDirection.Row;
                var curLabel  = new Label($"Current: {current}A");
                curLabel.style.fontSize = 11;
                curLabel.style.flexGrow = 1;
                var tempLabel = new Label($"Temp: {temp}°C");
                tempLabel.style.fontSize = 11;
                statsRow.Add(curLabel); statsRow.Add(tempLabel);

                card.Add(topRow); card.Add(track); card.Add(statsRow);
                list.Add(card);
            }
        }

        readonly string[] _logLines =
        {
            "[2026-03-04 14:32:15] INFO: Device connected",
            "[2026-03-04 14:32:16] INFO: Firmware version: v2.4.1",
            "[2026-03-04 14:32:16] INFO: Initializing motors...",
            "[2026-03-04 14:32:17] INFO: Motor 1 initialized successfully",
            "[2026-03-04 14:32:17] INFO: Motor 2 initialized successfully",
            "[2026-03-04 14:32:18] INFO: Motor 3 initialized successfully",
            "[2026-03-04 14:32:18] INFO: Sensor calibration loaded",
            "[2026-03-04 14:32:19] INFO: System ready",
            "[2026-03-04 14:35:22] INFO: Exercise started: Shoulder Flexion",
            "[2026-03-04 14:37:45] INFO: Exercise completed: Shoulder Flexion",
            "[2026-03-04 14:37:46] INFO: Accuracy: 92%",
            "[2026-03-04 14:38:12] INFO: User navigated to Dashboard",
            "[2026-03-04 14:40:33] INFO: Settings updated: sensitivity=50, force=60",
            "[2026-03-04 14:42:10] INFO: Performance metrics calculated",
            "[2026-03-04 14:45:00] INFO: Heartbeat check: OK",
            "[2026-03-04 14:50:00] INFO: Heartbeat check: OK",
            "[2026-03-04 14:52:18] INFO: Engineering UI accessed",
        };

        void PopulateLogs()
        {
            var logList = Root.Q<VisualElement>("log-list");
            if (logList == null) return;
            logList.Clear();

            foreach (var line in _logLines)
            {
                var label = new Label(line);
                label.style.fontSize   = 11;
                label.style.color      = new Color(0.27f, 0.95f, 0.27f);
                label.style.marginBottom = 4;
                label.style.whiteSpace = WhiteSpace.Normal;
                logList.Add(label);
            }
        }

        void ClearLogs()
        {
            var logList = Root.Q<VisualElement>("log-list");
            if (logList != null) logList.Clear();
        }

        void SetTab(string tab)
        {
            void Show(string name, bool v)
            {
                var el = Root.Q<VisualElement>(name);
                if (el != null) el.style.display = v ? DisplayStyle.Flex : DisplayStyle.None;
            }

            Show("diagnostics-panel", tab == "diagnostics");
            Show("calibration-panel", tab == "calibration");
            Show("logs-panel",        tab == "logs");

            UpdateTabStyle("tab-diagnostics-btn", tab == "diagnostics");
            UpdateTabStyle("tab-calibration-btn", tab == "calibration");
            UpdateTabStyle("tab-logs-btn",        tab == "logs");
        }

        void UpdateTabStyle(string btnName, bool active)
        {
            var btn = Root.Q<Button>(btnName);
            if (btn == null) return;
            btn.style.color = active
                ? new Color(0.22f, 0.39f, 0.92f)
                : new Color(0.42f, 0.45f, 0.50f);
            btn.style.borderBottomWidth = active ? 2 : 0;
            btn.style.borderBottomColor = new Color(0.22f, 0.39f, 0.92f);
        }
    }
}
