using System.ComponentModel.DataAnnotations;

namespace TokenService.Api.Models;

public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = null!;
    
    [Required]
    public Guid UserId { get; set; }
}
