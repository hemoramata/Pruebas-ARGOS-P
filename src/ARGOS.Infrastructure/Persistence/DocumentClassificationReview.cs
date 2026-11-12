namespace ARGOS.Infrastructure.Persistence;

/// <summary>Final human decision over an assisted classification. It never represents an autonomous decision.</summary>
public sealed class DocumentClassificationReview
{
    private DocumentClassificationReview() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ProtectedContentId { get; private set; }
    public Guid? SuggestedCategoryId { get; private set; }
    public Guid FinalCategoryId { get; private set; }
    public string SuggestionSource { get; private set; } = string.Empty;
    public decimal? Confidence { get; private set; }
    public string Decision { get; private set; } = string.Empty;
    public Guid ConfirmedByPersonId { get; private set; }
    public DateTimeOffset ConfirmedAtUtc { get; private set; }

    public static DocumentClassificationReview Create(Guid organizationId, Guid protectedContentId, Guid? suggestedCategoryId,
        Guid finalCategoryId, string? suggestionSource, decimal? confidence, string decision, Guid confirmedBy, DateTimeOffset now)
    {
        if (finalCategoryId == Guid.Empty || confirmedBy == Guid.Empty) throw new ArgumentException("La categoría final y la persona confirmante son obligatorias.");
        if (confidence is < 0 or > 1) throw new ArgumentException("La confianza debe estar entre 0 y 1.");
        if (decision is not ("Approved" or "Corrected" or "ManuallyClassified")) throw new ArgumentException("La decisión de clasificación no es válida.");
        return new()
        {
            Id = Guid.NewGuid(), OrganizationId = organizationId, ProtectedContentId = protectedContentId,
            SuggestedCategoryId = suggestedCategoryId, FinalCategoryId = finalCategoryId,
            SuggestionSource = string.IsNullOrWhiteSpace(suggestionSource) ? "Manual" : suggestionSource.Trim()[..Math.Min(80, suggestionSource.Trim().Length)],
            Confidence = confidence, Decision = decision, ConfirmedByPersonId = confirmedBy, ConfirmedAtUtc = now
        };
    }
}
