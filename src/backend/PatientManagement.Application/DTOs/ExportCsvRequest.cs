namespace PatientManagement.Application.DTOs;

public class ExportCsvRequest
{
    public required ExportScopeRequest Scope { get; set; }

    // Server-side confirmation gate, per the fixed Export spec -- the API
    // must not treat a request as implicitly confirmed. Defaults to false
    // (the unsafe default) so an omitted field is rejected, not accepted.
    public bool Confirmed { get; set; }
}
