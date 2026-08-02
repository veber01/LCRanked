using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LCRanked
{
    public class NamePromptUI : MonoBehaviour
    {
        public static NamePromptUI Instance;

        private TMP_InputField inputField;
        private TextMeshProUGUI statusText;
        private Button confirmButton;
        private TextMeshProUGUI confirmButtonText;

        private static readonly Color PanelBg = new Color(0.15f, 0.03f, 0.03f, 0.97f);
        private static readonly Color PanelBorder = new Color(0.35f, 0.08f, 0.08f, 1f);
        private static readonly Color InputBg = new Color(0.75f, 0.36f, 0.20f, 1f);
        private static readonly Color InputText = new Color(0.20f, 0.05f, 0.02f, 1f);
        private static readonly Color TitleColor = new Color(0.85f, 0.45f, 0.40f, 1f);
        private static readonly Color WarningColor = new Color(0.95f, 0.65f, 0.25f, 1f);
        private static readonly Color ErrorColor = new Color(0.95f, 0.35f, 0.35f, 1f);
        private static readonly Color ButtonTextColor = new Color(0.92f, 0.85f, 0.80f, 1f);

        public static void Create()
        {
            if (Instance != null) return;

            var canvasObj = new GameObject("LCRankedNamePrompt");
            DontDestroyOnLoad(canvasObj);

            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9000;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();

            Instance = canvasObj.AddComponent<NamePromptUI>();
            Instance.BuildUI(canvas.transform);
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
            backdropImage.color = new Color(0f, 0f, 0f, 0.65f);
            backdropImage.raycastTarget = true;

            var borderObj = new GameObject("PanelBorder");
            borderObj.transform.SetParent(parent, false);
            var borderRect = borderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = borderRect.anchorMax = new Vector2(0.5f, 0.5f);
            borderRect.sizeDelta = new Vector2(568, 388);
            borderRect.anchoredPosition = Vector2.zero;
            var borderImage = borderObj.AddComponent<Image>();
            borderImage.color = PanelBorder;

            var panelObj = new GameObject("Panel");
            panelObj.transform.SetParent(parent, false);
            var panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560, 380);
            panelRect.anchoredPosition = Vector2.zero;
            var panelImage = panelObj.AddComponent<Image>();
            panelImage.color = PanelBg;

            var layout = panelObj.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 30, 30);
            layout.spacing = 16;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            CreateLabel(panelObj.transform, "ENTER YOUR NAME", 28, TitleColor, FontStyles.Bold, 40);
            CreateLabel(panelObj.transform, "This is one-time only - your name CANNOT be changed later!", 16, WarningColor, FontStyles.Normal, 48);
            CreateInputField(panelObj.transform);
            statusText = CreateLabel(panelObj.transform, "", 15, ErrorColor, FontStyles.Normal, 30);
            CreateConfirmButton(panelObj.transform);

            UpdateConfirmInteractable();
        }

        private TextMeshProUGUI CreateLabel(Transform parent, string text, int fontSize, Color color, FontStyles style, float height)
        {
            var obj = new GameObject("Label");
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, height);

            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true;

            return tmp;
        }

        private void CreateInputField(Transform parent)
        {
            var fieldObj = new GameObject("NameInput");
            fieldObj.transform.SetParent(parent, false);
            var fieldRect = fieldObj.AddComponent<RectTransform>();
            fieldRect.sizeDelta = new Vector2(0, 56);

            var bgImage = fieldObj.AddComponent<Image>();
            bgImage.color = InputBg;

            inputField = fieldObj.AddComponent<TMP_InputField>();
            inputField.targetGraphic = bgImage;
            inputField.characterLimit = 20;

            var textAreaObj = new GameObject("TextArea");
            textAreaObj.transform.SetParent(fieldObj.transform, false);
            var textAreaRect = textAreaObj.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(16, 6);
            textAreaRect.offsetMax = new Vector2(-16, -6);
            textAreaObj.AddComponent<RectMask2D>();

            var placeholderObj = new GameObject("Placeholder");
            placeholderObj.transform.SetParent(textAreaObj.transform, false);
            var placeholderRect = placeholderObj.AddComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;
            var placeholderTmp = placeholderObj.AddComponent<TextMeshProUGUI>();
            placeholderTmp.text = "Type in your name...";
            placeholderTmp.fontSize = 22;
            placeholderTmp.fontStyle = FontStyles.Italic;
            placeholderTmp.color = new Color(InputText.r, InputText.g, InputText.b, 0.55f);
            placeholderTmp.alignment = TextAlignmentOptions.Left;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(textAreaObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var textTmp = textObj.AddComponent<TextMeshProUGUI>();
            textTmp.fontSize = 22;
            textTmp.color = InputText;
            textTmp.alignment = TextAlignmentOptions.Left;

            inputField.textViewport = textAreaRect;
            inputField.textComponent = textTmp;
            inputField.placeholder = placeholderTmp;
            inputField.onValueChanged.AddListener(_ => UpdateConfirmInteractable());
            inputField.onSubmit.AddListener(_ => OnConfirmClicked());
        }

        private void CreateConfirmButton(Transform parent)
        {
            var buttonObj = new GameObject("ConfirmButton");
            buttonObj.transform.SetParent(parent, false);
            var rect = buttonObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 44);

            confirmButton = buttonObj.AddComponent<Button>();
            var buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = new Color(0, 0, 0, 0);
            confirmButton.targetGraphic = buttonImage;

            confirmButtonText = CreateLabel(buttonObj.transform, "[ Confirm ]", 22, ButtonTextColor, FontStyles.Bold, 44);
            var textRect = confirmButtonText.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        private void UpdateConfirmInteractable()
        {
            bool hasText = inputField != null && !string.IsNullOrWhiteSpace(inputField.text);
            if (confirmButton != null) confirmButton.interactable = hasText;
            if (confirmButtonText != null) confirmButtonText.color = hasText ? ButtonTextColor : new Color(ButtonTextColor.r, ButtonTextColor.g, ButtonTextColor.b, 0.4f);
        }

        private void OnConfirmClicked()
        {
            if (inputField == null || string.IsNullOrWhiteSpace(inputField.text)) return;

            confirmButton.interactable = false;
            statusText.text = "";

            var plugin = Plugin.Instance;
            if (plugin?.Network == null || !plugin.Network.IsConnected)
            {
                statusText.text = "You are not connected to the server!";
                confirmButton.interactable = true;
                return;
            }

            plugin.Network.SetDisplayName(plugin.LocalPlayerId, inputField.text.Trim());
        }
        public void HandleResult(bool success, string error)
        {
            if (success)
            {
                Remove();
                return;
            }

            statusText.text = error ?? "Try again!";
            if (confirmButton != null) confirmButton.interactable = true;
        }
    }
}