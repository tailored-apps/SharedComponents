namespace TailoredApps.Demo.Services.Internals;

/// <summary>
/// The base of every exception this service throws on purpose. Its message is written for the caller and its
/// status code says what kind of refusal it is; the WebApi project turns both into the error response.
/// Anything that is not a ShopException is an internal error: the caller gets a generic 500.
/// </summary>
public abstract class ShopException(string message, int statusCode, Exception? innerException = null) : Exception(message, innerException)
{
    public int StatusCode { get; } = statusCode;
}
