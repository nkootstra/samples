using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

static class DinnerWorkflow
{
    public static Workflow Build(AIAgent classifier, AIAgent cook, AIAgent planner)
    {
        var classify = new Classify(classifier);
        var quick = new QuickDinner(cook);
        var plan = new Planner(planner);
        Func<DinnerRequest, string> declineRequest = _ =>
            "I only plan dinners. Ask me for tonight's dinner or for a few days.";
        ExecutorBinding decline = declineRequest.BindAsExecutor("decline");

        return new WorkflowBuilder(classify)
            .AddSwitch(classify, route => route
                .AddCase<DinnerRequest>(
                    r => r?.Kind == RequestKind.QuickDinner, quick)
                .AddCase<DinnerRequest>(
                    r => r?.Kind == RequestKind.WeekPlan, plan)
                .WithDefault(decline))
            .WithOutputFrom(quick, plan, decline)
            .Build();
    }
}
