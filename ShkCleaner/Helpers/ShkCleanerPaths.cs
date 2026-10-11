using System.IO;
using System.Text.Json;

namespace ShkCleaner.Helpers;

/// <summary>
/// Единая папка данных приложения: %AppData%\ShkCleaner\
/// Здесь живут statistics.json (статистика) и config.json (настройки).
/// ВАЖНО: используется имя папки именно "ShkCleaner" - старое "ShikdikClean" больше не нужен.
/// </summary>
public static class ShkCleanerPaths
{
    /// <summary>Папка %AppData%\ShkCleaner (создаётся автоматически при первом обращении).</summary>
    public static string ПапкаДанных
    {
        get
        {
            string путь = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), // это и есть %AppData%
                "ShkCleaner");
            Directory.CreateDirectory(путь); // если папки нет - создаём, ошибок не будет
            return путь;
        }
    }

    /// <summary>Полный путь к файлу статистики.</summary>
    public static string ФайлСтатистики => Path.Combine(ПапкаДанных, "statistics.json");

    /// <summary>Полный путь к файлу настроек (config.json).</summary>
    public static string ФайлНастроек => Path.Combine(ПапкаДанных, "config.json");
}
