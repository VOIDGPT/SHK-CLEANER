using System.Globalization;

namespace ShkCleaner.Models;

/// <summary>
/// Класс настроек приложения. Сохраняется в файл appsettings.json
/// рядом с исполняемым файлом (см. Helpers/SettingsManager.cs).
/// </summary>
public class AppSettings
{
    /// <summary>Запускать ли программу вместе с Windows (ярлык в папке «Автозагрузка»).</summary>
    public bool Автозапуск { get; set; } = false;

    /// <summary>Язык интерфейса: "ru" (русский), "en" (English), "tr" (Türkçe).</summary>
    public string Язык { get; set; } = "ru";

    /// <summary>Тема оформления: "dark", "light" или "blue".</summary>
    public string Тема { get; set; } = "dark";

    /// <summary>Показывать ли MessageBox-подтверждения перед удалением файлов/программ.</summary>
    public bool ПоказыватьПодтверждения { get; set; } = true;

    /// <summary>Создавать ли точку восстановления системы перед очисткой (пока заглушка).</summary>
    public bool СоздаватьТочкуВосстановления { get; set; } = false;

    /// <summary>Очищать ли Корзину автоматически после очистки системы.</summary>
    public bool ОчищатьКорзинуАвтоматически { get; set; } = false;

    /// <summary>Минимальный размер файла (МБ) в папке «Загрузки», который считается «большим».
    /// Используется для валидации: значение должно быть от 1 до 10240 МБ.</summary>
    public int МинимальныйРазмерФайлаЗагрузокМБ { get; set; } = 100;

    /// <summary>Возвращает копию настроек по умолчанию (для кнопки «Сбросить к заводским»).</summary>
    public static AppSettings Заводские() => new AppSettings();
}

/// <summary>
/// Статический помощник: форматирование размеров и другие мелкие утилиты.
/// </summary>
public static class Utils
{
    /// <summary>
    /// Превращает количество байт в понятную строку: "512 Б", "150.4 МБ", "3.2 ГБ".
    /// CultureInfo.InvariantCulture - чтобы разделитель точки не зависел от языка.
    /// </summary>
    public static string ФорматироватьРазмер(long байт)
    {
        if (байт < 0) байт = 0;
        string[] единицы = { "Б", "КБ", "МБ", "ГБ", "ТБ" };
        double значение = байт;
        int индекс = 0;
        // Делим на 1024, пока значение больше 1024 и есть следующая единица измерения
        while (значение >= 1024 && индекс < единицы.Length - 1)
        {
            значение /= 1024.0;
            индекс++;
        }
        // Байты показываем целым числом, остальные единицы - с одним знаком после точки
        return индекс == 0
            ? $"{(long)значение} {единицы[индекс]}"
            : $"{значение.ToString("0.#", CultureInfo.InvariantCulture)} {единицы[индекс]}";
    }
}
