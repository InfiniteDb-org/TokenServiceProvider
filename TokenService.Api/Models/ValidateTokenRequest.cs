using System.ComponentModel.DataAnnotations;

namespace TokenService.Api.Models;

public class ValidateTokenRequest
{
    [Required]
    public Guid? UserId { get; set; }
    
    [Required]
    public string AccessToken { get; set; } = null!;
    
    // [Required]
    // public string RefreshToken { get; set; } = null!;
}