namespace TicTacToe.WebMultiplayer.Tests;

// Time and callback dispatch are controlled separately to model delayed/queued timers.
internal sealed class ManualTimeProvider : TimeProvider
{
    private long _ticks = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).Ticks;
    private readonly List<ManualTimer> _timers = [];

    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);
    public void Advance(TimeSpan duration) => Interlocked.Add(ref _ticks, duration.Ticks);
    public ManualTimer LastTimer { get { lock (_timers) return _timers[^1]; } }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(callback, state);
        lock (_timers) _timers.Add(timer);
        return timer;
    }

    internal sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
    {
        public bool IsDisposed { get; private set; }
        // Disposing a real timer does not cancel a callback that has already been queued.
        public void FireQueuedCallback() => callback(state);
        public bool Change(TimeSpan dueTime, TimeSpan period) => !IsDisposed;
        public void Dispose() => IsDisposed = true;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
