using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using StriveMedical.Data;

namespace StriveMedical.UI
{
    // =========================================================================
    // BASE CONTROLLER — shared helpers used by every screen controller
    // =========================================================================
    public abstract class BaseController
    {
        protected VisualElement Root;
        protected Managers.AppManager App => Managers.AppManager.Instance;

        protected BaseController(VisualElement root)
        {
            Root = root;
            // Subclasses that need to assign fields before Initialize()
            // must call Initialize() themselves after base(root).
            // Simple subclasses (no extra fields) call Init() here via their
            // own constructor body, or use the static helper below.
        }

        // Call this from simple constructors: public Foo(VE r) : base(r) { Init(); }
        protected void Init() => Initialize();

        protected abstract void Initialize();

        // Query by name with a null warning
        protected T Q<T>(string name) where T : VisualElement
        {
            var el = Root.Q<T>(name);
            if (el == null) Debug.LogWarning($"[{GetType().Name}] Not found: {name}");
            return el;
        }

        // Wire a button click safely (avoids the ?. + += compile error)
        protected void On(string name, System.Action action)
        {
            var btn = Root.Q<Button>(name);
            if (btn != null) btn.RegisterCallback<ClickEvent>(_ => action());
        }

        // ── Style helpers ─────────────────────────────────────────────────────
        // UI Toolkit does not expose style.padding directly — set each side

        protected static void Pad(VisualElement el, float all)
        {
            el.style.paddingTop = el.style.paddingBottom =
            el.style.paddingLeft = el.style.paddingRight = all;
        }

        protected static void Pad(VisualElement el, float tb, float lr)
        {
            el.style.paddingTop = el.style.paddingBottom = tb;
            el.style.paddingLeft = el.style.paddingRight = lr;
        }

        protected static void Radius(VisualElement el, float r)
        {
            el.style.borderTopLeftRadius     = r;
            el.style.borderTopRightRadius    = r;
            el.style.borderBottomLeftRadius  = r;
            el.style.borderBottomRightRadius = r;
        }

        protected static void Border(VisualElement el, float w, Color c)
        {
            el.style.borderTopWidth    = el.style.borderBottomWidth =
            el.style.borderLeftWidth   = el.style.borderRightWidth = w;
            el.style.borderTopColor    = el.style.borderBottomColor =
            el.style.borderLeftColor   = el.style.borderRightColor = c;
        }

        // ── Shared top-bar components ─────────────────────────────────────────

        protected void InjectArmStatus(string slotName)
        {
            var slot = Root.Q<VisualElement>(slotName);
            if (slot == null) return;

            var pill = new VisualElement();
            pill.style.flexDirection  = FlexDirection.Row;
            pill.style.alignItems     = Align.Center;
            pill.style.backgroundColor = Color.white;
            Radius(pill, 8);
            pill.style.paddingLeft  = pill.style.paddingRight  = 12;
            pill.style.paddingTop   = pill.style.paddingBottom = 8;
            Border(pill, 1, new Color(0.9f, 0.9f, 0.9f));

            var dot = new VisualElement();
            dot.style.width = dot.style.height = 12;
            Radius(dot, 6);
            dot.style.marginRight = 8;

            var lbl = new Label();
            lbl.style.fontSize = 13;

            void Refresh()
            {
                bool armConnected = USB.USBManager.IsConnected;
                ArmOperatingMode armMode = PacketProcessor.PacketProcessor.Instance?.ArmMode
                                           ?? ArmOperatingMode.Unknown;

                if (!armConnected)
                {
                    dot.style.backgroundColor = new Color(0.937f, 0.267f, 0.267f);
                    lbl.text = "Arm Disconnected";
                }
                else if (armMode == ArmOperatingMode.AutoReturnHome ||
                         armMode == ArmOperatingMode.ManualHomeAssist)
                {
                    dot.style.backgroundColor = new Color(0.91f, 0.70f, 0.13f); // amber
                    lbl.text = "Arm Homing";
                }
                else if (armMode == ArmOperatingMode.AssistActive ||
                         armMode == ArmOperatingMode.RestoreAssist)
                {
                    dot.style.backgroundColor = new Color(0.204f, 0.298f, 0.239f);
                    lbl.text = "Arm Ready";
                }
                else
                {
                    dot.style.backgroundColor = new Color(0.204f, 0.298f, 0.239f);
                    lbl.text = "Arm Connected";
                }
            }

            Refresh();
            App.OnArmConnectionChanged += _ => Refresh();

            var pp = PacketProcessor.PacketProcessor.Instance;
            if (pp != null)
                pp.OnArmModeChanged += _ => Refresh();

            pill.RegisterCallback<ClickEvent>(_ =>
            {
                if (USB.USBManager.IsConnected)
                {
                    Refresh();
                    return;
                }

                var usbManager = USB.USBManager.Instance ?? Object.FindFirstObjectByType<USB.USBManager>();
                if (usbManager == null)
                {
                    Debug.LogWarning("Arm status pill clicked, but no USBManager was found in the scene.");
                    return;
                }

                Debug.Log("Arm status pill clicked while disconnected. Attempting USB reconnect.");
                usbManager.ConnectToTeensy();
                Refresh();
            });

            pill.Add(dot);
            pill.Add(lbl);
            slot.Add(pill);
        }

