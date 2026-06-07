using UnityEngine;
using TMPro;

/// <summary>
/// IME 変換候補ウィンドウをダイアログ全体の下端（Buttons 含む）に強制配置
/// </summary>
public class ImePositionController : MonoBehaviour
{
    private TMP_InputField[] _fields;
    private RectTransform    _dialogRT;

    private void Awake()
    {
        _fields = GetComponentsInChildren<TMP_InputField>(true);

        // Buttons（キャンセル/保存）の下端を基準にする
        // → ダイアログ全体（カード＋ボタン）の最下端
        var buttons = transform.Find("Buttons");
        if (buttons != null)
            _dialogRT = buttons.GetComponent<RectTransform>();
        else
        {
            var card = transform.Find("Content");
            if (card != null) _dialogRT = card.GetComponent<RectTransform>();
        }
    }

    private void LateUpdate()
    {
        foreach (var f in _fields)
        {
            if (f == null || !f.isFocused) continue;
            PlaceBelowDialog();
            break;
        }
    }

    private void PlaceBelowDialog()
    {
        if (_dialogRT == null) return;
        var corners = new Vector3[4];
        _dialogRT.GetWorldCorners(corners);

        // corners[0] = BottomLeft  ← Buttons の底辺
        Vector2 screenBL = RectTransformUtility.WorldToScreenPoint(null, corners[0]);

        // compositionCursorPos: (0,0) = 画面左上, Y は下に増加
        // screenBL.y は画面下からのピクセル → 上からに変換
        float compX = screenBL.x + 20f;
        float compY = Screen.height - screenBL.y + 6f;
        Input.compositionCursorPos = new Vector2(compX, compY);
    }
}