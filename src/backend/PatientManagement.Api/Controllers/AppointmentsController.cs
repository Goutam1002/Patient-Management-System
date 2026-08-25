using Microsoft.AspNetCore.Mvc;
using PatientManagement.Application.DTOs;
using PatientManagement.Application.Services;

namespace PatientManagement.Api.Controllers;

[ApiController]
[Route("api/appointments")]
public class AppointmentsController(
    IAppointmentService appointmentService,
    IWalkInService walkInService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<AppointmentDto>> Create(CreateAppointmentRequest request)
    {
        AppointmentDto? result;
        try
        {
            result = await appointmentService.CreateAsync(request);
        }
        catch (AppointmentSlotConflictException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }

        if (result is null)
        {
            return NotFound();
        }

        // There is no GET-by-id endpoint in this module's API surface, so the
        // Location header points at the daily list the new appointment appears
        // in -- a real, resolvable URL rather than an invented one.
        return Created($"/api/appointments/daily?date={result.ScheduledTime:yyyy-MM-dd}", result);
    }

    [HttpGet("daily")]
    public async Task<ActionResult<IReadOnlyList<AppointmentDto>>> GetDaily([FromQuery] DateOnly? date)
    {
        var results = await appointmentService.GetDailyAsync(date ?? DateOnly.FromDateTime(DateTime.Today));
        return Ok(results);
    }

    [HttpPut("{appointmentId:int}/status")]
    public async Task<ActionResult<AppointmentDto>> UpdateStatus(int appointmentId, UpdateAppointmentStatusRequest request)
    {
        AppointmentDto? result;
        try
        {
            result = await appointmentService.UpdateStatusAsync(appointmentId, request.Status!.Value);
        }
        catch (AppointmentStatusTransitionException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("walk-in")]
    public async Task<ActionResult<WalkInVisitDto>> CreateWalkIn(WalkInVisitRequest request)
    {
        try
        {
            var visit = await walkInService.CreateWalkInVisitAsync(request);
            return Created($"/api/appointments/daily?date={DateTime.Today:yyyy-MM-dd}", new WalkInVisitDto
            {
                VisitId = visit.Id,
                AppointmentId = visit.AppointmentId,
                PatientId = visit.PatientId,
                VisitNumber = visit.VisitNumber,
            });
        }
        catch (AppointmentSlotConflictException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }
}
