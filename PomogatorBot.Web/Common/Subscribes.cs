namespace PomogatorBot.Web.Common;

[Flags]
public enum Subscribes
{
    [SubscriptionMeta("Нет подписок", "#808080", "🚫")]
    None = 0,

    [SubscriptionMeta("Стримы", "#00bcd4", "🎮")]
    Streams = 1,

    [SubscriptionMeta("Menasi", "#ff6699", "🎨")]
    Menasi = 1 << 1,

    [SubscriptionMeta("Доброе утро", "#ffcc00", "🌅")]
    DobroeUtro = 1 << 2,

    [SubscriptionMeta("Spoki Noki", "#9c27b0", "🌙")]
    SpokiNoki = 1 << 3,

    [SubscriptionMeta("Все подписки", "#4CAF50", "✅")]
    All = Streams | Menasi | DobroeUtro | SpokiNoki,
}
