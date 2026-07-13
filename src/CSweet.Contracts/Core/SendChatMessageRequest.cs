using System.ComponentModel.DataAnnotations;

namespace CSweet.Contracts.Core;

public sealed record SendChatMessageRequest(
    [property: Required] string Message);
