using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// メニュー行のホバー/選択表示（▶カーソル＋行ハイライト）。着席メニューの▶文法に合わせる。
/// 行のGameObjectに付与。子 "CursorLabel" と自身の Image（透明・raycast受け）を制御する。
/// マウスホバーとキーボード選択(EventSystem)の両方に反応し、Selectableがある行では
/// ホバー時に選択も同期させる（マウスと矢印キーのカーソル位置が常に一致する）。
/// 既定はホバー/選択中のみ点灯（MenuShop等）。stickyCursor=true にするとExit/Deselectで
/// 消灯せず、グループ側（例: PolicyPromptUI）が HoverEntered 購読でカーソルを一元管理できる。
/// </summary>
public class MenuRowHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    private static readonly Color HoverBg = new Color32(50, 60, 70, 255);
    private static readonly Color ClearBg = new Color(1f, 1f, 1f, 0f);

    /// <summary>trueならPointerExit/Deselectで消灯しない（カーソル常駐・グループ管理用）</summary>
    public bool stickyCursor = false;

    /// <summary>カーソルがこの行に来た時に発火（グループ側のカーソル移動用）</summary>
    public event System.Action<MenuRowHighlight> HoverEntered;

    private GameObject _cursor;
    private Image _bg;

    private void Awake()
    {
        var t = transform.Find("CursorLabel");
        _cursor = t != null ? t.gameObject : null;
        _bg = GetComponent<Image>();
        SetHover(false);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        Arrive();
        // キーボード選択と位置を同期（Selectableがある行のみ）→ ホバー直後の矢印キーがそこから動く
        if (EventSystem.current != null && GetComponent<Selectable>() != null)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (!stickyCursor) SetHover(false);
    }

    public void OnSelect(BaseEventData e) => Arrive();

    public void OnDeselect(BaseEventData e)
    {
        if (!stickyCursor) SetHover(false);
    }

    private void Arrive()
    {
        SetHover(true);
        HoverEntered?.Invoke(this);
    }

    public void SetHover(bool on)
    {
        if (_cursor != null) _cursor.SetActive(on);
        if (_bg != null) _bg.color = on ? HoverBg : ClearBg;
    }
}
