using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using BepInEx.Logging;

namespace LCRanked
{
    public class NetworkClient : IDisposable
    {
        private readonly ManualLogSource _log;
        private readonly HttpClient _httpClient;
        private readonly Uri _serverUri;
        private readonly Uri _fallbackServerUri;
        private CancellationTokenSource _cts;
        private bool _isConnected;
        private string _playerId;

        public readonly ConcurrentQueue<JObject> IncomingMessages = new ConcurrentQueue<JObject>();
        public bool IsConnected => _isConnected;
        public bool IsConnecting { get; private set; }
        public event Action Connected;

        public NetworkClient(string serverUrl, ManualLogSource log)
        {
            var normalizedServerUrl = NormalizeServerUrl(serverUrl);
            _serverUri = new Uri(normalizedServerUrl);
            _fallbackServerUri = BuildFallbackUri(_serverUri);
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            _log = log;
        }

        private static string NormalizeServerUrl(string serverUrl)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                return "http://127.0.0.1:8080";
            }

            var normalized = serverUrl.Trim();
            normalized = normalized.Replace("ws://", "http://", StringComparison.OrdinalIgnoreCase)
                .Replace("wss://", "https://", StringComparison.OrdinalIgnoreCase)
                .Replace("localhost", "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                .Replace("[::1]", "127.0.0.1", StringComparison.OrdinalIgnoreCase);

            if (!normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                normalized = "https://" + normalized;
            }

            return normalized.TrimEnd('/');
        }

