using ARGOS.Domain.Aggregates.Procedure.Enums;
using ARGOS.Domain.Aggregates.Procedure.Errors;
using ARGOS.Domain.Aggregates.Procedure.Events;
using ARGOS.Domain.Aggregates.Procedure.ValueObjects;
using ARGOS.SharedKernel.Abstractions;
using ARGOS.SharedKernel.Results;

namespace ARGOS.Domain.Aggregates.Procedure;

/// <summary>
/// Represents an institutional procedure.
///
/// A Procedure defines WHAT the institution offers,
/// not HOW it is executed.
/// The execution is defined by one or more Workflows.
/// </summary>
public sealed class Procedure : AggregateRoot<ProcedureId>
{
    private readonly List<ProcedureDecision> _decisions = [];

    /// <summary>
    /// Stable identity shared by every version of this procedure.
    /// </summary>
    public ProcedureDefinitionId DefinitionId { get; private set; } = null!;

    /// <summary>
    /// Sequential version number inside the procedure definition.
    /// </summary>
    public int Version { get; private set; }

    /// <summary>
    /// Previous chronological revision, whether published or rejected.
    /// </summary>
    public ProcedureId? PreviousVersionId { get; private set; }

    /// <summary>
    /// Next chronological revision, whether published or rejected.
    /// </summary>
    public ProcedureId? NextVersionId { get; private set; }

    /// <summary>
    /// Internal procedure code.
    /// </summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>
    /// Procedure name.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Procedure description.
    /// </summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// Current procedure status.
    /// </summary>
    public ProcedureStatus Status { get; private set; }

    /// <summary>
    /// Approval policy snapshot used for this version after it enters review.
    /// </summary>
    public string? ApprovalPolicyVersion { get; private set; }

    public int RequiredApprovalCount { get; private set; }

    public bool SeparationOfDutiesRequired { get; private set; }

    public bool EmergencyPublicationAllowed { get; private set; }

    public bool EmergencyRatificationRequired { get; private set; }

    public bool EmergencyRatificationPending { get; private set; }

    public DateTime? EmergencyRatificationDueAtUtc { get; private set; }

    /// <summary>
    /// Complete, immutable record of lifecycle decisions over this version.
    /// </summary>
    public IReadOnlyCollection<ProcedureDecision> Decisions => _decisions.AsReadOnly();

    private Procedure()
    {
    }

    private Procedure(
        ProcedureId id,
        ProcedureDefinitionId definitionId,
        int version,
        ProcedureId? previousVersionId,
        string code,
        string name,
        string description)
        : base(id)
    {
        DefinitionId = definitionId;
        Version = version;
        PreviousVersionId = previousVersionId;
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        Description = description.Trim();

        Status = ProcedureStatus.Draft;

        AddDomainEvent(new ProcedureCreatedEvent(id));
    }

    /// <summary>
    /// Creates a new procedure.
    /// </summary>
    public static Procedure Create(
        string code,
        string name,
        string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new Procedure(
            ProcedureId.New(),
            ProcedureDefinitionId.New(),
            1,
            null,
            code,
            name,
            description);
    }

