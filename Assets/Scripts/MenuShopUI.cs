using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// メニュー購入UI（Cafeシーン / MenuShopCanvas）。
/// 入口: カフェスタッフ（Waiter）に近づいてクリック＝話しかけ（PurchaseInteractable）。
/// 購入フロー: 行ホバーで▶＋ハイライト（MenuRowHighlight）→ 温度選択（対象品のみ）→ 購入クリック
///  → ドリンク保有中なら案内バー（OKのみ）／未保有なら「本当に〜?」確認（はい/いいえ）
///  → はい=購入成立でメニューを閉じる（終了）／いいえ=メニューに戻る。
/// 子を「名前」で探して結線する:
///   Panel > Box > TitleLabel / CoinLabel / TabDrinkButton / TabFoodButton
///   / ListContent > RowTemplate(CursorLabel/NameLabel/...) 
///   / ConfirmBar(ConfirmLabel, ConfirmYesButton, ConfirmNoButton) / CloseButton
/// フードは在庫未実装のため表示のみ（3bで解放）。ドリンクは1杯制（DrinkInventory.MaxDrinks=1）。
/// </summary>
public class MenuShopUI : MonoBehaviour
{
    public static MenuShopUI Instance { get; private set; }

    [SerializeField] private MenuDatabase menuDatabase;

    private GameObject _panel;
    private TMPro.TMP_Text _coinLabel;
    private Button _tabDrinkBtn, _tabFoodBtn, _closeBtn;
    private TMPro.TMP_Text _tabDrinkLabel, _tabFoodLabel;
    private Transform _listContent;
    private GameObject _rowTemplate;
    private GameObject _confirmBar;
    private TMPro.TMP_Text _confirmLabel;
    private Button _confirmYesBtn, _confirmNoBtn;

    private MenuCategory _tab = MenuCategory.Drink;
    private bool _hotSelected = true;
    private MenuItemDef _pendingDef;
    private bool _pendingHot;
    private readonly List<GameObject> _rows = new();

    private static readonly Color ColActive = new Color32(232, 237, 242, 255);
    private static readonly Color ColMuted = new Color32(139, 152, 165, 255);
    private static readonly Color ColBuy = new Color32(127, 191, 127, 255);
    private static readonly Color ColDisabled = new Color32(85, 96, 107, 255);
    private static readonly Color ColTabOn = new Color32(58, 69, 82, 255);

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

    private void Update()
    {
        if (_panel == null || Keyboard.current == null) return;
        if (_panel.activeSelf && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (_confirmBar != null && _confirmBar.activeSelf) CancelConfirm();
            else Close();
        }
    }

    private bool IsSeatMenuOpen() =>
        SeatMenuController.Instance != null && SeatMenuController.Instance.IsOpen;

    // ── 開閉 ──────────────────────────────────────────

    public void Open()
    {
        if (_panel == null || IsSeatMenuOpen()) return;
        _tab = MenuCategory.Drink;
        CancelConfirm();
        _panel.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        CancelConfirm();
        if (_panel != null) _panel.SetActive(false);
    }

    private void SetTab(MenuCategory tab)
    {
        CancelConfirm();
        _tab = tab;
        Refresh();
    }

    // ── 表示更新 ──────────────────────────────────────

    private void Refresh()
    {
        RefreshHeader();
        RebuildRows();
    }

    private void RefreshHeader()
    {
        int coins = SaveDataManager.Instance != null ? SaveDataManager.Instance.GetCoins() : 0;
        if (_coinLabel != null) _coinLabel.text = coins + "C";
        bool drink = _tab == MenuCategory.Drink;
        if (_tabDrinkBtn != null && _tabDrinkBtn.image != null) _tabDrinkBtn.image.color = drink ? ColTabOn : Color.clear;
        if (_tabFoodBtn != null && _tabFoodBtn.image != null) _tabFoodBtn.image.color = drink ? Color.clear : ColTabOn;
        if (_tabDrinkLabel != null) _tabDrinkLabel.color = drink ? ColActive : ColMuted;
        if (_tabFoodLabel != null) _tabFoodLabel.color = drink ? ColMuted : ColActive;
    }

