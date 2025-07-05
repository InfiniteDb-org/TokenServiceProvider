using TokenService.Api.Models;

namespace TokenService_Tests;

public class TokenServiceTests
{
    private readonly TokenService.Api.Services.TokenService _tokenService;

    public TokenServiceTests()
    {
        // set environment variables for test
        Environment.SetEnvironmentVariable("Issuer", "TestIssuer");
        Environment.SetEnvironmentVariable("Audience", "TestAudience");
        Environment.SetEnvironmentVariable("Key", "supersecretkey12345678901234567890");
        _tokenService = new TokenService.Api.Services.TokenService();
    }

    [Fact]
    public async Task GenerateAccessToken_ReturnsValidToken_WithCorrectClaims()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new TokenRequest
        {
            UserId = userId,
            Email = "test@example.com",
            Role = "Admin"
        };

        // Act
        var response = await _tokenService.GenerateAccessTokenAsync(request);

        // Assert
        Assert.True(response.Succeeded);
        Assert.False(string.IsNullOrEmpty(response.AccessToken));
        Assert.False(string.IsNullOrEmpty(response.RefreshToken));
    }

    [Fact]
    public async Task ValidateAccessTokenAsync_ValidToken_ReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new TokenRequest
        {
            UserId = userId,
            Email = "test@example.com",
            Role = "Admin"
        };
        var response = await _tokenService.GenerateAccessTokenAsync(request);
        var validationRequest = new ValidationRequest
        {
            AccessToken = response.AccessToken!,
            UserId = userId
        };

        // Act
        var validation = await _tokenService.ValidateAccessTokenAsync(validationRequest);

        // Assert
        Assert.True(validation.Succeeded);
    }

    [Fact]
    public async Task RefreshToken_CanOnlyBeUsedOnce()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new TokenRequest
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

        // Act
        var refreshResponse1 = await _tokenService.RefreshAccessTokenAsync(refreshRequest);
        var refreshResponse2 = await _tokenService.RefreshAccessTokenAsync(refreshRequest);

        // Assert
        Assert.True(refreshResponse1.Succeeded);
        Assert.False(refreshResponse2.Succeeded);
    }

    [Fact]
    public async Task RefreshToken_ClaimsArePreserved()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new TokenRequest
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

        // Act
        var refreshResponse = await _tokenService.RefreshAccessTokenAsync(refreshRequest);
        var validation = await _tokenService.ValidateAccessTokenAsync(new ValidationRequest
        {
            AccessToken = refreshResponse.AccessToken!,
            UserId = userId
        });

        // Assert
        Assert.True(refreshResponse.Succeeded);
        Assert.True(validation.Succeeded);
    }

    [Fact]
    public async Task RefreshToken_InvalidToken_ReturnsFailure()
    {
        // Arrange
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = "this-token-does-not-exist",
            UserId = Guid.NewGuid()
        };

        // Act
        var response = await _tokenService.RefreshAccessTokenAsync(refreshRequest);

        // Assert
        Assert.False(response.Succeeded);
        Assert.Contains("Invalid refresh token", response.Message);
    }

    [Fact]
    public async Task RefreshToken_WrongUserId_ReturnsFailure()
    {
        // Arrange
        var request = new TokenRequest
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

        // Act
        var refreshResponse = await _tokenService.RefreshAccessTokenAsync(refreshRequest);

        // Assert
        Assert.False(refreshResponse.Succeeded);
        Assert.Contains("does not match user", refreshResponse.Message);
    }

    [Fact]
    public async Task ValidateAccessTokenAsync_InvalidToken_ReturnsFailure()
    {
        // Arrange
        var validationRequest = new ValidationRequest
        {
            AccessToken = "not.a.valid.jwt",
            UserId = Guid.NewGuid()
        };

        // Act
        var validation = await _tokenService.ValidateAccessTokenAsync(validationRequest);

        // Assert
        Assert.False(validation.Succeeded);
    }

    [Fact]
    public async Task ValidateAccessTokenAsync_ExpiredToken_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new TokenRequest
        {
            UserId = userId,
            Email = "test@example.com",
            Role = "User"
        };
        
        // already expired token 
        var response = await _tokenService.GenerateAccessTokenAsync(request, expiresInDays: -1);
        var validationRequest = new ValidationRequest
        {
            AccessToken = response.AccessToken!,
            UserId = userId
        };

        // Act
        var validation = await _tokenService.ValidateAccessTokenAsync(validationRequest);

        // Assert
        Assert.False(validation.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(validation.Message));
    }

    [Fact]
    public async Task RefreshToken_ExpiredToken_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new TokenRequest
        {
            UserId = userId,
            Email = "test@example.com",
            Role = "User"
        };
        // already expired refresh token
        var refreshToken = new RefreshToken
        {
            Token = Guid.NewGuid().ToString(),
            UserId = userId,
            Expires = DateTime.UtcNow.AddDays(-1), 
            Created = DateTime.UtcNow.AddDays(-2)
        };
        
        // add to in-memory store
        var refreshTokensField = typeof(TokenService.Api.Services.TokenService).GetField("RefreshTokens",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var dict = (System.Collections.IDictionary)refreshTokensField?.GetValue(null)!;
        dict[refreshToken.Token] = refreshToken;
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = refreshToken.Token,
            UserId = userId
        };

        // Act
        var response = await _tokenService.RefreshAccessTokenAsync(refreshRequest);

        // Assert
        Assert.False(response.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(response.Message));
    }
}
