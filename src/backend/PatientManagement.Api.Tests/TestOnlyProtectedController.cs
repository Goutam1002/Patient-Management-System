using Microsoft.AspNetCore.Mvc;

namespace PatientManagement.Api.Tests;

/// <summary>
/// Exists only in this test assembly, registered as an extra ApplicationPart
/// by AuthApiFactory, so tests can prove the pipeline's fallback
/// RequireAuthenticatedUser() policy actually blocks a controller that never
/// declared [Authorize] itself -- there's no real second controller yet to
/// exercise this against (Authentication is the first module with any HTTP
/// surface at all).
/// </summary>
[ApiController]
[Route("api/test/protected")]
public class TestOnlyProtectedController : ControllerBase
{
    [HttpGet]
    public ActionResult<string> WhoAmI() => Ok(User.Identity?.Name);
}
