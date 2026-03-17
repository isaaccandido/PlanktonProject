using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Plankton.Core.Domain.Commands.Infrastructure;
using Plankton.Core.Domain.ExceptionHandling;
using Plankton.Core.Domain.Models;
using Plankton.Core.Enums;
using Plankton.Core.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Plankton.Core.Domain.Commands.Sources;

public sealed class TelegramCommandSource(ILogger<TelegramCommandSource> logger) : ICommandSource
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private ITelegramBotClient? _botClient;

    public string? Token { get; set; }

    public event Func<CommandContext, Task<object?>>? CommandReceived;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            logger.LogError("Telegram token is not set. Cannot start TelegramCommandSource.");
            return;
        }

        _botClient = new TelegramBotClient(Token);

        var me = await _botClient.GetMe(cancellationToken);
        logger.LogInformation("Telegram bot started as @{Username} (Id {Id})", me.Username, me.Id);

        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandlePollingErrorAsync,
            cancellationToken: cancellationToken
        );

        cancellationToken.Register(() =>
        {
            try
            {
                _botClient = null;
                logger.LogInformation("TelegramCommandSource stopped.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error stopping TelegramCommandSource");
            }
        });

        return;

        async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            if (update.Type != UpdateType.Message || update.Message?.Type != MessageType.Text)
            {
                return;
            }

            var correlationId = Guid.NewGuid().ToString("N");
            var chatId = update.Message.Chat.Id;
            var commandText = update.Message.Text!;

            try
            {
                var commandParts = commandText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (commandParts.Length == 0)
                {
                    return;
                }

                var command = new CommandModel
                {
                    Name = commandParts[0],
                    Args = commandParts.Skip(1).ToArray(),
                    Source = SourceType.Telegram,
                    SenderId = $"{chatId}-{correlationId}"
                };

                var context = new CommandContext
                {
                    Command = command,
                    Token = chatId.ToString(),
                    CorrelationId = correlationId
                };

                var result = CommandReceived is not null
                    ? await CommandReceived.Invoke(context)
                    : null;

                if (result is not null)
                {
                    var response = new
                    {
                        correlationId,
                        data = result
                    };

                    await SendJsonAsync(botClient, chatId, response, ct);
                }
            }
            catch (DomainException de)
            {
                await SendProblemJsonAsync(botClient, chatId, de, correlationId, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[{CorrelationId}] Unhandled error processing Telegram message", correlationId);
                await SendProblemJsonAsync(botClient, chatId, null, correlationId, ct, ex.Message);
            }
        }

        Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
        {
            if (exception is ApiRequestException apiEx)
            {
                logger.LogError(apiEx, "Telegram API polling error");
            }
            else
            {
                logger.LogError(exception, "Telegram polling error");
            }

            return Task.Delay(5000, ct);
        }

        async Task SendProblemJsonAsync(
            ITelegramBotClient botClient,
            long chatId,
            DomainException? de,
            string correlationId,
            CancellationToken ct,
            string? fallbackMessage = null)
        {
            int status;
            string title;
            string type;
            var detail = fallbackMessage ?? de?.Message ?? "Unknown error";

            switch (de)
            {
                case InvalidCommandException ice:
                    status = 400;
                    title = "Invalid command";
                    type = "https://plankton.local/problems/invalid-command";
                    detail += ice.AllowedArgs is not null
                        ? $" Allowed arguments: {string.Join(", ", ice.AllowedArgs)}"
                        : "";
                    break;

                case UnauthorizedCommandException:
                    status = 401;
                    title = "Unauthorized";
                    type = "https://plankton.local/problems/unauthorized";
                    break;

                case RateLimitExceededException:
                    status = 429;
                    title = "Rate limit exceeded";
                    type = "https://plankton.local/problems/rate-limit-exceeded";
                    break;

                case EntityNotFoundException:
                    status = 404;
                    title = "Resource was not found";
                    type = "https://plankton.local/problems/resource-not-found";
                    break;

                default:
                    status = 500;
                    title = "Internal error";
                    type = "https://plankton.local/problems/internal-error";
                    break;
            }

            var problem = new
            {
                status,
                title,
                type,
                detail,
                correlationId
            };

            await SendJsonAsync(botClient, chatId, problem, ct);
        }
    }

    private static Task SendJsonAsync(
        ITelegramBotClient botClient,
        long chatId,
        object payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);

        return botClient.SendMessage(
            chatId: chatId,
            text: $"<pre>{WebUtility.HtmlEncode(json)}</pre>",
            parseMode: ParseMode.Html,
            cancellationToken: cancellationToken
        );
    }
}