using System.Collections.Generic;

namespace StriveMedical.Data
{
    // ── Enums ─────────────────────────────────────────────────────────────────

    public enum AppScreen
    {
        Login, Loading, Home, Dashboard, ModuleDetail,
        ExerciseWarning, ExerciseInProgress, LessonComplete,
        Settings, Notifications, EngineeringUI
    }

    public enum NotificationType { Success, Info, Alert }

    public enum DashboardTab { Modules, Performance, Leaderboard }

    public enum ExerciseWarningStep { Instruction, Confirm, Countdown }

    // ── Data models ───────────────────────────────────────────────────────────

    [System.Serializable]
    public class ExerciseDetail
    {
        public int    number;
        public string name;
        public string description;
        public string instructions;
        public int    passingScore;
        public int    reps;
        public string category;
    }

    [System.Serializable]
    public class ExerciseListItem
    {
        public int    id;
        public string name;
        public bool   completed;
        public string category;
    }

    [System.Serializable]
    public class ModuleData
    {
        public int    id;
        public string name;
        public string description;
        public int    progress;
        public ExerciseDetail currentExercise;
        public List<ExerciseListItem> exercises;
    }

    [System.Serializable]
    public class LeaderboardEntry
    {
        public int    rank;
        public string name;
        public int    score;
        public int    accuracy;
        public bool   isCurrentUser;
    }

    [System.Serializable]
    public class NotificationItem
    {
        public int              id;
        public NotificationType type;
        public string           title;
        public string           message;
        public string           time;
        public bool             read;
    }

    [System.Serializable]
    public class ModuleStatEntry
    {
        public string module;
        public string valueLabel;
        public int    progress;
    }

    // ── Module database ───────────────────────────────────────────────────────
    // Mirrors moduleDatabase in ModuleDetail.tsx

    public static class ModuleDatabase
    {
        public static readonly Dictionary<int, ModuleData> All =
            new Dictionary<int, ModuleData>
        {
            { 1, new ModuleData {
                id=1, name="Demo",
                description="Interactive demo showcasing pick-and-place dexterity and precision needle targeting.",
                progress=0,
                currentExercise = new ExerciseDetail {
                    number=1, name="Cube Placement",
                    description="Pick up the cube and place it inside the open box on the table.",
                    instructions="Grab the cube with the gripper and release it inside the box.",
                    passingScore=80, reps=1, category="Dexterity"
                },
                exercises = new List<ExerciseListItem> {
                    new ExerciseListItem { id=1, name="Cube Placement",   completed=false, category="Dexterity" },
                    new ExerciseListItem { id=2, name="Cube Retrieval",   completed=false, category="Dexterity" },
                    new ExerciseListItem { id=3, name="Needle Targeting", completed=false, category="Precision" },
                }
            }},
            { 2, new ModuleData {
                id=2, name="Stapling and Sealing",
                description="Develop proficiency in stapling techniques and vessel sealing for surgical applications.",
                progress=50,
                currentExercise = new ExerciseDetail {
                    number=23, name="Stapling",
                    description="Align tissue and deploy staples on the marked line with proper spacing and full closure.",
                    instructions="Complete every rep safely by avoiding penalties.",
                    passingScore=80, reps=10, category="Stapling Techniques"
                },
                exercises = new List<ExerciseListItem> {
                    new ExerciseListItem { id=1, name="Stapling",               completed=true,  category="Stapling Techniques" },
                    new ExerciseListItem { id=2, name="Vessel Sealing",         completed=true,  category="Sealing Techniques" },
                    new ExerciseListItem { id=3, name="Staple Line Integrity",  completed=false, category="Advanced Stapling" },
                    new ExerciseListItem { id=4, name="Seal and Divide",        completed=false, category="Advanced Sealing" },
                }
            }},
            { 3, new ModuleData {
                id=3, name="Camera and Instrument Control",
                description="Build coordination and control skills for camera operation and bimanual instrument manipulation.",
                progress=25,
                currentExercise = new ExerciseDetail {
                    number=30, name="Camera Centering",
                    description="Keep the target centered while maintaining a stable horizon and smooth motion.",
                    instructions="Complete every rep safely by avoiding penalties.",
                    passingScore=80, reps=10, category="Camera Control"
                },
                exercises = new List<ExerciseListItem> {
                    new ExerciseListItem { id=1, name="Camera Centering",           completed=true,  category="Camera Control" },
                    new ExerciseListItem { id=2, name="Clutch and Recenter",        completed=false, category="Camera Control" },
                    new ExerciseListItem { id=3, name="Bimanual Coordination",      completed=false, category="Instrument Control" },
                    new ExerciseListItem { id=4, name="Precision Grasp and Place",  completed=false, category="Instrument Control" },
                }
            }},
        };
    }

