namespace PatientManagement.Application.Services;

/// <summary>
/// Thrown when a CSV export request supplies neither a non-empty
/// PatientIds list nor a bounded DateFrom/DateTo range. This is the
/// mechanism behind the fixed Export spec's hard gate -- there is no
/// unbounded/all-patients export path, because "no scope supplied" is
/// rejected rather than defaulted to "export everything."
/// </summary>
public class ExportScopeInvalidException(string message) : Exception(message);
