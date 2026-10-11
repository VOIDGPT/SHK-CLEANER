using System.Diagnostics;
using System.IO;
using System.Security;
using System.Globalization;
using Microsoft.Win32;
using ShkCleaner.Models;

namespace ShkCleaner.Helpers;

/// <summary>
/// Класс для чтения списка установленных программ из реестра Windows
/// и запуска их деинсталляторов (как в «Программы и компоненты»).
/// </summary>
public class ProgramUninstaller
{
    /// <summary>
    /// Асинхронно собирает список всех установленных программ из трёх веток реестра:
    /// 1) HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall        (64-битные программы)
    /// 2) HKLM\SOFTWARE\WOW6432Node\...\Uninstall                          (32-битные программы)
    /// 3) HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall          (программы текущего пользователя)
    /// </summary>
    public Task<List<ProgramInfo>> ПолучитьПрограммыAsync()
    {
        // Чтение реестра - быстрая операция, но всё равно выносим в фоновый поток
        return Task.Run(() =>
        {
            var программы = new List<ProgramInfo>();
            // Словарь по имени, чтобы не показывать дубликаты из разных веток
            var поИмени = new Dictionary<string, ProgramInfo>(StringComparer.OrdinalIgnoreCase);

            const string относительныйПуть = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

            // Три источника данных (см. описание метода)
            var источники = new (RegistryHive улей, RegistryView вид)[]
            {
                (RegistryHive.LocalMachine, RegistryView.Registry64), // 64-битный HKLM (+ WOW6432Node читается автоматически)
                (RegistryHive.CurrentUser,  RegistryView.Registry64), // HKCU
            };

            foreach (var (улей, вид) in источники)
            {
                try
                {
                    using var базовыйКлюч = RegistryKey.OpenBaseKey(улей, вид);
                    using var ключUninstall = базовыйКлюч.OpenSubKey(относительныйПуть);
                    if (ключUninstall is null) continue;

                    foreach (string подпапка in ключUninstall.GetSubKeyNames())
                    {
                        try
                        {
                            using var ключПрограммы = ключUninstall.OpenSubKey(подпапка);
                            var программа = ПрочитатьПрограмму(ключПрограммы);
                            if (программа is not null && !поИмени.ContainsKey(программа.Имя))
                            {
                                поИмени[программа.Имя] = программа;
                                программы.Add(программа);
                            }
                        }
                        catch (Exception e) when (e is UnauthorizedAccessException || e is IOException)
                        {
                            // Этот подраздел реестра прочитать нельзя - пропускаем
                        }
                    }
                }
                catch (Exception e) when (e is UnauthorizedAccessException || e is IOException || e is SecurityException)
                {
                    // Нет доступа к ветке реестра - пропускаем источник целиком
                }
            }

            // Дополнительно явно читаем WOW6432Node (32-битные программы на 64-битной Windows)
            ПрочитатьWOW64(программы, поИмени);

            // Сортировка по алфавиту - так удобнее искать
            программы.Sort((a, b) => string.Compare(a.Имя, b.Имя, StringComparison.OrdinalIgnoreCase));
            return программы;
        });
    }

    /// <summary>Читает ветку HKLM\SOFTWARE\WOW6432Node\...\Uninstall.</summary>
    private void ПрочитатьWOW64(List<ProgramInfo> программы, Dictionary<string, ProgramInfo> поИмени)
    {
        try
        {
            using var базовыйКлюч = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            using var ключUninstall = базовыйКлюч.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (ключUninstall is null) return;

            foreach (string подпапка in ключUninstall.GetSubKeyNames())
            {
                try
                {
                    using var ключПрограммы = ключUninstall.OpenSubKey(подпапка);
                    var программа = ПрочитатьПрограмму(ключПрограммы);
                    if (программа is not null && !поИмени.ContainsKey(программа.Имя))
                    {
                        поИмени[программа.Имя] = программа;
                        программы.Add(программа);
                    }
                }
                catch (Exception e) when (e is UnauthorizedAccessException || e is IOException) { }
            }
        }
        catch (Exception e) when (e is UnauthorizedAccessException || e is IOException || e is SecurityException) { }
    }

