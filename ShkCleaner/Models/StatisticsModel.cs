using System.Text.Json.Serialization;

namespace ShkCleaner.Models;

/// <summary>
/// Вся накопленная статистика работы SHK CLEANER.
/// Сохраняется в JSON-файл %AppData%\ShkCleaner\statistics.json
/// (см. Helpers/StatisticsManager.cs).
/// </summary>
public class ShkCleanerStatistics
{
    /// <summary>Суммарно освобождено места на диске, байт (все очистки).</summary>
    public long ВсегоОсвобожденоБайт { get; set; }

    /// <summary>Сколько раз запускалась очистка системы.</summary>
    public int ОчистокВыполнено { get; set; }

    /// <summary>Сколько программ удалено через приложение.</summary>
    public int ПрограммУдалено { get; set; }

    /// <summary>Сколько фото отправлено в Корзину через приложение.</summary>
    public int ФотоУдалено { get; set; }

    /// <summary>Сколько сканов дисков выполнялось.</summary>
    public int ДисковПросканировано { get; set; }

    /// <summary>Дата первого запуска программы (для "стажа").</summary>
    public DateTime ПерваяЧистка { get; set; }

    /// <summary>Освобождено байт ПО ДНЯМ: ключ "2026-10-12", значение - байт за день.</summary>
    public Dictionary<string, long> ОсвобождениеПоДням { get; set; } = new();

    /// <summary>Освобождено байт ПО КАТЕГОРИЯМ очистки: ключ "TempUser", значение - байт.</summary>
    public Dictionary<string, long> ПоКатегориям { get; set; } = new();

    /// <summary>История действий (журнал), newest -> oldest не гарантируется, сортируется при показе.</summary>
    public List<StatEvent> История { get; set; } = new();

    /// <summary>Названия полученных достижений (бейджей).</summary>
    public List<string> ПолученныеДостижения { get; set; } = new();

    [JsonIgnore]
    public static ShkCleanerStatistics Пустая => new();
}

/// <summary>
/// Одна запись журнала действий ("очистил 1.2 ГБ", "удалил программу X" ...).
/// Экспортируется в CSV кнопкой "Экспорт CSV".
/// </summary>
public class StatEvent
{
    /// <summary>Момент события (сохраняется как ISO-8601 автоматически).</summary>
    public DateTime Время { get; set; }

    /// <summary>Тип: Clean / Uninstall / PhotoDelete / DiskScan.</summary>
    public string Тип { get; set; } = "";

    /// <summary>Подробности для человека: название категории или программы.</summary>
    public string Детали { get; set; } = "";

    /// <summary>Освобождено байт (0 для событий без размера).</summary>
    public long Байт { get; set; }
}

/// <summary>
/// Описание одного достижения (бейджа) системы геймификации.
/// </summary>
public class Achievement
{
    public Achievement(string код, string эмодзи, string названиеЛокаль, string описаниеЛокаль, Func<ShkCleanerStatistics, bool> условие)
    {
        Код = код; Эмодзи = эмодзи; НазваниеЛокаль = названиеЛокаль; ОписаниеЛокаль = описаниеЛокаль; Условие = условие;
    }

    /// <summary>Уникальный англоязычный код (пишется в statistics.json).</summary>
    public string Код { get; }
    /// <summary>Картинка бейджа (эмодзи).</summary>
    public string Эмодзи { get; }
    /// <summary>Ключ перевода названия в словаре Loc.</summary>
    public string НазваниеЛокаль { get; }
    /// <summary>Ключ перевода описания в словаре Loc.</summary>
    public string ОписаниеЛокаль { get; }
    /// <summary>Функция-проверка: получено ли уже достижение по текущей статистике.</summary>
    public Func<ShkCleanerStatistics, bool> Условие { get; }
}
