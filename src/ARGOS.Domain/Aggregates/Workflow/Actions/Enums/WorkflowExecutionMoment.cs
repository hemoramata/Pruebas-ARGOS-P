namespace ARGOS.Domain.Aggregates.Workflow.Actions.Enums;

public enum WorkflowExecutionMoment
{
    BeforeStart = 1,
    AfterStart = 2,
    BeforeFinish = 3,
    AfterFinish = 4,
    OnError = 5
}