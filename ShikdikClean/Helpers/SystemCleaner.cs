using System.IO;
using System.Runtime.InteropServices;

namespace ShxClean.Helpers;

/// <summary>
/// Класс для очистки системы от временных файлов и мусора.
/// Все методы асинхронные (async/await), чтобы интерфейс WPF не «зависал».
/// Ошибки доступа (Access Denied) перехватываются и файлы просто пропускаются.
/// </summary>
public class SystemCleaner
{
    /// <summary>Сколько файлов было пропущено из-за "Access Denied" при последней операции.</summary>
    public int ПропущеноФайлов { get; internal set; }

    // ---------- Пути к папкам для очистки ----------

    /// <summary>Папка временных файлов текущего пользователя (%TEMP%).</summary>
    public static string ПутьTempПользователя => Path.GetTempPath();

    /// <summary>Папка системных временных файлов C:\Windows\Temp.</summary>
    public static string ПутьTempСистемы =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");

    /// <summary>Папка Prefetch: C:\Windows\Prefetch.</summary>
    public static string ПутьPrefetch =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");

    /// <summary>Кэш шейдеров DirectX (лежит в локальных данных пользователя).</summary>
    public static string ПутьShaderCache
    {
        get
        {
            string локальныеДанные = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(локальныеДанные, "D3DSCache");
        }
    }

    /// <summary>Папки кэша браузеров Chrome / Edge / Firefox для текущего пользователя.</summary>
    public static string[] ПутиКэшБраузеров
    {
        get
        {
            string локальныеДанные = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return new[]
            {
                Path.Combine(локальныеДанные, "Google", "Chrome", "User Data", "Default", "Cache"),
                Path.Combine(локальныеДанные, "Google", "Chrome", "User Data", "Default", "Code Cache"),
                Path.Combine(локальныеДанные, "Microsoft", "Edge", "User Data", "Default", "Cache"),
                Path.Combine(локальныеДанные, "Microsoft", "Edge", "User Data", "Default", "Code Cache"),
                Path.Combine(локальныеДанные, "Packages", "MicrosoftEdge_8wekyb3d8bbwe", "TempState"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                             "Mozilla", "Firefox", "Profiles"), // кэш внутри профилей
            };
        }
    }

    /// <summary>Папка «Загрузки» текущего пользователя.</summary>
    public static string ПутьЗагрузки =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    // ---------- Сканирование ----------

