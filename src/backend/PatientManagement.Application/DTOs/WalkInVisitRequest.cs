namespace PatientManagement.Application.DTOs;

/// <summary>
/// Everything needed to create a walk-in patient's appointment + visit in
/// one flow. Mirrors Visit's mandatory-at-entry vitals exactly -- there is
/// no draft path, so all vitals are required here too.
/// </summary>
public record WalkInVisitRequest(
    int PatientId,
    int DurationMinutes,
    decimal Temperature,
    short BpSystolic,
    short BpDiastolic,
    int Pulse,
    decimal Weight,
    string? Complaints,
    string? Diagnosis);
