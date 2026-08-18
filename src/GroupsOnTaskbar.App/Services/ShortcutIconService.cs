using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using GroupsOnTaskbar.Core.Launch;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace GroupsOnTaskbar.App.Services;

public sealed class ShortcutIconService(string localFolderPath, IAppLogger logger)
{
    private readonly string _iconCacheFolderPath = Path.Combine(localFolderPath, "IconCache");
    private readonly IAppLogger _logger = logger;

    public async Task<BitmapImage?> GetIconAsync(
        string path,
        DateTimeOffset lastWriteUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var cachePath = Path.Combine(_iconCacheFolderPath, IconCacheKey.Create(path, lastWriteUtc));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(_iconCacheFolderPath);

            if (File.Exists(cachePath) && new FileInfo(cachePath).Length > 0)
            {
                return await LoadBitmapAsync(cachePath);
            }

            var targetFile = await StorageFile.GetFileFromPathAsync(path);
            using var thumbnail = await targetFile.GetThumbnailAsync(
                ThumbnailMode.SingleItem,
                64,
                ThumbnailOptions.UseCurrentScale);

            if (thumbnail is null || thumbnail.Size == 0)
            {
                return null;
            }

            await using (var sourceStream = thumbnail.AsStreamForRead())
            await using (var destinationStream = new FileStream(
                cachePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None))
            {
                await sourceStream.CopyToAsync(destinationStream, cancellationToken);
            }

            var cacheFile = new FileInfo(cachePath);

            if (!cacheFile.Exists || cacheFile.Length == 0)
            {
                if (cacheFile.Exists)
                {
                    cacheFile.Delete();
                }

                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();
            return await LoadBitmapAsync(cachePath);
        }
        catch (FileNotFoundException exception)
        {
            await _logger.WriteAsync(nameof(ShortcutIconService), exception, cancellationToken);
            return null;
        }
        catch (UnauthorizedAccessException exception)
        {
            await _logger.WriteAsync(nameof(ShortcutIconService), exception, cancellationToken);
            return null;
        }
        catch (COMException exception)
        {
            await _logger.WriteAsync(nameof(ShortcutIconService), exception, cancellationToken);
            return null;
        }
    }

    private static async Task<BitmapImage> LoadBitmapAsync(string cachePath)
    {
        var cacheFile = await StorageFile.GetFileFromPathAsync(cachePath);
        using var stream = await cacheFile.OpenAsync(FileAccessMode.Read);
        var image = new BitmapImage();
        await image.SetSourceAsync(stream);
        return image;
    }
}
