namespace Plankton.Bots.Models;

public record BotActionResultModel(bool Success, string? Reason = null, object? Result = null);