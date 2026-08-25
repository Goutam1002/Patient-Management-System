using Microsoft.AspNetCore.Mvc;
using PatientManagement.Application.DTOs;
using PatientManagement.Application.Services;

namespace PatientManagement.Api.Controllers;

/// <summary>
/// Module 7 (Patient History). The per-visit detail read (GET /api/visits/{id})
/// is a shared endpoint that already lives on VisitsController (Module 5) --
/// extended there rather than duplicated here, see that controller's summary.
/// </summary>
[ApiController]
[Route("api/patients/{patientId:int}/visits")]
public class PatientHistoryController(IPatientHistoryService patientHistoryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VisitSummaryDto>>> GetVisits(
        int patientId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var result = await patientHistoryService.GetVisitsAsync(patientId, from, to);
        return Ok(result);
    }
}
