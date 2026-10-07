namespace QuireClone.Services;

public sealed class TaskImageStorage(StorageSettings settings)
{
    public const long MaxImageBytes = 5 * 1024 * 1024;


    public async Task<(string Name, long Size)> SaveAsync(IFormFile image, CancellationToken cancellationToken)
    {
        if (image.Length is <= 0 or > MaxImageBytes)
            throw new ArgumentException("Bilder müssen zwischen 1 Byte und 5 MB groß sein.");

        await using var input = image.OpenReadStream();
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length is <= 0 or > MaxImageBytes)
            throw new ArgumentException("Bilder dürfen höchstens 5 MB groß sein.");

        var bytes = buffer.ToArray();
        var extension = GetExtension(bytes) ?? throw new ArgumentException(
            "Nur PNG-, JPEG-, GIF- und WebP-Bilder werden unterstützt.");
        var directory = settings.ImagesDirectory;
        Directory.CreateDirectory(directory);
        var name = $"{Guid.NewGuid():N}.{extension}";
        await using var output = new FileStream(Path.Combine(directory, name), FileMode.CreateNew);
        await output.WriteAsync(bytes, cancellationToken);
        return (name, bytes.LongLength);
    }

    public (string Path, string ContentType)? Find(string name)
    {
        var mime = ContentType(name);
        if (mime is null) return null;
        var path = Path.Combine(settings.ImagesDirectory, name);
        return File.Exists(path) ? (path, mime) : null;
    }

    // Only names created by SaveAsync are served or moved, never other files in the folder.
    public static bool IsStoredImageName(string name) => ContentType(name) is not null;

    private static string? ContentType(string name)
    {
        var extension = Path.GetExtension(name).ToLowerInvariant();
        var mime = extension switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => null
        };
        return mime is null || name.Length != 32 + extension.Length ||
            !Guid.TryParseExact(name[..32], "N", out _) ? null : mime;
    }

    private static string? GetExtension(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "png";
        if (bytes.Length >= 3 && bytes[..3].SequenceEqual(new byte[] { 255, 216, 255 })) return "jpg";
        if (bytes.Length >= 6 && (bytes[..6].SequenceEqual("GIF87a"u8) || bytes[..6].SequenceEqual("GIF89a"u8))) return "gif";
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8)) return "webp";
        return null;
    }
}
