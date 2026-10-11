using System.Security.Principal;
using System.Windows;
using ShikdikClean.Helpers;

namespace ShikdikClean;

/// <summary>
/// Точка входа приложения. Здесь мы:
/// 1) Проверяем, что программа запущена с правами администратора
///    (права также требуются самим манифестом app.manifest - это двойная проверка).
/// 2) Загружаем сохранённые настройки, применяем язык и тему перед созданием окна.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // ---------- Шаг 1. Проверка прав администратора ----------
        if (!ЗапущенКакАдминистратор())
        {
            // Обычно сюда попасть невозможно (манифест requireAdministrator не пустит),
            // но если пользователь обошёл UAC нестандартным способом - предупреждаем.
            MessageBox.Show(
                "Shikdik Clean требует прав администратора для очистки системных папок.\n" +
                "Пожалуйста, запустите программу от имени администратора.",
                "Требуется администратор",
                MessageBoxButton.OK,
                MessageBoxImage.Question);
            // Не прерываем работу: часть функций (TEMP пользователя) всё равно будет работать.
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
