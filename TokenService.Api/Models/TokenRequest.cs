using System.ComponentModel.DataAnnotations;

namespace TokenService.Api.Models;

public class TokenRequest
{
    [Required]
    public Guid? UserId { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public Dictionary<string, object>? CustomClaims { get; set; }
}