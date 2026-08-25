using Microsoft.AspNetCore.Mvc;
using PatientManagement.Application.DTOs;
using PatientManagement.Application.Services;

namespace PatientManagement.Api.Controllers;

[ApiController]
[Route("api/patients")]
public class PatientsController(IPatientService patientService, IRecentPatientsService recentPatientsService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PatientDto>> Create(CreatePatientRequest request)
    {
        var result = await patientService.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { patientId = result.PatientId }, result);
    }

    [HttpGet("{patientId:int}")]
    public async Task<ActionResult<PatientDto>> Get(int patientId)
    {
        var result = await patientService.GetAsync(patientId);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{patientId:int}")]
    public async Task<ActionResult<PatientDto>> Update(int patientId, UpdatePatientRequest request)
    {
        var result = await patientService.UpdateAsync(patientId, request);
        return result is null ? NotFound() : Ok(result);
    }

    // No DELETE action -- patients are never deleted, by design.

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<PatientDto>>> Search([FromQuery] string? name, [FromQuery] string? phone)
    {
        var results = await patientService.SearchAsync(name, phone);
        return Ok(results);
    }

    // Owned by Module 8 (Search & Navigation) -- kept on this controller
    // rather than a new one since it's a Patient read endpoint, same
    // reasoning Search itself already established on this controller.
    [HttpGet("recent")]
    public async Task<ActionResult<IReadOnlyList<RecentPatientDto>>> Recent([FromQuery] int count = 5)
    {
        if (count <= 0)
        {
            return BadRequest("count must be a positive integer.");
        }

        var results = await recentPatientsService.GetRecentAsync(count);
        return Ok(results);
    }
}
