using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using StriveMedical.Data;

namespace StriveMedical.UI
{
    // =========================================================================
    // DASHBOARD CONTROLLER  ←  Dashboard.tsx
    // =========================================================================
    public class DashboardController : BaseController
    {
        public DashboardController(VisualElement root) : base(root) { Init(); }

        protected override void Initialize()
        {
            InjectNotificationButton("notification-button-slot");
            InjectArmStatus("arm-status-slot");

            On("back-button", () => App.NavigateTo(AppScreen.Home));

            PopulateStatsCards();
            PopulateModulesList();
            PopulatePerformanceList();
            PopulateLeaderboard();
            SetTab("modules");

            On("tab-modules-btn",     () => SetTab("modules"));
            On("tab-performance-btn", () => SetTab("performance"));
            On("tab-leaderboard-btn", () => SetTab("leaderboard"));

            Root.Q<VisualElement>("progress-card")
                ?.RegisterCallback<ClickEvent>(_ => App.OpenModuleDetail(1));
        }

        // ── Stats cards ───────────────────────────────────────────────────────
        void PopulateStatsCards()
        {
            int total     = ModuleDatabase.All.Count;
            int completed = 0;

            var completedText = Root.Q<Label>("modules-completed-text");
            if (completedText != null) completedText.text = $"{completed}/{total}";

            var fill = Root.Q<VisualElement>("modules-progress-fill");
            if (fill != null)
                fill.style.width = Length.Percent(total > 0 ? (float)completed / total * 100f : 0f);
        }

        // ── Modules tab ───────────────────────────────────────────────────────
        void PopulateModulesList()
        {
            var list = Root.Q<VisualElement>("module-list");
            if (list == null) return;
            list.Clear();

            foreach (var kvp in ModuleDatabase.All)
                list.Add(BuildModuleRow(kvp.Value));
        }

        VisualElement BuildModuleRow(ModuleData data)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Column;
            Pad(row, 16);
            Radius(row, 8);
            row.style.marginBottom   = 12;
            row.style.backgroundColor = new Color(0.749f, 0.812f, 0.729f);

            var topRow = new VisualElement();
            topRow.style.flexDirection  = FlexDirection.Row;
            topRow.style.justifyContent = Justify.SpaceBetween;
            topRow.style.alignItems     = Align.Center;
            topRow.style.marginBottom   = 8;

            var nameLabel = new Label(data.name);
            nameLabel.style.fontSize = 14;

            var right = new VisualElement();
            right.style.flexDirection = FlexDirection.Row;
            right.style.alignItems    = Align.Center;

            var pct = new Label($"{data.progress}%");
            pct.style.fontSize    = 13;
            pct.style.marginRight = 8;

            var chevron = new Label("›");
            chevron.style.fontSize = 18;

            right.Add(pct);
            right.Add(chevron);
            topRow.Add(nameLabel);
            topRow.Add(right);

            var track = new VisualElement();
            track.AddToClassList("progress-track");
            var fill = new VisualElement();
            fill.AddToClassList("progress-fill");
            fill.style.width = Length.Percent(data.progress);
            track.Add(fill);

            row.Add(topRow);
            row.Add(track);

            int moduleId = data.id;
            row.RegisterCallback<ClickEvent>(_ => App.OpenModuleDetail(moduleId));
            return row;
        }

        // ── Performance tab ───────────────────────────────────────────────────
        void PopulatePerformanceList()
        {
            var list = Root.Q<VisualElement>("performance-list");
            if (list == null) return;
            list.Clear();

            foreach (var (label, value, progress, breakdown) in PerformanceData.Metrics)
                list.Add(BuildMetricRow(label, value, progress, breakdown));
        }

