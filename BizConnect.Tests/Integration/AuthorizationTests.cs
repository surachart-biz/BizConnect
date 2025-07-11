using System.Security.Claims;
using System.Text.Encodings.Web;
using BizConnect.Dal;
using BizConnect.Dal.Models;
using BizConnect.Services;
using BizConnect.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace BizConnect.Tests.Integration;

public class AuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthorizationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AdminDashboard_UnauthenticatedUser_RedirectsToLogin()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/Admin/Dashboard");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task AdminDashboard_RegularUser_ReturnsForbidden()
    {
        // Arrange
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Add test authentication
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", options => { });
            });
        }).CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Set up test user claims (regular user)
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", "User");

        // Act
        var response = await client.GetAsync("/Admin/Dashboard");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminDashboard_AdminUser_ReturnsSuccess()
    {
        // Arrange
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace the database with in-memory database for testing
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<BizConnectContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<BizConnectContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid());
                });

                // Mock the UserService to avoid database issues
                var mockUserService = new Mock<IUserService>();
                mockUserService.Setup(x => x.GetAllUsersAsync())
                    .ReturnsAsync(new List<User>());

                services.AddSingleton(mockUserService.Object);

                // Add test authentication
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", options => { });
            });
        }).CreateClient();

        // Set up test user claims (admin user)
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", "Admin");

        // Act
        var response = await client.GetAsync("/Admin/Dashboard");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HomePage_UnauthenticatedUser_ReturnsSuccess()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HomePage_AuthenticatedUser_ReturnsSuccess()
    {
        // Arrange
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Add test authentication
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", options => { });
            });
        }).CreateClient();

        // Set up test user claims
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", "User");

        // Act
        var response = await client.GetAsync("/");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}

public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorizationHeader = Request.Headers["Authorization"].ToString();
        
        if (string.IsNullOrEmpty(authorizationHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var role = authorizationHeader.Replace("Test ", "");
        
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, $"test{role.ToLower()}"),
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
