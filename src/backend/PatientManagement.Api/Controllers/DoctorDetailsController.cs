using Microsoft.AspNetCore.Mvc;
using PatientManagement.Application.DTOs;
using PatientManagement.Application.Services;

namespace PatientManagement.Api.Controllers;

[ApiController]
[Route("api/doctor-details")]
public class DoctorDetailsController(IDoctorDetailsService doctorDetailsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DoctorDetailsDto>> Get()
    {
        var result = await doctorDetailsService.GetAsync();
        return Ok(result);
    }

    [HttpPut]
    public async Task<ActionResult<DoctorDetailsDto>> Update(UpdateDoctorDetailsRequest request)
    {
        var result = await doctorDetailsService.UpdateAsync(request);
        return Ok(result);
    }
}
