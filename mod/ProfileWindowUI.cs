using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LCRanked.UI
{
    public class MyProfileMenuLink : MonoBehaviour
    {
        public static MyProfileMenuLink Instance;

        public static void Create()
        {
            if (Instance != null)
            {
                Instance.gameObject.SetActive(true);
                return;
            }

            var canvasObj = new GameObject("LCRankedMyProfileLink");
            DontDestroyOnLoad(canvasObj);

            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4000;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();

            Instance = canvasObj.AddComponent<MyProfileMenuLink>();
            Instance.BuildUI(canvas.transform);
        }

        public static void Hide()
        {
            if (Instance != null) Instance.gameObject.SetActive(false);
        }

        public static void Show()
        {
            if (Instance != null) Instance.gameObject.SetActive(true);
        }

        private void BuildUI(Transform parent)
        {
            var buttonObj = new GameObject("MyProfileLink");
            buttonObj.transform.SetParent(parent, false);
            var rect = buttonObj.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(220, 36);
            rect.anchoredPosition = new Vector2(-40, -900);

            var button = buttonObj.AddComponent<Button>();
            var bg = buttonObj.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0);
            button.targetGraphic = bg;

            var textObj = new GameObject("Label");
            textObj.transform.SetParent(buttonObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = "My Profile <";
            text.fontSize = 20;
            text.color = new Color(0.85f, 0.45f, 0.35f, 1f);
            text.alignment = TextAlignmentOptions.Right;

            button.onClick.AddListener(() =>
            {
                var plugin = Plugin.Instance;
                if (plugin == null) return;
                ProfileWindowUI.Create(plugin.LocalPlayerId);
            });
        }
    }

    public class ProfileWindowUI : MonoBehaviour
    {
        public static ProfileWindowUI Instance;

        private string currentPlayerId;
        private TMP_InputField searchField;
        private TextMeshProUGUI usernameLabel;
        private TextMeshProUGUI rankLabel;
        private TextMeshProUGUI rankingLabel;
        private TextMeshProUGUI eloLabel, winsLabel, lossesLabel, avgCollectedLabel;
        private TextMeshProUGUI winrateLabel, winstreakLabel, highestCollectedLabel;
        private TextMeshProUGUI totalMatchesLabel, totalPlaytimeLabel;
        private Transform historyContainer;
        private readonly List<GameObject> historyRowObjects = new List<GameObject>();

        private static readonly Color PanelBg = new Color(0.15f, 0.03f, 0.03f, 0.97f);
        private static readonly Color PanelBorder = new Color(0.35f, 0.08f, 0.08f, 1f);
        private static readonly Color RowBg = new Color(0.32f, 0.10f, 0.06f, 1f);
        private static readonly Color RowBorder = new Color(0.75f, 0.30f, 0.15f, 1f);
        private static readonly Color TextColor = new Color(0.92f, 0.55f, 0.40f, 1f);
        private static readonly Color InputBg = new Color(0.75f, 0.36f, 0.20f, 1f);
        private static readonly Color InputText = new Color(0.20f, 0.05f, 0.02f, 1f);

        public static void Create(string playerId)
        {
            if (Instance == null)
            {
                var canvasObj = new GameObject("LCRankedProfileWindow");
                DontDestroyOnLoad(canvasObj);

                var canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 8000;

                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                canvasObj.AddComponent<GraphicRaycaster>();

                Instance = canvasObj.AddComponent<ProfileWindowUI>();
                Instance.BuildUI(canvas.transform);
            }

            Instance.LoadProfile(playerId);
        }

        public static void Remove()
        {
            if (Instance == null) return;
            Destroy(Instance.gameObject);
            Instance = null;
        }

        private void LoadProfile(string playerId)
        {
            currentPlayerId = playerId;
            var plugin = Plugin.Instance;
            if (plugin?.Network == null || !plugin.Network.IsConnected) return;
            plugin.Network.RequestProfile(playerId, 10);
        }

        private void BuildUI(Transform parent)
        {
            var backdropObj = new GameObject("Backdrop");
            backdropObj.transform.SetParent(parent, false);
            var backdropRect = backdropObj.AddComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            var backdropImage = backdropObj.AddComponent<Image>();
            backdropImage.color = new Color(0f, 0f, 0f, 0.6f);

            var borderObj = new GameObject("PanelBorder");
            borderObj.transform.SetParent(parent, false);
            var borderRect = borderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = borderRect.anchorMax = new Vector2(0.5f, 0.5f);
            borderRect.sizeDelta = new Vector2(1010, 780);
            var borderImage = borderObj.AddComponent<Image>();
            borderImage.color = PanelBorder;

            var panelObj = new GameObject("Panel");
            panelObj.transform.SetParent(parent, false);
            var panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1000, 770);
            var panelImage = panelObj.AddComponent<Image>();
            panelImage.color = PanelBg;

            var mainLayout = panelObj.AddComponent<VerticalLayoutGroup>();
            mainLayout.padding = new RectOffset(24, 24, 20, 20);
            mainLayout.spacing = 12;
            mainLayout.childAlignment = TextAnchor.UpperCenter;
            mainLayout.childControlWidth = true;
            mainLayout.childControlHeight = true;
            mainLayout.childForceExpandWidth = true;
            mainLayout.childForceExpandHeight = false;

            BuildHeaderRow(panelObj.transform);
            BuildSectionHeader(panelObj.transform, "Ranked Statistics");
            BuildStatsGrid(panelObj.transform);
            BuildHistoryList(panelObj.transform);
            BuildFooterRow(panelObj.transform);
        }

        private void BuildHeaderRow(Transform parent)
        {
            var rowObj = new GameObject("HeaderRow");
            rowObj.transform.SetParent(parent, false);
            var rowLayoutElement = rowObj.AddComponent<LayoutElement>();
            rowLayoutElement.minHeight = 48;
            rowLayoutElement.preferredHeight = 48;
            rowLayoutElement.flexibleHeight = 0;

            var layout = rowObj.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            usernameLabel = CreateFlexLabel(rowObj.transform, "USERNAME", 32, TextAlignmentOptions.Left, 1f, FontStyles.Bold);
            rankLabel = CreateFixedLabel(rowObj.transform, "Rank: -", 18, TextAlignmentOptions.Left, 180);
            rankingLabel = CreateFixedLabel(rowObj.transform, "Ranking: -", 18, TextAlignmentOptions.Left, 130);

            // Search box
            var searchObj = new GameObject("SearchField");
            searchObj.transform.SetParent(rowObj.transform, false);
            var searchLayoutElement = searchObj.AddComponent<LayoutElement>();
            searchLayoutElement.preferredWidth = 180;
            searchLayoutElement.minWidth = 180;
            searchLayoutElement.preferredHeight = 36;
            searchLayoutElement.flexibleWidth = 0;
            searchLayoutElement.flexibleHeight = 0;

            var searchBg = searchObj.AddComponent<Image>();
            searchBg.color = InputBg;
            searchField = searchObj.AddComponent<TMP_InputField>();
            searchField.targetGraphic = searchBg;
            searchField.characterLimit = 20;

            var searchTextAreaObj = new GameObject("TextArea");
            searchTextAreaObj.transform.SetParent(searchObj.transform, false);
            var searchTextAreaRect = searchTextAreaObj.AddComponent<RectTransform>();
            searchTextAreaRect.anchorMin = Vector2.zero;
            searchTextAreaRect.anchorMax = Vector2.one;
            searchTextAreaRect.offsetMin = new Vector2(8, 2);
            searchTextAreaRect.offsetMax = new Vector2(-8, -2);
            searchTextAreaObj.AddComponent<RectMask2D>();

            var placeholderObj = new GameObject("Placeholder");
            placeholderObj.transform.SetParent(searchTextAreaObj.transform, false);
            var placeholderRect = placeholderObj.AddComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;
            var placeholderTmp = placeholderObj.AddComponent<TextMeshProUGUI>();
            placeholderTmp.text = "Search name...";
            placeholderTmp.fontSize = 15;
            placeholderTmp.fontStyle = FontStyles.Italic;
            placeholderTmp.color = new Color(InputText.r, InputText.g, InputText.b, 0.55f);
            placeholderTmp.alignment = TextAlignmentOptions.Left;

            var searchTextObj = new GameObject("Text");
            searchTextObj.transform.SetParent(searchTextAreaObj.transform, false);
            var searchTextRect = searchTextObj.AddComponent<RectTransform>();
            searchTextRect.anchorMin = Vector2.zero;
            searchTextRect.anchorMax = Vector2.one;
            searchTextRect.offsetMin = Vector2.zero;
            searchTextRect.offsetMax = Vector2.zero;
            var searchTextTmp = searchTextObj.AddComponent<TextMeshProUGUI>();
            searchTextTmp.fontSize = 15;
            searchTextTmp.color = InputText;
            searchTextTmp.alignment = TextAlignmentOptions.Left;

            searchField.textViewport = searchTextAreaRect;
            searchField.textComponent = searchTextTmp;
            searchField.placeholder = placeholderTmp;
            searchField.onSubmit.AddListener(OnSearchSubmit);

            // Close button
            var closeObj = new GameObject("CloseButton");
            closeObj.transform.SetParent(rowObj.transform, false);
            var closeLayoutElement = closeObj.AddComponent<LayoutElement>();
            closeLayoutElement.preferredWidth = 44;
            closeLayoutElement.minWidth = 44;
            closeLayoutElement.preferredHeight = 44;
            closeLayoutElement.flexibleWidth = 0;
            closeLayoutElement.flexibleHeight = 0;

            var closeButton = closeObj.AddComponent<Button>();
            var closeBg = closeObj.AddComponent<Image>();
            closeBg.color = new Color(0, 0, 0, 0);
            closeButton.targetGraphic = closeBg;

            var closeTextObj = new GameObject("Label");
            closeTextObj.transform.SetParent(closeObj.transform, false);
            var closeTextRect = closeTextObj.AddComponent<RectTransform>();
            closeTextRect.anchorMin = Vector2.zero;
            closeTextRect.anchorMax = Vector2.one;
            closeTextRect.offsetMin = Vector2.zero;
            closeTextRect.offsetMax = Vector2.zero;
            var closeText = closeTextObj.AddComponent<TextMeshProUGUI>();
            closeText.text = "X";
            closeText.fontSize = 26;
            closeText.fontStyle = FontStyles.Bold;
            closeText.color = TextColor;
            closeText.alignment = TextAlignmentOptions.Center;
            closeButton.onClick.AddListener(Remove);
        }

        private void OnSearchSubmit(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            var plugin = Plugin.Instance;
            if (plugin?.Network == null || !plugin.Network.IsConnected) return;
            plugin.Network.SearchPlayer(query.Trim());
        }

        private void BuildSectionHeader(Transform parent, string title)
        {
            var obj = new GameObject("SectionHeader");
            obj.transform.SetParent(parent, false);
            var layoutElement = obj.AddComponent<LayoutElement>();
            layoutElement.minHeight = 36;
            layoutElement.preferredHeight = 36;
            layoutElement.flexibleHeight = 0;

            var text = obj.AddComponent<TextMeshProUGUI>();
            text.text = title;
            text.fontSize = 26;
            text.fontStyle = FontStyles.Bold;
            text.color = TextColor;
            text.alignment = TextAlignmentOptions.Left;
        }

        private void BuildStatsGrid(Transform parent)
        {
            var row1 = CreateStatsRow(parent);
            eloLabel = CreateFixedLabel(row1, "Elo: -", 18, TextAlignmentOptions.Left, 150);
            winsLabel = CreateFixedLabel(row1, "Wins: -", 18, TextAlignmentOptions.Left, 150);
            lossesLabel = CreateFixedLabel(row1, "Losses: -", 18, TextAlignmentOptions.Left, 150);
            avgCollectedLabel = CreateFixedLabel(row1, "Average collected: -", 18, TextAlignmentOptions.Left, 260);

            var row2 = CreateStatsRow(parent);
            winrateLabel = CreateFixedLabel(row2, "Winrate: -", 18, TextAlignmentOptions.Left, 150);
            winstreakLabel = CreateFixedLabel(row2, "Winstreak: -", 18, TextAlignmentOptions.Left, 150);
            highestCollectedLabel = CreateFixedLabel(row2, "Highest collected: -", 18, TextAlignmentOptions.Left, 260);
        }

        private Transform CreateStatsRow(Transform parent)
        {
            var rowObj = new GameObject("StatsRow");
            rowObj.transform.SetParent(parent, false);
            var layoutElement = rowObj.AddComponent<LayoutElement>();
            layoutElement.minHeight = 30;
            layoutElement.preferredHeight = 30;
            layoutElement.flexibleHeight = 0;

            var layout = rowObj.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            return rowObj.transform;
        }

        private void BuildHistoryList(Transform parent)
        {
            var scrollObj = new GameObject("HistoryScrollView");
            scrollObj.transform.SetParent(parent, false);
            var scrollLayoutElement = scrollObj.AddComponent<LayoutElement>();
            scrollLayoutElement.flexibleHeight = 1;
            scrollLayoutElement.minHeight = 200;

            var scroll = scrollObj.AddComponent<ScrollRect>();
            scrollObj.AddComponent<RectMask2D>();
            var scrollBg = scrollObj.AddComponent<Image>();
            scrollBg.color = new Color(0, 0, 0, 0.15f);

            var contentObj = new GameObject("Content");
            contentObj.transform.SetParent(scrollObj.transform, false);
            var contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);

            var contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 3;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            var contentFitter = contentObj.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;

            historyContainer = contentObj.transform;
        }

        private void BuildFooterRow(Transform parent)
        {
            var rowObj = new GameObject("FooterRow");
            rowObj.transform.SetParent(parent, false);
            var layoutElement = rowObj.AddComponent<LayoutElement>();
            layoutElement.minHeight = 30;
            layoutElement.preferredHeight = 30;
            layoutElement.flexibleHeight = 0;

            var layout = rowObj.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            totalMatchesLabel = CreateFlexLabel(rowObj.transform, "Total Matches played: -", 16, TextAlignmentOptions.Left, 1f, FontStyles.Normal);
            totalPlaytimeLabel = CreateFlexLabel(rowObj.transform, "Total Playtime: -", 16, TextAlignmentOptions.Right, 1f, FontStyles.Normal);
        }

        private TextMeshProUGUI CreateFixedLabel(Transform parent, string text, int fontSize, TextAlignmentOptions align, float width)
        {
            var obj = new GameObject("Cell");
            obj.transform.SetParent(parent, false);
            var layoutElement = obj.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = width;
            layoutElement.minWidth = width;
            layoutElement.flexibleWidth = 0;
            layoutElement.flexibleHeight = 0;

            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = TextColor;
            tmp.alignment = align;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return tmp;
        }

        private TextMeshProUGUI CreateFlexLabel(Transform parent, string text, int fontSize, TextAlignmentOptions align, float flexWidth, FontStyles style)
        {
            var obj = new GameObject("Cell");
            obj.transform.SetParent(parent, false);
            var layoutElement = obj.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = flexWidth;
            layoutElement.flexibleHeight = 0;

            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = TextColor;
            tmp.alignment = align;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return tmp;
        }

        public void HandleStatsResult(JObject msg)
        {
            string playerId = msg["playerId"]?.ToString();
            if (playerId != currentPlayerId) return;

            bool noRecord = msg["noRecord"]?.ToObject<bool>() ?? false;
            if (noRecord)
            {
                usernameLabel.text = "NOT FOUND";
                rankLabel.text = "Rank: -";
                rankingLabel.text = "Ranking: -";
                eloLabel.text = "Elo: -";
                winsLabel.text = "Wins: -";
                lossesLabel.text = "Losses: -";
                avgCollectedLabel.text = "Average collected: -";
                winrateLabel.text = "Winrate: -";
                winstreakLabel.text = "Winstreak: -";
                highestCollectedLabel.text = "Highest collected: -";
                totalMatchesLabel.text = "Total Matches played: -";
                totalPlaytimeLabel.text = "Total Playtime: -";
                return;
            }

            string playerName = msg["playerName"]?.ToString() ?? "Unknown";
            string rankLabelText = msg["rankLabel"]?.ToString() ?? "Unranked";
            int? leaderboardRank = msg["leaderboardRank"]?.ToObject<int?>();
            int rating = msg["rating"]?.ToObject<int>() ?? 0;
            int wins = msg["wins"]?.ToObject<int>() ?? 0;
            int losses = msg["losses"]?.ToObject<int>() ?? 0;
            int matchesPlayed = msg["matchesPlayed"]?.ToObject<int>() ?? 0;
            double winRate = msg["winRate"]?.ToObject<double>() ?? 0;
            int avgCollected = msg["avgCollectedValue"]?.ToObject<int>() ?? 0;
            int bestCollected = msg["bestCollectedValue"]?.ToObject<int>() ?? 0;
            int currentStreak = msg["currentStreak"]?.ToObject<int>() ?? 0;
            int playtimeSeconds = msg["rankedPlaytimeSeconds"]?.ToObject<int>() ?? 0;

            usernameLabel.text = playerName.ToUpper();
            rankLabel.text = $"Rank: {rankLabelText}";
            rankingLabel.text = leaderboardRank.HasValue ? $"Ranking: #{leaderboardRank.Value}" : "Ranking: -";
            eloLabel.text = $"Elo: {rating}";
            winsLabel.text = $"Wins: {wins}";
            lossesLabel.text = $"Losses: {losses}";
            avgCollectedLabel.text = $"Average collected: {avgCollected}";
            winrateLabel.text = $"Winrate: {(winRate * 100):F0}%";
            winstreakLabel.text = $"Winstreak: {(currentStreak >= 0 ? "W" + currentStreak : "L" + (-currentStreak))}";
            highestCollectedLabel.text = $"Highest collected: {bestCollected}";
            totalMatchesLabel.text = $"Total Matches played: {matchesPlayed}";

            int days = playtimeSeconds / 86400;
            int hours = (playtimeSeconds % 86400) / 3600;
            totalPlaytimeLabel.text = $"Total Playtime: {days}d {hours}hrs";
        }

        public void HandleHistoryResult(JObject msg)
        {
            string playerId = msg["playerId"]?.ToString();
            if (playerId != currentPlayerId) return;

            foreach (var row in historyRowObjects) Destroy(row);
            historyRowObjects.Clear();

            var entries = msg["entries"] as JArray ?? new JArray();
            foreach (var entryToken in entries)
            {
                CreateHistoryRow(entryToken as JObject);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(historyContainer.GetComponent<RectTransform>());
        }

        public void HandleSearchResult(JObject msg)
        {
            var results = msg["results"] as JArray;
            if (results == null || results.Count == 0) return;

            string foundPlayerId = results[0]["playerId"]?.ToString();
            if (!string.IsNullOrEmpty(foundPlayerId))
            {
                LoadProfile(foundPlayerId);
            }
        }

        private static string StripLevelSuffix(string moonName)
        {
            if (string.IsNullOrEmpty(moonName)) return moonName;
            return moonName.EndsWith("Level") ? moonName.Substring(0, moonName.Length - "Level".Length) : moonName;
        }

        private void CreateHistoryRow(JObject entry)
        {
            if (entry == null) return;

            var self = entry["self"] as JObject;
            var opponents = entry["opponents"] as JArray;
            var opponent = opponents != null && opponents.Count > 0 ? opponents[0] as JObject : null;

            string moon = StripLevelSuffix(entry["moon"]?.ToString() ?? "-");
            string weather = entry["weather"]?.ToString() ?? "-";
            bool won = (self?["placement"]?.ToObject<int>() ?? 2) == 1;
            int selfCollected = self?["collectedValue"]?.ToObject<int>() ?? 0;
            int opponentCollected = opponent?["collectedValue"]?.ToObject<int>() ?? 0;
            string opponentName = opponent?["playerName"]?.ToString() ?? "-";
            int? ratingAfter = self?["ratingAfterMatch"]?.ToObject<int?>();
            string ratingText = ratingAfter.HasValue ? ratingAfter.Value.ToString() : "-";

            var rowObj = new GameObject("HistoryRow");
            rowObj.transform.SetParent(historyContainer, false);
            var rowLayoutElement = rowObj.AddComponent<LayoutElement>();
            rowLayoutElement.minHeight = 30;
            rowLayoutElement.preferredHeight = 30;
            rowLayoutElement.flexibleHeight = 0;
            rowLayoutElement.flexibleWidth = 1;

            var rowBg = rowObj.AddComponent<Image>();
            rowBg.color = RowBg;

            var outline = rowObj.AddComponent<Outline>();
            outline.effectColor = RowBorder;
            outline.effectDistance = new Vector2(1f, 1f);

            var layout = rowObj.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 3, 3);
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            string resultText = won ? "Win" : "Loss";
            Color resultColor = won ? new Color(0.5f, 0.9f, 0.5f, 1f) : new Color(0.9f, 0.4f, 0.4f, 1f);

            CreateHistoryCell(rowObj.transform, $"             {moon} - {weather} - {resultText}", 240, resultColor);
            CreateHistoryCell(rowObj.transform, $"{selfCollected} - {opponentCollected}", 130, TextColor);
            CreateHistoryCell(rowObj.transform, $"{usernameLabel.text} - {opponentName}", 220, TextColor);
            CreateHistoryCell(rowObj.transform, ratingText, 80, TextColor);

            historyRowObjects.Add(rowObj);
        }

        private void CreateHistoryCell(Transform parent, string text, float width, Color color)
        {
            var obj = new GameObject("Cell");
            obj.transform.SetParent(parent, false);
            var layoutElement = obj.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = width;
            layoutElement.minWidth = width;
            layoutElement.flexibleWidth = 0;
            layoutElement.flexibleHeight = 0;

            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 14;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
        }
    }
}