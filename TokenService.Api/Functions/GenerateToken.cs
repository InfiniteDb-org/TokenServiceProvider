using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using TokenService.Api.Helpers;
using TokenService.Api.Models;
using TokenService.Api.Services;

namespace TokenService.Api.Functions;

public class GenerateToken(ITokenService tokenService)
{
    private readonly ITokenService _tokenService = tokenService;

    [Function("GenerateToken")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger("GenerateToken");

        var bodyResult = await RequestBodyHelper.ReadAndValidateRequestBody<GenerateTokenRequest>(req, logger);
        if (!bodyResult.Succeeded)
            return ActionResultHelper.CreateResponse(bodyResult);

        var request = bodyResult.Data!;
        var tokenResponse = await _tokenService.GenerateAccessTokenAsync(request);
        return ActionResultHelper.CreateResponse(tokenResponse);
    }
}