        private static Uri BuildFallbackUri(Uri serverUri)
        {
            if (serverUri == null)
            {
                return null;
            }

            if (serverUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                serverUri.Host.Equals("::1", StringComparison.OrdinalIgnoreCase) ||
                serverUri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
            {
                return serverUri;
            }

            return null;
        }

        public void SetPlayerId(string playerId)
        {
            _playerId = playerId;
        }

        public void SetDisplayName(string playerId, string displayName)
        {
            _ = SendAsync(new { type = "set_display_name", playerId, displayName });
        }

        public async Task ConnectAsync()
        {
            if (IsConnected || IsConnecting)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            _isConnected = false;
            IsConnecting = true;
            try
            {
                await ConnectWithRetryAsync();
                _isConnected = true;
                _log.LogInfo($"[LCRanked] Connected to matchmaking server.");
                Connected?.Invoke();
                _ = ReceiveLoop(_cts.Token);
            }
            catch (Exception e)
            {
                _isConnected = false;
                _log.LogError($"[LCRanked] Failed to connect to matchmaking server: {e.Message}");
            }
            finally
            {
                IsConnecting = false;
            }
        }

        private async Task ConnectWithRetryAsync()
        {
            var probePaths = new[] { "/health", "/", "" };
            var candidateUris = GetCandidateUris();
            foreach (var uri in candidateUris)
            {
                if (uri == null)
                {
                    continue;
                }

                foreach (var probePath in probePaths)
                {
                    try
                    {
                        var probeUri = new Uri(uri, probePath);
                        using var response = await _httpClient.GetAsync(probeUri, _cts.Token);
                        if (response.IsSuccessStatusCode)
                        {
                            return;
                        }

                        _log.LogWarning($"[LCRanked] Probe failed for: {(int)response.StatusCode}");
                    }
                    catch (OperationCanceledException)
                    {
                        _log.LogWarning($"[LCRanked] Probe timed out");
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning($"[LCRanked] Probe failed: {ex.Message}");
                    }
                }
            }
        }

        private IEnumerable<Uri> GetCandidateUris()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var candidates = new List<Uri>();

            void AddIfNew(Uri uri)
            {
                if (uri == null)
                {
                    return;
                }

                var key = uri.ToString();
                if (seen.Contains(key))
                {
                    return;
                }

                seen.Add(key);
                candidates.Add(uri);
            }

            AddIfNew(_serverUri);
            AddIfNew(_fallbackServerUri);

            if (_serverUri != null)
            {
                var builder = new UriBuilder(_serverUri);
                if (builder.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
                {
                    builder.Scheme = Uri.UriSchemeHttps;
                    AddIfNew(builder.Uri);
                }
                else if (builder.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    builder.Scheme = Uri.UriSchemeHttp;
                    AddIfNew(builder.Uri);
                }

                if (builder.Port == 8080)
                {
                    builder.Port = -1;
                    AddIfNew(builder.Uri);
                }
            }

            return candidates;
        }

        private async Task ReceiveLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(_playerId))
                    {
                        var response = await _httpClient.GetAsync(new Uri(_serverUri, $"/api/messages/{Uri.EscapeDataString(_playerId)}"), token);
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            if (!string.IsNullOrWhiteSpace(content) && content != "[]")
                            {
                                var parsed = JArray.Parse(content);
                                foreach (var item in parsed)
                                {
                                    if (item is JObject obj)
                                    {
                                        IncomingMessages.Enqueue(obj);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _log.LogWarning($"[LCRanked] Polling failed: {ex.Message}");
                }

                await Task.Delay(500, token);
            }
        }

        public async Task SendAsync(object payload)
        {
            if (!IsConnected)
            {
                _log.LogWarning("[LCRanked] Tried to send while not connected.");
                return;
            }

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
            _log.LogInfo($"[LCRanked] Sending HTTP payload");
            try
            {
                var commandUri = new Uri(_serverUri, "/api/command");
                _log.LogInfo($"[LCRanked] Posting command");
                var response = await _httpClient.PostAsync(
                    commandUri,
                    new StringContent(json, Encoding.UTF8, "application/json"),
                    _cts.Token);

                var content = await response.Content.ReadAsStringAsync();
                _log.LogInfo($"[LCRanked] Command response from: {content}");
                if (!response.IsSuccessStatusCode)
                {
                    _log.LogError($"[LCRanked] HTTP command failed: {(int)response.StatusCode} {content}");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(content) && content != "null")
                {
                    try
                    {
                        var parsed = JToken.Parse(content);
                        if (parsed is JObject obj)
                        {
                            IncomingMessages.Enqueue(obj);
                        }
                        else if (parsed is JArray array)
                        {
                            foreach (var item in array)
                            {
                                if (item is JObject message)
                                {
                                    IncomingMessages.Enqueue(message);
                                }
                            }
                        }
                    }
                    catch (Exception parseEx)
                    {
                        _log.LogWarning($"[LCRanked] Failed to parse command response: {parseEx.Message}");
                    }
                }

                _log.LogInfo("[LCRanked] HTTP payload sent.");
            }
            catch (Exception e)
            {
                _log.LogError($"[LCRanked] Failed to send message: {e.Message}");
            }
        }

        public void JoinQueue(string playerId, string playerName, string deviceId, string mode = "solo_2p")
        {
            _ = SendAsync(new { type = "join_queue", playerId, playerName, deviceId, mode });
        }

        public void LeaveQueue(string playerId)
        {
            _ = SendAsync(new { type = "leave_queue", playerId });
        }

        public void ReportResult(string matchId, string playerId, int collectedValue, bool survived, float timeElapsedSeconds, bool aliveAt2pm = false, bool aliveAt9pm = false)
        {
            _ = SendAsync(new
            {
                type = "report_result",
                matchId,
                playerId,
                collectedValue,
                survived,
                timeElapsedSeconds,
                aliveAt2pm,
                aliveAt9pm
            });
        }

        public void Dispose()
        {
            try
            {
                _isConnected = false;
                IsConnecting = false;
                _cts?.Cancel();
                _httpClient?.Dispose();
            }
            catch { }
        }


        public async Task RequestPlayerStatsAsync(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId) || !IsConnected)
            {
                return;
            }

            try
            {
                var uri = new Uri(_serverUri, $"/api/stats/{Uri.EscapeDataString(playerId)}");
                var response = await _httpClient.GetAsync(uri, _cts?.Token ?? CancellationToken.None);
                if (!response.IsSuccessStatusCode)
                {
                    _log.LogWarning($"[LCRanked] Stats request failed: {(int)response.StatusCode}");
                    return;
                }

                var content = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(content) || content == "null")
                {
                    IncomingMessages.Enqueue(new JObject { ["type"] = "player_stats", ["playerId"] = playerId, ["noRecord"] = true });
                    return;
                }

