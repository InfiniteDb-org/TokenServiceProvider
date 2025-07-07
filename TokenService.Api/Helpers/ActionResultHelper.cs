using Microsoft.AspNetCore.Mvc;
using TokenService.Api.Models;

namespace TokenService.Api.Helpers;

// Centralizes mapping of domain result to HTTP response for consistency
public static class ActionResultHelper
{
    // helpers to ensure all responses are shaped the same way
    private static OkObjectResult Ok<T>(ResponseResult<T> result) => new(result);
    private static OkObjectResult Ok(ResponseResult result) => new(result);
    private static BadRequestObjectResult BadRequest(string? message) => new(new SimpleResponseResult { Succeeded = false, Message = message });
    private static NotFoundObjectResult NotFound(string? message) => new(new SimpleResponseResult { Succeeded = false, Message = message });
    private static ConflictObjectResult Conflict(string? message) => new(new SimpleResponseResult { Succeeded = false, Message = message });
    private static UnauthorizedObjectResult Unauthorized(string? message) => new(new SimpleResponseResult { Succeeded = false, Message = message });

    // Maps error message to correct HTTP status
    private static IActionResult MapError(string? message)
    {
        var msg = message?.ToLowerInvariant();
        if (msg?.Contains("not found") == true)
            return NotFound(message);
        if (msg?.Contains("unauthorized") == true || 
            msg?.Contains("invalid credentials") == true)
            return Unauthorized(message);
        if (msg?.Contains("already exists") == true)
            return Conflict(message);
        return BadRequest(message);
    }

    public static IActionResult CreateResponse<T>(ResponseResult<T> result)
    {
        if (result.Succeeded)
            return Ok(result);
        return MapError(result.Message);
    }

    public static IActionResult CreateResponse(ResponseResult result)
    {
        if (result.Succeeded)
            return Ok(result);
        return MapError(result.Message);
    }
}
