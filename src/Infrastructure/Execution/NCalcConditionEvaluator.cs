using Application;
using NCalc;

namespace Infrastructure.Execution;

public sealed class NCalcConditionEvaluator : IConditionEvaluator
{
    public bool Evaluate(string expression, IDictionary<string, object?> data)
    {
        var expr = new Expression(expression, ExpressionOptions.NoCache | ExpressionOptions.IgnoreCaseAtBuiltInFunctions);
        foreach (var kv in data)
        {
            if (kv.Value is string or int or long or double or decimal or bool || kv.Value is null)
                expr.Parameters[kv.Key] = kv.Value;
        }

        return Convert.ToBoolean(expr.Evaluate());
    }
}
