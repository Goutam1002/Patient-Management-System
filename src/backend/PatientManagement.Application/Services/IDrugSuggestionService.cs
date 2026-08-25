namespace PatientManagement.Application.Services;

public interface IDrugSuggestionService
{
    /// <summary>
    /// Distinct DrugName values from the doctor's own prior prescribing
    /// history (PrescriptionItem rows), for autocomplete only -- never a
    /// validation constraint on what DrugName may contain. Match semantics:
    /// case-insensitive Contains (not prefix-only), the same "contains
    /// anywhere in the field" default implementation-brd.md's fixed Patient
    /// search spec establishes for free-text lookup in this codebase -- the
    /// module file flagged this as an open detail, resolved here rather than
    /// escalated (implementation-time decision, see
    /// docs/implementation-progress.md Step 14). A null/blank term returns
    /// every distinct drug name on record rather than an empty list --
    /// unlike Patient search's "no terms -> []" rule (which guards against
    /// exposing unbounded patient data), this list is bounded by the
    /// doctor's own distinct drug vocabulary, not patient count, so serving
    /// the full list on an empty term is a reasonable typeahead-on-focus
    /// default rather than a privacy/scale concern.
    /// </summary>
    Task<IReadOnlyList<string>> GetSuggestionsAsync(string? term);
}
