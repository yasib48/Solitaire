using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Solitaire.EditorTools
{
    /// <summary>
    ///     Builds a reusable vertical ScrollView prefab ("ScrollArea"): a ScrollRect
    ///     with a RectMask2D viewport and an auto-sizing Content. Drop it into any
    ///     panel, then drop buttons/rows into its "Content" child — they stack top to
    ///     bottom and the area scrolls once they exceed the height.
    ///     Run via Tools/Solitaire/Build Scroll Area.
    /// </summary>
    public static class ScrollAreaBuilder
    {
        private const string OutputPrefabPath = "Assets/Prefabs/UI/ScrollArea.prefab";

        [MenuItem("Tools/Solitaire/Build Scroll Area")]
        public static void Build()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();

            var existing = GameObject.Find("Canvas/ScrollArea");
            if (existing != null)
                Object.DestroyImmediate(existing);

            // ---- Root: the ScrollRect (transparent background) ----
            var root = new GameObject("ScrollArea", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            root.layer = 5;
            if (canvas != null)
                root.transform.SetParent(canvas.transform, false);
            var rootRt = (RectTransform)root.transform;
            rootRt.sizeDelta = new Vector2(900, 1200);
            rootRt.anchoredPosition = Vector2.zero;

            var bg = root.GetComponent<Image>();
            bg.color = new Color(1, 1, 1, 0); // transparent; tint it if you want a visible area

            var scroll = root.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 25f;

            // ---- Viewport (clips content) ----
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.layer = 5;
            var vpRt = (RectTransform)viewport.transform;
            vpRt.SetParent(rootRt, false);
            Stretch(vpRt);

            // ---- Content (auto-stacks children, grows to fit) ----
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            content.layer = 5;
            var contentRt = (RectTransform)content.transform;
            contentRt.SetParent(vpRt, false);
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;

            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false; // keep each dropped button's own height
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = 10f;
            vlg.padding = new RectOffset(20, 20, 20, 20);

            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = vpRt;
            scroll.content = contentRt;

            // ---- Save prefab; keep the scene clean ----
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, OutputPrefabPath);
            Object.DestroyImmediate(root);

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[ScrollAreaBuilder] Built {OutputPrefabPath}.");
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
