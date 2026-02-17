using FluentAssertions;
using Infrastructure;
using Infrastructure.Execution;
using System.Collections.Generic;
using Xunit;

namespace Application.Tests;

public class ConditionEvaluatorTests
{
    [Fact]
    public void Evaluate_ShouldResolveBooleanExpression()
    {
        var evaluator = new NCalcConditionEvaluator();
        var result = evaluator.Evaluate("amount > 100 and urgent == true", new Dictionary<string, object?>
        {
            ["amount"] = 150,
            ["urgent"] = true
        });

        result.Should().BeTrue();
    }
}
