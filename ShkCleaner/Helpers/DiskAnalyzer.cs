using System.IO;
using ShkCleaner.Models;

namespace ShkCleaner.Helpers;

/// <summary>
/// Помощник раздела "Анализ диска": рекурсивное сканирование папок,
/// поиск самых тяжёлых файлов и построение плоского списка для визуализации.
/// Все долгие операции идут через Task.Run (фоновый поток), интерфейс не зависает.
/// </summary>
public class DiskAnalyzer
{
    /// <summary>Папки-«минные поля»: системные директории, которые НЕ сканируем глубоко.</summary>
    private static readonly string[] ПропуститьПапки = { "windows", "program files", "program files (x86)", "programdata", "$recycle.bin", "system volume information" };

    /// <summary>Возвращает список всех съёмных/локальных дисков: "C:\", "D:\" ...</summary>
    public static List<string> ПолучитьДиски()
    {
        var список = new List<string>();
        try
        {
            foreach (var д in DriveInfo.GetDrives())
            {
                try
                {
                    // Берём только диски с файловой системой и смонтированные
                    if (д.IsReady && д.DriveType is DriveType.Fixed or DriveType.Removable)
                        список.Add(д.Name); // формат "C:\"
                }
                catch { /* съёмный диск мог отвалиться - пропускаем */ }
            }
        }
        catch { /* GetDrives почти не падает, но пусть будет */ }
        return список;
    }

    /// <summary>
    /// Рекурсивно обходит дерево папок и возвращает:
    /// 1) список FolderScanItem (папка + её полный размер) - для визуализации со столбиками;
    /// 2) топ самых больших файлов на всём диске.
    /// progress - необязательный callback "какая папка сканируется сейчас".
    /// </summary>
    public async Task<(List<FolderScanItem> Папки, List<BigFileItem> ТопФайлов)> СканироватьДискAsync(
        string кореньДиска, int топКоличество = 30, IProgress<string>? progress = null, CancellationToken токен = default)
    {
        return await Task.Run(() =>
        {
            var всеФайлы = new List<BigFileItem>();   // собираем ВСЕ файлы чтобы потом взять топ-N
            _размерыПапок.Clear(); // очищаем словарь от прошлого сканирования
            var размерыПапок = СканироватьПапку(кореньДиска, 0, всеФайлы, progress, токен);

            // ---------- Строим плоский список для визуализации ----------
            var результат = new List<FolderScanItem>();
            СобратьСписок(кореньДиска, размерыПапок, 0, результат);

            // Самая большая папка = 100% ширины полоски, остальные - пропорционально
            long максимум = результат.Count > 0 ? результат.Max(r => r.РазмерБайт) : 1;
            if (максимум <= 0) максимум = 1;
            foreach (var r in результат)
                r.ДоляПрогресса = Math.Clamp((double)r.РазмерБайт / максимум, 0.001, 1.0);

            // ---------- Топ самых тяжёлых файлов ----------
            var топ = всеФайлы.OrderByDescending(f => f.РазмерБайт).Take(топКоличество).ToList();

            // Сортировка дерева: сначала по глубине, внутри уровня - по размеру (крупные сверху)
            var отсортировано = результат.OrderBy(r => r.Глубина).ThenByDescending(r => r.РазмерБайт).ToList();
            return (отсортировано, топ);
        }, токен);
    }

    /// <summary>
    /// Рекурсия: возвращает суммарный размер папки (включая подпапки) и заполняет словарь размеров.
    /// Ошибки доступа (Access Denied) просто пропускаем - это нормально при сканировании чужих профилей.
    /// </summary>
    private long СканироватьПапку(string путь, int глубина, List<BigFileItem> всеФайлы,
        IProgress<string>? progress, CancellationToken токен)
    {
        токен.ThrowIfCancellationRequested();
        long итог = 0;

        // Файлы текущей папки
        try
        {
            foreach (string файл in Directory.GetFiles(путь))
            {
                try
                {
                    var info = new FileInfo(файл);
                    if (!info.Exists) continue;
                    итог += info.Length;
                    // В топ собираем только крупные файлы (>50 МБ) - экономим память
                    if (info.Length > 50L * 1024 * 1024)
                        всеФайлы.Add(new BigFileItem
                        {
                            Путь = файл,
                            Имя = Path.GetFileName(файл),
                            РазмерБайт = info.Length,
                            ДатаИзменения = info.LastWriteTime,
                        });
                }
                catch (Exception e) when (e is UnauthorizedAccessException || e is IOException) { }
            }
        }
        catch (Exception e) when (e is UnauthorizedAccessException || e is IOException) { }

        // Подпапки (рекурсивно), кроме заведомо огромных системных
        try
        {
            foreach (string подпапка in Directory.GetDirectories(путь))
            {
                string имяПапки = Path.GetFileName(подпапка).ToLowerInvariant();
                if (глубина == 0 && ПропуститьПапки.Contains(имяПапки)) continue;
                if (имяПапки.StartsWith("$")) continue; // служебные $... папки

                progress?.Report(подпапка);
                итог += СканироватьПапку(подпапка, глубина + 1, всеФайлы, progress, токен);
            }
        }
        catch (Exception e) when (e is UnauthorizedAccessException || e is IOException) { }

        lock (_размерыПапок) _размерыПапок[путь] = итог;
        return итог;
    }

    // Словарь "путь папки -> её суммарный размер", наполняется во время рекурсии
    private readonly Dictionary<string, long> _размерыПапок = new();

    /// <summary>Превращает словарь размеров в плоский список FolderScanItem для интерфейса.</summary>
    private void СобратьСписок(string корень, Dictionary<string, long> размеры, int глубина, List<FolderScanItem> вывод)
    {
        if (!размеры.TryGetValue(корень, out long размерКорня)) return;

        // Дочерние папки текущего корня, сортируем по убыванию размера
        var дети = размеры
            .Where(kv => kv.Key.StartsWith(корень.TrimEnd('\\') + "\\")
                      && Path.GetDirectoryName(kv.Key)?.TrimEnd('\\') == корень.TrimEnd('\\'))
            .OrderByDescending(kv => kv.Value)
            .ToList();

        foreach (var (путь, размер) in дети)
        {
            // Показываем только значимые папки (>50 МБ), иначе список тонет в мелочи
            if (размер < 50L * 1024 * 1024) continue;
            вывод.Add(new FolderScanItem
            {
                Путь = путь,
                Имя = Path.GetFileName(путь),
                Глубина = глубина,
                РазмерБайт = размер,
                КоличествоФайлов = 0, // считаем ниже один раз для скорости
            });
            // Ограничение глубины визуализации - 4 уровня, дальше и так видно по полоскам
            if (глубина < 3) СобратьСписок(путь, размеры, глубина + 1, вывод);
        }
    }
}
