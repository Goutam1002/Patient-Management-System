using Microsoft.EntityFrameworkCore;
using PatientManagement.Application.DTOs;
using PatientManagement.Application.Services;
using PatientManagement.Infrastructure.Data;

namespace PatientManagement.Infrastructure.Services;

public class LoginService(AppDbContext db, IPasswordCrypto passwordCrypto, ISessionTokenStore sessionTokenStore) : ILoginService
{
    public async Task<LoginResult?> LoginAsync(LoginRequest request)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Username == request.Username);
        if (user is null)
        {
            return null;
        }

        // Decrypt-and-compare, per implementation-brd.md's Authentication spec --
        // a straight equality check, not a token exchange.
        var storedPassword = passwordCrypto.Decrypt(user.Password);
        if (storedPassword != request.Password)
        {
            return null;
        }

        var token = sessionTokenStore.IssueToken(user.Username);
        return new LoginResult { Username = user.Username, SessionToken = token };
    }
}
