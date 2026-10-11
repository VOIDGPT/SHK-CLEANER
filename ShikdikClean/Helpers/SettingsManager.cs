using System.IO;
using System.Text.Json;
using System.Windows;
using ShikdikClean.Models;

namespace ShikdikClean.Helpers;

/// <summary>
/// Класс для сохранения/загрузки настроек в файл appsettings.json,
/// управления автозапуском (ярлык в папке Shell:Startup) и переключения тем оформления.
/// </summary>
public static class SettingsManager
{
    /// <summary>Путь к файлу настроек - рядом с исполняемым файлом программы.</summary>
    private static string ПутьКФайлуНастроек =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

    // Словарь для записи JSON с русскими именами свойств и красивым отступом.
    private static readonly JsonSerializerOptions параметрыJson = new()
    {
        WriteIndented = true,                       // человекочитаемый файл
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // русские буквы без \uXXXX
    };

    /// <summary>Загружает настройки из файла. Если файла нет или он повреждён - возвращает значения по умолчанию.</summary>
    public static AppSettings Загрузить()
    {
        try
        {
            if (File.Exists(ПутьКФайлуНастроек))
            {
                string текст = File.ReadAllText(ПутьКФайлуНастроек);
                var настройки = JsonSerializer.Deserialize<AppSettings>(текст, параметрыJson);
                if (настройки is not null) return настройки;
            }
        }
        catch (Exception e)
        {
            // Повреждённый JSON - не критично, просто используем заводские настройки
            System.Diagnostics.Debug.WriteLine($"Не удалось прочитать настройки: {e.Message}");
        }
        return AppSettings.Заводские();
    }

    /// <summary>Сохраняет настройки в файл appsettings.json. Возвращает текст ошибки или null.</summary>
    public static string? Сохранить(AppSettings настройки)
    {
        try
        {
            string текст = JsonSerializer.Serialize(настройки, параметрыJson);
            File.WriteAllText(ПутьКФайлуНастроек, текст);
            return null; // null = успех
        }
        catch (Exception e)
        {
            return string.Format(Loc.Get("ErrorOccurred"), e.Message);
        }
    }

    /// <summary>
    /// Добавляет или удаляет ярлык программы из папки автозагрузки Windows
    /// (Environment.SpecialFolder.Startup). Возвращает текст ошибки или null.
    /// </summary>
    public static string? ПрименитьАвтозапуск(bool включать)
    {
        try
        {
            string путьЯрлыка = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                "Shikdik Clean.lnk");

            if (включать)
            {
                // Создаём .lnk файл через COM-объект WScript.Shell
                Type? типShell = Type.GetTypeFromProgID("WScript.Shell");
                if (типShell is null) throw new InvalidOperationException("WScript.Shell недоступен");

                dynamic shell = Activator.CreateInstance(типShell)!;
                dynamic ярлык = shell.CreateShortcut(путьЯрлыка);
                ярлык.TargetPath = Environment.ProcessPath;   // наша программа
                ярлык.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                ярлык.Description = "Shikdik Clean - очистка компьютера";
                ярлык.Save();

                // Освобождаем COM-объекты (важно для корректной работы!)
                System.Runtime.InteropServices.Marshal.ReleaseComObject(ярлык);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
            }
            else if (File.Exists(путьЯрлыка))
            {
                File.Delete(путьЯрлыка);
            }
            return null;
        }
        catch (Exception e)
        {
            return Loc.Get("AutostartError") + " " + e.Message;
        }
    }

    // ------------------------------------------------------------------
    // ТЕМЫ ОФОРМЛЕНИЯ: меняем значения кистей (Brushes) в ресурсах приложения.
    // В XAML все контролы берут цвета по DynamicResource, поэтому тема
    // меняется мгновенно без перезапуска.
    // ------------------------------------------------------------------

    /// <summary>Применяет тему по её коду: "dark", "light" или "blue".</summary>
    public static void ПрименитьТему(string кодТемы)
    {
        // Палитры трёх тем (см. требования: тёмная #1E1E1E, светлая белая, синяя #2D2D30 + #007ACC)
        Color фон, фонПанели, фонКарточки, текст, текстВторичный, акцент, граница;

        switch (кодТемы.ToLowerInvariant())
        {
            case "light": // Светлая тема
                фон = FromHex("#FFFFFF");
                фонПанели = FromHex("#F3F3F3");
                фонКарточки = FromHex("#FAFAFA");
                текст = FromHex("#1E1E1E");
                текстВторичный = FromHex("#5A5A5A");
                акцент = FromHex("#007ACC");
                граница = FromHex("#D0D0D0");
                break;

            case "blue": // Синяя тема
                фон = FromHex("#2D2D30");
                фонПанели = FromHex("#252528");
                фонКарточки = FromHex("#333337");
                текст = FromHex("#FFFFFF");
                текстВторичный = FromHex("#A6C8DC");
                акцент = FromHex("#007ACC");
                граница = FromHex("#3F3F46");
                break;

            default: // Тёмная тема (по умолчанию)
                фон = FromHex("#1E1E1E");
                фонПанели = FromHex("#171717");
                фонКарточки = FromHex("#252526");
                текст = FromHex("#FFFFFF");
                текстВторичный = FromHex("#A0A0A0");
                акцент = FromHex("#007ACC");
                граница = FromHex("#333333");
                break;
        }

        УстановитьКисть("ФонБраш", фон);
        УстановитьКисть("ФонПанелиБраш", фонПанели);
        УстановитьКисть("ФонКарточкиБраш", фонКарточки);
        УстановитьКисть("ТекстБраш", текст);
        УстановитьКисть("ТекстВторичныйБраш", текстВторичный);
        УстановитьКисть("АкцентБраш", акцент);
        УстановитьКисть("ГраницаБраш", граница);
    }

    /// <summary>Записывает кисть в ресурсы Application (создаёт, если её ещё нет).</summary>
    private static void УстановитьКисть(string имяРесурса, Color цвет)
    {
        var кисть = new System.Windows.Media.SolidColorBrush(цвет);
        кисть.Freeze(); // замороженная кисть работает быстрее
        Application.Current.Resources[имяРесурса] = кисть;
    }

    /// <summary>Превращает строку "#RRGGBB" или "#AARRGGBB" в структуру Color.</summary>
    private static Color FromHex(string hex)
    {
        hex = hex.TrimStart('#');
        byte a = (byte)(hex.Length == 8 ? Convert.ToByte(hex[..2], 16) : 255);
        int сдвиг = hex.Length == 8 ? 2 : 0;
        return Color.FromArgb(
            a,
            Convert.ToByte(hex.Substring(сдвиг, 2), 16),
            Convert.ToByte(hex.Substring(сдвиг + 2, 2), 16),
            Convert.ToByte(hex.Substring(сдвиг + 4, 2), 16));
    }
}
