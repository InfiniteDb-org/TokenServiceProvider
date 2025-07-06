using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace TokenService.Api.Functions;

public class ManualCleanupFunction(Services.TokenService tokenService)
{
    private readonly Services.TokenService _tokenService = tokenService;

    [Function("ManualCleanup")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "manual-cleanup")] HttpRequestData req)
    {
        // set daysToKeep to 0 to delete all old/used/timed out tokens directly
        var deletedCount = await _tokenService.CleanupOldRefreshTokensAsync(0);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync($"Deleted {deletedCount} tokens");
        return response;
    }
}