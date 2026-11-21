using System;
using AICompanion.Core;

namespace AICompanion.Settings
{
    public sealed partial class SettingsPanel : IWebSearchContext
    {
        /// <summary>
        /// Permission-gated public web context for chat and the game companion. The Settings
        /// assembly owns the provider; callers only see the Core contract.
        /// </summary>
        public void Search(string query, Action<WebSearchContext> done)
        {
            WebSearchTool.Request(this, query, response =>
            {
                if (response == null || !response.Ok)
                {
                    done?.Invoke(new WebSearchContext
                    {
                        Ok = false,
                        Error = response != null ? response.Error : "Web search failed.",
                    });
                    return;
                }

                done?.Invoke(new WebSearchContext
                {
                    Ok = true,
                    Text = WebSearchTool.BuildContext(response),
                    Count = response.Results.Count,
                });
            });
        }
    }
}
