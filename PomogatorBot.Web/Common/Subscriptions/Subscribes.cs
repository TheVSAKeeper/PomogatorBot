namespace PomogatorBot.Web.Common.Subscriptions;

[Flags]
public enum Subscribes
{
    [SubscriptionMeta("Нет подписок", "#808080", "🚫")]
    None = 0,

    [SubscriptionMeta("Образовательные трансляции", "#00bcd4", "☕")]
    EducationStreams = 1,

    [SubscriptionMeta("Игровые трансляции", "#ff6699", "🎮")]
    GameStreams = 1 << 1,

    [SubscriptionMeta("Кино с Максимчиком", "#ff9800", "🎬")]
    MovieWithMaximchik = 1 << 2,

    [SubscriptionMeta("Все подписки", "#4CAF50", "✅")]
    All = EducationStreams | GameStreams | MovieWithMaximchik,
}
