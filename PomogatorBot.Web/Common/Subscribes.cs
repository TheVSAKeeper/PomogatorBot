using System.ComponentModel;

namespace PomogatorBot.Web.Common;

// TODO: Подумать над вынесением иконки из названия
[Flags]
public enum Subscribes
{
    [Description("Нет подписок")]
    None = 0,

    [SubscriptionMeta("🎮 Стримы", "#00bcd4")]
    Streams = 1,

    [SubscriptionMeta("🎨 Menasi", "#ff6699")]
    Menasi = 1 << 1,

    [SubscriptionMeta("🌅 Доброе утро", "#ffcc00")]
    DobroeUtro = 1 << 2,

    [SubscriptionMeta("🌙 Spoki Noki", "#9c27b0")]
    SpokiNoki = 1 << 3,

    [Description("Все подписки")]
    All = Streams | Menasi | DobroeUtro | SpokiNoki,
}
