using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ShkCleaner.Models;

/// <summary>
/// Модель данных "Установленная программа".
/// Используется для отображения в DataGrid на странице "Удаление программ".
/// Наследуется от BaseModel, чтобы поддерживать уведомления интерфейса (INotifyPropertyChanged).
/// </summary>
public class ProgramInfo : BaseModel
{
    private string _имя = "";
    private string _издатель = "";
    private string _версия = "";
    private DateTime? _датаУстановки;
    private double _размерКб;
    private string _строкаУдаления = "";
    private bool _возможноТихоеУдаление;

    /// <summary>Название программы (например "Google Chrome").</summary>
    public string Имя
    {
        get => _имя;
        set => Заполнить(ref _имя, value);
    }

    /// <summary>Издатель программы (например "Google LLC").</summary>
    public string Издатель
    {
        get => _издатель;
        set => Заполнить(ref _издатель, value);
    }

    /// <summary>Версия программы.</summary>
    public string Версия
    {
        get => _версия;
        set => Заполнить(ref _версия, value);
    }

    /// <summary>Дата установки (может быть неизвестна, поэтому nullable).</summary>
    public DateTime? ДатаУстановки
    {
        get => _датаУстановки;
        set => Заполнить(ref _датаУстановки, value);
    }

    /// <summary>Размер программы в килобайтах (из реестра, поле EstimatedSize).</summary>
    public double РазмерКб
    {
        get => _размерКб;
        set => Заполнить(ref _размерКб, value);
    }

    /// <summary>
    /// Строка команды удаления (UninstallString из реестра).
    /// Именно её запускает Windows при деинсталляции.
    /// </summary>
    public string СтрокаУдаления
    {
        get => _строкаУдаления;
        set => Заполнить(ref _строкаУдаления, value);
    }

    /// <summary>Есть ли у программы тихий (фоновый) режим удаления (QuietUninstallString).</summary>
    public bool ВозможноТихоеУдаление
    {
        get => _возможноТихоеУдаление;
        set => Заполнить(ref _возможноТихоеУдаление, value);
    }

    // ---------- ИКОНКА ПРОГРАММЫ ----------
    private System.Windows.Media.ImageSource? _иконка;

    /// <summary>Путь к файлу, из которого можно извлечь иконку (DisplayIcon из реестра).</summary>
    public string? ПутьКИконкеФайла { get; set; }

    /// <summary>Индекс иконки внутри файла (у exe может быть несколько иконок).</summary>
    public int ИндексИконки { get; set; }

    /// <summary>
    /// Готовая картинка-иконка для таблицы. Заполняется асинхронно после загрузки списка.
    /// Если null - в таблице показываем красивую заглушку по первой букве названия.
    /// </summary>
    public System.Windows.Media.ImageSource? Иконка
    {
        get => _иконка;
        set
        {
            Заполнить(ref _иконка, value);
            OnPropertyChanged(nameof(ЕстьИконка));
        }
    }

    /// <summary>Есть ли реальная иконка (управляет видимостью заглушки).</summary>
    public bool ЕстьИконка => Иконка is not null;

    /// <summary>Первая буква названия - для красивой цветной заглушки.</summary>
    public string ПерваяБуква => Имя.Length > 0 ? Имя[..1].ToUpperInvariant() : "?";

    /// <summary>Дата установки в удобном виде для таблицы ("неизвестно", если даты нет).</summary>
    public string ДатаУстановкиСтрока =>
        ДатаУстановки.HasValue ? ДатаУстановки.Value.ToString("dd.MM.yyyy") : "—";

    /// <summary>Размер в человекочитаемом виде (МБ/ГБ).</summary>
    public string РазмерЧеловек => Utils.ФорматироватьРазмер((long)(РазмерКб * 1024));

    /// <summary>Поиск по таблице фильтрует именно по этим полям.</summary>
    public bool ПодходитПодФильтр(string фильтр)
    {
        if (string.IsNullOrWhiteSpace(фильтр)) return true;
        return Имя.Contains(фильтр, StringComparison.OrdinalIgnoreCase)
            || Издатель.Contains(фильтр, StringComparison.OrdinalIgnoreCase)
            || Версия.Contains(фильтр, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Базовый класс всех моделей: реализует INotifyPropertyChanged.
/// Благодаря этому WPF автоматически обновляет интерфейс, когда меняются свойства.
/// </summary>
public abstract class BaseModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Помощник: записывает новое значение и уведомляет интерфейс об изменении.</summary>
    protected void Заполнить<T>(ref T поле, T значение, [CallerMemberName] string имяСвойства = "")
    {
        // Если значение не изменилось - ничего не делаем (оптимизация)
        if (EqualityComparer<T>.Default.Equals(поле, значение)) return;
        поле = значение;
        OnPropertyChanged(имяСвойства);
    }

    /// <summary>Уведомляет WPF-интерфейс о том, что свойство с таким именем изменилось.</summary>
    protected void OnPropertyChanged([CallerMemberName] string? имяСвойства = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(имяСвойства));
    }
}
