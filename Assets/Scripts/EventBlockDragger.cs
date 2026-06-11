using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// タイムライン上の EventBlock のドラッグ移動／上下リサイズ。
/// ・中央を掴んで縦ドラッグ → 時刻移動（長さ維持）。ドラッグ中は半透明化
/// ・上端/下端のグリップ帯（縁の内側＋外側8px）を掴んでドラッグ → 開始/終了時刻の変更。
///   帯へのホバーで「ハンドルピル」を表示して掴める場所を示す
/// ・15分スナップ、同日内（0〜24時）クランプ、ドロップで即保存（onCommit）
/// ・ドラッグ閾値未満はクリック扱い。閾値超過時は EventSystem が eligibleForClick を
///   落とすため、同居する Button（編集モーダル）の誤発火は起きない。
/// </summary>
public class EventBlockDragger : MonoBehaviour,
    IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerMoveHandler, IPointerExitHandler, IPointerDownHandler, IPointerClickHandler
{
    private enum Mode { None, Move, ResizeTop, ResizeBottom }

    private ScheduleEvent _ev;
    private float  _hourHeight;
    private Action _onCommit;
    private Action _onClick;          // クリック（＝ドラッグ未満）で編集モーダルを開く
    private bool   _suppressClick;    // ドラッグ開始後のリリースでクリックを発火させない
    private RectTransform _rt, _parentRT;
    private CanvasGroup _cg;
    private GameObject _topHandle, _bottomHandle;
    private float _edgeInside = EDGE_IN;   // 縁の内側の掴み幅（低いブロックでは縮む）

    private Mode    _mode = Mode.None;
    private Vector2 _lp0;                 // ドラッグ開始時の親ローカル座標
    private float   _startH0, _endH0;     // ドラッグ開始時の時刻
    private float   _newStartH, _newEndH; // プレビュー中の時刻

    private const float EDGE_IN    = 20f;   // 縁の内側の掴み幅（上限）。中央の移動ゾーンと完全に分離
    private const float EDGE_OUT   = 8f;    // 縁の外側への当たり判定拡張
    private const float SNAP_H     = 0.25f; // 15分
    private const float MIN_LEN_H  = 0.25f; // 最短15分
    private const float DRAG_ALPHA = 0.65f; // ドラッグ中の半透明度

    public void Init(ScheduleEvent ev, float hourHeight, Action onCommit, Action onClick, Sprite handleSprite = null)
    {
        _ev = ev; _hourHeight = hourHeight; _onCommit = onCommit; _onClick = onClick;
        _rt = (RectTransform)transform;
        _parentRT = transform.parent as RectTransform;
        _cg = gameObject.GetComponent<CanvasGroup>();
        if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();

        // 低いブロックでは内側の掴み幅を縮め、中央の移動ゾーンを確保する
        float h = _rt.sizeDelta.y;
        _edgeInside = Mathf.Min(EDGE_IN, h * 0.3f);

        CreateGrip("TopGrip",    true);
        CreateGrip("BottomGrip", false);
        _topHandle    = CreateHandle("TopHandle",    true,  handleSprite);
        _bottomHandle = CreateHandle("BottomHandle", false, handleSprite);
    }

    /// <summary>縁の内外をまたぐ透明な当たり判定帯。カードの少し外からでも掴めるようにする</summary>
    private void CreateGrip(string name, bool top)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        float ay = top ? 1f : 0f;
        rt.anchorMin = new Vector2(0f, ay); rt.anchorMax = new Vector2(1f, ay);
        rt.pivot = new Vector2(0.5f, 0.5f);
        float height = _edgeInside + EDGE_OUT;
        float center = (EDGE_OUT - _edgeInside) / 2f;   // 外側へオフセット
        rt.sizeDelta = new Vector2(0f, height);
        rt.anchoredPosition = new Vector2(0f, top ? center : -center);
        var img = go.AddComponent<Image>();
        img.color = Color.clear;        // 不可視、raycast のみ
        // raycastTarget は既定で true（ここが当たり判定になる）
    }

    /// <summary>辺の中央に乗る小さなピル（リサイズ可能の視覚的手がかり）</summary>
    private GameObject CreateHandle(string name, bool top, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        float ay = top ? 1f : 0f;
        rt.anchorMin = new Vector2(0.5f, ay); rt.anchorMax = new Vector2(0.5f, ay);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(28f, 5f);
        rt.anchoredPosition = Vector2.zero;   // 辺をまたいで中央に乗せる
        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.9f);
        img.raycastTarget = false;
        if (sprite != null)
        {
            img.sprite = sprite; img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = sprite.border.x * 100f / (sprite.pixelsPerUnit * 2.5f); // ピル形
        }
        go.SetActive(false);
        return go;
    }

    /// <summary>ポインタ位置から掴みゾーンを判定（縁の外側 EDGE_OUT px もリサイズ扱い）</summary>
    private Mode ZoneAt(PointerEventData e)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, e.position, e.pressEventCamera ?? e.enterEventCamera, out var bl);
        float h = _rt.rect.height;
        if (bl.y > -_edgeInside)     return Mode.ResizeTop;
        if (bl.y < -h + _edgeInside) return Mode.ResizeBottom;
        return Mode.Move;
    }

    private void ShowHandles(Mode z)
    {
        if (_topHandle    != null) _topHandle.SetActive(z == Mode.ResizeTop);
        if (_bottomHandle != null) _bottomHandle.SetActive(z == Mode.ResizeBottom);
    }

    public void OnPointerMove(PointerEventData e)
    {
        if (_mode != Mode.None) return;   // 操作中はゾーン表示を固定
        ShowHandles(ZoneAt(e));
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (_mode != Mode.None) return;
        ShowHandles(Mode.None);
    }

    public void OnPointerDown(PointerEventData e) => _suppressClick = false;

    /// <summary>クリック＝編集モーダル。ドラッグが始まっていたら何もしない</summary>
    public void OnPointerClick(PointerEventData e)
    {
        if (_suppressClick || e.dragging) return;
        _onClick?.Invoke();
    }

    public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = true;

    public void OnBeginDrag(PointerEventData e)
    {
        _suppressClick = true;   // 閾値を超えた＝クリック意図ではない
        if (_ev == null || _parentRT == null) { _mode = Mode.None; return; }
        _startH0 = ParseH(_ev.time);
        if (_startH0 < 0f) { _mode = Mode.None; return; }   // 時間なしは対象外（第2弾）
        _endH0 = string.IsNullOrEmpty(_ev.endTime) ? _startH0 + 1f
                 : Mathf.Max(ParseH(_ev.endTime), _startH0 + MIN_LEN_H);
        _newStartH = _startH0; _newEndH = _endH0;

        _mode = ZoneAt(e);
        ShowHandles(_mode);               // リサイズ中はハンドルを出し続ける
        _cg.alpha = DRAG_ALPHA;           // 「持ち上げ」の視覚フィードバック
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRT, e.position, e.pressEventCamera, out _lp0);
    }

    public void OnDrag(PointerEventData e)
    {
        if (_mode == Mode.None) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRT, e.position, e.pressEventCamera, out var lp);
        float deltaH = -(lp.y - _lp0.y) / _hourHeight;   // 画面下方向 = 時刻が進む
        float len    = _endH0 - _startH0;

        switch (_mode)
        {
            case Mode.Move:
                _newStartH = Mathf.Clamp(Snap(_startH0 + deltaH), 0f, 24f - len);
                _newEndH   = _newStartH + len;
                break;
            case Mode.ResizeTop:
                _newStartH = Mathf.Clamp(Snap(_startH0 + deltaH), 0f, _endH0 - MIN_LEN_H);
                _newEndH   = _endH0;
                break;
            case Mode.ResizeBottom:
                _newStartH = _startH0;
                _newEndH   = Mathf.Clamp(Snap(_endH0 + deltaH), _startH0 + MIN_LEN_H, 24f);
                break;
        }
        // ライブプレビュー（確定時は Refresh で再構築されるため位置式は Build 側と同一に保つ）
        _rt.anchoredPosition = new Vector2(_rt.anchoredPosition.x, -_newStartH * _hourHeight - 1f);
        _rt.sizeDelta = new Vector2(_rt.sizeDelta.x,
            Mathf.Max((_newEndH - _newStartH) * _hourHeight - 2f, 16f));
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (_mode == Mode.None) return;
        bool changed = !Mathf.Approximately(_newStartH, _startH0) || !Mathf.Approximately(_newEndH, _endH0);
        _mode = Mode.None;
        _cg.alpha = 1f;
        ShowHandles(Mode.None);
        if (!changed) return;
        _ev.time    = ToHHmm(_newStartH);
        _ev.endTime = ToHHmm(_newEndH);
        _onCommit?.Invoke();   // 即保存＋再描画
    }

    private static float Snap(float h) => Mathf.Round(h / SNAP_H) * SNAP_H;

    private static float ParseH(string s)
    {
        if (string.IsNullOrEmpty(s)) return -1f;
        var p = s.Split(':');
        if (p.Length != 2 || !int.TryParse(p[0], out int hh) || !int.TryParse(p[1], out int mm)) return -1f;
        return hh + mm / 60f;
    }

    private static string ToHHmm(float h)
    {
        int total = Mathf.Clamp(Mathf.RoundToInt(h * 60f), 0, 1440);
        return $"{total / 60:D2}:{total % 60:D2}";
    }
}

