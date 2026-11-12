using System.Collections.Concurrent;

namespace ARGOS.Application.Security;

/// <summary>Single-node session registry. Restart and logout invalidate cookies.</summary>
public sealed class ActiveSessions(TimeProvider clock)
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan AbsoluteTimeout = TimeSpan.FromHours(8);
    private readonly ConcurrentDictionary<Guid, Session> sessions = new();
    private sealed record Session(Guid Person, DateTimeOffset IssuedAt, DateTimeOffset LastActivity);

    public void Add(Guid session, Guid person)
    {
        foreach (var entry in sessions.Where(x => !IsLive(x.Value, clock.GetUtcNow())))
            ((ICollection<KeyValuePair<Guid, Session>>)sessions).Remove(entry);
        var now = clock.GetUtcNow();
        sessions[session] = new(person, now, now);
    }

    public bool Contains(Guid session, Guid person) => sessions.TryGetValue(session, out var entry)
        && entry.Person == person && IsLive(entry, clock.GetUtcNow());

    /// <summary>Validates and records authenticated activity without extending the absolute limit.</summary>
    public bool Touch(Guid session, Guid person)
    {
        while (sessions.TryGetValue(session, out var entry))
        {
            var now = clock.GetUtcNow();
            if (entry.Person != person) return false;
            if (!IsLive(entry, now))
            {
                if (!((ICollection<KeyValuePair<Guid, Session>>)sessions).Remove(new(session, entry))) continue;
                return false;
            }
            if (sessions.TryUpdate(session, entry with { LastActivity = now }, entry)) return true;
        }
        return false;
    }

    public void Revoke(Guid session) => sessions.TryRemove(session, out _);

    public void RevokeForPerson(Guid person)
    {
        foreach (var entry in sessions.Where(x => x.Value.Person == person))
            ((ICollection<KeyValuePair<Guid, Session>>)sessions).Remove(entry);
    }

    public SessionRemaining? Remaining(Guid session, Guid person)
    {
        var now = clock.GetUtcNow();
        if (!sessions.TryGetValue(session, out var entry) || entry.Person != person || !IsLive(entry, now)) return null;
        return new((entry.LastActivity + IdleTimeout - now).TotalSeconds,
            (entry.IssuedAt + AbsoluteTimeout - now).TotalSeconds);
    }

    private static bool IsLive(Session entry, DateTimeOffset now) =>
        now - entry.LastActivity < IdleTimeout && now - entry.IssuedAt < AbsoluteTimeout;
}

public sealed record SessionRemaining(double IdleSeconds, double AbsoluteSeconds);
