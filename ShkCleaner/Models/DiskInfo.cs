using System.Windows.Media;

namespace ShkCleaner.Models;

/// <summary>
/// Модель "Анализ диска": информация о физическом диске (SMART через WMI)
/// и об одном логическом томе (C:, D: ...) с его занятым/свободным местом.
/// Наследует BaseModel - чтобы интерфейс сам обновлялся при изменении полей.
/// </summary>
public class DiskInfo : BaseModel
{
    // ---------- Физические характеристики (SMART / WMI) ----------

    private string _модель = "—";
    /// <summary>Модель диска, например "Samsung SSD 870 EVO 1TB".</summary>
    public string Модель { get => _модель; set { _модель = value; OnPropertyChanged(); } }

    private string _типДиска = "Неизвестно";
    /// <summary>Тип накопителя: SSD, HDD или Неизвестно.</summary>
    public string ТипДиска { get => _типДиска; set { _типДиска = value; OnPropertyChanged(); } }

    private int _здоровьеПроцент = -1;
    /// <summary>Оценка здоровья 0-100%. -1 = данные SMART недоступны.</summary>
    public int ЗдоровьеПроцент
    {
        get => _здоровьеПроцент;
        set
        {
            _здоровьеПроцент = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ЗдоровьеСтрока));
            OnPropertyChanged(nameof(ЗдоровьеКисть));
        }
    }

    /// <summary>Текст для карточки: "98%" или "н/д".</summary>
    public string ЗдоровьеСтрока => ЗдоровьеПроцент >= 0 ? $"{ЗдоровьеПроцент}%" : Loc.Get("NotAvailable");

    /// <summary>Цвет индикатора здоровья: зелёный / оранжевый / красный.</summary>
    public Brush ЗдоровьеКисть => ЗдоровьеПроцент switch
    {
        < 0 => (Brush)new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E)),
        >= 70 => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),   // зелёный
        >= 40 => new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),   // оранжевый
        _ => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),       // красный
    };

    private int _температураЦельсия = -1;
    /// <summary>Температура в °C. -1 = датчик недоступен.</summary>
    public int ТемператураЦельсия { get => _температураЦельсия; set { _температураЦельсия = value; OnPropertyChanged(); OnPropertyChanged(nameof(ТемператураСтрока)); } }

    /// <summary>Текст температуры: "42 °C" или "н/д".</summary>
    public string ТемператураСтрока => ТемператураЦельсия >= 0 ? $"{ТемператураЦельсия} °C" : Loc.Get("NotAvailable");

    private long _часыНаработки = -1;
    /// <summary>Часы наработки (PowerOnHours). -1 = SMART закрыт производителем.</summary>
    public long ЧасыНаработки { get => _часыНаработки; set { _часыНаработки = value; OnPropertyChanged(); OnPropertyChanged(nameof(НаработкаСтрока)); } }

    /// <summary>Красивый текст наработки: "3 года 4 месяца" или "12 480 ч".</summary>
    public string НаработкаСтрока
    {
        get
        {
            if (ЧасыНаработки < 0) return Loc.Get("NotAvailable");
            long лет = ЧасыНаработки / (24 * 365);
            long месяцев = (ЧасыНаработки % (24 * 365)) / (24 * 30);
            if (лет > 0) return $"{лет} г. {месяцев} мес.";
            return $"{ЧасыНаработки:#,0} ч.";
        }
    }

    private bool _естьSmart;
    /// <summary>Удалось ли вообще прочитать SMART-атрибуты этого диска.</summary>
    public bool ЕстьSmart { get => _естьSmart; set { _естьSmart = value; OnPropertyChanged(); OnPropertyChanged(nameof(SmartНетСтрока)); } }

    /// <summary>Подпись "SMART недоступен" для виртуальных/сетевых дисков.</summary>
    public string SmartНетСтрока => Loc.Get("SmartUnavailable");

    // ---------- Логические тома на этом диске ----------

    private string _томаСводка = "";
    /// <summary>Строка вида "C: 210/476 ГБ • D: 800/1000 ГБ" - все разделы диска.</summary>
    public string ТомаСводка { get => _томаСводка; set { _томаСводка = value; OnPropertyChanged(); } }

    /// <summary>Буквы всех логических томов этого физического диска ("C:", "D:"...).</summary>
    public List<string> БуквыТомов { get; set; } = new();
}

/// <summary>
/// Одна строка результата сканирования папок (для визуализации "деревом"
/// со столбиками-прогрессбарами вместо настоящего treemap).
/// </summary>
public class FolderScanItem : BaseModel
{
    public string Путь { get; set; } = "";

    /// <summary>Имя папки без пути (показывается в списке).</summary>
    public string Имя { get; set; } = "";

    /// <summary>Глубина вложенности (0 = корневые папки диска) - для отступа в списке.</summary>
    public int Глубина { get; set; }

    private long _размерБайт;
    public long РазмерБайт
    {
        get => _размерБайт;
        set { _размерБайт = value; OnPropertyChanged(); OnPropertyChanged(nameof(РазмерСтрока)); }
    }

    /// <summary>Размер человекочитаемый: "3.2 ГБ".</summary>
    public string РазмерСтрока => Utils.ФорматироватьРазмер(РазмерБайт);

    /// <summary>Доля от размера самого большого элемента (0..1) - ширина полоски.</summary>
    public double ДоляПрогресса { get; set; }

    /// <summary>Количество файлов внутри папки.</summary>
    public int КоличествоФайлов { get; set; }
}

/// <summary>
/// Самый тяжёлый файл на диске (список "Топ файлов").
/// </summary>
public class BigFileItem
{
    public string Путь { get; set; } = "";
    public string Имя { get; set; } = "";
    public long РазмерБайт { get; set; }
    public string РазмерСтрока => Utils.ФорматироватьРазмер(РазмерБайт);
    public DateTime ДатаИзменения { get; set; }
    public string ДатаСтрока => ДатаИзменения.ToString("dd.MM.yyyy");
}
