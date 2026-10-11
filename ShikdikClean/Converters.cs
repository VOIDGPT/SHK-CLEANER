using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ShxClean;

/// <summary>
/// Конвертер для XAML: превращает bool в Visibility.
/// true  -> Collapsed (элемент скрыт)
/// false -> Visible   (элемент виден)
/// Пример использования: заглушка миниатюры видна, пока ЕстьМиниатюра == false.
/// </summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? значение, Type targetType, object? параметр, CultureInfo культура)
    {
        if (значение is bool b && b)
            return Visibility.Collapsed; // true -> спрятать
        return Visibility.Visible;       // false или null -> показать
    }

    // Обратное преобразование не нужно (Binding Mode=OneWay), но интерфейс обязывает
    public object ConvertBack(object? значение, Type targetType, object? параметр, CultureInfo культура)
        => throw new NotSupportedException();
}