                var stats = JObject.Parse(content);
                stats["type"] = "player_stats";
                IncomingMessages.Enqueue(stats);
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[LCRanked] Failed to fetch player stats: {ex.Message}");
            }
        }

        // ------------------------- LEADERBOARD STUFF -------------------------
        public async Task RequestLeaderboardAsync(int page, int limit)
        {
            if (!IsConnected) return;

            try
            {
                var uri = new Uri(_serverUri, $"/api/leaderboard?page={page}&limit={limit}");
                var response = await _httpClient.GetAsync(uri, _cts?.Token ?? CancellationToken.None);
                if (!response.IsSuccessStatusCode)
                {
                    _log.LogWarning($"[LCRanked] Leaderboard request failed: {(int)response.StatusCode}");
                    return;
                }

                var content = await response.Content.ReadAsStringAsync();
                var obj = JObject.Parse(content);
                obj["type"] = "leaderboard_page";
                IncomingMessages.Enqueue(obj);
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[LCRanked] Failed to fetch leaderboard: {ex.Message}");
            }
        }

        public void RequestLeaderboardPage(int page, int limit)
        {
            _ = RequestLeaderboardAsync(page, limit);
        }

        public async Task RequestProfileStatsAsync(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId) || !IsConnected) return;

            try
            {
                var uri = new Uri(_serverUri, $"/api/stats/{Uri.EscapeDataString(playerId)}");
                var response = await _httpClient.GetAsync(uri, _cts?.Token ?? CancellationToken.None);
                if (!response.IsSuccessStatusCode)
                {
                    _log.LogWarning($"[LCRanked] Profile stats request failed: {(int)response.StatusCode}");
                    return;
                }

                var content = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(content) || content == "null")
                {
                    IncomingMessages.Enqueue(new JObject { ["type"] = "profile_stats", ["playerId"] = playerId, ["noRecord"] = true });
                    return;
                }

                var stats = JObject.Parse(content);
                stats["type"] = "profile_stats";
                IncomingMessages.Enqueue(stats);
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[LCRanked] Failed to fetch profile stats: {ex.Message}");
            }
        }

        public async Task RequestProfileHistoryAsync(string playerId, int limit)
        {
            if (string.IsNullOrWhiteSpace(playerId) || !IsConnected) return;

            try
            {
                var uri = new Uri(_serverUri, $"/api/history/{Uri.EscapeDataString(playerId)}?limit={limit}");
                var response = await _httpClient.GetAsync(uri, _cts?.Token ?? CancellationToken.None);
                if (!response.IsSuccessStatusCode)
                {
                    _log.LogWarning($"[LCRanked] Profile history request failed: {(int)response.StatusCode}");
                    return;
                }

                var content = await response.Content.ReadAsStringAsync();
                var array = JArray.Parse(string.IsNullOrWhiteSpace(content) ? "[]" : content);
                IncomingMessages.Enqueue(new JObject { ["type"] = "profile_history", ["playerId"] = playerId, ["entries"] = array });
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[LCRanked] Failed to fetch profile history: {ex.Message}");
            }
        }

        public async Task RequestPlayerSearchAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || !IsConnected) return;

            try
            {
                var uri = new Uri(_serverUri, $"/api/search?name={Uri.EscapeDataString(query)}&limit=5");
                var response = await _httpClient.GetAsync(uri, _cts?.Token ?? CancellationToken.None);
                if (!response.IsSuccessStatusCode)
                {
                    _log.LogWarning($"[LCRanked] Player search failed: {(int)response.StatusCode}");
                    return;
                }

                var content = await response.Content.ReadAsStringAsync();
                var array = JArray.Parse(string.IsNullOrWhiteSpace(content) ? "[]" : content);
                IncomingMessages.Enqueue(new JObject { ["type"] = "profile_search_result", ["query"] = query, ["results"] = array });
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[LCRanked] Failed to search players: {ex.Message}");
            }
        }

        public void RequestProfile(string playerId, int historyLimit = 10)
        {
            _ = RequestProfileStatsAsync(playerId);
            _ = RequestProfileHistoryAsync(playerId, historyLimit);
        }

        public void SearchPlayer(string query)
        {
            _ = RequestPlayerSearchAsync(query);
        }
    }
}
