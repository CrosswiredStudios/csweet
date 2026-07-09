using System.Net;

namespace CSweet.App.Services;

public sealed class ApiClientException : Exception
{
    public ApiClientException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