    private void RebuildRows()
    {
        foreach (var r in _rows) if (r != null) { r.SetActive(false); Destroy(r); } // Destroyは同フレーム残留するため先に非表示化
        _rows.Clear();
        if (menuDatabase == null || _rowTemplate == null || _listContent == null) return;

        int coins = SaveDataManager.Instance != null ? SaveDataManager.Instance.GetCoins() : 0;
        foreach (var def in menuDatabase.GetByCategory(_tab))
        {
            var row = Instantiate(_rowTemplate, _listContent);
            row.name = "Row_" + def.id;
            row.SetActive(true);
            BuildRow(row.transform, def, coins);
            _rows.Add(row);
        }
    }

    private void BuildRow(Transform row, MenuItemDef def, int coins)
    {
        var nameLabel = FindTextIn(row, "NameLabel");
        var portions = FindTextIn(row, "PortionsLabel");
        var price = FindTextIn(row, "PriceLabel");
        var hotT = FindDeep(row, "HotButton");
        var iceT = FindDeep(row, "IceButton");
        var buyT = FindDeep(row, "BuyButton");
        var buyBtn = buyT != null ? buyT.GetComponent<Button>() : null;
        var buyLabel = buyT != null ? buyT.GetComponentInChildren<TMPro.TMP_Text>(true) : null;

        string dn = SaveDataManager.Instance != null ? SaveDataManager.Instance.ResolveDisplayName(def) : def.displayName;
        if (nameLabel != null) nameLabel.text = dn;
        if (portions != null) portions.text = new string('●', Mathf.Clamp(def.portions, 1, 8));
        if (price != null) price.text = def.price + "C";

        bool isDrink = def.category == MenuCategory.Drink;
        bool canAfford = coins >= def.price;
        bool buyable = canAfford; // 保有中でも押せる（押下時に案内を出す）

        if (buyT != null) buyT.gameObject.SetActive(true);
        if (buyBtn != null)
        {
            buyBtn.interactable = buyable;
            buyBtn.onClick.RemoveAllListeners();
            var captured = def;
            buyBtn.onClick.AddListener(() => RequestBuy(captured));
        }
        if (buyLabel != null) buyLabel.color = buyable ? ColBuy : ColDisabled;
        if (price != null) price.color = canAfford ? ColActive : ColMuted;

        bool showTemp = isDrink && def.hasTemperature;
        if (hotT != null) hotT.gameObject.SetActive(showTemp);
        if (iceT != null) iceT.gameObject.SetActive(showTemp);
        if (showTemp)
        {
            WireTempButton(hotT, true);
            WireTempButton(iceT, false);
            RefreshTempVisual(hotT, iceT);
        }
    }

