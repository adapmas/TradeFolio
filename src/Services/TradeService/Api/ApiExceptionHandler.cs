using Microsoft.AspNetCore.Diagnostics;
using TradeFolio.Common.Errors;

namespace TradeService.Api;

/// <summary>
/// Last line of defence: whatever goes wrong, the caller gets the standard error shape and a trace id,
/// never an exception message, a stack trace or a SQL error. The details go to the log only.
/// </summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    private const string BaseUri = "https://api.tradefolio.local/errors/";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // The caller disconnected: nothing to report and nobody to report it to.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = 499;
            return true;
        }

        ApiProblem problem;

        if (exception is BadHttpRequestException bad)
        {
            // Malformed JSON, wrong content type, body too large: the caller's mistake, not a server fault.
            var title = bad.StatusCode switch
            {
                StatusCodes.Status413PayloadTooLarge => "Request body too large",
                StatusCodes.Status415UnsupportedMediaType => "Unsupported media type",
                _ => "Malformed request"
            };

            logger.LogInformation(
                "Rejected a bad request ({Status}). TraceId {TraceId}", bad.StatusCode, httpContext.TraceIdentifier);

            problem = new ApiProblem(BaseUri + "bad-request", title, bad.StatusCode, httpContext.TraceIdentifier);
        }
        else
        {
            // The full exception goes to the log; the caller only gets the trace id to quote.
            logger.LogError(exception, "Unhandled exception. TraceId {TraceId}", httpContext.TraceIdentifier);

            problem = new ApiProblem(
                BaseUri + "internal",
                "An unexpected error occurred",
                StatusCodes.Status500InternalServerError,
                httpContext.TraceIdentifier);
        }

        await ApiResults.Problem(httpContext, problem).ExecuteAsync(httpContext);
        return true;
    }
}