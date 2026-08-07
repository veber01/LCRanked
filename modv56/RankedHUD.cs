using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LCRanked.UI
{
    public class RankedHUD : MonoBehaviour
    {
        public static RankedHUD Instance;

        public TextMeshProUGUI mmrText;
        private TextMeshProUGUI moonText;
        private TextMeshProUGUI seedText;
        private TextMeshProUGUI opponentText;
        private TextMeshProUGUI matchIdText;
        private TextMeshProUGUI weatherText;

        public static void Create()
        {
            if (Instance != null)
                return;

            GameObject canvasObj = new GameObject("LC Ranked HUD");

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;

            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            DontDestroyOnLoad(canvasObj);

            Instance = canvasObj.AddComponent<RankedHUD>();
            Instance.BuildUI(canvas.transform);
        }

        public static void Remove()
        {
        Destroy(Instance.gameObject);
        }

        private void BuildUI(Transform parent)
        {
            GameObject panelObj = new GameObject("Panel");
            panelObj.transform.SetParent(parent, false);

            Image image = panelObj.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.65f);

            RectTransform rect = panelObj.GetComponent<RectTransform>();

            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);

            rect.sizeDelta = new Vector2(240, 230);
            rect.anchoredPosition = new Vector2(-30, 0);

            VerticalLayoutGroup layout = panelObj.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 6;
            layout.childControlHeight = false;
            layout.childControlWidth = true;

            CreateHeader(panelObj.transform);

            mmrText = CreateLabel(panelObj.transform, "Unrated");
            moonText = CreateLabel(panelObj.transform, "Titan");
            weatherText = CreateLabel(panelObj.transform, "Weather: Clear");
            seedText = CreateLabel(panelObj.transform, "Seed: 4928810");
            opponentText = CreateLabel(panelObj.transform, "Opponent: Playing...");
            matchIdText = CreateLabel(panelObj.transform, "Match: -----");
        }

        private void CreateHeader(Transform parent)
        {
            GameObject obj = new GameObject("Header");
            obj.transform.SetParent(parent, false);

            TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();

            text.text = "LC RANKED";
            text.fontSize = 28;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 36);
        }

        private TextMeshProUGUI CreateLabel(Transform parent, string value)
        {
            GameObject obj = new GameObject(value);
            obj.transform.SetParent(parent, false);

            TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();

            text.text = value;
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Left;

            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 24);

            return text;
        }

        public void SetMMR(int mmr)
        {
            mmrText.text = $"{mmr} MMR";
        }

        public void SetMoon(string moon)
        {
            moonText.text = moon;
        }

        public void SetSeed(int seed)
        {
            seedText.text = $"Seed: {seed}";
        }

        public void SetOpponent(string status)
        {
            opponentText.text = $"Opponent: {status}";
        }

        public void SetMatchId(string id)
        {
            matchIdText.text = $"Match: {id}";
            matchIdText.fontSize = 10;
        }
                public void SetWeather(string weather)
        {
            weatherText.text = $"Weather: {weather}";
        }

    }
}