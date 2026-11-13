using System;
using System.Collections.Generic;
using AICompanion.Memory;

namespace AICompanion.Settings.Presenters
{
    /// <summary>The memory list as presenters see it. <see cref="MemoryStorePort"/> wraps the real store.</summary>
    public interface IMemoryPort
    {
        IReadOnlyList<MemoryEntry> Entries { get; }
        bool Delete(string id);
        int DeleteAll();
    }

    /// <summary>One remembered thing in a list.</summary>
    public readonly struct MemoryChoice
    {
        public readonly string Id;
        public readonly string Label;

        public MemoryChoice(string id, string label)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
        }
    }

    /// <summary>
    /// JUH-104 PR B: "what she remembers, with the option to forget something" (S-PR-09…11,
    /// wheel → Memories). Forgetting always takes two steps through <see cref="PrivacyConfirm"/>.
    /// </summary>
    public sealed class MemoryPresenter
    {
        readonly Func<IMemoryPort> _port;

        public MemoryPresenter(Func<IMemoryPort> port)
        {
            _port = port ?? (() => null);
            Confirm = new PrivacyConfirm();
        }

        public PrivacyConfirm Confirm { get; }

        public int Count
        {
            get
            {
                var port = _port();
                return port != null && port.Entries != null ? port.Entries.Count : 0;
            }
        }

        public string Summary
        {
            get
            {
                var port = _port();
                return MemoryViewerModel.Summary(port != null ? port.Entries : new MemoryEntry[0]);
            }
        }

        /// <summary>The first page of memories, newest first as the store orders them.</summary>
        public IReadOnlyList<MemoryChoice> Page(int max = MemoryViewerModel.DefaultPageSize)
        {
            var list = new List<MemoryChoice>();
            var port = _port();
            if (port == null || port.Entries == null)
                return list;
            var page = MemoryViewerModel.Page(port.Entries, max);
            for (var i = 0; i < page.Count; i++)
            {
                var entry = page[i];
                if (entry != null)
                    list.Add(new MemoryChoice(entry.id, MemoryViewerModel.Label(entry)));
            }

            return list;
        }

        public void AskForget(string id)
        {
            Confirm.Request(PrivacyConfirmKind.DeleteMemory, id);
        }

        public void AskForgetAll()
        {
            Confirm.Request(PrivacyConfirmKind.DeleteAllMemories);
        }

        /// <summary>The question for the pending request, or empty.</summary>
        public string PendingPrompt()
        {
            if (!Confirm.HasPending)
                return string.Empty;
            return PrivacyConfirm.Prompt(Confirm.Pending, Count, TextOf(Confirm.MemoryId));
        }

        /// <summary>Runs the pending forget. Returns true when something was deleted.</summary>
        public bool ConfirmPending()
        {
            string id;
            var kind = Confirm.Take(out id);
            var port = _port();
            if (port == null)
                return false;
            if (kind == PrivacyConfirmKind.DeleteMemory)
                return !string.IsNullOrEmpty(id) && port.Delete(id);
            if (kind == PrivacyConfirmKind.DeleteAllMemories)
                return port.DeleteAll() > 0;
            return false;
        }

        string TextOf(string id)
        {
            var port = _port();
            if (port == null || port.Entries == null || string.IsNullOrEmpty(id))
                return string.Empty;
            for (var i = 0; i < port.Entries.Count; i++)
            {
                var entry = port.Entries[i];
                if (entry != null && entry.id == id)
                    return entry.text;
            }

            return string.Empty;
        }
    }

    /// <summary>Adapter from the running <see cref="MemoryRuntime.Store"/>.</summary>
    public sealed class MemoryStorePort : IMemoryPort
    {
        public IReadOnlyList<MemoryEntry> Entries => MemoryRuntime.Store.Entries;
        public bool Delete(string id) => MemoryRuntime.Store.Delete(id);
        public int DeleteAll() => MemoryRuntime.Store.DeleteAll();
    }
}
