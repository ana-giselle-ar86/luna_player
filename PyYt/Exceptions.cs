namespace PyYt;

/// <summary>
/// Base exception for all PyYt errors. Mirrors py_yt's <c>PyYTSearchError</c>
/// (exceptions.py). The historical name <c>PyYtException</c> is kept because it is
/// what the whole port throws; the derived types below mirror py_yt's specialized
/// exceptions so callers can catch the same categories.
/// </summary>
public class PyYtException : Exception
{
    public PyYtException(string message) : base(message) { }
    public PyYtException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Raised when parsing YouTube API or HTML responses fails (py_yt: ParsingError).</summary>
public sealed class ParsingError : PyYtException
{
    public ParsingError(string message) : base(message) { }
    public ParsingError(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Raised when an HTTP request to YouTube fails or returns an invalid response (py_yt: RequestError).</summary>
public sealed class RequestError : PyYtException
{
    public RequestError(string message) : base(message) { }
    public RequestError(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Raised when a video is not found or inaccessible (py_yt: VideoNotFoundError).</summary>
public sealed class VideoNotFoundError : PyYtException
{
    public VideoNotFoundError(string message) : base(message) { }
    public VideoNotFoundError(string message, Exception innerException) : base(message, innerException) { }
}
