namespace ShikdikClean.Models;

/// <summary>
/// Модель данных "Категория очистки".
/// Одна такая запись = один чекбокс на странице "Очистка системы"
/// (например "Временные файлы пользователя (%TEMP%)").
/// </summary>
public class CleanCategory : BaseModel
{
    private bool _отмечено;
    private long _размерБайт;
    private int _количествоФайлов;
    private bool _найден;

    /// <summary>Уникальный код категории (используется в коде, например "TempUser").</summary>
    public string Код { get; set; } = "";

    private string _название = "";
    /// <summary>Название для отображения (переводится локализатором).</summary>
    public string Название
    {
        get => _название;
        set => Заполнить(ref _название, value);
    }

    /// <summary>Отмечен ли чекбокс (будет ли эта категория очищаться).</summary>
    public bool Отмечено
    {
        get => _отмечено;
        set => Заполнить(ref _отмечено, value);
    }

    /// <summary>Сколько байт занимает мусор в этой категории (после сканирования).</summary>
    public long РазмерБайт
    {
        get => _размерБайт;
        set
        {
            Заполнить(ref _размерБайт, value);
            // Сообщаем интерфейсу обновить производные текстовые поля
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(РазмерЧеловек));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(Инфо));
        }
    }

    /// <summary>Сколько файлов найдено в категории.</summary>
    public int КоличествоФайлов
    {
        get => _количествоФайлов;
        set
        {
            Заполнить(ref _количествоФайлов, value);
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(Инфо));
        }
    }

    /// <summary>Было ли выполнено сканирование этой категории.</summary>
    public bool Найден
    {
        get => _найден;
        set
        {
            Заполнить(ref _найден, value);
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(Инфо));
        }
    }

    /// <summary>Размер в удобном виде: "150.4 МБ".</summary>
    public string РазмерЧеловек => Utils.ФорматироватьРазмер(РазмерБайт);

    /// <summary>Строка статуса под названием категории.</summary>
    public string Инфо => !Найден
        ? Loc.Get("ScanHint")
        : string.Format(Loc.Get("FoundFiles"), КоличествоФайлов, РазмерЧеловек);
}

/// <summary>
/// Модель данных "Большой файл из папки Загрузки".
/// Показывается отдельным списком (файлы крупнее 100 МБ).
/// </summary>
public class BigFile
{
    public string ПутьКФайлу { get; set; } = "";
    public string Имя => System.IO.Path.GetFileName(ПутьКФайлу);
    public long РазмерБайт { get; set; }
    public string РазмерЧеловек => Utils.ФорматироватьРазмер(РазмерБайт);
    public DateTime ДатаИзменения { get; set; }
    public override string ToString() => $"{Имя} ({РазмерЧеловек})";
}
