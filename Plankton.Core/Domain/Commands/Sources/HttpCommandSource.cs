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

    // Updated to return a value
    public event Func<CommandContext, Task<object?>>? CommandReceived;

    private WebApplication? _app;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        LogStartingCommandSource(logger);

        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();

        _app = builder.Build();

        // Global exception handling
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
            catch (Exception ex)
            {
                var correlationId = context.Response.Headers.ContainsKey(XCorrelationId)
                    ? context.Response.Headers[XCorrelationId].ToString()
                    : Guid.NewGuid().ToString("N");

                context.Response.Headers[XCorrelationId] = correlationId;

                logger.LogError(ex, "Unhandled exception (CorrelationId: {CorrelationId})", correlationId);

                var problem = ex switch
                {
                    UnauthorizedAccessException => new ProblemDetails
                    {
                        Status = StatusCodes.Status401Unauthorized,
                        Title = "Unauthorized",
                        Type = "https://example.com/problems/unauthorized",
                        Instance = context.Request.Path
                    },
                    InvalidCommandException ice => new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Invalid command",
                        Type = "https://example.com/problems/invalid-command",
                        Detail = ice.Message,
                        Instance = context.Request.Path
                    },
                    _ => new ProblemDetails
                    {
                        Status = StatusCodes.Status500InternalServerError,
                        Title = "Internal server error",
                        Type = "https://example.com/problems/internal-error",
                        Detail = ex.Message,
                        Instance = context.Request.Path
                    }
                };

                context.Response.ContentType = "application/problem+json";
                context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(problem, cancellationToken);
            }
        });

        _app.MapPost("/command", async (HttpContext context, CommandRequestModel request) =>
        {
            if (CommandReceived is null)
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

            var correlationId = context.Request.Headers.TryGetValue(XCorrelationId, out var cid)
                ? cid.ToString()
                : Guid.NewGuid().ToString("N");

            context.Response.Headers[XCorrelationId] = correlationId;

            var token = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

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

            return result is null ? Results.Accepted() : Results.Ok(result);
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
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error stopping HttpCommandSource");
                }
            }, cancellationToken);
        });
    }

    [LoggerMessage(LogLevel.Information, "Starting HttpCommandSource...")]
    static partial void LogStartingCommandSource(ILogger<HttpCommandSource> logger);
}