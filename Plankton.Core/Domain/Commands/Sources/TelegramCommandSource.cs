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
    public event Func<CommandContext, Task<object?>>? CommandReceived;

    private ITelegramBotClient? _botClient;
    public string? Token { get; set; }

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
            HandleUpdateAsync,
            HandlePollingErrorAsync,
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
            if (update.Type != UpdateType.Message || update.Message?.Type != MessageType.Text) return;

            var correlationId = Guid.NewGuid().ToString("N");
            var chatId = update.Message.Chat.Id;
            var commandText = update.Message.Text!;

            try
            {
                var commandParts = commandText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (commandParts.Length == 0) return;

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
                    Token = update.Message.Chat.Id.ToString(),
                    CorrelationId = correlationId
                };

                var result = await CommandReceived?.Invoke(context)!;

                if (result != null)
                {
                    var response = new
                    {
                        correlationId,
                        data = result
                    };

                    var json = JsonSerializer.Serialize(response);

                    // TODO fix response, maybe a transformer and stuff. 
                    await botClient.SendMessage(
                        chatId,
                        json,
                        ParseMode.MarkdownV2,
                        cancellationToken: ct
                    );
                }
            }
            catch (DomainException de)
            {
                await SendProblemJsonAsync(botClient, chatId, de, correlationId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[{CorrelationId}] Unhandled error processing Telegram message", correlationId);
                await SendProblemJsonAsync(botClient, chatId, null, correlationId, ex.Message);
            }
        }

        async Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
        {
            if (exception is ApiRequestException apiEx)
                logger.LogError(apiEx, "Telegram API polling error");
            else
                logger.LogError(exception, "Telegram polling error");

            await Task.Delay(5000, ct);
        }

        async Task SendProblemJsonAsync(
            ITelegramBotClient botClient,
            long chatId, DomainException? de,
            string correlationId,
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
                    type = $"https://plankton.local/problems/invalid-command";
                    detail += ice.AllowedArgs != null
                        ? $" Allowed arguments: {string.Join(", ", ice.AllowedArgs)}"
                        : "";
                    break;
                case UnauthorizedCommandException:
                    status = 401;
                    title = "Unauthorized";
                    type = $"https://plankton.local/problems/unauthorized";
                    break;
                case RateLimitExceededException:
                    status = 429;
                    title = "Rate limit exceeded";
                    type = $"https://plankton.local/problems/rate-limit-exceeded";
                    break;
                case EntityNotFoundException:
                    status = 404;
                    title = "Resource was not found";
                    type = $"https://plankton.local/problems/resource-not-found";
                    break;
                default:
                    status = 500;
                    title = "Internal error";
                    type = $"https://plankton.local/problems/internal-error";
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

            var json = JsonSerializer.Serialize(problem);
            await botClient.SendMessage(chatId, json, ParseMode.Markdown, cancellationToken: cancellationToken);
        }
    }
}