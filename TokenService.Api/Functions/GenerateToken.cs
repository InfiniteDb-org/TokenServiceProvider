using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TokenService.Api.Models;
using TokenService.Api.Services;
using JsonException = System.Text.Json.JsonException;

namespace TokenService.Api.Functions;

public class GenerateToken(ILogger<GenerateToken> logger, ITokenService tokenService)
{
    private readonly ILogger<GenerateToken> _logger = logger;
    private readonly ITokenService _tokenService = tokenService;

    private static BadRequestObjectResult BadRequest(string? message) => new(new TokenResponse { Succeeded = false, Message = message });

    [Function("GenerateToken")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
    {
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            if (string.IsNullOrEmpty(body))
            {
                const string message = "Request body is empty.";
                _logger.LogWarning(message);
                return BadRequest(message);
            }

            TokenRequest? tokenRequest;
            try
            {
                tokenRequest = JsonConvert.DeserializeObject<TokenRequest>(body);
                if (tokenRequest == null)
                    throw new JsonException("Deserialization returned null");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize request body.");
                return BadRequest("Invalid JSON format in request body.");
            }

            var tokenResponse = await _tokenService.GenerateAccessTokenAsync(tokenRequest);
            return tokenResponse.Succeeded
                ? new OkObjectResult(tokenResponse)
                : BadRequest(tokenResponse.Message);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Unhandled exception in GenerateToken.");
            return BadRequest("Internal server error while generating token.");
        }
    }
}