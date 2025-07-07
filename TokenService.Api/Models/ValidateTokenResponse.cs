namespace TokenService.Api.Models;

public class ValidationResponse : ResponseResult
{
    public bool Succeeded { get; set; }
    public string? Message { get; set; }
    // public string? NewAccessToken { get; set; }
}