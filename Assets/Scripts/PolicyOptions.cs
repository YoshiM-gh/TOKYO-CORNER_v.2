/// <summary>
/// 一日の方針（さくせん）の選択肢。単一ソース。
/// 使用箇所: WeeklyCalendarUI / DailyCalendarUI の方針行、PolicyPromptUI（着席→フォーカス入場時の問いかけ）。
/// 先頭の空文字列 = 未設定。UIのサイクル順もこの並び。追加・変更はこのファイルだけを編集する。
/// </summary>
public static class PolicyOptions
{
    public static readonly string[] All =
    {
        "",
        "ガンガンいこうぜ",
        "しっかりマイペース",
        "いろいろやろうぜ",
        "ととのえていこうぜ",
        "かいふくゆうせん",
        "ともだちだいじに",
        "かぞくをだいじに",
        "じぶんをだいじに",
        "こいびとだいじに",
    };
}
