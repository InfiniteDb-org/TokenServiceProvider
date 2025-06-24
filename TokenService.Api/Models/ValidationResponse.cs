namespace TokenService.Api.Models;

public class ValidationResponse
{
    public bool Succeeded { get; set; }
    public string? Message { get; set; }
    // public string? NewAccessToken { get; set; }
}