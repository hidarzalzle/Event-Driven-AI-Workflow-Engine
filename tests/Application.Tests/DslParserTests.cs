using Application;
using FluentAssertions;
using Xunit;

namespace Application.Tests;

public class DslParserTests
{
    [Fact]
    public void Parse_ConditionDsl_ShouldResolve()
    {
        var json = "{\"name\":\"x\",\"trigger\":{\"type\":\"webhook\"},\"steps\":[{\"id\":\"c\",\"type\":\"condition\",\"expression\":\"1==1\",\"trueNext\":\"a\",\"falseNext\":\"b\"},{\"id\":\"a\",\"type\":\"delay\",\"delaySeconds\":1},{\"id\":\"b\",\"type\":\"delay\",\"delaySeconds\":1}]}";
        var dsl = WorkflowDslParser.Parse(json);
        dsl.Steps.Should().HaveCount(3);
    }
}
