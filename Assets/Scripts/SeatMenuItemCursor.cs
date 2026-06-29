using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 着席メニューの各選択肢に付ける。EventSystemの「選択」状態に応じて子 "Cursor"（▶）を表示/非表示する。
/// マウスオーバーするとその項目を選択状態にする（ホバー＝選択＝▶表示）。キーボード上下でも選択が動く。
/// 無効(interactable=false)の項目はホバーしても選択されない。
/// </summary>
[RequireComponent(typeof(UnityEngine.UI.Selectable))]
public class SeatMenuItemCursor : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
{
    private GameObject _cursor;

    private void Awake()
    {
        var c = transform.Find("Cursor");
        _cursor = c != null ? c.gameObject : null;
        if (_cursor != null) _cursor.SetActive(false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (_cursor != null) _cursor.SetActive(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (_cursor != null) _cursor.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        var sel = GetComponent<UnityEngine.UI.Selectable>();
        if (sel != null && sel.IsInteractable() && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }
}
