using System.Collections.Concurrent;
using FaceAnonymizer.Core.Abstractions;

namespace FaceAnonymizer.Api.Services;

/// <summary>
/// Тимчасове сховище результатів обробки зображень.
/// Замість передачі base64 (що роздуває трафік на ~33%)
/// клієнт отримує URL і завантажує зображення як бінарний потік.
/// </summary>
public sealed class ResultStore : IDisposable
{
    private readonly string _root;
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<string, (DateTime Created, string Extension, string ContentType)> _entries = new();
    private readonly Timer _cleanup;

    public ResultStore(IWebHostEnvironment env)
    {
        _root = Path.Combine(env.ContentRootPath, "storage", "results");
        Directory.CreateDirectory(_root);

        // Очищення застарілих файлів кожні 5 хвилин
        _cleanup = new Timer(_ => Purge(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>Зберігає зображення у відповідному форматі та повертає унікальний ідентифікатор.</summary>
    public string Save(byte[] imageBytes, ImageOutputFormat format)
    {
        var id = Guid.NewGuid().ToString("N");
        var ext = IImageCodec.Extension(format);
        var contentType = IImageCodec.ContentType(format);
        var path = Path.Combine(_root, id + ext);
        File.WriteAllBytes(path, imageBytes);
        _entries[id] = (DateTime.UtcNow, ext, contentType);
        return id;
    }

    /// <summary>Повертає шлях та content-type за ідентифікатором або null якщо не знайдено.</summary>
    public (string Path, string ContentType)? Get(string id)
    {
        // Захист від path traversal
        if (string.IsNullOrWhiteSpace(id) || id.Contains('.') || id.Contains('/') || id.Contains('\\'))
            return null;

        if (!_entries.TryGetValue(id, out var entry))
            return null;

        var path = Path.Combine(_root, id + entry.Extension);
        return File.Exists(path) ? (path, entry.ContentType) : null;
    }

    /// <summary>Видаляє записи старіші за TTL.</summary>
    private void Purge()
    {
        var cutoff = DateTime.UtcNow - _ttl;
        foreach (var (id, entry) in _entries)
        {
            if (entry.Created >= cutoff) continue;

            _entries.TryRemove(id, out _);
            var path = Path.Combine(_root, id + entry.Extension);
            try { File.Delete(path); } catch { /* ігноруємо */ }
        }
    }

    public void Dispose()
    {
        _cleanup.Dispose();

        foreach (var (id, entry) in _entries)
        {
            var path = Path.Combine(_root, id + entry.Extension);
            try { File.Delete(path); } catch { /* ігноруємо */ }
        }
    }
}
