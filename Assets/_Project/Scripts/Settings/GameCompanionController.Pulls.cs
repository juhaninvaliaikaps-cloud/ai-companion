using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using AICompanion.GameCompanion;
using UnityEngine;
using UnityEngine.Networking;

namespace AICompanion.Settings
{
    /// <summary>
    /// JUH-148: her reactions to your pulls in Genshin Impact, Honkai: Star Rail and Zenless Zone
    /// Zero. While the game is running, Happi reads the history link the game cached when you
    /// last opened the pull history, then asks HoYoverse's own gacha log API for one banner's
    /// newest page every <see cref="PullPollSeconds"/> seconds, in turn. The link goes only to
    /// HoYoverse's host; it is never logged or saved.
    /// </summary>
    public sealed partial class GameCompanionController
    {
        public const float PullPollSeconds = 15f;
        public const int PullPageSize = 20;
        public const long MaxCacheBytes = 64L * 1024 * 1024;

        public const string PullLinkMissing = "To follow your pulls, open the pull history in the game once (it refreshes her link for about a day).";

        readonly Dictionary<string, GachaBannerTracker> _banners = new Dictionary<string, GachaBannerTracker>();
        GameTitle _pullGame = GameTitle.Unknown;
        string _pullLink;
        string _deadCacheStamp = string.Empty;
        int _bannerIndex;
        float _nextPullPoll;
        bool _pullBusy;

        /// <summary>True while Happi has a working history link for the open game.</summary>
        public bool FollowingPulls
        {
            get { return _pullLink != null && _pullGame != GameTitle.Unknown; }
        }

        void KeepPulls(CompanionSettings settings)
        {
            var game = GachaGame;
            if (!settings.GamePullHistory || !GachaHistoryLink.Supports(game))
            {
                if (_pullGame != GameTitle.Unknown)
                    ResetPulls(GameTitle.Unknown);
                return;
            }

            if (game != _pullGame)
                ResetPulls(game);
            if (_pullBusy || Time.unscaledTime < _nextPullPoll)
                return;
            _nextPullPoll = Time.unscaledTime + PullPollSeconds;
            _pullBusy = true;
            if (_pullLink == null)
                FindLinkInBackground(game);
            else
                StartCoroutine(PollBanner(game, _pullLink));
        }

        void ResetPulls(GameTitle game)
        {
            _pullGame = game;
            _pullLink = null;
            _banners.Clear();
            _bannerIndex = 0;
            _deadCacheStamp = string.Empty;
            _nextPullPoll = 0f;
        }

        void FindLinkInBackground(GameTitle game)
        {
            var localLow = LocalLowFolder();
            var dead = _deadCacheStamp;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                string stamp;
                var link = FindLink(game, localLow, out stamp);
                _main.Enqueue(() =>
                {
                    _pullBusy = false;
                    if (game != _pullGame)
                        return;
                    if (link == null || stamp == dead)
                    {
                        if (link == null)
                            Touch(PullLinkMissing);
                        return;
                    }

                    _pullLink = link;
                    _deadCacheStamp = stamp;
                    Touch("Following your pulls in " + CommentaryPrompt.GameName(game) + ".");
                    _nextPullPoll = 0f;
                });
            });
        }

        /// <summary>Off the main thread: the log, then the newest cache file with a link. Read only.</summary>
        static string FindLink(GameTitle game, string localLow, out string stamp)
        {
            stamp = string.Empty;
            try
            {
                string dataFolder = null;
                var logs = GachaCacheFiles.LogFiles(game, localLow);
                for (var i = 0; i < logs.Count && dataFolder == null; i++)
                {
                    if (File.Exists(logs[i]))
                        dataFolder = GachaCacheFiles.DataFolder(game, ReadShared(logs[i], 8L * 1024 * 1024));
                }

                if (dataFolder == null)
                    return null;
                var versions = new List<string>();
                var webCaches = Path.Combine(dataFolder, "webCaches");
                if (Directory.Exists(webCaches))
                {
                    foreach (var dir in Directory.GetDirectories(webCaches))
                        versions.Add(Path.GetFileName(dir));
                }

                var files = GachaCacheFiles.CacheFiles(dataFolder, versions);
                for (var i = 0; i < files.Count; i++)
                {
                    if (!File.Exists(files[i]))
                        continue;
                    var info = new FileInfo(files[i]);
                    var link = GachaHistoryLink.FindLatest(ReadShared(files[i], MaxCacheBytes), game);
                    if (link == null)
                        continue;
                    stamp = files[i] + "|" + info.LastWriteTimeUtc.Ticks + "|" + info.Length;
                    return link;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Game companion: couldn't read the pull history cache (" + ex.GetType().Name + ")");
            }

            return null;
        }

        /// <summary>Reads a file the game may have open, at most the last <paramref name="maxBytes"/>.</summary>
        static string ReadShared(string path, long maxBytes)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var length = stream.Length;
                if (length > maxBytes)
                    stream.Seek(length - maxBytes, SeekOrigin.Begin);
                var count = (int)Math.Min(length, maxBytes);
                var bytes = new byte[count];
                var read = 0;
                while (read < count)
                {
                    var n = stream.Read(bytes, read, count - read);
                    if (n <= 0)
                        break;
                    read += n;
                }

                return Encoding.UTF8.GetString(bytes, 0, read);
            }
        }

        static string LocalLowFolder()
        {
            var profile = Environment.GetEnvironmentVariable("USERPROFILE");
            return string.IsNullOrEmpty(profile) ? null : Path.Combine(Path.Combine(profile, "AppData"), "LocalLow");
        }

        IEnumerator PollBanner(GameTitle game, string link)
        {
            var banners = GachaHistoryLink.Banners(game);
            if (banners.Length == 0)
            {
                _pullBusy = false;
                yield break;
            }

            var banner = banners[_bannerIndex % banners.Length];
            _bannerIndex++;
            var url = GachaHistoryLink.PageUrl(game, link, banner, "0", PullPageSize);
            if (url == null || !GachaHistoryLink.IsHoyoHost(GachaHistoryLink.HostOf(url)))
            {
                _pullLink = null;
                _pullBusy = false;
                yield break;
            }

            string body = null;
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success && request.downloadHandler != null)
                    body = request.downloadHandler.text;
            }

            _pullBusy = false;
            if (!_alive || game != _pullGame || body == null)
                yield break;

            var page = GachaLogPage.Parse(body, game);
            if (page.RetCode == GachaLogPage.AuthkeyTimeout)
            {
                // Expired: wait for a newer link in the cache.
                _pullLink = null;
                Touch(PullLinkMissing);
                yield break;
            }

            if (page.RetCode == GachaLogPage.TooFrequent)
            {
                _nextPullPoll = Time.unscaledTime + PullPollSeconds * 4f;
                yield break;
            }

            if (!page.Ok)
            {
                Debug.Log("Game companion: gacha log answered " + page.RetCode);
                yield break;
            }

            GachaBannerTracker tracker;
            if (!_banners.TryGetValue(banner, out tracker))
            {
                tracker = new GachaBannerTracker();
                _banners[banner] = tracker;
            }

            Handle(tracker.Read(game, page, DateTime.UtcNow));
        }
    }
}
