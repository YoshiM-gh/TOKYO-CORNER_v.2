using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// DayCol（日付列）に追加するドラッグ作成コンポーネント。
/// 空きスロット（Slot_XX）からの縦ドラッグで 15 分刻みの時間範囲を選択し、
/// ドラッグ終了時に onCreated(dateKey, startTime, endTime) を呼ぶ。
///
/// ・Slot_XX の Button は IDragHandler を持たないため、ドラッグイベントは
///   親方向に伝播してこのコンポーネントが受け取る（クリックは従来通り Button が処理）
/// ・ドラッグ開始でクリック判定は EventSystem 側で自動キャンセルされる
/// ・EventBlock 上からのドラッグは pointerPressRaycast の名前判定で除外
/// </summary>
public class TimelineDragCreator : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private string _dateKey;
    private int    _hourCount  = 24;
    private float  _hourHeight = 60f;
    private Action<string, string, string> _onCreated;

    private RectTransform _rt;
    private bool  _active;
    private float _startQ;          // ドラッグ開始 quarter（15分単位インデックス）
    private float _curA, _curB;     // 現在の選択範囲（quarter）

    private RectTransform   _ghostRT;
    private TextMeshProUGUI _ghostLabel;

    public void Setup(string dateKey, int hourCount, float hourHeight,
                      Action<string, string, string> onCreated)
    {
        _dateKey    = dateKey;
        _hourCount  = hourCount;
        _hourHeight = hourHeight;
        _onCreated  = onCreated;
        _rt = GetComponent<RectTransform>();
    }

    // ── ドラッグハンドラ ──────────────────────────────────────
    public void OnBeginDrag(PointerEventData e)
    {
        _active = false;
        // 空きスロットから開始したドラッグのみ受け付ける
        var pressGO = e.pointerPressRaycast.gameObject;
        if (pressGO == null) return;
        if (!pressGO.name.StartsWith("Slot_")) return;

        if (!ScreenToQ(e.pressPosition, e.pressEventCamera, out float q)) return;
        _startQ = Mathf.Clamp(Mathf.Floor(q), 0, _hourCount * 4 - 1);
        _active = true;
        BuildGhost();
        UpdateGhost(_startQ, _startQ + 1f);  // 最低 15 分
    }

    public void OnDrag(PointerEventData e)
    {
        if (!_active) return;
        if (!ScreenToQ(e.position, e.pressEventCamera, out float q)) return;

        float a, b;
        if (q >= _startQ)
        {
            // 下方向ドラッグ：開始は floor 固定、終了は ceil
            a = _startQ;
            b = Mathf.Ceil(q);
        }
        else
        {
            // 上方向ドラッグ：開始を floor で更新、終了は元の開始+1
            a = Mathf.Floor(q);
            b = _startQ + 1f;
        }
        a = Mathf.Clamp(a, 0f, _hourCount * 4 - 1);
        b = Mathf.Clamp(b, a + 1f, _hourCount * 4);
        UpdateGhost(a, b);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!_active) return;
        _active = false;
        float a = _curA, b = _curB;
        DestroyGhost();
        _onCreated?.Invoke(_dateKey, QToTime(a), QToTime(b));
    }

    // ── 座標変換 ─────────────────────────────────────────────
    /// <summary>スクリーン座標 → quarter（15分単位の浮動小数インデックス）</summary>
    private bool ScreenToQ(Vector2 screenPos, Camera cam, out float q)
    {
        q = 0f;
        if (_rt == null) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rt, screenPos, cam, out var lp)) return false;
        // pivot(0,1)・上端 y=0、下方向が負 → 反転して 0〜totalH に正規化
        float yDown = -lp.y;
        q = yDown / (_hourHeight / 4f);
        return true;
    }

    private static string QToTime(float q)
    {
        int qi = Mathf.RoundToInt(q);
        int h  = qi / 4;
        int m  = (qi % 4) * 15;
        return $"{h:D2}:{m:D2}";
    }

    // ── ゴーストブロック ──────────────────────────────────────
    private void BuildGhost()
    {
        DestroyGhost();
        var go = new GameObject("DragGhost", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        _ghostRT = go.GetComponent<RectTransform>();
        _ghostRT.anchorMin = new Vector2(0f, 1f);
        _ghostRT.anchorMax = new Vector2(1f, 1f);
        _ghostRT.pivot     = new Vector2(0.5f, 1f);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.30f, 0.55f, 0.95f, 0.40f);
        img.raycastTarget = false;
        go.transform.SetAsLastSibling();  // 最前面

        var lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(go.transform, false);
        var lblRT = lblGO.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = new Vector2(6f, 2f); lblRT.offsetMax = new Vector2(-6f, -2f);
        _ghostLabel = lblGO.AddComponent<TextMeshProUGUI>();
        _ghostLabel.fontSize  = 12f;
        _ghostLabel.color     = Color.white;
        _ghostLabel.alignment = TextAlignmentOptions.TopLeft;
        _ghostLabel.raycastTarget = false;
    }

    private void UpdateGhost(float a, float b)
    {
        _curA = a; _curB = b;
        if (_ghostRT == null) return;
        float quarterH = _hourHeight / 4f;
        _ghostRT.sizeDelta        = new Vector2(0f, (b - a) * quarterH);
        _ghostRT.anchoredPosition = new Vector2(0f, -a * quarterH);
        if (_ghostLabel != null)
            _ghostLabel.text = $"{QToTime(a)}-{QToTime(b)}";
    }

    private void DestroyGhost()
    {
        if (_ghostRT != null) Destroy(_ghostRT.gameObject);
        _ghostRT = null; _ghostLabel = null;
    }

    private void OnDisable()
    {
        _active = false;
        DestroyGhost();
    }
}
