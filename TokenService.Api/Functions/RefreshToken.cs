using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TokenService.Api.Models;
using TokenService.Api.Services;

namespace TokenService.Api.Functions;

public class RefreshTokenFunction(ITokenService tokenService)
{
    private readonly ITokenService _tokenService = tokenService;

    [Function("RefreshToken")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "refresh-token")] HttpRequestData req,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger("RefreshTokenFunction");
        var requestBody = await req.ReadAsStringAsync();
        logger.LogInformation("RAW BODY: {RequestBody}", requestBody);
        var refreshRequest = JsonConvert.DeserializeObject<RefreshTokenRequest>(requestBody);
        logger.LogInformation("DESERIALIZED: {Result}", refreshRequest == null ? "null" : $"UserId={refreshRequest.UserId}, RefreshToken={refreshRequest.RefreshToken}");
        if (refreshRequest == null || string.IsNullOrEmpty(refreshRequest.RefreshToken))
            return new BadRequestObjectResult(new { Succeeded = false, Message = "Invalid refresh token request." });

        var response = await _tokenService.RefreshAccessTokenAsync(refreshRequest);
        if (!response.Succeeded)
            return new BadRequestObjectResult(response);

        return new OkObjectResult(response);
    }
}
