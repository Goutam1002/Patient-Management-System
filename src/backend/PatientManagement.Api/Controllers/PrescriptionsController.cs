using Microsoft.AspNetCore.Mvc;
using PatientManagement.Application.DTOs;
using PatientManagement.Application.Services;

namespace PatientManagement.Api.Controllers;

/// <summary>
/// Deliberately no PUT/PATCH/DELETE action anywhere in this controller --
/// a hard gate, not an oversight (see Prescription's immutability rule in
/// implementation-brd.md and the module's own Business Rules). A correction
/// always goes through Create again, producing a new Prescription row.
/// </summary>
[ApiController]
[Route("api/prescriptions")]
public class PrescriptionsController(
    IPrescriptionService prescriptionService,
    IDrugSuggestionService drugSuggestionService) : ControllerBase
{
    [HttpPost("/api/visits/{visitId:int}/prescriptions")]
    public async Task<ActionResult<PrescriptionDto>> Create(int visitId, CreatePrescriptionRequest request)
    {
        var result = await prescriptionService.CreatePrescriptionAsync(visitId, request);
        return result is null ? NotFound() : Created($"/api/prescriptions/{result.Id}", result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PrescriptionDto>> Get(int id)
    {
        var result = await prescriptionService.GetAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    // Query parameter is named "prefix" to match the module's own fixed API
    // surface (GET /api/prescriptions/drug-suggestions?prefix=), even though
    // the match semantics behind it are Contains, not StartsWith -- see
    // IDrugSuggestionService's remarks.
    [HttpGet("drug-suggestions")]
    public async Task<ActionResult<IReadOnlyList<string>>> DrugSuggestions([FromQuery] string? prefix)
    {
        var results = await drugSuggestionService.GetSuggestionsAsync(prefix);
        return Ok(results);
    }
}
