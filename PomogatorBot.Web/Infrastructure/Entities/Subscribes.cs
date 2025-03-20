namespace PomogatorBot.Web.Infrastructure.Entities;

[Flags]
public enum Subscribes
{
    None = 0,
    Streams = 1,
    Menasi = 1 << 1,
    DobroeUtro = 1 << 2,
    SpokiNoki = 1 << 3,
    All = Streams | Menasi | DobroeUtro | SpokiNoki,
}
