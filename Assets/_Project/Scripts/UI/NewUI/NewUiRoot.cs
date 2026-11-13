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
            view.Attach(chat, settings);

            var wheel = GetComponent<Wheel.HappiWheelHost>();
            if (wheel == null)
                wheel = gameObject.AddComponent<Wheel.HappiWheelHost>();
            wheel.Attach(chat, settings);

            var settingsView = GetComponent<HappiSettingsView>();
            if (settingsView == null)
                settingsView = gameObject.AddComponent<HappiSettingsView>();
            settingsView.Attach(settings, chat);
        }

        void TurnOff()
        {
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
