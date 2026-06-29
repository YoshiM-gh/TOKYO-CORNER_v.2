using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「お店に戻る」ボタン用。実行時に自分のButton.onClickへExitFocus()を結線する。
/// （永続リスナーのエディタ結線が不要になる＝コードだけで完結）
/// </summary>
[RequireComponent(typeof(Button))]
public class ReturnToCafeButton : MonoBehaviour
{
    private void Start()
    {
        var btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(() => SceneRouter.Instance.ExitFocus());
        }
    }
}
