using Domain;
using FluentAssertions;
using System;
using Xunit;

namespace Domain.Tests;

public class WorkflowInstanceTests
{
    [Fact]
    public void Start_FromPending_ShouldRun()
    {
        var i = new WorkflowInstance();
        i.Start(DateTime.UtcNow);
        i.Status.Should().Be(WorkflowInstanceStatus.Running);
    }

    [Fact]
    public void InvalidTransition_ShouldThrow()
    {
        var i = new WorkflowInstance();
        i.Complete(DateTime.UtcNow);
        var act = () => i.Start(DateTime.UtcNow);
        act.Should().Throw<InvalidOperationException>();
    }
}
