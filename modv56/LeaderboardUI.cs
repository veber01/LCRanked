using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LCRanked
{
    public class LeaderboardMenuLink : MonoBehaviour
    {
        public static LeaderboardMenuLink Instance;

        public static void Create()
        {
            if (Instance != null)
            {
                Instance.gameObject.SetActive(true);
                return;
            }

            var canvasObj = new GameObject("LCRankedLeaderboardLink");
            DontDestroyOnLoad(canvasObj);

            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4000;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();

            Instance = canvasObj.AddComponent<LeaderboardMenuLink>();
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
            var buttonObj = new GameObject("LeaderboardsLink");
            buttonObj.transform.SetParent(parent, false);
            var rect = buttonObj.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(220, 36);
            rect.anchoredPosition = new Vector2(-40, -850);

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
            text.text = "Leaderboards <";
            text.fontSize = 20;
            text.color = new Color(0.85f, 0.45f, 0.35f, 1f);
            text.alignment = TextAlignmentOptions.Right;

            button.onClick.AddListener(() => LeaderboardWindowUI.Create());
        }
    }

    public class LeaderboardWindowUI : MonoBehaviour
    {
        public static LeaderboardWindowUI Instance;

        private const int PageSize = 20;
        private int currentPage = 1;
        private int totalPages = 1;

        private Transform rowContainer;
        private TextMeshProUGUI pageLabel;
        private TextMeshProUGUI statusLabel;
        private Button prevButton;
        private Button nextButton;
        private readonly List<GameObject> rowObjects = new List<GameObject>();

        private static readonly Color PanelBg = new Color(0.15f, 0.03f, 0.03f, 0.97f);
        private static readonly Color PanelBorder = new Color(0.35f, 0.08f, 0.08f, 1f);
        private static readonly Color RowBg = new Color(0.32f, 0.10f, 0.06f, 1f);
        private static readonly Color RowBorder = new Color(0.75f, 0.30f, 0.15f, 1f);
        private static readonly Color TextColor = new Color(0.92f, 0.55f, 0.40f, 1f);

        public static void Create()
        {
            if (Instance != null) return;

            var canvasObj = new GameObject("LCRankedLeaderboardWindow");
            DontDestroyOnLoad(canvasObj);

            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 8000;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();

            Instance = canvasObj.AddComponent<LeaderboardWindowUI>();
            Instance.BuildUI(canvas.transform);
            Instance.RequestPage(1);
        }

        public static void Remove()
        {
            if (Instance == null) return;
            Destroy(Instance.gameObject);
            Instance = null;
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
            borderRect.sizeDelta = new Vector2(880, 700);
            var borderImage = borderObj.AddComponent<Image>();
            borderImage.color = PanelBorder;

            var panelObj = new GameObject("Panel");
            panelObj.transform.SetParent(parent, false);
            var panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(870, 690);
            var panelImage = panelObj.AddComponent<Image>();
            panelImage.color = PanelBg;

            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(panelObj.transform, false);
            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(0, 50);
            titleRect.anchoredPosition = new Vector2(0, -20);
            var titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "ELO LEADERBOARDS";
            titleText.fontSize = 30;
            titleText.color = TextColor;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;

            var closeObj = new GameObject("CloseButton");
            closeObj.transform.SetParent(panelObj.transform, false);
            var closeRect = closeObj.AddComponent<RectTransform>();
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.sizeDelta = new Vector2(44, 44);
            closeRect.anchoredPosition = new Vector2(-16, -16);
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
            closeText.color = TextColor;
            closeText.fontStyle = FontStyles.Bold;
            closeText.alignment = TextAlignmentOptions.Center;
            closeButton.onClick.AddListener(Remove);

            var scrollObj = new GameObject("ScrollView");
            scrollObj.transform.SetParent(panelObj.transform, false);
            var scrollRect = scrollObj.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(24, 70);
            scrollRect.offsetMax = new Vector2(-24, -80);
            var scroll = scrollObj.AddComponent<ScrollRect>();
            scrollObj.AddComponent<RectMask2D>();

            var contentObj = new GameObject("Content");
            contentObj.transform.SetParent(scrollObj.transform, false);
            var contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            var contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 6;
            contentLayout.childControlHeight = true;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandWidth = false;
            contentLayout.childForceExpandHeight = false;
            var contentFitter = contentObj.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;

            rowContainer = contentObj.transform;

            var pagBarObj = new GameObject("PaginationBar");
            pagBarObj.transform.SetParent(panelObj.transform, false);
            var pagBarRect = pagBarObj.AddComponent<RectTransform>();
            pagBarRect.anchorMin = new Vector2(0f, 0f);
            pagBarRect.anchorMax = new Vector2(1f, 0f);
            pagBarRect.pivot = new Vector2(0.5f, 0f);
            pagBarRect.sizeDelta = new Vector2(0, 50);
            pagBarRect.anchoredPosition = new Vector2(0, 16);
            var pagLayout = pagBarObj.AddComponent<HorizontalLayoutGroup>();
            pagLayout.childAlignment = TextAnchor.MiddleCenter;
            pagLayout.spacing = 24;
            pagLayout.childControlWidth = false;
            pagLayout.childControlHeight = false;

            prevButton = CreateTextButton(pagBarObj.transform, "< Prev", () => RequestPage(currentPage - 1));
            pageLabel = CreateLabel(pagBarObj.transform, "Page 1 / 1", 120);
            nextButton = CreateTextButton(pagBarObj.transform, "Next >", () => RequestPage(currentPage + 1));

            statusLabel = CreateLabel(panelObj.transform, "", 40);
            var statusRect = statusLabel.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.5f, 0.5f);
            statusRect.anchorMax = new Vector2(0.5f, 0.5f);
            statusLabel.gameObject.SetActive(false);
        }

        private Button CreateTextButton(Transform parent, string label, Action onClick)
        {
            var obj = new GameObject(label);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(100, 40);
            var button = obj.AddComponent<Button>();
            var bg = obj.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0);
            button.targetGraphic = bg;

            var textObj = new GameObject("Label");
            textObj.transform.SetParent(obj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 20;
            text.color = TextColor;
            text.alignment = TextAlignmentOptions.Center;
            button.onClick.AddListener(() => onClick());
            return button;
        }

        private TextMeshProUGUI CreateLabel(Transform parent, string text, float width)
        {
            var obj = new GameObject("Label");
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, 40);
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 20;
            tmp.color = TextColor;
            tmp.alignment = TextAlignmentOptions.Center;
            return tmp;
        }

        private void RequestPage(int page)
        {
            if (page < 1) return;
            var plugin = Plugin.Instance;
            if (plugin?.Network == null || !plugin.Network.IsConnected) return;
            plugin.Network.RequestLeaderboardPage(page, PageSize);
        }

        public void HandlePageResult(int page, int totalPagesReceived, JsonEntry[] entries)
        {
            currentPage = page;
            totalPages = totalPagesReceived;
            pageLabel.text = $"Page {currentPage} / {totalPages}";
            prevButton.interactable = currentPage > 1;
            nextButton.interactable = currentPage < totalPages;

            foreach (var row in rowObjects) Destroy(row);
            rowObjects.Clear();

            foreach (var entry in entries)
            {
                CreateRow(entry);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(rowContainer.GetComponent<RectTransform>());

            if (rowObjects.Count > 0)
            {
                var firstRow = rowObjects[0];
                for (int i = 0; i < firstRow.transform.childCount; i++)
                {
                    var child = firstRow.transform.GetChild(i);
                }
            }
        }

        private void CreateRow(JsonEntry entry)
        {
            var rowObj = new GameObject($"Row_{entry.placement}");
            rowObj.transform.SetParent(rowContainer, false);
            var rowRect = rowObj.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);

            var rowLayoutElement = rowObj.AddComponent<LayoutElement>();
            rowLayoutElement.minHeight = 32;
            rowLayoutElement.preferredHeight = 32;
            rowLayoutElement.flexibleHeight = 0;
            rowLayoutElement.flexibleWidth = 1;

            var rowBg = rowObj.AddComponent<Image>();
            rowBg.color = RowBg;

            var outline = rowObj.AddComponent<Outline>();
            outline.effectColor = RowBorder;
            outline.effectDistance = new Vector2(1f, 1f);

            var layout = rowObj.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 3, 3);
            layout.spacing = 8;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            AddRowLabel(rowObj.transform, $"          #{entry.placement}", 100, TextAlignmentOptions.Left, 16);
            AddRowLabel(rowObj.transform, entry.playerName, 240, TextAlignmentOptions.Left, 16);
            AddSteamProfileButton(rowObj.transform, entry.playerId);
            AddRowLabel(rowObj.transform, entry.rating.ToString(), 250, TextAlignmentOptions.Right, 16);

            rowObjects.Add(rowObj);
        }

        private TextMeshProUGUI AddRowLabel(Transform parent, string text, float width, TextAlignmentOptions align, int fontSize = 22)
        {
            var obj = new GameObject("Cell");
            obj.transform.SetParent(parent, false);
            var layoutElement = obj.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = width;
            layoutElement.minWidth = width;
            layoutElement.flexibleWidth = 0;

            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = TextColor;
            tmp.alignment = align;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return tmp;
        }

        private void AddSteamProfileButton(Transform parent, string steamId)
        {
            var obj = new GameObject("SteamProfileButton");
            obj.transform.SetParent(parent, false);
            var layoutElement = obj.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 110;   // was 130
            layoutElement.minWidth = 110;
            layoutElement.preferredHeight = 24;   // was 32
            layoutElement.flexibleWidth = 0;
            layoutElement.flexibleHeight = 0;

            var button = obj.AddComponent<Button>();
            var bg = obj.AddComponent<Image>();
            bg.color = new Color(0.75f, 0.30f, 0.15f, 0.35f);
            button.targetGraphic = bg;

            var textObj = new GameObject("Label");
            textObj.transform.SetParent(obj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = "Steam Profile";
            text.fontSize = 13; // was 16
            text.color = TextColor;
            text.alignment = TextAlignmentOptions.Center;

            button.onClick.AddListener(() => OpenSteamProfile(steamId));
        }

        private static readonly System.Text.RegularExpressions.Regex SteamIdPattern =
            new System.Text.RegularExpressions.Regex(@"^\d+$");

        private static void OpenSteamProfile(string steamId)
        {
            if (string.IsNullOrEmpty(steamId) || !SteamIdPattern.IsMatch(steamId)) return;

            try
            {
                string url = $"https://steamcommunity.com/profiles/{steamId}/";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[LCRanked] Failed to open: {steamId}: {ex.Message}");
            }
        }


        public struct JsonEntry
        {
            public int placement;
            public string playerId;
            public string playerName;
            public int rating;
            public int highestCollected;
        }
    }
}