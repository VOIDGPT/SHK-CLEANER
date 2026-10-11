using System.Diagnostics;
using System.Security.Principal;
using System.Windows;
using ShikdikClean.Helpers;

namespace ShikdikClean;

/// <summary>
/// Точка входа приложения. Здесь мы:
/// 1) Проверяем права администратора. Если их нет - перезапускаем себя
///    через UAC (verb "runas"). Правы администратора обязательны!
///    (В манифесте стоит asInvoker специально, чтобы Visual Studio могла
///    запускать программу на отладку без ошибки "Access is denied".)
/// 2) Загружаем сохранённые настройки, применяем язык и тему перед созданием окна.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // ---------- Шаг 0. Глобальная защита от "тихих" вылетов ----------
        // Если где-то в коде произойдёт необработанная ошибка, мы покажем
        // понятное сообщение вместо белого окна и молчаливого закрытия.
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                "Произошла непредвиденная ошибка:\n\n" + args.Exception.Message,
                "Shikdik Clean — ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true; // не даём приложению упасть
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            MessageBox.Show(
                "Критическая ошибка:\n\n" + args.ExceptionObject,
                "Shikdik Clean — ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        };

        // ---------- Шаг 1. Проверка прав администратора ----------
        if (!ЗапущенКакАдминистратор())
        {
            // Аргумент --shikdik-restart защита от бесконечного цикла перезапусков
            // (например, если пользователь в окне UAC нажал "Нет").
            bool этоПерезапуск = e.Args.Contains("--shikdik-restart");

            var ответ = MessageBox.Show(
                "Shikdik Clean требует прав администратора,\n" +
                "чтобы очищать системные папки и корзину.\n\n" +
                "Нажмите \"OK\" - программа перезапустится и запросит права (UAC).\n" +
                "Нажмите \"Отмена\" - программа продолжит работу в ограниченном режиме.",
                "Требуется администратор",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (ответ == MessageBoxResult.OK && !этоПерезапуск)
            {
                try
                {
                    // Перезапускаем свою exe-шку с повышенными правами (появит окно UAC)
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = Environment.ProcessPath ?? "Shikdik Clean.exe",
                        Arguments = "--shikdik-restart",
                        Verb = "runas",              // ключевое слово: запросить права администратора
                        UseShellExecute = true      // verb runas работает только так
                    };
                    Process.Start(startInfo);
                    Shutdown();                      // закрываем текущий (неадминский) экземпляр
                    return;                          // не создаём окно
                }
                catch (Exception ex)
                {
                    // Если пользователь отклонил окно UAC - получаем Exception
                    MessageBox.Show($"Не удалось запросить права администратора: {ex.Message}\n" +
                                    "Программа продолжит работу в ограниченном режиме.",
                                    "Права не получены", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            // Если пользователь нажал "Отмена" или перезапуск не удался -
            // продолжаем работу без прав (часть функций будет ограничена).
        }

        // ---------- Шаг 2. Загружаем настройки, язык и тему ----------
        try
        {
            НастройкиГлавная = SettingsManager.Загрузить();
            Loc.УстановитьЯзык(НастройкиГлавная.Язык);      // язык интерфейса
            SettingsManager.ПрименитьТему(НастройкиГлавная.Тема); // цвета темы
        }
        catch (Exception ex)
        {
            // Если файл настроек повреждён - работаем с заводскими значениями
            НастройкиГлавная = Models.AppSettings.Заводские();
            System.Diagnostics.Debug.WriteLine($"Ошибка загрузки настроек: {ex.Message}");
        }

        base.OnStartup(e);
    }

    /// <summary>Глобальный доступ к текущим настройкам приложения из любой страницы.</summary>
    public static Models.AppSettings НастройкиГлавная { get; internal set; } = new();

    /// <summary>Проверяет, входит ли текущий пользователь в группу администраторов.</summary>
    public static bool ЗапущенКакАдминистратор()
    {
        try
        {
            using var идентификация = WindowsIdentity.GetCurrent();
            var принципал = new WindowsPrincipal(идентификация);
            // IsInRole проверяет наличие elevated-токена (администратора)
            return принципал.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
