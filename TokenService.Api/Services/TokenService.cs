using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TokenService.Api.Models;

namespace TokenService.Api.Services;

public interface ITokenService
{
    Task<TokenResponse> GenerateAccessTokenAsync(TokenRequest request, int expiresInDays = 30);
    Task<ValidationResponse> ValidateAccessTokenAsync(ValidationRequest request);
    Task<TokenResponse> RefreshAccessTokenAsync(RefreshTokenRequest request);
}

public class TokenService : ITokenService
{
    private readonly string _issuer = Environment.GetEnvironmentVariable("Issuer") ?? throw new InvalidOperationException("Issuer environment variable not set.");
    private readonly string _audience = Environment.GetEnvironmentVariable("Audience") ?? throw new InvalidOperationException("Audience environment variable not set.");
    private readonly string _key = Environment.GetEnvironmentVariable("Key") ?? throw new InvalidOperationException("Key environment variable not set.");
    
    private static readonly Dictionary<string, RefreshToken> RefreshTokens = new();

    public Task<TokenResponse> GenerateAccessTokenAsync(TokenRequest request, int expiresInDays = 30)
    {
        try
        {
            // Check that UserId is provided
            if (request.UserId is null)
                throw new NullReferenceException("No UserId provided");
            
            // Create signing credentials for JWT
            var credentials =  new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)), 
                SecurityAlgorithms.HmacSha256) ?? throw new NullReferenceException("Unable to create credentials");
            
            // Add claims to the token
            List<Claim> claims =
            [
                new(ClaimTypes.NameIdentifier, request.UserId.Value.ToString()),
                new(JwtRegisteredClaimNames.Sub, request.UserId.Value.ToString())
            ];
            if (!string.IsNullOrEmpty(request.Email))
                claims.Add(new Claim(ClaimTypes.Email, request.Email));
            claims.Add(new Claim(ClaimTypes.Role, CapitalizeRole(request.Role ?? "user")));
            
            // 
            if (request.CustomClaims is not null)
            {
                foreach (var (key, value) in request.CustomClaims)
                {
                    // PascalCase på claim-typ, value bara lowercase för admin
                    var claimType = char.ToUpper(key[0]) + key.Substring(1);
                    var claimValue = key.Equals("admin", StringComparison.OrdinalIgnoreCase)
                        ? value.ToString()!.ToLowerInvariant()
                        : value.ToString()!;
                    claims.Add(new Claim(claimType, claimValue));
                }
            }

            // Build the token descriptor
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Issuer = _issuer,
                Audience = _audience,
                SigningCredentials = credentials,
                // Expires = DateTime.UtcNow.AddMinutes(15) // if we also implement a refresh token 
                Expires = DateTime.UtcNow.AddDays(expiresInDays) // for now, let's set it to 30 days
            };

            var tokenHandler =  new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            
            // refresh token generation
            var refreshToken = GenerateRefreshToken(request.UserId.Value, request.Email, request.Role);
            RefreshTokens[refreshToken.Token] = refreshToken;
            // TODO: Persist refreshToken i databas eller minne (för demo: statiskt fält eller enkel lista)

            // Return the generated JWT token and refresh token
            return Task.FromResult(new TokenResponse
            {
                Succeeded = true,
                AccessToken = tokenHandler.WriteToken(token),
                RefreshToken = refreshToken.Token,
                Message = $"Token generated for user {request.Email ?? request.UserId.Value.ToString()}."
            });
        }
    
        catch (Exception ex)
        {
            // Return error if token generation fails
            return Task.FromResult(new TokenResponse { Succeeded = false, Message = ex.Message });
        }
    }

    public Task<ValidationResponse> ValidateAccessTokenAsync(ValidationRequest request)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        
        try
        {
            // Validate the JWT token signature and claims
            var principal = tokenHandler.ValidateToken(request.AccessToken, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)),
                ClockSkew = TimeSpan.Zero,
            }, out SecurityToken validatedToken);
            
            // Check if the token is a valid JWT
            var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                              ?? throw new NullReferenceException("UserId in claims is null");

            if (!Guid.TryParse(userIdClaim, out var userIdFromToken))
                throw new Exception("UserId in claims is not a valid Guid.");

            if (request.UserId is null)
                throw new Exception("UserId in request is null.");

            if (userIdFromToken != request.UserId.Value)
                throw new Exception("UserId in claims does not match UserId in request");

            var username = principal.FindFirst(ClaimTypes.Email)?.Value ?? userIdClaim;

            // Return success if token is valid and userId matches
            return Task.FromResult(new ValidationResponse { Succeeded = true, Message = $"Token is valid for {username}." });
        }
        catch (Exception ex)
        { return Task.FromResult(new ValidationResponse { Succeeded = false, Message = ex.Message }); }
    }

    public async Task<TokenResponse> RefreshAccessTokenAsync(RefreshTokenRequest request)
    {
        if (!RefreshTokens.TryGetValue(request.RefreshToken, out var storedToken))
            return new TokenResponse { Succeeded = false, Message = "Invalid refresh token." };
        if (storedToken.Used || storedToken.Revoked)
            return new TokenResponse { Succeeded = false, Message = "Refresh token already used or revoked." };
        if (storedToken.Expires < DateTime.UtcNow)
            return new TokenResponse { Succeeded = false, Message = "Refresh token expired." };
        if (storedToken.UserId != request.UserId)
            return new TokenResponse { Succeeded = false, Message = "Refresh token does not match user." };
        
        // mark as use (simple demo, in production: create new refresh token and revoke old one)
        storedToken.Used = true;
        RefreshTokens[storedToken.Token] = storedToken;
        
        // creat new access token and refresh token
        var tokenRequest = new TokenRequest { UserId = storedToken.UserId, Email = storedToken.Email, Role = storedToken.Role };
        var newAccessToken = await GenerateAccessTokenAsync(tokenRequest);
        return newAccessToken;
    }

    // helper method to ensure consistent role casing
    private static string CapitalizeRole(string role) =>
        string.IsNullOrWhiteSpace(role) ? "user" : char.ToUpper(role[0]) + role[1..].ToLower();

    // Helper: Generate a secure refresh token
    private static RefreshToken GenerateRefreshToken(Guid userId, string? email, string? role)
    {
        var randomBytes = new byte[64];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return new RefreshToken
        {
            Token = Convert.ToBase64String(randomBytes),
            UserId = userId,
            Expires = DateTime.UtcNow.AddDays(30), // 30 days expiration
            Created = DateTime.UtcNow,
            Email = email,
            Role = role
        };
    }
}
