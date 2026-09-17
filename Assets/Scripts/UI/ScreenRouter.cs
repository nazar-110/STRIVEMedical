using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using StriveMedical.Data;

namespace StriveMedical.UI
{
    public class ScreenRouter : MonoBehaviour
    {
        [Header("Drag each Screen UXML asset here")]
        [SerializeField] VisualTreeAsset loginScreenAsset;
        [SerializeField] VisualTreeAsset loadingScreenAsset;
        [SerializeField] VisualTreeAsset homeScreenAsset;
        [SerializeField] VisualTreeAsset dashboardScreenAsset;
        [SerializeField] VisualTreeAsset moduleDetailScreenAsset;
        [SerializeField] VisualTreeAsset exerciseWarningScreenAsset;
        [SerializeField] VisualTreeAsset exerciseInProgressScreenAsset;
        [SerializeField] VisualTreeAsset lessonCompleteScreenAsset;
        [SerializeField] VisualTreeAsset settingsScreenAsset;
        [SerializeField] VisualTreeAsset notificationsScreenAsset;
        [SerializeField] VisualTreeAsset engineeringUIScreenAsset;

        UIDocument _doc;
        VisualElement _root;

        // Keep a reference to the active exercise-in-progress controller
        // so hardware code can call SetProgress() on it
        public ExerciseInProgressController ActiveExerciseController { get; private set; }

        Dictionary<AppScreen, VisualTreeAsset> _assets;

        void Awake()
        {
            _doc  = GetComponent<UIDocument>();
            _root = _doc.rootVisualElement;

            _assets = new Dictionary<AppScreen, VisualTreeAsset>
            {
                { AppScreen.Login,              loginScreenAsset },
                { AppScreen.Loading,            loadingScreenAsset },
                { AppScreen.Home,               homeScreenAsset },
                { AppScreen.Dashboard,          dashboardScreenAsset },
                { AppScreen.ModuleDetail,       moduleDetailScreenAsset },
                { AppScreen.ExerciseWarning,    exerciseWarningScreenAsset },
                { AppScreen.ExerciseInProgress, exerciseInProgressScreenAsset },
                { AppScreen.LessonComplete,     lessonCompleteScreenAsset },
                { AppScreen.Settings,           settingsScreenAsset },
                { AppScreen.Notifications,      notificationsScreenAsset },
                { AppScreen.EngineeringUI,      engineeringUIScreenAsset },
            };
        }

        void OnEnable()
        {
            // AppManager may not exist yet during OnEnable (execution order).
            // We subscribe in Start instead; this guard handles re-enables after Start.
            if (Managers.AppManager.Instance != null)
                Managers.AppManager.Instance.OnScreenChanged += NavigateTo;
        }

        void OnDisable()
        {
            if (Managers.AppManager.Instance != null)
                Managers.AppManager.Instance.OnScreenChanged -= NavigateTo;
        }

        void Start()
        {
            if (Managers.AppManager.Instance == null)
            {
                Debug.LogError("[ScreenRouter] AppManager not found. Make sure an AppManager component exists in the scene and its script executes before ScreenRouter.");
                return;
            }
            // Subscribe here in case OnEnable fired before AppManager.Awake()
            Managers.AppManager.Instance.OnScreenChanged -= NavigateTo; // avoid double-subscribe
            Managers.AppManager.Instance.OnScreenChanged += NavigateTo;
            NavigateTo(Managers.AppManager.Instance.CurrentScreen);
        }

        void NavigateTo(AppScreen screen)
        {
            StopAllCoroutines();
            ActiveExerciseController = null;
            _root.Clear();

            if (!_assets.TryGetValue(screen, out var asset) || asset == null)
            {
                Debug.LogError($"[ScreenRouter] No UXML asset assigned for: {screen}");
                return;
            }

            asset.CloneTree(_root);

            switch (screen)
            {
                case AppScreen.Login:
                    new LoginController(_root);
                    break;
                case AppScreen.Loading:
                    new LoadingController(_root, this);
                    break;
                case AppScreen.Home:
                    new HomeController(_root);
                    break;
                case AppScreen.Dashboard:
                    new DashboardController(_root);
                    break;
                case AppScreen.ModuleDetail:
                    new ModuleDetailController(_root);
                    break;
                case AppScreen.ExerciseWarning:
                    new ExerciseWarningController(_root, this);
                    break;
                case AppScreen.ExerciseInProgress:
                    ActiveExerciseController = new ExerciseInProgressController(_root);
                    break;
                case AppScreen.LessonComplete:
                    new LessonCompleteController(_root);
                    break;
                case AppScreen.Settings:
                    new SettingsController(_root);
                    break;
                case AppScreen.Notifications:
                    new NotificationsController(_root);
                    break;
                case AppScreen.EngineeringUI:
                    new EngineeringUIController(_root);
                    break;
            }
        }

        public void StartCoroutineOnRouter(System.Collections.IEnumerator routine)
            => StartCoroutine(routine);

        public void StopAllOnRouter() => StopAllCoroutines();
    }
}
