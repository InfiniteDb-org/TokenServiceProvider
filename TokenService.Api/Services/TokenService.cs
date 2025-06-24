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
}

public class TokenService : ITokenService
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly string _key;
    
    public TokenService()
    {
        // Read JWT configuration from environment variables
        _issuer = Environment.GetEnvironmentVariable("Issuer") ?? throw new InvalidOperationException("Issuer environment variable not set.");
        _audience = Environment.GetEnvironmentVariable("Audience") ?? throw new InvalidOperationException("Audience environment variable not set.");
        _key = Environment.GetEnvironmentVariable("Key") ?? throw new InvalidOperationException("Key environment variable not set.");
    }
    
    public async Task<TokenResponse> GenerateAccessTokenAsync(TokenRequest request, int expiresInDays = 30)
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
            List<Claim> claims = [
                new(ClaimTypes.NameIdentifier, request.UserId.Value.ToString()),
                new(JwtRegisteredClaimNames.Sub, request.UserId.Value.ToString())
            ];
            if (!string.IsNullOrEmpty(request.Email))
                claims.Add(new Claim(ClaimTypes.Email, request.Email));
            if (!string.IsNullOrEmpty(request.Role))
                claims.Add(new Claim(ClaimTypes.Role, request.Role));
            
            // Add all custom claims to the JWT, ensuring "Admin" is always capitalized and its value is lowercase.
            // This guarantees compatibility with the API's admin authorization policy.
            if (request.CustomClaims is not null)
            {
                foreach (var (key, value) in request.CustomClaims)
                {
                    var claimType = key.Equals("admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : key;
                    var claimValue = value.ToString()!.ToLowerInvariant();
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
                Expires = DateTime.UtcNow.AddDays(expiresInDays) // for now, lets set it to 30 days
            };

            var tokenHandler =  new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            
            // Return the generated JWT token
            return new TokenResponse
            {
                Succeeded = true,
                AccessToken = tokenHandler.WriteToken(token),
                Message = $"Token generated for user {request.Email ?? request.UserId.Value.ToString()}."
            };
        }
    
        catch (Exception ex)
        {
            // Return error if token generation fails
            return new TokenResponse { Succeeded = false, Message = ex.Message };
        }
    }

    public async Task<ValidationResponse> ValidateAccessTokenAsync(ValidationRequest request)
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
            return new ValidationResponse { Succeeded = true, Message = $"Token is valid for {username}." };
        }
        catch (Exception ex)
        { return new ValidationResponse { Succeeded = false, Message = ex.Message }; }
    }
}
