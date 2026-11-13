using AICompanion.Core;
using AICompanion.Settings;
using AICompanion.UI;
using UnityEngine;

namespace AICompanion.NewUI
{
    /// <summary>
    /// Turns the new Happi views on and off with <see cref="NewUiSwitch"/> (the "New UI
    /// (preview)" setting or <c>-newui</c>). Installed on the app host like the settings panel.
    /// While the switch is off it does nothing else, so the app behaves as on main.
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
        }

        void TurnOff()
        {
            var view = GetComponent<HappiChatView>();
            if (view != null)
                view.Detach();
        }
    }
}
