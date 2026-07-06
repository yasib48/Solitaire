using UnityEngine;
using TMPro;

[ExecuteAlways]
[RequireComponent(typeof(TextMeshProUGUI))]
public class TMP_LeftSnapImage : MonoBehaviour
{

    public bool isRightSnap = false;
    
    [Header("Sol Taraf Image")]
    [SerializeField] private RectTransform leftImage;
    [SerializeField] private float imageSpacing = 5f;
    public float offsetY;

    private TextMeshProUGUI tmp;
    private RectTransform rectTransform;
    private int lastHash;
    private bool anchorsSet;
    private bool dirty;

    private void Awake()
    {
        tmp = GetComponent<TextMeshProUGUI>();
        rectTransform = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        anchorsSet = false;
        dirty = true;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
    }

    private void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
    }

    private void OnTextChanged(Object obj)
    {
        if (obj == tmp)
            dirty = true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!gameObject.activeInHierarchy) return;

        if (tmp == null) tmp = GetComponent<TextMeshProUGUI>();
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        anchorsSet = false;
        dirty = true;
    }
#endif

    private void LateUpdate()
    {
        if (leftImage == null || tmp == null) return;

        int hash = ComputeHash();

        if (!dirty && hash == lastHash)
            return;

        dirty = false;
        lastHash = hash;

        tmp.ForceMeshUpdate();
        UpdateImagePosition();
    }

    public void ForceRefresh()
    {
        if (tmp == null || leftImage == null) return;

        tmp.ForceMeshUpdate();
        lastHash = ComputeHash();
        dirty = false;

        UpdateImagePosition();
    }

    private int ComputeHash()
    {
        int h = tmp.textInfo.characterCount;
        h = h * 397 ^ tmp.fontSize.GetHashCode();
        h = h * 397 ^ rectTransform.rect.width.GetHashCode();
        h = h * 397 ^ offsetY.GetHashCode();
        return h;
    }

    private void UpdateImagePosition()
    {
        if (!anchorsSet)
        {
            // Anchor to the text's centre so the glyph coordinates (which TMP
            // reports relative to the text pivot/centre) can be used directly
            // without double-counting half the text width.
            if (isRightSnap)
            {
                leftImage.anchorMin = new Vector2(0.5f, 0.5f);
                leftImage.anchorMax = new Vector2(0.5f, 0.5f);
                leftImage.pivot = new Vector2(0f, 0.5f);
            }
            else
            {
                leftImage.anchorMin = new Vector2(0.5f, 0.5f);
                leftImage.anchorMax = new Vector2(0.5f, 0.5f);
                leftImage.pivot = new Vector2(1f, 0.5f);
            }
            anchorsSet = true;
        }

        TMP_TextInfo textInfo = tmp.textInfo;
        if (textInfo.characterCount == 0) return;

        if (isRightSnap)
        {
            TMP_CharacterInfo lastChar = textInfo.characterInfo[textInfo.characterCount - 1];
            if (!lastChar.isVisible) return;

            float lastCharX = lastChar.bottomRight.x;
            float targetX = lastCharX + imageSpacing;

            Vector2 pos = leftImage.anchoredPosition;
            if (pos.x != targetX || pos.y != offsetY)
                leftImage.anchoredPosition = new Vector2(targetX, offsetY);
        }
        else
        {
            TMP_CharacterInfo firstChar = textInfo.characterInfo[0];
            if (!firstChar.isVisible) return;

            float firstCharX = firstChar.bottomLeft.x;
            float targetX = firstCharX - imageSpacing;

            Vector2 pos = leftImage.anchoredPosition;
            if (pos.x != targetX || pos.y != offsetY)
                leftImage.anchoredPosition = new Vector2(targetX, offsetY);
        }
    }
}




// using UnityEngine;
// using TMPro;

// [ExecuteAlways]
// [RequireComponent(typeof(TextMeshProUGUI))]
// public class TMP_LeftSnapImage : MonoBehaviour
// {

//     public bool isRightSnap = false;
    
//     [Header("Sol Taraf Image")]
//     [SerializeField] private RectTransform leftImage;
//     [SerializeField] private float imageSpacing = 5f;
//     public Vector2 offset;

//     private TextMeshProUGUI tmp;
//     private RectTransform rectTransform;
//     private int lastHash;
//     private bool anchorsSet;
//     private bool dirty;

//     private void Awake()
//     {
//         tmp = GetComponent<TextMeshProUGUI>();
//         rectTransform = GetComponent<RectTransform>();
//     }

//     private void OnEnable()
//     {
//         anchorsSet = false;
//         dirty = true;
//         TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
//     }

//     private void OnDisable()
//     {
//         TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
//     }

//     private void OnTextChanged(Object obj)
//     {
//         if (obj == tmp)
//             dirty = true;
//     }

// #if UNITY_EDITOR
//     private void OnValidate()
//     {
//         if (!gameObject.activeInHierarchy) return;

//         if (tmp == null) tmp = GetComponent<TextMeshProUGUI>();
//         if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
//         anchorsSet = false;
//         dirty = true;
//     }
// #endif

//     private void LateUpdate()
//     {
//         if (leftImage == null || tmp == null) return;

//         int hash = ComputeHash();

//         if (!dirty && hash == lastHash)
//             return;

//         dirty = false;
//         lastHash = hash;

//         tmp.ForceMeshUpdate();
//         UpdateImagePosition();
//     }

//     public void ForceRefresh()
//     {
//         if (tmp == null || leftImage == null) return;

//         tmp.ForceMeshUpdate();
//         lastHash = ComputeHash();
//         dirty = false;

//         UpdateImagePosition();
//     }

//     private int ComputeHash()
//     {
//         int h = tmp.textInfo.characterCount;
//         h = h * 397 ^ tmp.fontSize.GetHashCode();
//         h = h * 397 ^ rectTransform.rect.width.GetHashCode();
//         return h;
//     }

//     private void UpdateImagePosition()
//     {
//         if (!anchorsSet)
//         {
//             if (isRightSnap)
//             {
//                 leftImage.anchorMin = new Vector2(1f, 0.5f);
//                 leftImage.anchorMax = new Vector2(1f, 0.5f);
//                 leftImage.pivot = new Vector2(0f, 0.5f);
//             }
//             else
//             {
//                 leftImage.anchorMin = new Vector2(0f, 0.5f);
//                 leftImage.anchorMax = new Vector2(0f, 0.5f);
//                 leftImage.pivot = new Vector2(1f, 0.5f);
//             }
//             anchorsSet = true;
//         }

//         TMP_TextInfo textInfo = tmp.textInfo;
//         if (textInfo.characterCount == 0) return;

//         if (isRightSnap)
//         {
//             TMP_CharacterInfo lastChar = textInfo.characterInfo[textInfo.characterCount - 1];
//             if (!lastChar.isVisible) return;

//             float lastCharX = lastChar.bottomRight.x;
//             float targetX = lastCharX + imageSpacing;

//             Vector2 pos = leftImage.anchoredPosition;
//             if (pos.x != targetX || pos.y != 0f)
//                 leftImage.anchoredPosition = new Vector2(targetX, 0f);
//         }
//         else
//         {
//             TMP_CharacterInfo firstChar = textInfo.characterInfo[0];
//             if (!firstChar.isVisible) return;

//             float firstCharX = firstChar.bottomLeft.x;
//             float targetX = firstCharX - imageSpacing;

//             Vector2 pos = leftImage.anchoredPosition;
//             if (pos.x != targetX || pos.y != 0f)
//                 leftImage.anchoredPosition = new Vector2(targetX, 0f);
//         }
//     }
// }
