using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using TokenService.Api.Helpers;
using TokenService.Api.Models;
using TokenService.Api.Services;

namespace TokenService.Api.Functions;

public class RefreshToken(ITokenService tokenService)
{
    private readonly ITokenService _tokenService = tokenService;

    [Function("RefreshToken")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger("RefreshToken");

        var bodyResult = await RequestBodyHelper.ReadAndValidateRequestBody<RefreshTokenRequest>(req, logger);
        if (!bodyResult.Succeeded)
            return ActionResultHelper.CreateResponse(bodyResult);

        var request = bodyResult.Data!;
        var tokenResponse = await _tokenService.RefreshAccessTokenAsync(request);
        return ActionResultHelper.CreateResponse(tokenResponse);
    }
}
