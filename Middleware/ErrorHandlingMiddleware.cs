using System.Net;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BeastVault.Api.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        var (statusCode, message, details) = exception switch
        {
            DbUpdateException { InnerException: SqliteException sqEx } => sqEx.SqliteErrorCode == 19
                ? ((int)HttpStatusCode.Conflict, "Conflict: duplicate or constraint violation", sqEx.Message)
                : ((int)HttpStatusCode.BadRequest, "Database error", sqEx.Message),
            DbUpdateException dbEx =>
                ((int)HttpStatusCode.BadRequest, "Error saving data", dbEx.InnerException?.Message ?? dbEx.Message),
            ArgumentException argEx =>
                ((int)HttpStatusCode.BadRequest, "Invalid data", argEx.Message),
            UnauthorizedAccessException =>
                ((int)HttpStatusCode.Forbidden, "Access denied", string.Empty),
            KeyNotFoundException knfEx =>
                ((int)HttpStatusCode.NotFound, "Resource not found", knfEx.Message),
            _ =>
                ((int)HttpStatusCode.InternalServerError, "An unexpected error occurred", string.Empty)
        };

        context.Response.StatusCode = statusCode;
        var result = JsonSerializer.Serialize(new { message, details });
        await context.Response.WriteAsync(result);
    }
}
