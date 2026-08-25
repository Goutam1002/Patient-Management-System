namespace PatientManagement.Domain.Models;

/// <summary>
/// One row per completed export (CSV or PDF) -- who, what scope, which
/// format, when. Required by the fixed Export spec: "the export endpoint
/// isn't done until logging exists and is tested." Written only after a
/// scope/confirmation check has already passed -- a rejected export request
/// never reaches this table.
/// </summary>
public class ExportAuditLog
{
    public int Id { get; set; }
    public DateTime PerformedAt { get; set; }
    public ExportFormat Format { get; set; }
    public ExportScopeType ScopeType { get; set; }

    // Serialized patient IDs ("1,2,3") for SelectedPatients, or the
    // "yyyy-MM-dd..yyyy-MM-dd" range for DateRange -- free text rather than
    // a second scope table, since this row is only ever read back, not queried by scope.
    public required string ScopeDetail { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }
}
