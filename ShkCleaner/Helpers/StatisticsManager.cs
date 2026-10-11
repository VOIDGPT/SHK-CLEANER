using System.IO;
using System.Text;
using System.Text.Json;
using ShkCleaner.Models;

namespace ShkCleaner.Helpers;

/// <summary>
/// Хранит и обновляет статистику работы SHK CLEANER:
/// сколько очищено, удалено программ/фото, история действий и достижения.
/// Всё складывается в JSON: %AppData%\ShkCleaner\statistics.json
/// </summary>
public static class StatisticsManager
{
    // Настройки сериализации: русские буквы без \uXXXX + отступы для удобства чтения
    private static readonly JsonSerializerOptions jsonПараметры = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Текущая статистика (грузится один раз при запуске).</summary>
    public static ShkCleanerStatistics Текущая { get; private set; } = Загрузить();

    /// <summary>Список всех достижений (бейджей). Условие проверяется по статистике.</summary>
    public static readonly Achievement[] Достижения = new[]
    {
        new Achievement("first_clean", "🧹", "AchFirstClean", "AchFirstCleanDesc", s => s.ОчистокВыполнено >= 1),
        new Achievement("clean_10", "📦", "AchTenCleans", "AchTenCleansDesc", s => s.ОчистокВыполнено >= 10),
        new Achievement("freed_1gb", "💾", "AchOneGigabyte", "AchOneGigabyteDesc", s => s.ВсегоОсвобожденоБайт >= 1L * 1024 * 1024 * 1024),
        new Achievement("freed_10gb", "🏆", "AchTenGigabytes", "AchTenGigabytesDesc", s => s.ВсегоОсвобожденоБайт >= 10L * 1024 * 1024 * 1024),
        new Achievement("uninstall_1", "🗑️", "AchFirstUninstall", "AchFirstUninstallDesc", s => s.ПрограммУдалено >= 1),
        new Achievement("photos_50", "🖼️", "AchFiftyPhotos", "AchFiftyPhotosDesc", s => s.ФотоУдалено >= 50),
        new Achievement("disk_scan_1", "💾", "AchFirstDiskScan", "AchFirstDiskScanDesc", s => s.ДисковПросканировано >= 1),
    };

    /// <summary>Читает statistics.json. Повреждённый файл = чистая статистика (не падаем!)</summary>
    private static ShkCleanerStatistics Загрузить()
    {
        try
        {
            if (File.Exists(ShkCleanerPaths.ФайлСтатистики))
            {
                var данные = JsonSerializer.Deserialize<ShkCleanerStatistics>(
                    File.ReadAllText(ShkCleanerPaths.ФайлСтатистики), jsonПараметры);
                if (данные is not null) return данные;
            }
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Статистика не прочитана: {e.Message}");
        }
        return new ShkCleanerStatistics();
    }

    /// <summary>Сохраняет статистику в JSON. Ошибки записи тихо логируем - это не критично.</summary>
    private static void Сохранить()
    {
        try
        {
            File.WriteAllText(ShkCleanerPaths.ФайлСтатистики,
                JsonSerializer.Serialize(Текущая, jsonПараметры));
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Статистика не сохранена: {e.Message}");
        }
    }

    /// <summary>Ключ дня для словаря "по дням": yyyy-MM-dd (invariant - всегда точка-дефис).</summary>
    private static string КлючДня(DateTime д) => д.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    // ==================== Публичные методы учёта ====================

