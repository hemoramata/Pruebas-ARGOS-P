using ARGOS.Domain.Aggregates.Evidence.Errors;
using ARGOS.Domain.Aggregates.Evidence.ValueObjects;
using ARGOS.SharedKernel.Abstractions;
using ARGOS.SharedKernel.Results;

namespace ARGOS.Domain.Aggregates.Evidence;

/// <summary>
/// Identifies a person, organization, institution, device or system that
/// produced, issued, collected or submitted evidence.
/// </summary>
public sealed class EvidenceSource : Entity<EvidenceSourceId>
{
    public string SourceTypeCode { get; private set; } = string.Empty;
    public string SourceReference { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public DateTimeOffset ProducedAtUtc { get; private set; }
    public DateTimeOffset ReceivedAtUtc { get; private set; }
    public string Channel { get; private set; } = string.Empty;
    public Guid? ReceivedByPositionId { get; private set; }

    private EvidenceSource()
    {
    }

    private EvidenceSource(
        EvidenceSourceId id,
        string sourceTypeCode,
        string sourceReference,
        string role,
        DateTimeOffset producedAtUtc,
        DateTimeOffset receivedAtUtc,
        string channel,
        Guid? receivedByPositionId)
        : base(id)
    {
        SourceTypeCode = sourceTypeCode.Trim().ToUpperInvariant();
        SourceReference = sourceReference.Trim();
        Role = role.Trim();
        ProducedAtUtc = producedAtUtc;
        ReceivedAtUtc = receivedAtUtc;
        Channel = channel.Trim().ToUpperInvariant();
        ReceivedByPositionId = receivedByPositionId;
    }

    public static Result<EvidenceSource> Create(
        string? sourceTypeCode,
        string? sourceReference,
        string? role,
        DateTimeOffset producedAtUtc,
        DateTimeOffset receivedAtUtc,
        string? channel,
        Guid? receivedByPositionId = null)
    {
        if (string.IsNullOrWhiteSpace(sourceTypeCode)
            || sourceTypeCode.Trim().Length > 100
            || string.IsNullOrWhiteSpace(sourceReference)
            || sourceReference.Trim().Length > 200
            || string.IsNullOrWhiteSpace(role)
            || role.Trim().Length > 200
            || string.IsNullOrWhiteSpace(channel)
            || channel.Trim().Length > 100)
        {
            return Result<EvidenceSource>.Failure(EvidenceErrors.InvalidSourceData);
        }

        if (receivedAtUtc < producedAtUtc)
            return Result<EvidenceSource>.Failure(EvidenceErrors.InvalidSourceDates);

        return Result<EvidenceSource>.Success(new EvidenceSource(
            EvidenceSourceId.New(),
            sourceTypeCode,
            sourceReference,
            role,
            producedAtUtc,
            receivedAtUtc,
            channel,
            receivedByPositionId));
    }
}
