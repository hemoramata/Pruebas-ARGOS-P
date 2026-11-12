using ARGOS.Domain.Aggregates.OrganizationSetup.Enums;

namespace ARGOS.Domain.Aggregates.OrganizationSetup;

public sealed class OrganizationTerm
{
    private OrganizationTerm()
    {
    }

    internal OrganizationTerm(
        OrganizationConcept concept,
        string singular,
        string plural)
    {
        Concept = concept;
        Singular = singular;
        Plural = plural;
    }

    public OrganizationConcept Concept { get; private set; }

    public string Singular { get; private set; } = string.Empty;

    public string Plural { get; private set; } = string.Empty;

    internal void ChangeLabels(string singular, string plural)
    {
        Singular = singular;
        Plural = plural;
    }

    internal OrganizationTerm Clone() => new(Concept, Singular, Plural);
}
