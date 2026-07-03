using System.Collections.Concurrent;
using System.Text;

namespace Prism.Core;

/// <summary>
/// Process-wide cache for parsed configuration objects. Entries are keyed by the config type, the
/// source file path(s), and each file's last-write timestamp — an edited config file is re-parsed on
/// the next job while unchanged files are parsed exactly once per process instead of once per job.
/// </summary>
public static class ConfigCache {
    private static readonly ConcurrentDictionary<string, object> cache = new();

    /// <summary>
    /// Returns the cached instance for <paramref name="paths"/> or invokes <paramref name="loader"/>
    /// and caches its result. Thread-safe; the loader may run more than once under a race but only
    /// one result is kept.
    /// </summary>
    public static T GetOrLoad<T>(Func<T> loader, params string[] paths) where T : class {
        string key = BuildKey(typeof(T), paths);
        return (T)cache.GetOrAdd(key, _ => loader());
    }

    private static string BuildKey(Type type, string[] paths) {
        StringBuilder key = new(type.FullName);
        foreach (string path in paths) {
            key.Append('|').Append(path).Append('@');
            key.Append(File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0L);
        }
        return key.ToString();
    }
}
