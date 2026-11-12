namespace ARGOS.Domain.Aggregates.Workflow.Actions.Enums;

public enum WorkflowActionType
{
    GenerateDocument = 1,
    SendEmail = 2,
    SendNotification = 3,
    CreateTask = 4,
    RegisterAudit = 5,
    GeneratePdf = 6,
    DigitalSignature = 7,
    InvokeWebService = 8,
    Custom = 9
}