    /// <summary>
    /// Асинхронно подсчитывает количество и общий размер файлов в указанной папке.
    /// Возвращает кортеж (кол-во файлов, размер в байтах). Не выбрасывает исключений.
    /// Работа выполняется в фоновом потоке (Task.Run), UI не блокируется.
    /// </summary>
    public Task<(int файлов, long байт)> СканироватьПапкуAsync(string путь, int минимальныйРазмерБайт = 0)
    {
        return Task.Run(() =>
        {
            int количество = 0;
            long размер = 0;
            try
            {
                if (!Directory.Exists(путь)) return (0, 0L);

                // EnumerationOptions - не падать на недоступных подпапках
                var варианты = new EnumerationOptions
                {
                    IgnoreInaccessible = true,          // пропускать то, к чему нет доступа
                    RecurseSubdirectories = true,
                };

                foreach (string файл in Directory.EnumerateFiles(путь, "*", варианты))
                {
                    try
                    {
                        var info = new FileInfo(файл);
                        if (info.Length >= минимальныйРазмерБайт)
                        {
                            количество++;
                            размер += info.Length;
                        }
                    }
                    catch (Exception e) when (e is UnauthorizedAccessException || e is IOException)
                    {
                        // Файл недоступен - просто пропускаем его
                        ПропущеноФайлов++;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Нет доступа к самой папке - возвращаем то, что успели посчитать
            }
            catch (IOException)
            {
                // Ошибка ввода-вывода (например диск отключён) - тоже пропускаем
            }
            return (количество, размер);
        });
    }

    /// <summary>Возвращает список больших файлов (>= minBytes) в папке «Загрузки».</summary>
    public Task<List<Models.BigFile>> ПолучитьБольшиеФайлыЗагрузокAsync(long минимальныйРазмерБайт)
    {
        return Task.Run(() =>
        {
            var результат = new List<Models.BigFile>();
            try
            {
                if (!Directory.Exists(ПутьЗагрузки)) return результат;
                foreach (string файл in Directory.EnumerateFiles(ПутьЗагрузки, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        var info = new FileInfo(файл);
                        if (info.Length >= минимальныйРазмерБайт)
                        {
                            результат.Add(new Models.BigFile
                            {
                                ПутьКФайлу = файл,
                                РазмерБайт = info.Length,
                                ДатаИзменения = info.LastWriteTime,
                            });
                        }
                    }
                    catch (Exception e) when (e is UnauthorizedAccessException || e is IOException) { /* пропускаем */ }
                }
                // Самые большие - наверх списка
                результат.Sort((a, b) => b.РазмерБайт.CompareTo(a.РазмерБайт));
            }
            catch (Exception e) when (e is UnauthorizedAccessException || e is IOException) { /* нет доступа к папке */ }
            return результат;
        });
    }

    // ---------- Очистка ----------

    /// <summary>
    /// Асинхронно удаляет всё содержимое папки (саму папку оставляет).
    /// Возвращает количество удалённых файлов и освобождённые байты.
    /// Файлы, которые нельзя удалить (заняты браузером, Access Denied), пропускаются.
    /// </summary>
    public Task<(int удалено, long байт)> ОчиститьПапкуAsync(string путь)
    {
        return Task.Run(() =>
        {
            int удалено = 0;
            long байт = 0;
            try
            {
                if (!Directory.Exists(путь)) return (0, 0L);

                // Сначала файлы, потом пустые подпапки (сверху вниз по дереву)
                var варианты = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true };

                foreach (string файл in Directory.EnumerateFiles(путь, "*", варианты))
                {
                    if (ПопробоватьУдалитьФайл(файл, out long размерФайла))
                    {
                        удалено++;
                        байт += размерФайла;
                    }
                    else
                    {
                        ПропущеноФайлов++;
                    }
                }

                // Удаляем ставшие пустыми подпапки (снимаем флаг рекурсии: обходим сами bottom-up)
                УдалитьПустыеПодпапки(путь);
            }
            catch (Exception e) when (e is UnauthorizedAccessException || e is IOException)
            {
                // Нет доступа к папке целиком - выходим с текущим результатом
            }
            return (удалено, байт);
        });
    }

    /// <summary>Пытается удалить один файл. Снимает атрибут «только для чтения», если он мешает.</summary>
    private bool ПопробоватьУдалитьФайл(string путь, out long размер)
    {
        размер = 0;
        try
        {
            var info = new FileInfo(путь);
            if (!info.Exists) return false;
            размер = info.Length;

            // Читаемый-only файл удалить нельзя - сначала снимаем атрибут
            if (info.IsReadOnly) info.IsReadOnly = false;

            info.Delete();
            return true;
        }
        catch (Exception e) when (e is UnauthorizedAccessException || e is IOException)
        {
            // Файл занят другой программой или нет прав - пропускаем (ВАЖНО: не падаем!)
            return false;
        }
    }

    /// <summary>Рекурсивно удаляет пустые подпапки (bottom-up).</summary>
    private void УдалитьПустыеПодпапки(string путь)
    {
        try
        {
            foreach (string подпапка in Directory.GetDirectories(путь))
            {
                УдалитьПустыеПодпапки(подпапка);           // сначала вглубь
                try
                {
                    if (Directory.GetFileSystemEntries(подпапка).Length == 0)
                        Directory.Delete(подпапка);         // потом пустую папку
                }
                catch (Exception e) when (e is UnauthorizedAccessException || e is IOException) { }
            }
        }
        catch (Exception e) when (e is UnauthorizedAccessException || e is IOException) { }
    }

    /// <summary>
    /// Очищает Корзину полностью через Win32 API SHEmptyRecycleBin.
    /// code 0 или 12000 (уже пуста) считаются успехом.
    /// </summary>
    public Task<bool> ОчиститьКорзинуAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                // SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND = 7
                int код = SHEmptyRecycleBin(IntPtr.Zero, null, 0x7);
                return код == 0 || код == 12000; // 12000 = корзина уже пуста
            }
            catch
            {
                return false;
            }
        });
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? путь, uint флаги);
}
