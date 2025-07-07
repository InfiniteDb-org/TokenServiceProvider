using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TokenService.Api.Services;

namespace TokenService.Api.Functions;

public class CleanupRefreshTokensFunction(ITokenService tokenService, ILoggerFactory loggerFactory)
{
    private readonly ITokenService _tokenService = tokenService;
    private readonly ILogger _logger = loggerFactory.CreateLogger<CleanupRefreshTokensFunction>();
    
    // Scheduled Azure Function: cleans up expired/used refresh tokens at night
    [Function("CleanupRefreshTokensFunction")]
    public async Task Run([TimerTrigger("0 0 2 * * *")] TimerInfo myTimer)
    {
        _logger.LogInformation($"CleanupRefreshTokensFunction triggered at: {DateTime.UtcNow}");
        try
        {
            // Calls service to remove old/used refresh tokens from DB
            var removedCount = await _tokenService.CleanupOldRefreshTokensAsync();
            _logger.LogInformation($"Cleanup complete. Removed {removedCount} old/used refresh tokens.");
        }
        catch (Exception ex)
        { _logger.LogError(ex, "Error during refresh token cleanup."); }
    }
}
