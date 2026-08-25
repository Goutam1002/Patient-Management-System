using Microsoft.AspNetCore.Mvc;
using PatientManagement.Application.DTOs;
using PatientManagement.Application.Services;

namespace PatientManagement.Api.Controllers;

/// <summary>
/// Module 9 (Data Export). Both export actions require request.Confirmed
/// (checked again server-side by IExportService, never trusted as
/// UI-only) and always write an ExportAuditLog row on success -- see
/// ExportService for the scope/confirmation hard gates.
/// </summary>
[ApiController]
[Route("api/exports")]
public class ExportsController(IExportService exportService) : ControllerBase
{
    [HttpPost("csv")]
    public async Task<IActionResult> ExportCsv(ExportCsvRequest request)
    {
        try
        {
            var zipBytes = await exportService.ExportCsvAsync(request, CurrentUsername);
            return File(zipBytes, "application/zip", "patient-export.zip");
        }
        catch (ExportNotConfirmedException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (ExportScopeInvalidException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("pdf")]
    public async Task<IActionResult> ExportPdf(ExportPdfRequest request)
    {
        byte[]? pdfBytes;
        try
        {
            pdfBytes = await exportService.ExportPdfAsync(request, CurrentUsername);
        }
        catch (ExportNotConfirmedException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        if (pdfBytes is null)
        {
            return NotFound();
        }

        return File(pdfBytes, "application/pdf", $"patient-{request.PatientId}-summary.pdf");
    }

    [HttpGet("audit-log")]
    public async Task<ActionResult<IReadOnlyList<ExportAuditLogDto>>> AuditLog()
    {
        var results = await exportService.GetAuditLogAsync();
        return Ok(results);
    }

    private string CurrentUsername => User.Identity!.Name!;
}
