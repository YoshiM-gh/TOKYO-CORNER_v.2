using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// MUIP WindowManager の Start() がボタンテキストを上書きするため、
/// 1フレーム後に正しいタブ名に書き戻す。
/// </summary>
public class WindowManagerTabFixer : MonoBehaviour
{
    [System.Serializable]
    public class TabEntry
    {
        public string tabName;
        public GameObject buttonGO;
    }

    [SerializeField] private TabEntry[] tabs;

    IEnumerator Start()
    {
        yield return null; // 1フレーム待って MUIP の初期化後に実行
        foreach (var tab in tabs)
        {
            if (tab.buttonGO == null) continue;
            foreach (var tmp in tab.buttonGO.GetComponentsInChildren<TextMeshProUGUI>(true))
                tmp.text = tab.tabName;
        }
    }
}