        protected void InjectNotificationButton(string slotName)
        {
            var slot = Root.Q<VisualElement>(slotName);
            if (slot == null) return;

            int unread = 0;
            foreach (var n in NotificationsData.All) if (!n.read) unread++;

            var btn = new Button();
            btn.text = unread > 0 ? $"🔔 {unread}" : "🔔";
            btn.style.fontSize        = 20;
            btn.style.width = btn.style.height = 40;
            btn.style.backgroundColor = Color.clear;
            Border(btn, 0, Color.clear);
            btn.RegisterCallback<ClickEvent>(_ => App.NavigateTo(AppScreen.Notifications));
            slot.Add(btn);
        }
    }


    // =========================================================================
    // LOGIN CONTROLLER  ←  Login.tsx
    // =========================================================================
    public class LoginController : BaseController
    {
        public LoginController(VisualElement root) : base(root) { Init(); }

        protected override void Initialize()
        {
            var usernameInput = Q<TextField>("username-input");
            var passwordInput = Q<TextField>("password-input");
            var loginBtn      = Q<Button>("login-button");
            var errorLabel    = Q<Label>("error-label");

            if (loginBtn != null)
                loginBtn.RegisterCallback<ClickEvent>(_ =>
                {
                    string user = usernameInput?.value?.Trim() ?? "";
                    string pass = passwordInput?.value ?? "";

                    if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
                    {
                        if (errorLabel != null)
                        {
                            errorLabel.text = "Please enter your username/email and password.";
                            errorLabel.style.display = DisplayStyle.Flex;
                        }
                        return;
                    }
                    App.NavigateTo(AppScreen.Loading);
                });

            usernameInput?.RegisterValueChangedCallback(_ =>
            {
                if (errorLabel != null) errorLabel.style.display = DisplayStyle.None;
            });
        }
    }


    // =========================================================================
    // LOADING CONTROLLER  ←  LoadingScreen.tsx
    // =========================================================================
    public class LoadingController : BaseController
    {
        readonly ScreenRouter _router;

        public LoadingController(VisualElement root, ScreenRouter router) : base(root)
        {
            _router = router;
            Init();
        }

        protected override void Initialize()
        {
            var logo    = Root.Q<VisualElement>("logo-image");
            var spinner = Root.Q<VisualElement>("spinner-container");

            if (logo    != null) logo.style.opacity    = 0;
            if (spinner != null) spinner.style.opacity = 0;

            _router.StartCoroutineOnRouter(RunAnimation(logo, spinner));
        }

        IEnumerator RunAnimation(VisualElement logo, VisualElement spinner)
        {
            // Phase 1: logo fades in over 1.5s (easeOut)
            float elapsed = 0f;
            while (elapsed < 1.5f)
            {
                float eased = 1f - Mathf.Pow(1f - elapsed / 1.5f, 3f);
                if (logo != null) logo.style.opacity = eased;
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (logo != null) logo.style.opacity = 1;

            // Phase 2: spinner fades in after 0.5s
            yield return new WaitForSeconds(0.5f);
            float fade = 0f;
            while (fade < 0.5f)
            {
                if (spinner != null) spinner.style.opacity = fade / 0.5f;
                fade += Time.deltaTime;
                yield return null;
            }
            if (spinner != null) spinner.style.opacity = 1;

            // Phase 3: spin while waiting
            float angle  = 0f;
            float waited = 0f;
            var   spinEl = Root.Q<VisualElement>("spinner");

            while (waited < 1f)
            {
                angle += 360f * Time.deltaTime;
                if (spinEl != null)
                    spinEl.style.rotate = new StyleRotate(
                        new Rotate(new Angle(angle, AngleUnit.Degree)));
                waited += Time.deltaTime;
                yield return null;
            }

            App.NavigateTo(AppScreen.Home);
        }
    }


    // =========================================================================
    // HOME CONTROLLER  ←  Home.tsx
    // =========================================================================
    public class HomeController : BaseController
    {
        public HomeController(VisualElement root) : base(root) { Init(); }

        protected override void Initialize()
        {
            InjectNotificationButton("notification-button-slot");
            InjectArmStatus("arm-status-slot");

            On("Quit-button",     QuitHandler.RequestQuit);
            On("start-button",    () => App.NavigateTo(AppScreen.Dashboard));
            On("settings-button", () => App.NavigateTo(AppScreen.Settings));
            On("logout-button",   () => App.NavigateTo(AppScreen.Login));

            var startBtn = Root.Q<Button>("start-button");
            if (startBtn != null) startBtn.style.backgroundColor = App.PrimaryColor;
        }
    }
}
