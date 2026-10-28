using System.Collections.Generic;
using AICompanion.Memory;

namespace AICompanion.Settings
{
    /// <summary>
    /// The memory viewer on the Privacy tab. Kept in its own file so another agent
    /// editing <see cref="SettingsPanel"/> only merges around a single call site.
    /// </summary>
    public sealed partial class SettingsPanel
    {
        const int MemoryRows = MemoryViewerModel.DefaultPageSize;

        string _pendingMemoryDelete;
        bool _confirmMemoryClear;

        void BuildMemorySection()
        {
            var store = MemoryRuntime.Store;
            var entries = store.Entries;

            Note("Memories on this PC");
            Note(MemoryViewerModel.Summary(entries));
            Note("Stored under ProjectLeia, outside the repository. Memories made in private mode are never sent to a cloud model.");

            if (entries.Count == 0)
                return;

            if (_confirmMemoryClear)
            {
                Note("Delete all " + entries.Count + " memories? This cannot be undone.");
                AddButton("Confirm delete all", () =>
                {
                    store.DeleteAll();
                    _confirmMemoryClear = false;
                    _pendingMemoryDelete = null;
                    _rebuild = true;
                });
                AddButton("Cancel", () =>
                {
                    _confirmMemoryClear = false;
                    _rebuild = true;
                });
                return;
            }

            if (_pendingMemoryDelete != null)
            {
                var target = FindMemory(entries, _pendingMemoryDelete);
                Note(target != null ? "Delete \"" + target.text + "\"?" : "Delete this memory?");
                AddButton("Confirm delete", () =>
                {
                    store.Delete(_pendingMemoryDelete);
                    _pendingMemoryDelete = null;
                    _rebuild = true;
                });
                AddButton("Cancel", () =>
                {
                    _pendingMemoryDelete = null;
                    _rebuild = true;
                });
                return;
            }

            var page = MemoryViewerModel.Page(entries, MemoryRows);
            for (var i = 0; i < page.Count; i++)
            {
                var entry = page[i];
                AddButton(MemoryViewerModel.Label(entry), () =>
                {
                    _pendingMemoryDelete = entry.id;
                    _rebuild = true;
                });
            }

            if (entries.Count > page.Count)
                Note("Showing " + page.Count + " of " + entries.Count + ". Delete one to reveal more.");

            AddButton("Delete all memories", () =>
            {
                _confirmMemoryClear = true;
                _rebuild = true;
            });
        }

        static MemoryEntry FindMemory(IReadOnlyList<MemoryEntry> entries, string id)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].id == id)
                    return entries[i];
            }

            return null;
        }
    }
}
