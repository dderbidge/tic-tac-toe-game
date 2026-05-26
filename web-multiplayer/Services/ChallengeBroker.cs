using System;
using System.Collections.Generic;
using System.Threading;

namespace TicTacToe.WebMultiplayer.Services;

public sealed record Challenge(
    string ChallengeId,
    string FromConnId,
    string FromName,
    string ToConnId,
    string ToName,
    DateTimeOffset ExpiresAt
);

public sealed class ChallengeBroker : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Challenge> _challenges = new();
    private readonly Timer _sweeper;
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromSeconds(30);

    public event Action<Challenge>? Expired;

    public ChallengeBroker()
    {
        _sweeper = new Timer(_ => SweepExpired(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public Challenge Create(string fromConn, string fromName, string toConn, string toName)
    {
        var c = new Challenge(
            ChallengeId: Guid.NewGuid().ToString("N")[..10],
            FromConnId: fromConn,
            FromName: fromName,
            ToConnId: toConn,
            ToName: toName,
            ExpiresAt: DateTimeOffset.UtcNow + Lifetime);
        lock (_gate) _challenges[c.ChallengeId] = c;
        return c;
    }

    public Challenge? Take(string challengeId)
    {
        lock (_gate)
        {
            if (_challenges.Remove(challengeId, out var c)) return c;
            return null;
        }
    }

    public bool Cancel(string challengeId, string fromConnId)
    {
        lock (_gate)
        {
            if (_challenges.TryGetValue(challengeId, out var c) && c.FromConnId == fromConnId)
                return _challenges.Remove(challengeId);
            return false;
        }
    }

    public void RemoveAllInvolving(string connectionId)
    {
        List<Challenge> toRemove = new();
        lock (_gate)
        {
            foreach (var c in _challenges.Values)
                if (c.FromConnId == connectionId || c.ToConnId == connectionId)
                    toRemove.Add(c);
            foreach (var c in toRemove) _challenges.Remove(c.ChallengeId);
        }
    }

    private void SweepExpired()
    {
        var now = DateTimeOffset.UtcNow;
        List<Challenge> expired = new();
        lock (_gate)
        {
            foreach (var (id, c) in _challenges)
                if (c.ExpiresAt <= now) expired.Add(c);
            foreach (var c in expired) _challenges.Remove(c.ChallengeId);
        }
        foreach (var c in expired) Expired?.Invoke(c);
    }

    public void Dispose() => _sweeper.Dispose();
}
