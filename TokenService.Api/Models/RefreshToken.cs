using System;

namespace TokenService.Api.Models;

public class RefreshToken
{
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public DateTime Expires { get; set; }
    public bool Revoked { get; set; } = false;
    public bool Used { get; set; } = false;
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public string? Email { get; set; }
    public string? Role { get; set; }
}
