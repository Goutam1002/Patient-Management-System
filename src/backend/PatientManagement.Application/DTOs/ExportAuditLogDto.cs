namespace PatientManagement.Application.DTOs;

public class ExportAuditLogDto
{
    public int Id { get; set; }
    public DateTime PerformedAt { get; set; }
    public required string Format { get; set; }
    public required string ScopeType { get; set; }
    public required string ScopeDetail { get; set; }
    public required string Username { get; set; }
}
