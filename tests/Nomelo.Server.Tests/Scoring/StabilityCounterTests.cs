using FluentAssertions;
using Nomelo.Server.Scoring;

namespace Nomelo.Server.Tests.Scoring;

public class StabilityCounterTests
{
    [Fact]
    public void Empty_history_returns_zero()
        => StabilityCounter.ConsecutiveNonUpsets(Array.Empty<bool>()).Should().Be(0);

    [Fact]
    public void All_non_upsets_returns_full_length()
        => StabilityCounter.ConsecutiveNonUpsets(new[] { false, false, false }).Should().Be(3);

    [Fact]
    public void Counts_from_most_recent_when_latest_first()
    {
        // most-recent-first: false, false, true, false
        StabilityCounter.ConsecutiveNonUpsets(new[] { false, false, true, false }).Should().Be(2);
    }

    [Fact]
    public void Stops_at_first_upset()
    {
        StabilityCounter.ConsecutiveNonUpsets(new[] { true, false, false }).Should().Be(0);
    }
}