    /// <summary>Учитывает завершённую очистку системы: освобождено байт + категории.</summary>
    public static List<string> ОчисткаЗавершена(long освобожденоБайт, Dictionary<string, long>? категории = null)
    {
        var новые = new List<string>();
        if (Текущая.ПерваяЧистка == default) Текущая.ПерваяЧистка = DateTime.Now;
        Текущая.ОчистокВыполнено++;
        Текущая.ВсегоОсвобожденоБайт += освобожденоБайт;

        string день = КлючДня(DateTime.Now);
        Текущая.ОсвобождениеПоДням[день] =
            Текущая.ОсвобождениеПоДням.GetValueOrDefault(день) + освобожденоБайт;

        if (категории is not null)
            foreach (var (код, байт) in категории)
                Текущая.ПоКатегориям[код] = Текущая.ПоКатегориям.GetValueOrDefault(код) + байт;

        ДобавитьСобытие("Clean", Loc.Get("SysCleanup"), освобожденоБайт);
        НовыеДостижения(новые);
        Сохранить();
        return новые;
    }

    /// <summary>Учитывает удаление программы через раздел "Удаление программ".</summary>
    public static List<string> ПрограммаУдалена(string имяПрограммы)
    {
        var новые = new List<string>();
        Текущая.ПрограммУдалено++;
        ДобавитьСобытие("Uninstall", имяПрограммы, 0);
        НовыеДостижения(новые);
        Сохранить();
        return новые;
    }

    /// <summary>Учитывает отправку фото в Корзину.</summary>
    public static List<string> ФотоУдалены(int количество, long байт)
    {
        var новые = new List<string>();
        Текущая.ФотоУдалено += количество;
        ДобавитьСобытие("PhotoDelete", $"{количество}", байт);
        НовыеДостижения(новые);
        Сохранить();
        return новые;
    }

    /// <summary>Учитывает сканирование диска в разделе "Анализ диска".</summary>
    public static List<string> ДискиПросканированы(int количествоПапок)
    {
        var новые = new List<string>();
        Текущая.ДисковПросканировано++;
        ДобавитьСобытие("DiskScan", $"{количествоПапок}", 0);
        НовыеДостижения(новые);
        Сохранить();
        return новые;
    }

    /// <summary>Добавляет запись в журнал истории (не старше 500 последних событий).</summary>
    private static void ДобавитьСобытие(string тип, string детали, long байт)
    {
        Текущая.История.Add(new StatEvent { Время = DateTime.Now, Тип = тип, Детали = детали, Байт = байт });
        if (Текущая.История.Count > 500)
            Текущая.История.RemoveAt(0); // выкидываем самое старое событие
    }

    /// <summary>Проверяет все достижения, добавляет newly-полученные в список "новые".</summary>
    private static void НовыеДостижения(List<string> новые)
    {
        foreach (var д in Достижения)
        {
            if (!Текущая.ПолученныеДостижения.Contains(д.Код) && д.Условие(Текущая))
            {
                Текущая.ПолученныеДостижения.Add(д.Код);
                новые.Add(д.НазваниеЛокаль); // ключ перевода - UI сам подставит текст
            }
        }
    }

    /// <summary>Экспортирует историю действий в CSV-файл. Возвращает текст ошибки или null.</summary>
    public static string? ЭкспортироватьCsv(string путьКФайлу)
    {
        try
        {
            var sb = new StringBuilder();
            // Заголовок файла отчёта - с официальным названием программы
            sb.AppendLine("; Отчёт SHK CLEANER - история действий");
            sb.AppendLine("Дата;Время;Тип;Подробности;Освобождено байт");
            foreach (var с in Текущая.История.OrderByDescending(x => x.Время))
            {
                // В CSV кавычки внутри текста удваиваются, чтобы не ломать формат
                string детали = с.Детали.Replace("\"", "\"\"");
                sb.AppendLine($"{с.Время:yyyy-MM-dd};{с.Время:HH:mm:ss};{с.Тип};\"{детали}\";{с.Байт}");
            }
            // UTF-8 с BOM - чтобы Excel корректно открыл русские буквы
            File.WriteAllText(путьКФайлу, sb.ToString(), new UTF8Encoding(true));
            return null;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    /// <summary>Полностью обнуляет статистику (для кнопки "Сброс" на странице Статистика).</summary>
    public static void Сбросить()
    {
        Текущая = new ShkCleanerStatistics();
        Сохранить();
    }
}
