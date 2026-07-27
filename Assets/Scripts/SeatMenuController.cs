using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 着席時に出す独立メニュー（会話UI/VNとは別系統）。Cafeシーンのキャンバスに置く。
/// SitDown から SeatMenuController.OpenFor(this) で開く。
/// ボタン: 飲む / 食べる / 集中する / 席を立つ。
/// 子を「名前」で探して結線する（SerializeField手動割当に依存しない）:
///   DrinkButton, FoodButton, FocusButton, StandButton（各 Button）
///   各ボタンの子に Label（TMP_Text）
/// 表示/非表示する本体はキャンバス直下の "Panel"（無ければ自分自身）。
/// </summary>
public class SeatMenuController : MonoBehaviour
{
    public static SeatMenuController Instance { get; private set; }

    /// <summary>着席メニューが表示中か（他UIとの排他用）。</summary>
    public bool IsOpen => _panel != null && _panel.activeSelf;

    private GameObject _panel;
    private Button _drinkBtn, _foodBtn, _focusBtn, _standBtn;
    private TMPro.TMP_Text _drinkLabel, _foodLabel, _focusLabel, _standLabel;
    private SeatInteractableImproved _seat;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Bind();
        if (_panel != null) _panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Bind()
    {
        var p = transform.Find("Panel");
        _panel = p != null ? p.gameObject : gameObject;
        _drinkBtn = FindButton("DrinkButton", out _drinkLabel);
        _foodBtn  = FindButton("FoodButton",  out _foodLabel);
        _focusBtn = FindButton("FocusButton", out _focusLabel);
        _standBtn = FindButton("StandButton", out _standLabel);
        if (_drinkBtn != null) _drinkBtn.onClick.AddListener(OnDrink);
        if (_foodBtn  != null) _foodBtn.onClick.AddListener(OnFood);
        if (_focusBtn != null) _focusBtn.onClick.AddListener(OnFocus);
        if (_standBtn != null) _standBtn.onClick.AddListener(OnStand);
    }

    private Button FindButton(string goName, out TMPro.TMP_Text label)
    {
        label = null;
        var t = FindDeep(transform, goName);
        if (t == null) return null;
        var lt = t.Find("Label");
        if (lt != null) label = lt.GetComponent<TMPro.TMP_Text>();
        if (label == null) label = t.GetComponentInChildren<TMPro.TMP_Text>(true);
        return t.GetComponent<Button>();
    }

    private Transform FindDeep(Transform root, string goName)
    {
        if (root.name == goName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var r = FindDeep(root.GetChild(i), goName);
            if (r != null) return r;
        }
        return null;
    }

    public static void OpenFor(SeatInteractableImproved seat)
    {
        if (Instance == null)
        {
            // メニュー未配置のフォールバック: 直接フォーカスへ
            if (SceneRouter.Instance != null) SceneRouter.Instance.EnterFocus();
            return;
        }
        Instance.OpenInternal(seat);
    }

    private void OpenInternal(SeatInteractableImproved seat)
    {
        _seat = seat;
        Refresh();
        if (_panel != null) _panel.SetActive(true);
        SelectFirstEnabled();
    }

    private void SelectFirstEnabled()
    {
        Button first = null;
        if (_drinkBtn != null && _drinkBtn.interactable) first = _drinkBtn;
        else if (_foodBtn != null && _foodBtn.interactable) first = _foodBtn;
        else if (_focusBtn != null && _focusBtn.interactable) first = _focusBtn;
        else if (_standBtn != null && _standBtn.interactable) first = _standBtn;
        if (first != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(first.gameObject);
    }

    public void Close()
    {
        if (_panel != null) _panel.SetActive(false);
        _seat = null;
    }

    private void Refresh()
    {
        int sips = 0, maxSips = 0;
        var di = DrinkInventory.Instance;
        if (di != null) { sips = di.TotalSipsRemaining(); maxSips = di.TotalSipsMax(); }
        var dlist = di != null ? di.GetDrinks() : null;
        if (_drinkLabel != null)
        {
            if (dlist != null && dlist.Count > 0)
                _drinkLabel.text = dlist[0].displayName + " を飲む（残り " + dlist[0].sipsRemaining + "/" + dlist[0].sipsMax + " 口）";
            else
                _drinkLabel.text = "ドリンクを飲む (0/0)";
        }
        if (_drinkBtn != null) _drinkBtn.interactable = sips > 0;

        int bites = 0;
        var fi = FoodInventory.Instance;
        if (fi != null) bites = fi.TotalBitesRemaining();
        var flist = fi != null ? fi.GetFoods() : null;
        if (_foodLabel != null)
        {
            if (flist != null && flist.Count > 0)
                _foodLabel.text = flist[0].displayName + " を食べる（残り " + flist[0].bitesRemaining + "/" + flist[0].bitesMax + " 口）";
            else
                _foodLabel.text = "フードを食べる (0/0)";
        }
        if (_foodBtn != null) _foodBtn.interactable = bites > 0;
        if (_drinkLabel != null) _drinkLabel.color = (_drinkBtn != null && _drinkBtn.interactable) ? new Color(1f,1f,1f,1f) : new Color(0.45f,0.45f,0.45f,1f);
        if (_foodLabel != null) _foodLabel.color = (_foodBtn != null && _foodBtn.interactable) ? new Color(1f,1f,1f,1f) : new Color(0.45f,0.45f,0.45f,1f);

        if (_focusLabel != null) _focusLabel.text = "フォーカスモードに入る";
        if (_standLabel != null) _standLabel.text = "席を立つ";
    }

    private void OnDrink()
    {
        var di = DrinkInventory.Instance;
        if (di != null && di.HasAnySip() && di.TakeSip()) ShowMealLine(di.LastSipLine);
        Refresh();
    }

    private void OnFood()
    {
        var fi = FoodInventory.Instance;
        if (fi != null && fi.HasAnyBite() && fi.TakeBite()) ShowMealLine(fi.LastBiteLine);
        Refresh();
    }

    /// <summary>食事の一言をプレイヤーのセリフとして会話UIで表示（話者名=登録した名前）</summary>
    private void ShowMealLine(string line)
    {
        if (string.IsNullOrEmpty(line) || DialogueUI.Instance == null) return;
        string speaker = SaveDataManager.Instance != null && !string.IsNullOrEmpty(SaveDataManager.Instance.PlayerName)
            ? SaveDataManager.Instance.PlayerName : "わたし";
        DialogueUI.Instance.ShowLines(speaker, new[] { line }, null, null, false, DialogueUI.PortraitSide.Left);
    }

    private void OnFocus()
    {
        Close();
        // 2026-07-27: 「今日はどんな一日にしたいですか？」の問いかけは廃止。
        // 方針の設定はWeeklyタブに一本化し、儀式ループ（注文→着席→フォーカス突入）に
        // 余計な問いを挟まない。PolicyPromptUI は呼び出しを外しただけで残してあるので、
        // 復活させたい場合はここで OpenOrPass を呼び戻せばよい。
        if (SceneRouter.Instance != null) SceneRouter.Instance.EnterFocus();
    }

    private void OnStand()
    {
        var seat = _seat;
        Close();
        if (seat != null) seat.RequestStandUp();
    }
}