    // ── Leaderboard data ──────────────────────────────────────────────────────
    // Mirrors leaderboardData in Dashboard.tsx and LessonComplete.tsx

    public static class LeaderboardData
    {
        public static readonly List<LeaderboardEntry> Global = new List<LeaderboardEntry>
        {
            new LeaderboardEntry { rank=1, name="Alex Johnson", score=9850, accuracy=98 },
            new LeaderboardEntry { rank=2, name="Sarah Smith",  score=9720, accuracy=96 },
            new LeaderboardEntry { rank=3, name="You",          score=9580, accuracy=95, isCurrentUser=true },
            new LeaderboardEntry { rank=4, name="Mike Brown",   score=9440, accuracy=94 },
            new LeaderboardEntry { rank=5, name="Emily Davis",  score=9320, accuracy=93 },
        };
    }

    // ── Notifications data ────────────────────────────────────────────────────
    // Mirrors notifications array in Notifications.tsx

    public static class NotificationsData
    {
        public static readonly List<NotificationItem> All = new List<NotificationItem>
        {
            new NotificationItem { id=1, type=NotificationType.Success,
                title="Exercise Completed",
                message="Great job! You completed Shoulder Flexion with 95% accuracy.",
                time="2 hours ago", read=false },
            new NotificationItem { id=2, type=NotificationType.Info,
                title="New Module Available",
                message="Range of Motion Training module is now available.",
                time="5 hours ago", read=false },
            new NotificationItem { id=3, type=NotificationType.Alert,
                title="Reminder",
                message="Don't forget to complete your daily exercises.",
                time="1 day ago", read=true },
            new NotificationItem { id=4, type=NotificationType.Success,
                title="Leaderboard Update",
                message="You've moved up to rank #3 on the global leaderboard!",
                time="2 days ago", read=true },
            new NotificationItem { id=5, type=NotificationType.Info,
                title="System Update",
                message="New features have been added to improve your experience.",
                time="3 days ago", read=true },
        };
    }

    // ── Dashboard performance metrics ─────────────────────────────────────────
    // Mirrors moduleStats in Dashboard.tsx

    public static class PerformanceData
    {
        public static readonly List<(string label, string value, int progress, List<ModuleStatEntry> breakdown)> Metrics
            = new List<(string, string, int, List<ModuleStatEntry>)>
        {
            ("Accuracy Rate", "92%", 92, new List<ModuleStatEntry> {
                new ModuleStatEntry { module="Demo",           valueLabel="95%",  progress=95 },
                new ModuleStatEntry { module="Stapling and Sealing",          valueLabel="88%",  progress=88 },
                new ModuleStatEntry { module="Camera and Instrument Control", valueLabel="93%",  progress=93 },
            }),
            ("Completion Rate", "50%", 50, new List<ModuleStatEntry> {
                new ModuleStatEntry { module="Demo",           valueLabel="75%",  progress=75 },
                new ModuleStatEntry { module="Stapling and Sealing",          valueLabel="50%",  progress=50 },
                new ModuleStatEntry { module="Camera and Instrument Control", valueLabel="25%",  progress=25 },
            }),
            ("Avg Time per Exercise", "2:52", -1, null),
            ("Total Sessions",        "24",   -1, null),
            ("Range of Motion Progress", "+18°", 72, new List<ModuleStatEntry> {
                new ModuleStatEntry { module="Demo",           valueLabel="+22°", progress=88 },
                new ModuleStatEntry { module="Stapling and Sealing",          valueLabel="+18°", progress=72 },
                new ModuleStatEntry { module="Camera and Instrument Control", valueLabel="+14°", progress=56 },
            }),
            ("Strength Improvement", "+25%", 65, new List<ModuleStatEntry> {
                new ModuleStatEntry { module="Demo",           valueLabel="+32%", progress=80 },
                new ModuleStatEntry { module="Stapling and Sealing",          valueLabel="+25%", progress=65 },
                new ModuleStatEntry { module="Camera and Instrument Control", valueLabel="+18%", progress=50 },
            }),
            ("Consistency Streak",   "7 days", -1, null),
            ("Pain Level Reduction", "-3.2",    68, null),
        };
    }
}