    private void WireTempButton(Transform t, bool hot)
    {
        var b = t != null ? t.GetComponent<Button>() : null;
        if (b == null) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() => { _hotSelected = hot; CancelConfirm(); Refresh(); });
    }

    private void RefreshTempVisual(Transform hotT, Transform iceT)
    {
        var hl = hotT != null ? hotT.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
        var il = iceT != null ? iceT.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
        if (hl != null) hl.color = _hotSelected ? ColActive : ColMuted;
        if (il != null) il.color = _hotSelected ? ColMuted : ColActive;
        var hi = hotT != null ? hotT.GetComponent<Image>() : null;
        var ii = iceT != null ? iceT.GetComponent<Image>() : null;
        if (hi != null) hi.color = _hotSelected ? ColTabOn : Color.clear;
        if (ii != null) ii.color = _hotSelected ? Color.clear : ColTabOn;
    }

    // ── 購入（案内 / 確認 の2モードバー） ──────────────

    private void RequestBuy(MenuItemDef def)
    {
        _pendingDef = def;
        _pendingHot = _hotSelected;
        if (_confirmBar == null || _confirmLabel == null)
        {
            ExecuteConfirmedBuy(); // 確認バー未配置時のフォールバック: 即購入
            return;
        }
        bool drinkHeld = def.category == MenuCategory.Drink && DrinkInventory.Instance != null && !DrinkInventory.Instance.CanPurchase();
        bool foodHeld = def.category == MenuCategory.Food && FoodInventory.Instance != null && !FoodInventory.Instance.CanPurchase();
        if (drinkHeld || foodHeld)
        {
            _pendingDef = null; // 案内のみ・購入予約なし
            _confirmLabel.text = drinkHeld
                ? "ドリンクは一杯ずつ。飲み切ってから次を購入してくださいね。"
                : "フードは一皿ずつ。食べ切ってから次を購入してくださいね。";
            SetBarMode(true);
        }
        else
        {
            _confirmLabel.text = "本当に " + PendingDisplayName() + "（" + def.price + "C）を購入しますか？";
            SetBarMode(false);
        }
        _confirmBar.SetActive(true);
    }

    private void SetBarMode(bool infoOnly)
    {
        if (_confirmYesBtn != null)
        {
            _confirmYesBtn.gameObject.SetActive(!infoOnly);
            var yl = _confirmYesBtn.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (yl != null) yl.text = "はい";
        }
        if (_confirmNoBtn != null)
        {
            var nl = _confirmNoBtn.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (nl != null) nl.text = infoOnly ? "OK" : "いいえ";
        }
    }

    private string PendingDisplayName()
    {
        if (_pendingDef == null) return "";
        string dn = SaveDataManager.Instance != null ? SaveDataManager.Instance.ResolveDisplayName(_pendingDef) : _pendingDef.displayName;
        if (_pendingDef.hasTemperature) dn += _pendingHot ? "（ホット）" : "（アイス）";
        return dn;
    }

    private void ExecuteConfirmedBuy()
    {
        var def = _pendingDef;
        bool hot = _pendingHot;
        CancelConfirm();
        if (def == null || SaveDataManager.Instance == null) { Refresh(); return; }
        if (def.category == MenuCategory.Drink && DrinkInventory.Instance != null && !DrinkInventory.Instance.CanPurchase()) { Refresh(); return; }
        if (def.category == MenuCategory.Food && FoodInventory.Instance != null && !FoodInventory.Instance.CanPurchase()) { Refresh(); return; }
        if (!SaveDataManager.Instance.TryPurchase(def)) { Refresh(); return; }

        string acquiredName = SaveDataManager.Instance.ResolveDisplayName(def);
        if (def.category == MenuCategory.Drink && DrinkInventory.Instance != null)
        {
            if (def.hasTemperature) acquiredName += hot ? "（ホット）" : "（アイス）";
            DrinkInventory.Instance.AddDrink(def.id, acquiredName, def.portions);
        }
        else if (def.category == MenuCategory.Food && FoodInventory.Instance != null)
        {
            FoodInventory.Instance.AddFood(def.id, acquiredName, def.portions);
        }
        Close(); // はい=購入成立 → メニューを閉じて終了
        if (AcquireToastUI.Instance != null) AcquireToastUI.Instance.Show(acquiredName);
        if (PlayerEmote.Instance != null) PlayerEmote.Instance.Show("♪");
    }

    private void CancelConfirm()
    {
        _pendingDef = null;
        if (_confirmBar != null) _confirmBar.SetActive(false);
    }

    // ── 名前検索ヘルパー ──────────────────────────────

    private TMPro.TMP_Text FindTextIn(Transform root, string goName)
    {
        var t = FindDeep(root, goName);
        return t != null ? t.GetComponent<TMPro.TMP_Text>() : null;
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

    private void Bind()
    {
        var p = FindDeep(transform, "Panel");
        _panel = p != null ? p.gameObject : null;
        var ct = FindDeep(transform, "CoinLabel");
        _coinLabel = ct != null ? ct.GetComponent<TMPro.TMP_Text>() : null;
        _tabDrinkBtn = FindButton("TabDrinkButton", out _tabDrinkLabel);
        _tabFoodBtn = FindButton("TabFoodButton", out _tabFoodLabel);
        _closeBtn = FindButton("CloseButton", out _);
        _listContent = FindDeep(transform, "ListContent");
        var rt = FindDeep(transform, "RowTemplate");
        _rowTemplate = rt != null ? rt.gameObject : null;
        if (_rowTemplate != null) _rowTemplate.SetActive(false);
        var cb = FindDeep(transform, "ConfirmBar");
        _confirmBar = cb != null ? cb.gameObject : null;
        _confirmLabel = FindTextIn(transform, "ConfirmLabel");
        _confirmYesBtn = FindButton("ConfirmYesButton", out _);
        _confirmNoBtn = FindButton("ConfirmNoButton", out _);

        if (_tabDrinkBtn != null) _tabDrinkBtn.onClick.AddListener(() => SetTab(MenuCategory.Drink));
        if (_tabFoodBtn != null) _tabFoodBtn.onClick.AddListener(() => SetTab(MenuCategory.Food));
        if (_closeBtn != null) _closeBtn.onClick.AddListener(Close);
        if (_confirmYesBtn != null) _confirmYesBtn.onClick.AddListener(ExecuteConfirmedBuy);
        if (_confirmNoBtn != null) _confirmNoBtn.onClick.AddListener(CancelConfirm);
        if (_confirmBar != null) _confirmBar.SetActive(false);
    }
}
