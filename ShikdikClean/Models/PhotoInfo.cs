using System.IO;
using System.Windows.Media.Imaging;

namespace ShikdikClean.Models;

/// <summary>
/// Модель данных "Фотография".
/// Хранит информацию об одном изображении на диске и его миниатюру для сетки.
/// </summary>
public class PhotoInfo : BaseModel
{
    private bool _выбрано;
    private BitmapSource? _миниатюра;

    /// <summary>Полный путь к файлу изображения.</summary>
    public string ПутьКФайлу { get; set; } = "";

    /// <summary>Имя файла (например "IMG_2026.jpg").</summary>
    public string ИмяФайла => Path.GetFileName(ПутьКФайлу);

    /// <summary>Папка, в которой лежит файл (для подписи в предпросмотре).</summary>
    public string Папка => Path.GetDirectoryName(ПутьКФайлу) ?? "";

    private long _размерБайт;
    /// <summary>Размер файла в байтах.</summary>
    public long РазмерБайт
    {
        get => _размерБайт;
        set
        {
            Заполнить(ref _размерБайт, value);
            // Уведомляем интерфейс, что производные строковые поля тоже изменились
            OnPropertyChanged(nameof(РазмерЧеловек));
        }
    }

    /// <summary>Размер в человекочитаемом виде (КБ/МБ).</summary>
    public string РазмерЧеловек => Utils.ФорматироватьРазмер(РазмерБайт);

    private int _ширина;
    private int _высота;

    /// <summary>Ширина изображения в пикселях (0 = ещё не загружено / ошибка).</summary>
    public int Ширина { get => _ширина; set => Заполнить(ref _ширина, value); }

    /// <summary>Высота изображения в пикселях.</summary>
    public int Высота { get => _высота; set => Заполнить(ref _высота, value); }

    /// <summary>Разрешение для отображения: "1920 x 1080".</summary>
    public string Разрешение =>
        (Ширина > 0 && Высота > 0) ? $"{Ширина} x {Высота}" : "—";

    /// <summary>Дата изменения файла (для панели предпросмотра).</summary>
    public DateTime ДатаИзменения { get; set; }

    /// <summary>Отмечен ли файл чекбоксом для удаления.</summary>
    public bool Выбрано
    {
        get => _выбрано;
        set => Заполнить(ref _выбрано, value);
    }

    /// <summary>
    /// Миниатюра 150x150 для сетки. Загружается асинхронно после создания модели,
    /// поэтому может быть null (в интерфейсе тогда показывается заглушка).
    /// </summary>
    public BitmapSource? Миниатюра
    {
        get => _миниатюра;
        set
        {
            Заполнить(ref _миниатюра, value);
            OnPropertyChanged(nameof(ЕстьМиниатюра));
        }
    }

    /// <summary>Загружена ли уже миниатюра (управляет видимостью картинки-заглушки).</summary>
    public bool ЕстьМиниатюра => _миниатюра != null;

    /// <summary>Полноразмерное изображение для панели предпросмотра (грузится по клику).</summary>
    public BitmapSource? ПолноеИзображение { get; set; }
}
