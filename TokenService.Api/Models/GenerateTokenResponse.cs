
namespace TokenService.Api.Models;


public class GenerateTokenResponse : ResponseResult
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
}
