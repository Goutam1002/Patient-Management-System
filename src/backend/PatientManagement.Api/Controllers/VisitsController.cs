using Microsoft.AspNetCore.Mvc;
using PatientManagement.Application.DTOs;
using PatientManagement.Application.Services;

namespace PatientManagement.Api.Controllers;

/// <summary>
/// Module 5 (Consultation Workflow) owns this controller's whole HTTP
/// surface, even though StartConsultation's URL nests under
/// /api/appointments/{appointmentId}/... -- rather than adding an action to
/// Module 4's AppointmentsController (a different module's file), the route
/// is declared here with an absolute template override so all three
/// consultation endpoints live in one place and Module 4's controller is
/// left untouched.
/// </summary>
[ApiController]
[Route("api/visits")]
public class VisitsController(IConsultationService consultationService) : ControllerBase
{
    [HttpPost("/api/appointments/{appointmentId:int}/start-consultation")]
    public async Task<ActionResult<VisitDto>> StartConsultation(int appointmentId, StartConsultationRequest request)
    {
        VisitDto? result;
        try
        {
            result = await consultationService.StartConsultationAsync(appointmentId, request);
        }
        catch (ConsultationAlreadyStartedException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }

        if (result is null)
        {
            return NotFound();
        }

        return Created($"/api/visits/{result.Id}", result);
    }

    [HttpGet("{visitId:int}")]
    public async Task<ActionResult<VisitDto>> Get(int visitId)
    {
        var result = await consultationService.GetAsync(visitId);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{visitId:int}")]
    public async Task<ActionResult<VisitDto>> Update(int visitId, UpdateVisitRequest request)
    {
        var result = await consultationService.UpdateAsync(visitId, request);
        return result is null ? NotFound() : Ok(result);
    }
}
