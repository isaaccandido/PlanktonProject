using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.Commands.Infrastructure;
using Plankton.Core.Domain.ExceptionHandling;
using Plankton.Core.Domain.Models;
using Plankton.Core.Enums;
using Plankton.Core.Interfaces;

namespace Plankton.Core.Domain.Commands.Sources;

public sealed partial class HttpCommandSource(ILogger<HttpCommandSource> logger) : ICommandSource
{
    private const string XCorrelationId = "X-Correlation-Id";
    private const string Authorization = "Authorization";
    private const string BearerPrefix = "Bearer ";
    private const string CommandEndpoint = "/command";
    private const string BaseAddress = "https://isaaccandido.com/plankton"; // TODO: make configurable

    public event Func<CommandContext, Task<object?>>? CommandReceived;

    private WebApplication? _app;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        LogStartingCommandSource(logger);

        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();

        _app = builder.Build();

        _app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (OperationCanceledException)
            {
                // ignore shutdown cancellations
            }
            catch (DomainException de)
            {
                var correlationId = context.Response.Headers.ContainsKey(XCorrelationId)
                    ? context.Response.Headers[XCorrelationId].ToString()
                    : Guid.NewGuid().ToString("N");

                context.Response.Headers[XCorrelationId] = correlationId;

                switch (de)
                {
                    case InvalidCommandException ice:
                        LogInvalidCommand(logger, correlationId, ice.Message);
                        await HandleProblemAsync(
                            context,
                            StatusCodes.Status400BadRequest,
                            "Invalid command",
                            $"{BaseAddress}/problems/invalid-command",
                            ice.Message,
                            context.Request.Path,
                            ice.AllowedArgs
                        );
                        break;

                    case UnauthorizedCommandException _:
                        await HandleProblemAsync(
                            context,
                            StatusCodes.Status401Unauthorized,
                            "Unauthorized",
                            $"{BaseAddress}/problems/unauthorized",
                            de.Message,
                            context.Request.Path
                        );
                        break;

                    case RateLimitExceededException _:
                        await HandleProblemAsync(
                            context,
                            StatusCodes.Status429TooManyRequests,
                            "Rate limit exceeded",
                            $"{BaseAddress}/problems/rate-limit-exceeded",
                            de.Message,
                            context.Request.Path
                        );
                        break;

                    default:
                        await HandleProblemAsync(
                            context,
                            StatusCodes.Status400BadRequest,
                            "Domain error",
                            $"{BaseAddress}/problems/domain-error",
                            de.Message,
                            context.Request.Path
                        );
                        break;
                }
            }
            catch (Exception ex)
            {
                var correlationId = context.Response.Headers.ContainsKey(XCorrelationId)
                    ? context.Response.Headers[XCorrelationId].ToString()
                    : Guid.NewGuid().ToString("N");

                context.Response.Headers[XCorrelationId] = correlationId;

                LogUnhandledExceptionCorrelationId(logger, correlationId);

                await HandleProblemAsync(
                    context,
                    StatusCodes.Status500InternalServerError,
                    "Internal server error",
                    $"{BaseAddress}/problems/internal-error",
                    ex.Message,
                    context.Request.Path
                );
            }
        });

        _app.MapPost(CommandEndpoint, async (HttpContext context, CommandRequestModel request) =>
        {
            if (CommandReceived is null)
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

            var correlationId = context.Request.Headers.TryGetValue(XCorrelationId, out var cid)
                ? cid.ToString()
                : Guid.NewGuid().ToString("N");

            context.Response.Headers[XCorrelationId] = correlationId;

            var token = context.Request.Headers[Authorization].ToString().Replace(BearerPrefix, string.Empty);

            var command = new CommandModel
            {
                Name = request.Name,
                Args = request.Args,
                Source = SourceType.Http,
                SenderId = $"{context.Connection.RemoteIpAddress}:{context.Connection.RemotePort}-{correlationId}"
            };

            var commandContext = new CommandContext
            {
                Command = command,
                Token = token,
                CorrelationId = correlationId
            };

            var result = await CommandReceived.Invoke(commandContext);

            return result is null
                ? Results.Accepted()
                : Results.Ok(result);
        });

        await _app.StartAsync(cancellationToken);

        cancellationToken.Register(() =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    if (_app is not null)
                        await _app.StopAsync(CancellationToken.None);
                }
                catch (Exception)
                {
                    LogErrorStoppingHttpCommandSource(logger);
                }
            }, cancellationToken);
        });
    }

    private static async Task HandleProblemAsync(
        HttpContext context,
        int statusCode,
        string title,
        string type,
        string? detail,
        string instance,
        string[]? allowedArgs = null)
    {
        var correlationId = context.Response.Headers.ContainsKey(XCorrelationId)
            ? context.Response.Headers[XCorrelationId].ToString()
            : Guid.NewGuid().ToString("N");

        context.Response.Headers[XCorrelationId] = correlationId;

        var finalDetail = detail;
        if (allowedArgs is { Length: > 0 }) finalDetail += $" Allowed arguments: {string.Join(", ", allowedArgs)}";

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = type,
            Detail = finalDetail,
            Instance = instance
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsJsonAsync(problem);
    }

    [LoggerMessage(LogLevel.Error, "Invalid command (CorrelationId: {correlationId}): {message}")]
    static partial void LogInvalidCommand(ILogger<HttpCommandSource> logger, string correlationId, string message);

    [LoggerMessage(LogLevel.Error, "Unhandled exception (CorrelationId: {correlationId})")]
    static partial void LogUnhandledExceptionCorrelationId(ILogger<HttpCommandSource> logger, string correlationId);

    [LoggerMessage(LogLevel.Information, "Starting HttpCommandSource...")]
    static partial void LogStartingCommandSource(ILogger<HttpCommandSource> logger);

    [LoggerMessage(LogLevel.Error, "Error stopping HttpCommandSource")]
    static partial void LogErrorStoppingHttpCommandSource(ILogger<HttpCommandSource> logger);
}