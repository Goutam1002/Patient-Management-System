using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PatientManagement.Api.Authentication;
using PatientManagement.Application.Services;
using PatientManagement.Infrastructure.Data;
using PatientManagement.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AppDb")));

builder.Services.AddScoped<IPasswordCrypto, AesPasswordCrypto>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IWalkInService, WalkInService>();
builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddSingleton<ISessionTokenStore, InMemorySessionTokenStore>();

// Every controller requires a valid session token by default; the login
// endpoint itself opts out with [AllowAnonymous]. This is how every future
// module's API surface ends up behind the single-doctor login gate without
// each controller having to remember to declare [Authorize] individually.
builder.Services
    .AddAuthentication(SessionTokenDefaults.AuthenticationScheme)
    .AddScheme<AuthenticationSchemeOptions, SessionTokenAuthenticationHandler>(
        SessionTokenDefaults.AuthenticationScheme, _ => { });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

const string AngularDevCorsPolicy = "AngularDevCorsPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularDevCorsPolicy, policy =>
    {
        var angularOrigin = builder.Configuration["AngularDevServerOrigin"] ?? "http://localhost:4200";
        policy.WithOrigins(angularOrigin)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var passwordCrypto = scope.ServiceProvider.GetRequiredService<IPasswordCrypto>();
    await DoctorAccountSeeder.SeedAsync(db, passwordCrypto, app.Configuration);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors(AngularDevCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;

