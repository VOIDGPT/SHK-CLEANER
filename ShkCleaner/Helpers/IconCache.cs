using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShkCleaner.Helpers;

/// <summary>
/// Кэш извлечённых иконок программ.
/// Нужен, чтобы один и тот же файл (например, C:\Windows\System32\shell32.dll)
/// не декодировался десятки раз для разных записей реестра - экономим память
/// и ускоряем загрузку списка программ.
/// </summary>
public static class IconCache
{
    // Словарь безопасен для многопоточного использования
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ImageSource?> _кэш = new();

    /// <summary>
    /// Возвращает иконку из файла (из кэша, если уже извлекали ранее).
    /// sizePx - желаемый размер в пикселях (32 для таблицы, 64-256 для предпросмотра).
    /// </summary>
    public static ImageSource? Получить(string путьКФайлу, int индекс, int sizePx = 32)
    {
        if (string.IsNullOrWhiteSpace(путьКФайлу)) return null;

        string ключ = $"{путьКФайлу}|{индекс}";
        // GetOrAdd: если значения ещё нет - вызывает фабрику один раз и кладёт в словарь
        return _кэш.GetOrAdd(ключ, _ =>
        {
            try
            {
                // Извлекаем нативный значок Windows из exe/dll/ico файла
                var оригинал = ProgramUninstaller.ИзвлечьИконку(путьКФайлу, индекс);
                if (оригинал is not BitmapSource битмап) return оригинал;

                // Масштабируем и пересохраняем через PNG-энкодер: результат
                // полностью независим от оригинала и безопасен для любого потока.
                var энкодер = new PngBitmapEncoder();
                энкодер.Frames.Add(BitmapFrame.Create(
                    new TransformedBitmap(битмап, new ScaleTransform(
                        (double)sizePx / битмап.PixelWidth,
                        (double)sizePx / битмап.PixelHeight))));

                using var память = new System.IO.MemoryStream();
                энкодер.Save(память);
                память.Position = 0;

                var готовая = new BitmapImage();
                готовая.BeginInit();
                готовая.CacheOption = BitmapCacheOption.OnLoad; // прочитать сразу, поток можно закрыть
                готовая.StreamSource = память;
                готовая.EndInit();
                готовая.Freeze(); // замороженную картинку можно показывать из любого потока
                return готовая;
            }
            catch
            {
                // Иконка - украшение: при любой проблеме просто показываем заглушку
                return null;
            }
        });
    }
}
