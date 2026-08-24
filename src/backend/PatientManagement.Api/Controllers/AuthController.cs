using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PatientManagement.Application.DTOs;
using PatientManagement.Application.Services;

namespace PatientManagement.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(ILoginService loginService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResult>> Login(LoginRequest request)
    {
        var result = await loginService.LoginAsync(request);
        if (result is null)
        {
            return Unauthorized();
        }

        return Ok(result);
    }
}
