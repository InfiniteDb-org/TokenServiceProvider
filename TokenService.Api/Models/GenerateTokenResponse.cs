
namespace TokenService.Api.Models;


public class TokenResponse : ResponseResult
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
}
