using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

static class DinnerWorkflow
{
    public const int MaxRounds = 3;

    public static Workflow Build(AIAgent planner, AIAgent peanutChecker)
    {
        var plan = new Planner(planner);
        var peanuts = new PeanutCheck(peanutChecker);
        Func<PlanDraft, RuleCheck> checkFish = HouseholdRules.CheckFish;
        ExecutorBinding fish = checkFish.BindAsExecutor("fish");
        Func<PlanDraft, RuleCheck> checkTime = HouseholdRules.CheckWeekdayTime;
        ExecutorBinding weekdayTime = checkTime.BindAsExecutor("weekday-time");
        var combine = new CombineChecks();
        var result = new PlanResult();

        return new WorkflowBuilder(plan)
            .AddFanOutEdge(plan, [peanuts, fish, weekdayTime])
            .AddFanInBarrierEdge([peanuts, fish, weekdayTime], combine)
            .AddEdge<Review>(combine, plan,
                condition: r => r is { Passed: false, Draft.Round: < MaxRounds })
            .AddEdge<Review>(combine, result,
                condition: r => r is { Passed: true } or { Draft.Round: >= MaxRounds })
            .WithOutputFrom(result)
            .Build();
    }
}
