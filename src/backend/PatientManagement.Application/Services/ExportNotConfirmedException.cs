namespace PatientManagement.Application.Services;

/// <summary>
/// Thrown when an export request's Confirmed flag is not true. The
/// confirmation gate is enforced here, server-side -- a UI dialog alone
/// would not satisfy the fixed Export spec's "the API should not treat an
/// export request as implicitly confirmed" requirement.
/// </summary>
public class ExportNotConfirmedException(string message) : Exception(message);
