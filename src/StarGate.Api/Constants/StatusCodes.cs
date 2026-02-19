namespace StarGate.Api.Constants;

/// <summary>
/// HTTP status code constants including custom codes.
/// </summary>
public static class StatusCodes
{
    /// <summary>
    /// 499 Client Closed Request (Nginx extension).
    /// Used when client closes the connection before the server sends a response.
    /// </summary>
    public const int Status499ClientClosedRequest = 499;
}
