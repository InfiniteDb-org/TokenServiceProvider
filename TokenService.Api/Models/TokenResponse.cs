using System.ComponentModel.DataAnnotations;

namespace TokenService.Api.Models;

public class TokenResponse
{
    [Required]
    public bool Succeeded { get; set; }
    
    [Required]
    public string? AccessToken { get; set; }
    public string? Message { get; set; }
    public string? RefreshToken { get; set; }
}