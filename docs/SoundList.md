# TOKYO-CORNER サウンドリスト

SE / BGM / 環境音 / ジングルの棚卸し。  
ファイルは `Assets/Audio/` 以下に配置し、以下の命名規則に従う。

---

## 命名規則

| 種別 | プレフィックス | 例 |
|------|---------------|-----|
| SE（効果音） | `se_` | `se_ui_decide` |
| BGM | `bgm_` | `bgm_cafe_afternoon` |
| 環境音（ループ） | `amb_` | `amb_cafe_crowd` |
| ジングル（短曲） | `jingle_` | `jingle_opening_complete` |

サブカテゴリはアンダースコア区切りで続ける（`se_<カテゴリ>_<名前>`）。

---

## 1. 共通UI

| ID | ファイル名 | 説明 | 状態 |
|----|-----------|------|------|
| UI-01 | `se_ui_decide` | 決定・OK ボタン | 未実装 |
| UI-02 | `se_ui_cancel` | キャンセル・戻る | 未実装 |
| UI-03 | `se_ui_cursor` | カーソル移動（◀▶） | 未実装 |
| UI-04 | `se_ui_open` | パネル展開 | 未実装 |
| UI-05 | `se_ui_close` | パネル閉じ | 未実装 |

---

## 2. 会話（ADV テキスト）

| ID | ファイル名 | 説明 | 状態 |
|----|-----------|------|------|
| DLG-01 | `se_dlg_type` | タイプライター 1文字 | 未実装 |
| DLG-02 | `se_dlg_advance` | テキスト送り（▼クリック） | 未実装 |
| DLG-03 | `se_dlg_name_appear` | 話者名プレート出現 | 未実装 |

---

## 3. オープニング

| ID | ファイル名 | 説明 | 状態 |
|----|-----------|------|------|
| OP-01 | `jingle_opening_complete` | 登録完了・カフェ遷移前 | 未実装 |
| OP-02 | `se_opening_wave` | ナギのバイバイ（Wave） | 未実装 |
| OP-03 | `se_opening_cheer` | アバターの小躍り（Cheer） | 未実装 |
| OP-04 | `bgm_opening` | オープニングシーン BGM | 未実装 |

---

## 4. カフェ（自由移動モード）

| ID | ファイル名 | 説明 | 状態 |
|----|-----------|------|------|
| CAFE-01 | `bgm_cafe_morning` | 午前帯 BGM | 未実装 |
| CAFE-02 | `bgm_cafe_afternoon` | 午後帯 BGM | 未実装 |
| CAFE-03 | `amb_cafe_crowd` | 店内ざわめき（ループ） | 未実装 |
| CAFE-04 | `se_cafe_footstep` | 足音（プレイヤー） | 未実装 |
| CAFE-05 | `se_cafe_door_open` | ドア開閉 | 未実装 |
| CAFE-06 | `se_purchase_coin` | コイン支払い | 未実装 |
| CAFE-07 | `se_purchase_get` | アイテム取得 | 未実装 |
| CAFE-08 | `se_drink_sip` | 一口飲む（F キー） | 未実装 |
| CAFE-09 | `se_food_bite` | 一口食べる | 未実装 |
| CAFE-10 | `se_trash_discard` | ゴミ箱廃棄 | 未実装 |

---

## 5. フォーカスモード（ポモドーロ）

| ID | ファイル名 | 説明 | 状態 |
|----|-----------|------|------|
| FOC-01 | `se_focus_start` | 着席・タイマー開始 | 未実装 |
| FOC-02 | `se_focus_end` | タイマー終了（作業フェーズ） | 未実装 |
| FOC-03 | `se_focus_break_start` | 休憩フェーズ開始 | 未実装 |
| FOC-04 | `jingle_focus_complete` | 全ラウンド完了 | 未実装 |
| FOC-05 | `se_focus_tick` | 残り 10 秒のカウントダウン | 未実装 |
| FOC-06 | `bgm_focus_lo_fi` | フォーカス中 BGM（Lo-Fi 系） | 未実装 |
| FOC-07 | `amb_focus_rain` | 雨音（環境音オプション） | 未実装 |
