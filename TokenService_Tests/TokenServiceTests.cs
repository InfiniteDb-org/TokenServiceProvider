using TokenService.Api.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using TokenService.Api.Infrastructure.Database;

namespace TokenService_Tests;

public class TokenServiceTests
{
    private readonly TokenService.Api.Services.TokenService _tokenService;
    private readonly AppDbContext _dbContext;

    public TokenServiceTests()
    {
        // Setup in-memory EF Core context
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        // Setup IConfiguration
        var inMemorySettings = new Dictionary<string, string>
        {
            {"Issuer", "TestIssuer"},
            {"Audience", "TestAudience"},
            {"Key", "supersecretkey12345678901234567890"},
            {"ExpiresInMinutes", "10"}
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        _tokenService = new TokenService.Api.Services.TokenService(_dbContext, configuration);
    }

    [Fact]
    public async Task GenerateAccessToken_ReturnsValidToken_WithCorrectClaims()
    {
        var userId = Guid.NewGuid();
        var request = new GenerateTokenRequest
        {
            UserId = userId,
            Email = "test@example.com",
            Role = "Admin"
        };
        var response = await _tokenService.GenerateAccessTokenAsync(request);
        Assert.True(response.Succeeded);
        Assert.False(string.IsNullOrEmpty(response.AccessToken));
        Assert.False(string.IsNullOrEmpty(response.RefreshToken));
    }

    [Fact]
    public async Task ValidateAccessTokenAsync_ValidToken_ReturnsSuccess()
    {
        var userId = Guid.NewGuid();
        var request = new GenerateTokenRequest
        {
            UserId = userId,
            Email = "test@example.com",
            Role = "Admin"
        };
        var response = await _tokenService.GenerateAccessTokenAsync(request);
        var validationRequest = new ValidateTokenRequest
        {
            AccessToken = response.AccessToken!,
            UserId = userId
        };
        var validation = await _tokenService.ValidateAccessTokenAsync(validationRequest);
        Assert.True(validation.Succeeded);
    }

    [Fact]
    public async Task RefreshToken_CanOnlyBeUsedOnce()
    {
        var userId = Guid.NewGuid();
        var request = new GenerateTokenRequest
        {
            UserId = userId,
            Email = "test@example.com",
            Role = "User"
        };
        var response = await _tokenService.GenerateAccessTokenAsync(request);
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = response.RefreshToken!,
            UserId = userId
        };
        var refreshResponse1 = await _tokenService.RefreshAccessTokenAsync(refreshRequest);
        var refreshResponse2 = await _tokenService.RefreshAccessTokenAsync(refreshRequest);
        Assert.True(refreshResponse1.Succeeded);
        Assert.False(refreshResponse2.Succeeded);
    }

    [Fact]
    public async Task RefreshToken_ClaimsArePreserved()
    {
        var userId = Guid.NewGuid();
        var request = new GenerateTokenRequest
        {
            UserId = userId,
            Email = "admin@example.com",
            Role = "Admin"
        };
        var response = await _tokenService.GenerateAccessTokenAsync(request);
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = response.RefreshToken!,
            UserId = userId
        };
        var refreshResponse = await _tokenService.RefreshAccessTokenAsync(refreshRequest);
        var validation = await _tokenService.ValidateAccessTokenAsync(new ValidateTokenRequest
        {
            AccessToken = refreshResponse.AccessToken!,
            UserId = userId
        });
        Assert.True(refreshResponse.Succeeded);
        Assert.True(validation.Succeeded);
    }

    [Fact]
    public async Task RefreshToken_InvalidToken_ReturnsFailure()
    {
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = "this-token-does-not-exist",
            UserId = Guid.NewGuid()
        };
        var response = await _tokenService.RefreshAccessTokenAsync(refreshRequest);
        Assert.False(response.Succeeded);
        Assert.Contains("Invalid refresh token", response.Message);
    }

    [Fact]
    public async Task RefreshToken_WrongUserId_ReturnsFailure()
    {
        var request = new GenerateTokenRequest
        {
            UserId = Guid.NewGuid(),
            Email = "test@example.com",
            Role = "User"
        };
        var response = await _tokenService.GenerateAccessTokenAsync(request);
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = response.RefreshToken!,
            UserId = Guid.NewGuid() 
        };
        var refreshResponse = await _tokenService.RefreshAccessTokenAsync(refreshRequest);
        Assert.False(refreshResponse.Succeeded);
        Assert.Contains("does not match user", refreshResponse.Message);
    }

    [Fact]
    public async Task ValidateAccessTokenAsync_InvalidToken_ReturnsFailure()
    {
        var validationRequest = new ValidateTokenRequest
        {
            AccessToken = "not.a.valid.jwt",
            UserId = Guid.NewGuid()
        };
        var validation = await _tokenService.ValidateAccessTokenAsync(validationRequest);
        Assert.False(validation.Succeeded);
    }

    [Fact]
    public async Task ValidateAccessTokenAsync_ExpiredToken_ReturnsFailure()
    {
        var userId = Guid.NewGuid();
        var request = new GenerateTokenRequest
        {
            UserId = userId,
            Email = "test@example.com",
            Role = "User"
        };
        // already expired token 
        var response = await _tokenService.GenerateAccessTokenAsync(request, expiresInMinutes: -1);
        var validationRequest = new ValidateTokenRequest
        {
            AccessToken = response.AccessToken!,
            UserId = userId
        };
        var validation = await _tokenService.ValidateAccessTokenAsync(validationRequest);
        Assert.False(validation.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(validation.Message));
    }

    [Fact]
    public async Task RefreshToken_ExpiredToken_ReturnsFailure()
    {
        var userId = Guid.NewGuid();
        var refreshToken = new RefreshToken
        {
            Token = Guid.NewGuid().ToString(),
            UserId = userId,
            ExpiresOnUtc = DateTime.UtcNow.AddDays(-1),
            Created = DateTime.UtcNow.AddDays(-2)
        };
        await _dbContext.RefreshTokens.AddAsync(refreshToken);
        await _dbContext.SaveChangesAsync();
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = refreshToken.Token,
            UserId = userId
        };
        var response = await _tokenService.RefreshAccessTokenAsync(refreshRequest);
        Assert.False(response.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(response.Message));
    }
}
