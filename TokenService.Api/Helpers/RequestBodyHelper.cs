using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TokenService.Api.Models;

namespace TokenService.Api.Helpers;

// Helper class for reading and validating HTTP request bodies in Azure Functions
public static class RequestBodyHelper
{
    // Reads and validates the request body, deserializing it to the specified type (Azure Functions Worker SDK)
    public static async Task<ResponseResult<T>> ReadAndValidateRequestBody<T>(HttpRequestData req, ILogger logger)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        logger.LogInformation("Raw request body: {Body}", body);
        if (string.IsNullOrEmpty(body))
        {
            logger.LogWarning("Request body is empty.");
            return ResponseResult<T>.Failure("Request body is empty.");
        }

        try
        {
            var request = JsonConvert.DeserializeObject<T>(body);
            if (request == null)
            {
                logger.LogWarning("Deserialization returned null for body: {Body}", body);
                return ResponseResult<T>.Failure("Invalid request format.");
            }
            return ResponseResult<T>.Success(request);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deserialize request body: {Body}", body);
            return ResponseResult<T>.Failure("Invalid JSON format in request body.");
        }
    }
}