using System.Net;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ParNegar.Shared.DTOs.Common;
using ParNegar.Shared.Exceptions;

namespace ParNegar.API.Middleware;

/// <summary>
/// Global exception handler middleware
/// تبدیل همه Exception ها به JSON با ساختار یکپارچ
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.TraceIdentifier;

        // استخراج Inner Exception های تو در تو
        var innerException = GetInnermostException(exception);

        // Log exception
        _logger.LogError(exception,
            "Unhandled exception occurred. TraceId: {TraceId}, Path: {Path}, InnerException: {InnerException}",
            correlationId, context.Request.Path, innerException.Message);

        var response = context.Response;
        response.ContentType = "application/json";

        var errorResponse = new ErrorResponse
        {
            TraceId = correlationId,
            Timestamp = DateTime.UtcNow,
            Path = context.Request.Path,
            Method = context.Request.Method
        };

        // Map exception to appropriate status code and message
        switch (exception)
        {
            case UnauthorizedException ex:
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                errorResponse.StatusCode = 401;
                errorResponse.Title = "Unauthorized";
                errorResponse.Message = ex.Message;
                // ⚠️ برای کلاینت: بررسی statusCode === 401
                break;

            case ForbiddenException ex:
                response.StatusCode = (int)HttpStatusCode.Forbidden;
                errorResponse.StatusCode = 403;
                errorResponse.Title = "Forbidden";
                errorResponse.Message = ex.Message;
                break;

            case EntityNotFoundException ex:
                response.StatusCode = (int)HttpStatusCode.NotFound;
                errorResponse.StatusCode = 404;
                errorResponse.Title = "Not Found";
                errorResponse.Message = ex.Message;
                break;

            case BusinessValidationException ex:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.StatusCode = 400;
                errorResponse.Title = "Validation Error";
                errorResponse.Message = ex.Message;
                errorResponse.Errors = ex.Errors;
                break;

            case ConcurrencyException ex:
                response.StatusCode = (int)HttpStatusCode.Conflict;
                errorResponse.StatusCode = 409;
                errorResponse.Title = "Concurrency Conflict";
                errorResponse.Message = ex.Message;
                break;

            case FluentValidation.ValidationException ex:
                response.StatusCode = (int)HttpStatusCode.UnprocessableEntity;
                errorResponse.StatusCode = 422;
                errorResponse.Title = "Validation Failed";
                errorResponse.Message = "One or more validation errors occurred.";
                errorResponse.Errors = ex.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                break;

            case TimeoutException ex:
                response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                errorResponse.StatusCode = 408;
                errorResponse.Title = "Request Timeout";
                errorResponse.Message = "The request timed out. Please try again.";
                break;

            case ArgumentException ex:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.StatusCode = 400;
                errorResponse.Title = "Invalid Argument";
                errorResponse.Message = ex.Message;
                break;

            case NotSupportedException ex:
                response.StatusCode = (int)HttpStatusCode.NotImplemented;
                errorResponse.StatusCode = 501;
                errorResponse.Title = "Not Supported";
                errorResponse.Message = ex.Message;
                break;

            case DbUpdateException ex:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.StatusCode = 400;
                errorResponse.Title = "Database Error";

                // بررسی اینکه آیا Inner Exception یک SqlException است
                if (innerException is SqlException innerSqlEx)
                {
                    errorResponse.Message = TranslateSqlError(innerSqlEx);
                }
                else
                {
                    errorResponse.Message = "An error occurred while updating the database.";
                }
                break;

            case SqlException sqlException:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.StatusCode = 400;
                errorResponse.Title = "Database Error";
                errorResponse.Message = TranslateSqlError(sqlException);
                break;

            case InvalidOperationException ex:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.StatusCode = 400;
                errorResponse.Title = "Invalid Operation";
                errorResponse.Message = _environment.IsDevelopment()
                    ? ex.Message
                    : "The requested operation is not valid.";
                break;

            default:
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                errorResponse.StatusCode = 500;
                errorResponse.Title = "Internal Server Error";
                errorResponse.Message = _environment.IsDevelopment()
                    ? exception.Message
                    : "An error occurred while processing your request.";

                // فقط در Development اطلاعات بیشتر نشان بده
                if (_environment.IsDevelopment())
                {
                    errorResponse.Details = new
                    {
                        Type = exception.GetType().Name,
                        StackTrace = exception.StackTrace,
                        InnerException = innerException.Message,
                        InnerExceptionType = innerException.GetType().Name
                    };
                }
                break;
        }

        var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _environment.IsDevelopment(),
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        await response.WriteAsync(jsonResponse);
    }

    /// <summary>
    /// استخراج آخرین Inner Exception به صورت بازگشتی
    /// </summary>
    private static Exception GetInnermostException(Exception exception)
    {
        while (exception.InnerException != null)
        {
            exception = exception.InnerException;
        }
        return exception;
    }

    /// <summary>
    /// ترجمه کدهای خطای SQL Server به پیام‌های کاربرپسند فارسی
    /// </summary>
    private static string TranslateSqlError(SqlException sqlException)
    {
        var errorNumber = sqlException.Number;

        return errorNumber switch
        {
            // Connection Errors
            2 => "خطا در اتصال به پایگاه داده. لطفاً بعداً تلاش کنید.",

            // Duplicate Key Violations
            2601 or 2627 => ExtractDuplicateKeyMessage(sqlException.Message),

            // Foreign Key Violations
            547 => ExtractForeignKeyMessage(sqlException.Message),

            // NULL Constraint Violations
            515 => ExtractNullConstraintMessage(sqlException.Message),

            // Identity Insert Errors
            544 => "خطا در درج شناسه. لطفاً با پشتیبانی تماس بگیرید.",

            // String Truncation
            8152 => "یکی از فیلدها بیش از حد طولانی است.",

            // Login Failures
            18456 => "خطا در احراز هویت پایگاه داده.",

            // Timeout
            -2 => "زمان اتصال به پایگاه داده تمام شد. لطفاً دوباره تلاش کنید.",

            // Default
            _ => "خطای پایگاه داده رخ داده است."
        };
    }

    /// <summary>
    /// استخراج پیام خطای کلید تکراری
    /// </summary>
    private static string ExtractDuplicateKeyMessage(string message)
    {
        // مثال: "Cannot insert duplicate key in object 'dbo.Users'..."
        // پیام فارسی: "این مقدار قبلاً ثبت شده است."

        if (message.Contains("Username", StringComparison.OrdinalIgnoreCase))
            return "این نام کاربری قبلاً ثبت شده است.";

        if (message.Contains("Email", StringComparison.OrdinalIgnoreCase))
            return "این ایمیل قبلاً ثبت شده است.";

        return "این مقدار تکراری است و قبلاً ثبت شده است.";
    }

    /// <summary>
    /// استخراج پیام خطای Foreign Key
    /// </summary>
    private static string ExtractForeignKeyMessage(string message)
    {
        if (message.Contains("DELETE", StringComparison.OrdinalIgnoreCase))
            return "این رکورد قابل حذف نیست زیرا در جای دیگری استفاده شده است.";

        return "عملیات ناموفق. ارتباط این رکورد با سایر داده‌ها نقض شده است.";
    }

    /// <summary>
    /// استخراج پیام خطای NULL Constraint
    /// </summary>
    private static string ExtractNullConstraintMessage(string message)
    {
        // مثال: "Cannot insert NULL into column 'Username'"
        // استخراج نام فیلد
        var startIndex = message.IndexOf("'") + 1;
        var endIndex = message.IndexOf("'", startIndex);

        if (startIndex > 0 && endIndex > startIndex)
        {
            var fieldName = message.Substring(startIndex, endIndex - startIndex);
            return $"فیلد '{fieldName}' الزامی است و نمی‌تواند خالی باشد.";
        }

        return "یکی از فیلدهای الزامی خالی است.";
    }
}

/// <summary>
/// Extension method for adding middleware
/// </summary>
public static class GlobalExceptionHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}
