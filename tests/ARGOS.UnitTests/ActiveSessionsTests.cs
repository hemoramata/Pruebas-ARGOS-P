using ARGOS.Application.Security;

namespace ARGOS.UnitTests;

public sealed class ActiveSessionsTests
{
    [Fact]
    public void IdleSessionIsRejectedAfterThirtyMinutes()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var sessions = new ActiveSessions(clock);
        var session = Guid.NewGuid();
        var person = Guid.NewGuid();
        sessions.Add(session, person);
        clock.Advance(TimeSpan.FromMinutes(30).Add(TimeSpan.FromSeconds(1)));
        Assert.False(sessions.Touch(session, person));
    }

    [Fact]
    public void ActivityDoesNotExtendEightHourAbsoluteLimit()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var sessions = new ActiveSessions(clock);
        var session = Guid.NewGuid();
        var person = Guid.NewGuid();
        sessions.Add(session, person);
        for (var interval = 0; interval < 16; interval++)
        {
            clock.Advance(TimeSpan.FromMinutes(29));
            Assert.True(sessions.Touch(session, person));
        }
        clock.Advance(TimeSpan.FromMinutes(17).Add(TimeSpan.FromSeconds(1)));
        Assert.False(sessions.Touch(session, person));
    }

    private sealed class TestClock(DateTimeOffset current) : TimeProvider
    {
        private DateTimeOffset current = current;
        public override DateTimeOffset GetUtcNow() => current;
        public void Advance(TimeSpan amount) => current += amount;
    }
}
