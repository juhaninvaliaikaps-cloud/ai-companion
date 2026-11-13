using System;
using System.Collections.Generic;
using UnityEngine;

namespace AICompanion.AI
{
    /// <summary>
    /// One line per session when chat uses a non-loopback plain-HTTP custom endpoint.
    /// The line names the host only, never the path or the key.
    /// </summary>
    public static class PrivateEndpointNotice
    {
        static readonly HashSet<string> _logged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static void LogPlainHttpOnce(string baseUrl)
        {
            if (!PrivateEndpointRules.IsPlainHttpNonLocal(baseUrl))
                return;
            if (!Uri.TryCreate(baseUrl == null ? null : baseUrl.Trim(), UriKind.Absolute, out var uri))
                return;

            var host = uri.Host;
            if (string.IsNullOrEmpty(host) || !_logged.Add(host))
                return;

            Debug.Log("Private endpoint: chat uses plain HTTP to " + host + ". The key and chat can be read on the network.");
        }

        public static void Reset()
        {
            _logged.Clear();
        }
    }
}
