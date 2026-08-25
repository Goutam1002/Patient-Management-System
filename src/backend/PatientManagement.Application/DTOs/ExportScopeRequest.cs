namespace PatientManagement.Application.DTOs;

/// <summary>
/// Exactly one of the two scoping modes must be supplied -- a non-empty
/// PatientIds list, or a bounded (both-ends-present) DateFrom/DateTo range.
/// Neither present is rejected by IExportService rather than treated as
/// "export everything": there is no unbounded/all-patients scope, by
/// construction, per the fixed Export spec's hard gate.
/// </summary>
public class ExportScopeRequest
{
    public List<int>? PatientIds { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
}
