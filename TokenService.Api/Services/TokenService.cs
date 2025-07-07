using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TokenService.Api.Infrastructure.Database;
using TokenService.Api.Models;

namespace TokenService.Api.Services;

public interface ITokenService
{
    Task<GenerateTokenResponse> GenerateAccessTokenAsync(GenerateTokenRequest request, int expiresInMinutes = 0);
    Task<ValidateTokenResponse> ValidateAccessTokenAsync(ValidateTokenRequest request);
    Task<GenerateTokenResponse> RefreshAccessTokenAsync(RefreshTokenRequest request);
    Task<int> CleanupOldRefreshTokensAsync(int daysToKeep = 30);
}

public class TokenService: ITokenService
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly string _key;
    private readonly int _expiresInMinutes;
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    
    public TokenService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
        _issuer = configuration["Issuer"] ?? throw new NullReferenceException("Issuer config not set");
        _audience = configuration["Audience"] ?? throw new NullReferenceException("Audience config not set");
        _key = configuration["Key"] ?? throw new NullReferenceException("Key config not set");

        // config key for access token expiration
        if (!int.TryParse(configuration["ExpiresInMinutes"], out _expiresInMinutes)) _expiresInMinutes = 10; 
    }
    

    // Generates a new JWT access token and refresh token for the specified user and persists the refresh token.
    public async Task<GenerateTokenResponse> GenerateAccessTokenAsync(GenerateTokenRequest request, int expiresInMinutes = 0)
    {
        try
        {
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
            
            // Add custom claims
            if (request.CustomClaims is not null)
            {
                foreach (var (key, value) in request.CustomClaims)
                {
                    var claimType = char.ToUpper(key[0]) + key[1..];
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
                Expires = DateTime.UtcNow.AddMinutes(expiresInMinutes == 0 ? _expiresInMinutes : expiresInMinutes)
            };

            var tokenHandler =  new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            
            // Generate and persist a new refresh token for the user
            var refreshToken = GenerateRefreshToken(request.UserId.Value, request.Email, request.Role);
            await _context.RefreshTokens.AddAsync(refreshToken);
            await _context.SaveChangesAsync();
            

            // Return the generated JWT token 
            return new GenerateTokenResponse
            {
                Succeeded = true,
                AccessToken = tokenHandler.WriteToken(token),
                RefreshToken = refreshToken.Token,
                Message = $"Token generated for user {request.Email ?? request.UserId.Value.ToString()}."
            };
        }
    
        catch (Exception ex)
        { return new GenerateTokenResponse { Succeeded = false, Message = ex.Message }; }
    }

    public Task<ValidateTokenResponse> ValidateAccessTokenAsync(ValidateTokenRequest request)
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
            return Task.FromResult(new ValidateTokenResponse { Succeeded = true, Message = $"Token is valid for {username}." });
        }
        catch (Exception ex)
        { return Task.FromResult(new ValidateTokenResponse { Succeeded = false, Message = ex.Message }); }
    }

    public async Task<GenerateTokenResponse> RefreshAccessTokenAsync(RefreshTokenRequest request)
    {
        var storedToken = await _context.RefreshTokens.FirstOrDefaultAsync(r => r.Token == request.RefreshToken);
        if (storedToken == null)
            return new GenerateTokenResponse { Succeeded = false, Message = "Invalid refresh token." };
        if (storedToken.Used || storedToken.Revoked)
            return new GenerateTokenResponse { Succeeded = false, Message = "Refresh token already used or revoked." };
        if (storedToken.ExpiresOnUtc < DateTime.UtcNow)
            return new GenerateTokenResponse { Succeeded = false, Message = "Refresh token expired." };
        if (storedToken.UserId != request.UserId)
            return new GenerateTokenResponse { Succeeded = false, Message = "Refresh token does not match user." };

        // Mark old as used
        storedToken.Used = true;
        await _context.SaveChangesAsync();
        
        // create new refresh token
        var newRefreshToken = GenerateRefreshToken(storedToken.UserId, storedToken.Email, storedToken.Role);
        await _context.RefreshTokens.AddAsync(newRefreshToken);
        await _context.SaveChangesAsync();
        
        // create new access token
        var tokenRequest = new GenerateTokenRequest { UserId = storedToken.UserId, Email = storedToken.Email, Role = storedToken.Role };
        var newAccessToken = await GenerateAccessTokenAsync(tokenRequest);
        
        // return both tokens
        return new GenerateTokenResponse
        {
            Succeeded = true,
            AccessToken = newAccessToken.AccessToken,
            RefreshToken = newRefreshToken.Token,
            Message = "Access and refresh token rotated."
        };
    }

    // helper method to ensure consistent role casing
    private static string CapitalizeRole(string role) =>
        string.IsNullOrWhiteSpace(role) ? "user" : char.ToUpper(role[0]) + role[1..].ToLower();

    // Helper: Generate a secure refresh token
    private RefreshToken GenerateRefreshToken(Guid userId, string? email, string? role)
    {
        var randomBytes = new byte[64];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        
        // config key for refresh token expiration (days)
        var refreshTokenExpiresInDays = _configuration.GetValue("RefreshTokenExpiresInDays", 7);
        return new RefreshToken
        {
            Token = Convert.ToBase64String(randomBytes),
            UserId = userId,
            ExpiresOnUtc = DateTime.UtcNow.AddDays(refreshTokenExpiresInDays),
            Created = DateTime.UtcNow,
            Email = email,
            Role = role
        };
    }
    
    // Helper: Cleanup old refresh tokens
    public async Task<int> CleanupOldRefreshTokensAsync(int daysToKeep = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(-daysToKeep);
        var tokensToDelete = await _context.RefreshTokens
            .Where(t => t.Used || t.Revoked || t.ExpiresOnUtc < DateTime.UtcNow)
            .Where(t => t.Created < cutoff)
            .ToListAsync();

        _context.RefreshTokens.RemoveRange(tokensToDelete);
        await _context.SaveChangesAsync();
        return tokensToDelete.Count;
    }
}