    /// <summary>
    /// Creates the next immutable version without changing the effective version.
    /// </summary>
    public Result<Procedure> CreateNewVersion(
        string name,
        string description)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(description))
            return Result<Procedure>.Failure(ProcedureErrors.InvalidVersionData);

        if (Status == ProcedureStatus.Archived)
            return Result<Procedure>.Failure(ProcedureErrors.ArchivedVersionCannotBeRevised);

        if (Status is not ProcedureStatus.Published and not ProcedureStatus.Rejected)
            return Result<Procedure>.Failure(ProcedureErrors.VersionCannotBeRevised);

        if (NextVersionId is not null)
            return Result<Procedure>.Failure(ProcedureErrors.VersionAlreadyHasSuccessor);

        var newVersion = new Procedure(
            ProcedureId.New(),
            DefinitionId,
            Version + 1,
            Id,
            Code,
            name,
            description);

        NextVersionId = newVersion.Id;

        newVersion.RaiseDomainEvent(new ProcedureVersionCreatedEvent(
            DefinitionId,
            newVersion.Id,
            newVersion.Version,
            Id));

        return Result<Procedure>.Success(newVersion);
    }

    /// <summary>
    /// Submits the immutable version for the approvals required by its policy.
    /// </summary>
    public Result SubmitForReview(
        int requiredApprovalCount,
        bool separationOfDutiesRequired,
        bool emergencyPublicationAllowed,
        bool emergencyRatificationRequired,
        ProcedureDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Status != ProcedureStatus.Draft)
            return Result.Failure(ProcedureErrors.InvalidTransition);

        if (requiredApprovalCount is < 1 or > 10
            || emergencyPublicationAllowed != emergencyRatificationRequired)
        {
            return Result.Failure(ProcedureErrors.InvalidApprovalPolicy);
        }

        ApprovalPolicyVersion = context.PolicyVersion;
        RequiredApprovalCount = requiredApprovalCount;
        SeparationOfDutiesRequired = separationOfDutiesRequired;
        EmergencyPublicationAllowed = emergencyPublicationAllowed;
        EmergencyRatificationRequired = emergencyRatificationRequired;

        RecordLifecycleDecision(
            ProcedureStatus.InReview,
            ProcedureDecisionType.SubmittedForReview,
            context);

        return Result.Success();
    }

    /// <summary>
    /// Records an approval and moves to Approved after reaching the configured count.
    /// </summary>
    public Result Approve(ProcedureDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Status != ProcedureStatus.InReview)
            return Result.Failure(ProcedureErrors.InvalidTransition);

        if (!UsesApprovalPolicy(context))
            return Result.Failure(ProcedureErrors.InvalidApprovalPolicy);

        var submitter = _decisions.Single(decision =>
            decision.Type == ProcedureDecisionType.SubmittedForReview);

        if (SeparationOfDutiesRequired
            && SameActor(submitter.ActorReference, context.ActorReference))
        {
            return Result.Failure(ProcedureErrors.SeparationOfDutiesRequired);
        }

        if (_decisions.Any(decision =>
            decision.Type == ProcedureDecisionType.Approved
            && SameActor(decision.ActorReference, context.ActorReference)))
        {
            return Result.Failure(ProcedureErrors.DuplicateApproval);
        }

        _decisions.Add(new ProcedureDecision(ProcedureDecisionType.Approved, context));

        var recordedApprovals = _decisions.Count(decision =>
            decision.Type == ProcedureDecisionType.Approved);

        RaiseDomainEvent(new ProcedureApprovalRecordedEvent(
            DefinitionId,
            Id,
            context.ActorReference,
            recordedApprovals,
            RequiredApprovalCount,
            context.OccurredAtUtc));

        if (recordedApprovals >= RequiredApprovalCount)
        {
            RecordStatusChange(
                ProcedureStatus.Approved,
                ProcedureDecisionType.Approved,
                context);
        }

        return Result.Success();
    }

    public Result Reject(ProcedureDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Status != ProcedureStatus.InReview)
            return Result.Failure(ProcedureErrors.InvalidTransition);

        if (!UsesApprovalPolicy(context))
            return Result.Failure(ProcedureErrors.InvalidApprovalPolicy);

        RecordLifecycleDecision(
            ProcedureStatus.Rejected,
            ProcedureDecisionType.Rejected,
            context);

        return Result.Success();
    }

    /// <summary>
    /// Publishes this approved version and retires the previously published version atomically.
    /// </summary>
    public Result Publish(
        Procedure? currentlyPublished,
        ProcedureDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Status != ProcedureStatus.Approved)
            return Result.Failure(ProcedureErrors.InvalidTransition);

        if (!UsesApprovalPolicy(context))
            return Result.Failure(ProcedureErrors.InvalidApprovalPolicy);

        var predecessorResult = RetirePublishedPredecessor(currentlyPublished, context);
        if (predecessorResult.IsFailure)
            return predecessorResult;

        RecordLifecycleDecision(
            ProcedureStatus.Published,
            ProcedureDecisionType.Published,
            context);

        RaiseDomainEvent(new ProcedureVersionPublishedEvent(
            DefinitionId,
            Id,
            currentlyPublished?.Id,
            IsEmergencyPublication: false,
            context.OccurredAtUtc));

        return Result.Success();
    }

    /// <summary>
    /// Publishes before ordinary approvals are complete when the policy explicitly permits it.
    /// Ratification always remains mandatory.
    /// </summary>
    public Result PublishEmergency(
        Procedure? currentlyPublished,
        ProcedureDecisionContext context,
        DateTime ratificationDueAtUtc)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Status is not ProcedureStatus.InReview and not ProcedureStatus.Approved)
            return Result.Failure(ProcedureErrors.InvalidTransition);

        if (!UsesApprovalPolicy(context))
            return Result.Failure(ProcedureErrors.InvalidApprovalPolicy);

        if (!EmergencyPublicationAllowed || !EmergencyRatificationRequired)
            return Result.Failure(ProcedureErrors.EmergencyPublicationNotAllowed);

        if (ratificationDueAtUtc.Kind != DateTimeKind.Utc
            || ratificationDueAtUtc <= context.OccurredAtUtc)
        {
            return Result.Failure(ProcedureErrors.InvalidRatificationDeadline);
        }

        var predecessorResult = RetirePublishedPredecessor(currentlyPublished, context);
        if (predecessorResult.IsFailure)
            return predecessorResult;

        EmergencyRatificationPending = true;
        EmergencyRatificationDueAtUtc = ratificationDueAtUtc;

        RecordLifecycleDecision(
            ProcedureStatus.Published,
            ProcedureDecisionType.EmergencyPublished,
            context);

        RaiseDomainEvent(new ProcedureVersionPublishedEvent(
            DefinitionId,
            Id,
            currentlyPublished?.Id,
            IsEmergencyPublication: true,
            context.OccurredAtUtc));

        return Result.Success();
    }

    public Result RatifyEmergencyPublication(ProcedureDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Status != ProcedureStatus.Published || !EmergencyRatificationPending)
            return Result.Failure(ProcedureErrors.EmergencyRatificationNotPending);

        if (!UsesApprovalPolicy(context))
            return Result.Failure(ProcedureErrors.InvalidApprovalPolicy);

        var emergencyPublisher = _decisions.Single(decision =>
            decision.Type == ProcedureDecisionType.EmergencyPublished);
        var submitter = _decisions.Single(decision =>
            decision.Type == ProcedureDecisionType.SubmittedForReview);

        if (SeparationOfDutiesRequired
            && (SameActor(emergencyPublisher.ActorReference, context.ActorReference)
                || SameActor(submitter.ActorReference, context.ActorReference)))
        {
            return Result.Failure(ProcedureErrors.SeparationOfDutiesRequired);
        }

        _decisions.Add(new ProcedureDecision(
            ProcedureDecisionType.EmergencyRatified,
            context));
        EmergencyRatificationPending = false;

        RaiseDomainEvent(new ProcedureApprovalRecordedEvent(
            DefinitionId,
            Id,
            context.ActorReference,
            RecordedApprovals: 1,
            RequiredApprovals: 1,
            context.OccurredAtUtc));

        return Result.Success();
    }

    public Result Retire(ProcedureDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Status != ProcedureStatus.Published)
            return Result.Failure(ProcedureErrors.InvalidTransition);

        RecordLifecycleDecision(
            ProcedureStatus.Retired,
            ProcedureDecisionType.Retired,
            context);

        return Result.Success();
    }

    public Result Archive(ProcedureDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Status is not ProcedureStatus.Rejected and not ProcedureStatus.Retired)
            return Result.Failure(ProcedureErrors.InvalidTransition);

        RecordLifecycleDecision(
            ProcedureStatus.Archived,
            ProcedureDecisionType.Archived,
            context);

        return Result.Success();
    }

    private bool UsesApprovalPolicy(ProcedureDecisionContext context)
        => string.Equals(
            ApprovalPolicyVersion,
            context.PolicyVersion,
            StringComparison.OrdinalIgnoreCase);

    private Result RetirePublishedPredecessor(
        Procedure? currentlyPublished,
        ProcedureDecisionContext context)
    {
        if (currentlyPublished is null)
            return Result.Success();

        if (currentlyPublished == this
            || currentlyPublished.DefinitionId != DefinitionId
            || currentlyPublished.Status != ProcedureStatus.Published)
        {
            return Result.Failure(ProcedureErrors.InvalidPublishedPredecessor);
        }

        currentlyPublished.RecordLifecycleDecision(
            ProcedureStatus.Retired,
            ProcedureDecisionType.Retired,
            context);

        return Result.Success();
    }

    private void RecordLifecycleDecision(
        ProcedureStatus newStatus,
        ProcedureDecisionType decisionType,
        ProcedureDecisionContext context)
    {
        _decisions.Add(new ProcedureDecision(decisionType, context));
        RecordStatusChange(newStatus, decisionType, context);
    }

    private void RecordStatusChange(
        ProcedureStatus newStatus,
        ProcedureDecisionType decisionType,
        ProcedureDecisionContext context)
    {
        var previousStatus = Status;
        Status = newStatus;

        RaiseDomainEvent(new ProcedureLifecycleChangedEvent(
            DefinitionId,
            Id,
            previousStatus,
            newStatus,
            decisionType,
            context.ActorReference,
            context.OccurredAtUtc));
    }

    private static bool SameActor(string first, string second)
        => string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
}
