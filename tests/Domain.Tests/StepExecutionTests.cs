using Domain;
using FluentAssertions;
using System;
using Xunit;

namespace Domain.Tests;

public class StepExecutionTests
{
    [Fact]
    public void Fail_ShouldSetFailedState()
    {
        var step = new StepExecution();
        step.StartAttempt(DateTime.UtcNow.AddSeconds(-1));
        step.Fail(DateTime.UtcNow, "boom");

        step.Status.Should().Be(StepExecutionStatus.Failed);
        step.Error.Should().Be("boom");
        step.DurationMs.Should().NotBeNull();
    }
}
