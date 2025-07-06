
namespace TokenService.Api.Models;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Token { get; set; } = null!;
    public Guid UserId { get; set; }
    public DateTime ExpiresOnUtc { get; set; }
    public bool Revoked { get; set; } = false;
    public bool Used { get; set; } = false;
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public string? Email { get; set; }
    public string? Role { get; set; }
}
