namespace GroupsOnTaskbar.Core.Logging;

public interface IAppLogger
{
    Task WriteAsync(
        string category,
        Exception exception,
        CancellationToken cancellationToken = default);

    Task WriteAsync(
        string category,
        string message,
        CancellationToken cancellationToken = default);
}
