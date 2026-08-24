namespace PatientManagement.Application.DTOs;

public class LoginResult
{
    public required string Username { get; set; }
    public required string SessionToken { get; set; }
}
