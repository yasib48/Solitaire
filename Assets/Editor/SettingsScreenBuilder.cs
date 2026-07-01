using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Solitaire.EditorTools
{
    /// <summary>
    ///     Builds a scrollable, easily-extensible Settings screen prefab that
    ///     matches the reference mockup (header + toggle rows + section headers +
    ///     nav rows with a chevron). Reuses the existing green Toggle prefab for
    ///     the switch rows. Content is English placeholder text; add or remove a
    ///     row by editing the calls in Build() (or by duplicating a row under
    ///     Content in the scene). Run via Tools/Solitaire/Build Settings Screen.
    /// </summary>
    public static class SettingsScreenBuilder
    {
        private const string TogglePrefabPath = "Assets/Prefabs/UI/Toggle.prefab";
        private const string OutputPrefabPath = "Assets/Prefabs/UI/SettingsScreen.prefab";

        private const float PadLeft = 60f;
        private const float PadRight = 60f;
        private const float PadTop = 20f;
        private const float PadBottom = 40f;
        private const float RowSpacing = 4f;

        private static readonly Color Black = new(0.12f, 0.12f, 0.12f);
        private static readonly Color Grey = new(0.6f, 0.6f, 0.6f);
        private static readonly Color HeaderBar = new(0.96f, 0.96f, 0.96f);

        [MenuItem("Tools/Solitaire/Build Settings Screen")]
        public static void Build()
        {
            var togglePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TogglePrefabPath);
            var canvas = Object.FindFirstObjectByType<Canvas>();

            // Remove a previous preview instance so re-runs don't stack up.
            var existing = GameObject.Find("Canvas/SettingsScreen");
            if (existing != null)
                Object.DestroyImmediate(existing);

            // ---- Root (fills its parent, white page background) ----
            var root = new GameObject("SettingsScreen", typeof(RectTransform), typeof(Image));
            SetLayerUI(root);
            if (canvas != null)
                root.transform.SetParent(canvas.transform, false);
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(1080, 1920);
            Stretch(rootRt);
            root.GetComponent<Image>().color = Color.white;

            // ---- Header bar ----
            var header = CreateChild(root.transform, "Header", typeof(Image));
            AnchorTop(header.GetComponent<RectTransform>(), 150f);
            header.GetComponent<Image>().color = HeaderBar;

            var back = CreateChild(header.transform, "BackButton", typeof(Image), typeof(Button));
            var backImg = back.GetComponent<Image>();
            backImg.color = new Color(1, 1, 1, 0); // invisible but clickable
            back.GetComponent<Button>().targetGraphic = backImg;
            var backRt = back.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(0, 0.5f);
            backRt.anchorMax = new Vector2(0, 0.5f);
            backRt.sizeDelta = new Vector2(120, 120);
            backRt.anchoredPosition = new Vector2(80, 0);
            var backIcon = CreateText(back.transform, "Icon", "←", 54, TextAlignmentOptions.Center);
            Stretch(backIcon.GetComponent<RectTransform>());
            backIcon.color = Black;

            var title = CreateText(header.transform, "Title", "Settings", 46, TextAlignmentOptions.Center);
            Stretch(title.GetComponent<RectTransform>());
            title.fontStyle = FontStyles.Bold;
            title.color = Black;

            // ---- Scroll view (fills the area below the header) ----
            var scroll = CreateChild(root.transform, "ScrollView", typeof(Image), typeof(ScrollRect));
            var scrollRt = scroll.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = new Vector2(0, -150);
            scroll.GetComponent<Image>().color = new Color(1, 1, 1, 0);
            var scrollRect = scroll.GetComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.scrollSensitivity = 25f;

            var viewport = CreateChild(scroll.transform, "Viewport", typeof(Image), typeof(Mask));
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Mask>().showMaskGraphic = false;
            viewport.GetComponent<Image>().color = Color.white;

            var content = CreateChild(viewport.transform, "Content");
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1);
            contentRt.anchoredPosition = Vector2.zero;

            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentRt;

            // ---- Rows (English placeholders; edit freely) ----
            AddToggleRow(content.transform, togglePrefab, "Ads", true);
            AddNavRow(content.transform, "Promo code");
            AddSectionHeader(content.transform, "PREFERENCES");
            AddToggleRow(content.transform, togglePrefab, "Sound effects", true);
            AddToggleRow(content.transform, togglePrefab, "Auto hints", true);
            AddToggleRow(content.transform, togglePrefab, "Auto complete", true);
            AddToggleRow(content.transform, togglePrefab, "Auto collect", false,
                "Automatically collects the cards when the game is completed.");
            AddToggleRow(content.transform, togglePrefab, "Quick game", false);
            AddToggleRow(content.transform, togglePrefab, "3-card mode", false);
            AddNavRow(content.transform, "Score");

            // ---- Manually stack the rows so the baked prefab renders correctly ----
            LayoutBake(contentRt);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, OutputPrefabPath);
            Object.DestroyImmediate(root); // only deliver the prefab; keep the scene clean

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[SettingsScreenBuilder] Built {OutputPrefabPath}.");
        }

        // ---------- helpers ----------

        private static void AddToggleRow(Transform parent, GameObject togglePrefab, string label,
            bool isOn, string description = null)
        {
            if (togglePrefab != null)
            {
                var row = (GameObject)PrefabUtility.InstantiatePrefab(togglePrefab, parent);
                row.name = "Row - " + label;
                var toggle = row.GetComponent<Toggle>();
                if (toggle != null)
                    toggle.isOn = isOn;
                var lbl = FindLabel(row.transform);
                if (lbl != null)
                {
                    lbl.text = label;
                    lbl.color = Black;
                }
                SetRowHeight(row, 96);
            }

            if (!string.IsNullOrEmpty(description))
            {
                var desc = CreateText(parent, "Description", description, 26, TextAlignmentOptions.TopLeft);
                desc.color = Grey;
                SetRowHeight(desc.gameObject, 64);
            }
        }

        private static void AddNavRow(Transform parent, string label)
        {
            var row = CreateChild(parent, "Nav - " + label, typeof(Image), typeof(Button));
            var img = row.GetComponent<Image>();
            img.color = new Color(1, 1, 1, 0);
            row.GetComponent<Button>().targetGraphic = img;
            SetRowHeight(row, 96);

            var lbl = CreateText(row.transform, "Label", label, 34, TextAlignmentOptions.Left);
            lbl.color = Black;
            var lblRt = lbl.GetComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero;
            lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero;
            lblRt.offsetMax = new Vector2(-70, 0);

            var chevron = CreateText(row.transform, "Chevron", "›", 46, TextAlignmentOptions.Right);
            chevron.color = Grey;
            var chRt = chevron.GetComponent<RectTransform>();
            chRt.anchorMin = new Vector2(1, 0);
            chRt.anchorMax = new Vector2(1, 1);
            chRt.pivot = new Vector2(1, 0.5f);
            chRt.sizeDelta = new Vector2(70, 0);
            chRt.anchoredPosition = Vector2.zero;
        }

        private static void AddSectionHeader(Transform parent, string text)
        {
            var lbl = CreateText(parent, "Section - " + text, text, 26, TextAlignmentOptions.BottomLeft);
            lbl.color = Grey;
            lbl.characterSpacing = 6f;
            SetRowHeight(lbl.gameObject, 80);
        }

        // Deterministically stacks Content's children from the top using each
        // row's LayoutElement height, then sizes Content for the ScrollRect.
        // Runs in the editor so the baked prefab renders without needing a
        // runtime layout pass. Add/remove a row and re-run to re-flow.
        private static void LayoutBake(RectTransform content)
        {
            var y = PadTop;
            for (var i = 0; i < content.childCount; i++)
            {
                if (content.GetChild(i) is not RectTransform rt)
                    continue;

                var le = rt.GetComponent<LayoutElement>();
                var h = le != null && le.preferredHeight > 0 ? le.preferredHeight : 96f;

                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1);
                rt.sizeDelta = new Vector2(-(PadLeft + PadRight), h);
                rt.anchoredPosition = new Vector2((PadLeft - PadRight) / 2f, -y);

                y += h + RowSpacing;
            }

            y += PadBottom - RowSpacing;
            content.sizeDelta = new Vector2(0, y);
        }

        private static void SetRowHeight(GameObject go, float height)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null)
                le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
        }

        private static TMP_Text FindLabel(Transform root)
        {
            var labels = root.GetComponentsInChildren<TMP_Text>(true);
            foreach (var l in labels)
                if (l.gameObject.name == "Label")
                    return l;
            return labels.Length > 0 ? labels[0] : null;
        }

        private static GameObject CreateChild(Transform content_parent, string name, params System.Type[] comps)
        {
            var all = new System.Type[comps.Length + 1];
            all[0] = typeof(RectTransform);
            comps.CopyTo(all, 1);
            var go = new GameObject(name, all);
            SetLayerUI(go);
            go.GetComponent<RectTransform>().SetParent(content_parent, false);
            return go;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size,
            TextAlignmentOptions align)
        {
            var go = CreateChild(parent, name, typeof(TextMeshProUGUI));
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = Black;
            tmp.enableAutoSizing = false;
            return tmp;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void AnchorTop(RectTransform rt, float height)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(0, height);
            rt.anchoredPosition = Vector2.zero;
        }

        private static void SetLayerUI(GameObject go)
        {
            go.layer = 5; // UI
        }
    }
}
