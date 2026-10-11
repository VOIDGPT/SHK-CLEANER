using System.IO;
using Microsoft.VisualBasic.FileIO; // FileSystem.DeleteFile - удаление в Корзину
using ShkCleaner.Models;

namespace ShkCleaner.Helpers;

/// <summary>
/// Класс для работы с фотографиями: сканирование папок, получение информации
/// о файлах и безопасное удаление (в Корзину, а не навсегда!).
/// </summary>
public class PhotoManager
{
    /// <summary>Поддерживаемые расширения изображений (в нижнем регистре с точкой).</summary>
    public static readonly string[] Расширения = { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif" };

    /// <summary>Папка «Изображения» текущего пользователя по умолчанию.</summary>
    public static string ПапкаПоУмолчанию =>
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

    /// <summary>Проверяет, является ли файл изображением по расширению.</summary>
    public static bool ЭтоИзображение(string путьКФайлу) =>
        Расширения.Contains(Path.GetExtension(путьКФайлу).ToLowerInvariant());

    /// <summary>
    /// Асинхронно сканирует папку и ВСЕ подпапки, возвращая список найденных изображений.
    /// Ошибки доступа к отдельным файлам/папкам молча пропускаются.
    /// </summary>
    public Task<List<PhotoInfo>> СканироватьПапкуAsync(string путьКПапке)
    {
        return Task.Run(() =>
        {
            var результат = new List<PhotoInfo>();
            try
            {
                if (!Directory.Exists(путьКПапке)) return результат;

                var варианты = new EnumerationOptions
                {
                    IgnoreInaccessible = true,       // не падать на закрытых для нас папках
                    RecurseSubdirectories = true,    // искать во всех подпапках
                };

                foreach (string файл in Directory.EnumerateFiles(путьКПапке, "*", варианты))
                {
                    if (!ЭтоИзображение(файл)) continue; // берём только картинки

                    try
                    {
                        var info = new FileInfo(файл);
                        результат.Add(new PhotoInfo
                        {
                            ПутьКФайлу = файл,
                            РазмерБайт = info.Length,
                            ДатаИзменения = info.LastWriteTime,
                        });
                    }
                    catch (Exception e) when (e is UnauthorizedAccessException || e is IOException)
                    {
                        // Информация об этом файле недоступна - пропускаем
                    }
                }
            }
            catch (Exception e) when (e is UnauthorizedAccessException || e is IOException)
            {
                // Нет доступа к корневой папке - возвращаем пустой список
            }

            // Сортировка: сначала новые фотографии
            результат.Sort((a, b) => b.ДатаИзменения.CompareTo(a.ДатаИзменения));
            return результат;
        });
    }

    /// <summary>
    /// Безопасно удаляет файл, перемещая его в КОРЗИНУ Windows
    /// (методом Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile).
    /// Возвращает текст ошибки или null, если всё получилось.
    /// </summary>
    public string? УдалитьВКорзину(string путьКФайлу)
    {
        try
        {
            FileSystem.DeleteFile(
                путьКФайлу,
                UIOption.OnlyErrorDialogs,          // показывать диалог только при ошибке
                RecycleOption.SendToRecycleBin);    // ВАЖНО: в Корзину, а не навсегда!
            return null; // null = успех
        }
        catch (ArgumentException)
        {
            // Файл уже удалён
            return null;
        }
        catch (Exception e)
        {
            return string.Format(Loc.Get("ErrorOccurred"), $"{Path.GetFileName(путьКФайлу)}: {e.Message}");
        }
    }

    /// <summary>
    /// Читает разрешение (ширину и высоту) изображения без полной загрузки файла в память.
    /// Используется BitmapFrame.Create с флагом DelayCreation - декодируются только заголовки.
    /// </summary>
    public static void ЗаполнитьИнформациюОРазмере(PhotoInfo фото)
    {
        // Читаем через BitmapFrame (он сам управляет потоком внутри)
        try
        {
            var кадр = System.Windows.Media.Imaging.BitmapFrame.Create(
                new Uri(фото.ПутьКФайлу, UriKind.Absolute),
                System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation,
                System.Windows.Media.Imaging.BitmapCacheOption.None);
            фото.Ширина = (int)кадр.PixelWidth;
            фото.Высота = (int)кадр.PixelHeight;
        }
        catch (Exception e)
        {
            // Формат повреждён или не поддерживается (например WEBP без кодека) - оставляем 0x0
            System.Diagnostics.Debug.WriteLine($"Не удалось прочитать разрешение: {e.Message}");
        }
    }

    /// <summary>
    /// Создаёт миниатюру 150x150 пикселей для сетки фотографий.
    /// DecodePixelWidth говорит WPF декодировать картинку сразу в уменьшенном размере -
    /// это быстро и не ест много памяти даже для огромных фото.
    /// </summary>
    public static System.Windows.Media.Imaging.BitmapImage СоздатьМиниатюру(string путьКФайлу)
    {
        var битмап = new System.Windows.Media.Imaging.BitmapImage();
        using var поток = new FileStream(путьКФайлу, FileMode.Open, FileAccess.Read, FileShare.Read);
        битмап.BeginInit();
        битмап.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; // загрузить сразу и отпустить файл
        битмап.DecodePixelWidth = 150;                                               // размер миниатюры
        битмап.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
        битмап.StreamSource = поток;
        битмап.EndInit();
        битмап.Freeze(); // замораживаем, чтобы можно было использовать из других потоков и ускорить рендер
        return битмап;
    }

    /// <summary>Загружает полноразмерное изображение для панели предпросмотра.</summary>
    public static System.Windows.Media.Imaging.BitmapImage ЗагрузитьПолноеИзображение(string путьКФайлу)
    {
        var битмап = new System.Windows.Media.Imaging.BitmapImage();
        using var поток = new FileStream(путьКФайлу, FileMode.Open, FileAccess.Read, FileShare.Read);
        битмап.BeginInit();
        битмап.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        битмап.StreamSource = поток;
        битмап.EndInit();
        битмап.Freeze();
        return битмап;
    }
}