    /// <summary>
    /// Извлекает данные одной программы из её ключа реестра.
    /// Возвращает null, если запись не является обычной программой
    /// (системные обновления, "Parent" записи и т.п.).
    /// </summary>
    private static ProgramInfo? ПрочитатьПрограмму(RegistryKey? ключ)
    {
        if (ключ is null) return null;

        string имя = ключ.GetValue("DisplayName") as string ?? "";
        string строкаУдаления = ключ.GetValue("UninstallString") as string ?? "";

        // Фильтруем мусор: пустые имена, системные обновления Windows, записи без удаления
        if (string.IsNullOrWhiteSpace(имя)) return null;
        if (bool.TryParse(ключ.GetValue("SystemComponent") as string, out bool компонентСистемы) && компонентСистемы) return null;
        if ((ключ.GetValue("ReleaseType") as string)?.Contains("Update") == true) return null;
        // Обновления (KBxxxxxx) обычно без UninstallString - скрываем их
        if (string.IsNullOrWhiteSpace(строкаУдаления) && имя.StartsWith("KB", StringComparison.OrdinalIgnoreCase)) return null;

        var программа = new ProgramInfo
        {
            Имя = имя.Trim(),
            Издатель = (ключ.GetValue("Publisher") as string ?? "—").Trim(),
            Версия = (ключ.GetValue("DisplayVersion") as string ?? "—").Trim(),
            СтрокаУдаления = строкаУдаления.Trim(),
            ВозможноТихоеУдаление = ключ.GetValue("QuietUninstallString") is string,
        };

        // Дата установки: в реестре хранится как строка "2026-01-15" или "MM/dd/yyyy" - пробуем оба варианта
        if (ключ.GetValue("InstallDate") is string датаСтрока &&
            DateTime.TryParseExact(датаСтрока, "yyyyMMdd", CultureInfo.InvariantCulture,
                                   DateTimeStyles.None, out DateTime дата))
        {
            программа.ДатаУстановки = дата;
        }

        // EstimatedSize хранится в КИЛОБАЙТАХ (DWORD)
        object? размерОбъект = ключ.GetValue("EstimatedSize");
        if (размерОбъект is int размерКб)
        {
            программа.РазмерКб = размерКб;
        }

        // ---------- ИКОНКА ПРОГРАММЫ ----------
        // 1) Пробуем готовое поле DisplayIcon (путь к .exe/.ico/.dll, иногда с индексом: "C:\a.exe,0")
        string? иконкаПутьСтрока = ключ.GetValue("DisplayIcon") as string;
        if (!string.IsNullOrWhiteSpace(иконкаПутьСтрока))
        {
            string путь = иконкаПутьСтрока.Trim().Trim('"');
            int запятая = путь.LastIndexOf(',');
            int индексИконки = 0;
            // Если после запятой число - это индекс иконки внутри файла
            if (запятая > 1 && int.TryParse(путь[(запятая + 1)..], out int idx))
            {
                индексИконки = idx;
                путь = путь[..запятая];
            }
            // Разворачиваем переменные окружения (%SystemRoot% и т.п.)
            try { путь = Environment.ExpandEnvironmentVariables(путь); } catch { }
            if (File.Exists(путь))
            {
                программа.ПутьКИконкеФайла = путь;
                программа.ИндексИконки = индексИконки;
            }
        }

        // 2) Запасной вариант: ищем unins*.exe в папке установки
        if (программа.ПутьКИконкеФайла is null)
        {
            string? папкаУстановки = ключ.GetValue("InstallLocation") as string;
            if (!string.IsNullOrWhiteSpace(папкаУстановки))
            {
                try
                {
                    if (Directory.Exists(папкаУстановки))
                    {
                        var файлы = Directory.GetFiles(папкаУстановки, "unins*.exe");
                        if (файлы.Length > 0) программа.ПутьКИконкеФайла = файлы[0];
                    }
                }
                catch (Exception e) when (e is UnauthorizedAccessException || e is IOException) { }
            }
        }

        return программа;
    }