        VisualElement BuildMetricRow(string label, string value, int progress,
                                     List<ModuleStatEntry> breakdown)
        {
            var container = new VisualElement();
            container.style.marginBottom = 24;

            bool hasBreakdown = breakdown != null && breakdown.Count > 0;

            var topRow = new VisualElement();
            topRow.style.flexDirection  = FlexDirection.Row;
            topRow.style.justifyContent = Justify.SpaceBetween;
            topRow.style.alignItems     = Align.Center;

            var leftSide = new VisualElement();
            leftSide.style.flexGrow = 1;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems    = Align.Center;

            var labelEl = new Label(label);
            labelEl.style.fontSize = 15;
            labelEl.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerRow.Add(labelEl);

            Label chevron = null;
            if (hasBreakdown)
            {
                chevron      = new Label(" ›");
                chevron.style.fontSize = 18;
                headerRow.Add(chevron);
            }
            leftSide.Add(headerRow);

            var valueEl = new Label(value);
            valueEl.style.fontSize = 26;
            valueEl.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueEl.style.marginLeft = 16;

            topRow.Add(leftSide);
            topRow.Add(valueEl);
            container.Add(topRow);

            if (progress >= 0)
            {
                var track = new VisualElement();
                track.AddToClassList("progress-track");
                track.style.marginTop = 8;
                var fill = new VisualElement();
                fill.AddToClassList("progress-fill");
                fill.style.width = Length.Percent(progress);
                track.Add(fill);
                container.Add(track);
            }

            if (hasBreakdown)
            {
                var panel = new VisualElement();
                panel.style.display       = DisplayStyle.None;
                panel.style.marginLeft    = 16;
                panel.style.marginTop     = 12;
                Pad(panel, 16);
                Radius(panel, 8);
                panel.style.backgroundColor = new Color(0.97f, 0.97f, 0.97f);

                var title = new Label("By Training Module:");
                title.style.fontSize = 13;
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.color    = new Color(0.27f, 0.27f, 0.27f);
                title.style.marginBottom = 12;
                panel.Add(title);

                foreach (var stat in breakdown)
                {
                    var statRow = new VisualElement();
                    statRow.style.marginBottom = 12;

                    var statTop = new VisualElement();
                    statTop.style.flexDirection  = FlexDirection.Row;
                    statTop.style.justifyContent = Justify.SpaceBetween;
                    statTop.style.marginBottom   = 4;

                    var modName = new Label(stat.module);
                    modName.style.fontSize = 13;
                    var statVal = new Label(stat.valueLabel);
                    statVal.style.fontSize = 16;
                    statVal.style.unityFontStyleAndWeight = FontStyle.Bold;
                    statTop.Add(modName);
                    statTop.Add(statVal);

                    var track = new VisualElement();
                    track.AddToClassList("progress-track");
                    var fill = new VisualElement();
                    fill.AddToClassList("progress-fill");
                    fill.style.width = Length.Percent(stat.progress);
                    track.Add(fill);

                    statRow.Add(statTop);
                    statRow.Add(track);
                    panel.Add(statRow);
                }
                container.Add(panel);

                bool expanded = false;
                topRow.RegisterCallback<ClickEvent>(_ =>
                {
                    expanded = !expanded;
                    panel.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                    if (chevron != null) chevron.text = expanded ? " ↓" : " ›";
                });
            }

            return container;
        }

        // ── Leaderboard tab ───────────────────────────────────────────────────
        void PopulateLeaderboard()
        {
            var list = Root.Q<VisualElement>("leaderboard-list");
            if (list == null) return;
            list.Clear();
            foreach (var entry in LeaderboardData.Global)
                list.Add(BuildLeaderboardRow(entry));

            var me = LeaderboardData.Global.Find(e => e.isCurrentUser);
            if (me != null)
            {
                var r = Root.Q<Label>("user-rank-text");
                var s = Root.Q<Label>("user-score-text");
                var a = Root.Q<Label>("user-accuracy-text");
                if (r != null) r.text = $"#{me.rank}";
                if (s != null) s.text = $"{me.score:N0}";
                if (a != null) a.text = $"{me.accuracy}%";
            }
        }

