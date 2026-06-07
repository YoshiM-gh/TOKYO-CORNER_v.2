using UnityEngine;
using TMPro;

/// <summary>
/// TextMeshProUGUI に付与することで UITheme_FocusMode のフォントスケール変更に自動追従する。
///
/// 使い方：
///   1. TextMeshProUGUI を持つ GameObject に Add Component
///   2. Inspector で Role を選択
///   3. UITheme_FocusMode.SetFontScale() を呼ぶと自動で更新される
///
/// ポイント：
///   - オプトイン方式（付けていないオブジェクトは変化しない）
///   - 非表示中でも OnEnable 時に自動適用
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
[DisallowMultipleComponent]
public class ThemedText : MonoBehaviour
{
    [SerializeField] public FontRole role = FontRole.Body;

    private TextMeshProUGUI _tmp;

    private void Awake()
    {
        _tmp = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        UITheme_FocusMode.OnThemeChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        UITheme_FocusMode.OnThemeChanged -= Apply;
    }

    /// <summary>フォントサイズを即座に適用する（Inspector の「Apply Now」からも呼べる）</summary>
    [ContextMenu("Apply Now")]
    public void Apply()
    {
        if (_tmp == null) _tmp = GetComponent<TextMeshProUGUI>();
        if (_tmp != null) _tmp.fontSize = UITheme_FocusMode.GetFontSize(role);
    }
}