    /// <summary>
    /// Извлекает иконку из exe/dll/ico файла средствами Windows API.
    /// Возвращает BitmapSource для показа в таблице или null при любой неудаче
    /// (иконка - украшение, приложение из-за неё падать не должно!).
    /// </summary>
    public static System.Windows.Media.ImageSource? ИзвлечьИконку(string путьКФайлу, int индекс)
    {
        try
        {
            IntPtr большойЗначок = IntPtr.Zero, маленькийЗначок = IntPtr.Zero;
            // Извлекаем сразу два размера: 32x32 (для таблицы) и 256x256 (для предпросмотра)
            int результат = ExtractIconExW(путьКФайлу, индекс,
                ref большойЗначок, ref маленькийЗначок, 1);
            if (результат == 0) return null;

            IntPtr выбранный = большойЗначок != IntPtr.Zero ? большойЗначок : маленькийЗначок;
            if (выбранный == IntPtr.Zero) return null;

            using var значок = System.Drawing.Icon.FromHandle(выбранный);
            using var битмап = значок.ToBitmap();

            // Конвертируем GDI+ изображение в WPF-картинку через память (PNG)
            using var memory = new MemoryStream();
            битмап.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
            memory.Position = 0;

            var картинка = new System.Windows.Media.Imaging.BitmapImage();
            картинка.BeginInit();
            картинка.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            картинка.StreamSource = memory;
            картинка.EndInit();
            картинка.Freeze(); // можно использовать из любого потока, рендер быстрее

            // Освобождаем нативные handles значков Windows
            if (большойЗначок != IntPtr.Zero) DestroyIcon(большойЗначок);
            if (маленькийЗначок != IntPtr.Zero) DestroyIcon(маленькийЗначок);

            return картинка;
        }
        catch (Exception e)
        {
            // Любая проблема с извлечением (битый файл, нет прав) - тихо возвращаем null
            System.Diagnostics.Debug.WriteLine($"Иконка не извлечена: {e.Message}");
            return null;
        }
    }

    // P/Invoke объявления Win32 API (нужны для извлечения иконок чужих программ)
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int ExtractIconExW(string путьКФайлу, int индекс,
        ref IntPtr значкиБольшие, ref IntPtr значкиМаленькие, uint количество);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    /// <summary>
    /// Запускает деинсталлятор программы (UninstallString из реестра).
    /// Метод разбирает команду: путь к exe + аргументы. Возвращает текст ошибки или null при успехе.
    /// </summary>
    public string? УдалитьПрограмму(ProgramInfo программа)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(программа.СтрокаУдаления))
                return Loc.Get("NoUninstallString");

            string команда = программа.СтрокаУдаления.Trim();

            // msiexec команды запускаем напрямую
            string файлЗапуска;
            string аргументы;

            if (команда.StartsWith("\""))
            {
                // Формат: "C:\Папка\unins000.exe" /SILENT
                int закрывающаяКавычка = команда.IndexOf('"', 1);
                if (закрывающаяКавычка < 0)
                {
                    файлЗапуска = команда.Trim('"');
                    аргументы = "";
                }
                else
                {
                    файлЗапуска = команда[1..закрывающаяКавычка];
                    аргументы = команда[(закрывающаяКавычка + 1)..].Trim();
                }
            }
            else if (команда.StartsWith("MsiExec", StringComparison.OrdinalIgnoreCase))
            {
                // Формат: MsiExec.exe /I{GUID} - разделяем по первому пробелу
                int пробел = команда.IndexOf(' ');
                файлЗапуска = команда[..пробел];
                аргументы = команда[(пробел + 1)..];
            }
            else
            {
                // Формат: C:\unins000.exe /SILENT (без кавычек)
                int пробел = команда.IndexOf(' ');
                if (пробел < 0)
                {
                    файлЗапуска = команда;
                    аргументы = "";
                }
                else
                {
                    файлЗапуска = команда[..пробел];
                    аргументы = команда[(пробел + 1)..];
                }
            }

            var стартИнфо = new ProcessStartInfo
            {
                FileName = файлЗапуска,
                Arguments = аргументы,
                UseShellExecute = true,   // чтобы Windows сама показала UAC при необходимости
            };

            using var процесс = Process.Start(стартИнфо);
            return процесс is null ? Loc.Get("NoUninstallString") : null; // null = всё хорошо
        }
        catch (Exception e)
        {
            // Например, файл деинсталлятора удалён, но запись в реестре осталась
            return string.Format(Loc.Get("ErrorOccurred"), e.Message);
        }
    }
}
