using System;

namespace AICompanion.Core
{
    /// <summary>Result of one permission-gated public web lookup.</summary>
    public struct WebSearchContext
    {
        public bool Ok;
        public string Error;
        public string Text;
        public int Count;
    }

    /// <summary>
    /// Host contract for web context. UI and game-companion code can ask for a search without
    /// referencing the Settings assembly; SettingsPanel implements the permission-gated tool.
    /// </summary>
    public interface IWebSearchContext
    {
        void Search(string query, Action<WebSearchContext> done);
    }
}
