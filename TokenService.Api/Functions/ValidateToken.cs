using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using TokenService.Api.Helpers;
using TokenService.Api.Models;
using TokenService.Api.Services;

namespace TokenService.Api.Functions;

public class ValidateToken(ITokenService tokenService)
{
    private readonly ITokenService _tokenService = tokenService;

    [Function("ValidateToken")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger("ValidateToken");

        var bodyResult = await RequestBodyHelper.ReadAndValidateRequestBody<ValidateTokenRequest>(req, logger);
        logger.LogInformation("BodyResult: Succeeded={Succeeded}, Message={Message}", bodyResult.Succeeded, bodyResult.Message);

        if (!bodyResult.Succeeded)
            return ActionResultHelper.CreateResponse(bodyResult);

        var validationRequest = bodyResult.Data!;
        logger.LogInformation("ValidationRequest: userId={UserId}, accessToken={AccessToken}", validationRequest.UserId, validationRequest.AccessToken);

        var tokenResponse = await _tokenService.ValidateAccessTokenAsync(validationRequest);
        logger.LogInformation("TokenResponse: Succeeded={Succeeded}, Message={Message}", tokenResponse.Succeeded, tokenResponse.Message);

        return ActionResultHelper.CreateResponse(tokenResponse);
    }
}