        // Static so LessonCompleteController can reuse it
        public static VisualElement BuildLeaderboardRow(LeaderboardEntry entry)
        {
            var row = new VisualElement();
            row.style.flexDirection  = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems     = Align.Center;
            Pad(row, 12);
            Radius(row, 8);
            row.style.marginBottom   = 8;
            row.style.backgroundColor = entry.isCurrentUser
                ? new Color(0.9f, 0.95f, 0.9f)
                : Color.white;
            Border(row, entry.isCurrentUser ? 2 : 1,
                   entry.isCurrentUser
                       ? new Color(0.204f, 0.298f, 0.239f)
                       : new Color(0.9f, 0.9f, 0.9f));

            var left = new VisualElement();
            left.style.flexDirection = FlexDirection.Row;
            left.style.alignItems    = Align.Center;

            if (entry.rank <= 3)
            {
                var trophy = new Label("🏆");
                trophy.style.fontSize    = 16;
                trophy.style.marginRight = 8;
                left.Add(trophy);
            }

            var rankLabel = new Label($"#{entry.rank}");
            rankLabel.style.fontSize = 14;
            rankLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            rankLabel.style.marginRight = 12;
            rankLabel.style.width = 32;

            var nameLabel = new Label(entry.name);
            nameLabel.style.fontSize = 14;
            if (entry.isCurrentUser)
                nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

            left.Add(rankLabel);
            left.Add(nameLabel);

            var right = new VisualElement();
            right.style.alignItems = Align.FlexEnd;

            var scoreLabel = new Label($"{entry.score} pts");
            scoreLabel.style.fontSize = 13;
            scoreLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

            var accLabel = new Label($"{entry.accuracy}% accuracy");
            accLabel.style.fontSize = 12;
            accLabel.style.color    = new Color(0.42f, 0.45f, 0.50f);

            right.Add(scoreLabel);
            right.Add(accLabel);
            row.Add(left);
            row.Add(right);
            return row;
        }

        // ── Tab switching ─────────────────────────────────────────────────────
        void SetTab(string tab)
        {
            void Show(string name, bool visible)
            {
                var el = Root.Q<VisualElement>(name);
                if (el != null) el.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            Show("modules-panel",     tab == "modules");
            Show("performance-panel", tab == "performance");
            Show("leaderboard-panel", tab == "leaderboard");

            UpdateTabStyle("tab-modules-btn",     tab == "modules");
            UpdateTabStyle("tab-performance-btn", tab == "performance");
            UpdateTabStyle("tab-leaderboard-btn", tab == "leaderboard");
        }

        void UpdateTabStyle(string btnName, bool active)
        {
            var btn = Root.Q<Button>(btnName);
            if (btn == null) return;
            btn.style.backgroundColor = active ? Color.white : Color.clear;
            btn.style.color = active
                ? new Color(0.07f, 0.07f, 0.07f)
                : new Color(0.42f, 0.45f, 0.50f);
        }

        // Expose helpers for subclasses/other controllers
        protected new static void Pad(VisualElement el, float all)
        {
            el.style.paddingTop = el.style.paddingBottom =
            el.style.paddingLeft = el.style.paddingRight = all;
        }
        protected new static void Radius(VisualElement el, float r)
        {
            el.style.borderTopLeftRadius     = r;
            el.style.borderTopRightRadius    = r;
            el.style.borderBottomLeftRadius  = r;
            el.style.borderBottomRightRadius = r;
        }
        protected new static void Border(VisualElement el, float w, Color c)
        {
            el.style.borderTopWidth    = el.style.borderBottomWidth =
            el.style.borderLeftWidth   = el.style.borderRightWidth = w;
            el.style.borderTopColor    = el.style.borderBottomColor =
            el.style.borderLeftColor   = el.style.borderRightColor = c;
        }
    }
}
