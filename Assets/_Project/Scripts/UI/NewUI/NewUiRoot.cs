using AICompanion.Core;
using AICompanion.Settings;
using AICompanion.UI;
using UnityEngine;

namespace AICompanion.NewUI
{
    /// <summary>
    /// Installs the Happi views on the app host. The new UI is the app's UI; the switch is kept
    /// for compatibility with old settings and tests, but it always resolves on at startup.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NewUiRoot : MonoBehaviour
    {
        bool _applied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var hosts = FindObjectsByType<AppBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < hosts.Length; i++)
            {
                if (hosts[i] != null && hosts[i].GetComponent<NewUiRoot>() == null)
                    hosts[i].gameObject.AddComponent<NewUiRoot>();
            }
        }

        public bool Applied => _applied;

        void Update()
        {
            var want = NewUiSwitch.Active;
            if (want == _applied)
                return;

            var chat = GetComponent<ChatPanel>();
            var settings = GetComponent<SettingsPanel>();
            if (want && (chat == null || settings == null))
                return;

            _applied = want;
            if (want)
                TurnOn(chat, settings);
            else
                TurnOff();
        }

        void TurnOn(ChatPanel chat, SettingsPanel settings)
        {
            var view = GetComponent<HappiChatView>();
            if (view == null)
                view = gameObject.AddComponent<HappiChatView>();
            if (RibbonMenu.Enabled)
            {
                if (!TryGetComponent<RibbonAudio>(out var audio))
                    audio = gameObject.AddComponent<RibbonAudio>();
                audio.Attach(settings);
            }
            view.Attach(chat, settings);

            var wheel = GetComponent<Wheel.HappiWheelHost>();
            if (wheel == null)
                wheel = gameObject.AddComponent<Wheel.HappiWheelHost>();
            wheel.Attach(chat, settings);

            var settingsView = GetComponent<HappiSettingsView>();
            if (settingsView == null)
                settingsView = gameObject.AddComponent<HappiSettingsView>();
            settingsView.Attach(settings, chat);

            if (RibbonMenu.Enabled)
            {
                gameObject.AddComponent<RibbonMenu>().Attach(chat, settings);
                if (TryGetComponent<ExpressionPortraitPanel>(out var portrait))
                    portrait.RibbonCutouts = true;
            }

            // JUH-150: the Persona Studio window, opened from the Persona tab.
            var studio = GetComponent<PersonaStudioView>();
            if (studio == null)
                studio = gameObject.AddComponent<PersonaStudioView>();
            studio.Attach(settings);

            // JUH-122: floating relationship changes near her head.
            var floating = GetComponent<FloatingFeedbackView>();
            if (floating == null)
                floating = gameObject.AddComponent<FloatingFeedbackView>();
            floating.Attach(settings);

            // JUH-27: first-use consent for desktop tools.
            var consent = GetComponent<DesktopToolConsentView>();
            if (consent == null)
                consent = gameObject.AddComponent<DesktopToolConsentView>();
            consent.Attach(settings);
        }

        void TurnOff()
        {
            if (TryGetComponent<ExpressionPortraitPanel>(out var portrait))
                portrait.RibbonCutouts = false;

            if (TryGetComponent<RibbonAudio>(out var audio))
                audio.Detach();

            var ribbon = GetComponent<RibbonMenu>();
            if (ribbon != null)
                Destroy(ribbon);

            var consent = GetComponent<DesktopToolConsentView>();
            if (consent != null)
                consent.Detach();

            var floating = GetComponent<FloatingFeedbackView>();
            if (floating != null)
                floating.Detach();

            var studio = GetComponent<PersonaStudioView>();
            if (studio != null)
                studio.Detach();

            var settingsView = GetComponent<HappiSettingsView>();
            if (settingsView != null)
                settingsView.Detach();

            var wheel = GetComponent<Wheel.HappiWheelHost>();
            if (wheel != null)
                wheel.Detach();

            var view = GetComponent<HappiChatView>();
            if (view != null)
                view.Detach();
        }
    }
}
