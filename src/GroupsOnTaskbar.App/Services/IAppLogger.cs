namespace GroupsOnTaskbar.App.Services;

public interface IAppLogger
{
    Task WriteAsync(
        string category,
        Exception exception,
        CancellationToken cancellationToken = default);
}
