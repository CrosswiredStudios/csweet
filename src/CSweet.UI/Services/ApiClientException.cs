using System.Net;

namespace CSweet.UI.Services;

public sealed class ApiClientException : Exception
{
    public ApiClientException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
