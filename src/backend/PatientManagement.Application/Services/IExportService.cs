using PatientManagement.Application.DTOs;

namespace PatientManagement.Application.Services;

public interface IExportService
{
    /// <summary>
    /// Returns the ZIP bytes containing patients.csv + visits.csv. Throws
    /// ExportNotConfirmedException if not confirmed, ExportScopeInvalidException
    /// if the scope is neither a non-empty patient list nor a bounded date range.
    /// </summary>
    Task<byte[]> ExportCsvAsync(ExportCsvRequest request, string username);

    /// <summary>
    /// Returns the single-patient PDF bytes, or null if the patient does not
    /// exist. Throws ExportNotConfirmedException if not confirmed.
    /// </summary>
    Task<byte[]?> ExportPdfAsync(ExportPdfRequest request, string username);

    Task<IReadOnlyList<ExportAuditLogDto>> GetAuditLogAsync();
}
