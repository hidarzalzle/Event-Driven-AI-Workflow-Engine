using Application;
using FluentAssertions;
using System;
using Xunit;

namespace Application.Tests;

public class RetryCalculatorTests
{
    [Fact]
    public void Next_ShouldIncreaseWithAttempts()
    {
        var policy = new RetryPolicyDsl(MaxAttempts: 5, InitialDelayMs: 100, BackoffFactor: 2, Jitter: false);
        var now = DateTime.UtcNow;

        var first = RetryCalculator.Next(policy, 1, now);
        var second = RetryCalculator.Next(policy, 2, now);

        (second - first).TotalMilliseconds.Should().BeGreaterThan(0);
        (first - now).TotalMilliseconds.Should().BeApproximately(100, 2);
        (second - now).TotalMilliseconds.Should().BeApproximately(200, 2);
    }
}
