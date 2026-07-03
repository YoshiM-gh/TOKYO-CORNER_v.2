using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// メニュー行のホバー表示（▶カーソル＋行ハイライト）。着席メニューの▶文法に合わせる。
/// RowTemplate に付与。子 "CursorLabel" と自身の Image（透明・raycast受け）を制御する。
/// </summary>
public class MenuRowHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private static readonly Color HoverBg = new Color32(50, 60, 70, 255);
    private static readonly Color ClearBg = new Color(1f, 1f, 1f, 0f);

    private GameObject _cursor;
    private Image _bg;

    private void Awake()
    {
        var t = transform.Find("CursorLabel");
        _cursor = t != null ? t.gameObject : null;
        _bg = GetComponent<Image>();
        SetHover(false);
    }

    public void OnPointerEnter(PointerEventData e) => SetHover(true);
    public void OnPointerExit(PointerEventData e) => SetHover(false);

    private void SetHover(bool on)
    {
        if (_cursor != null) _cursor.SetActive(on);
        if (_bg != null) _bg.color = on ? HoverBg : ClearBg;
    }
}
