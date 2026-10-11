using System.Diagnostics;
using System.IO;
using System.Collections;      // необобщённый IEnumerable (обход логического дерева)
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents; // Hyperlink (ссылка на GitHub на странице "О программе")
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ShikdikClean.Helpers;
using ShikdikClean.Models;

namespace ShikdikClean;

/// <summary>
/// Логика главного окна: переключение разделов и все обработчики событий.
/// Разделы - это просто Grid'ы, которые показываются/скрываются (Visibility),
/// выбор раздела управляется RadioButton'ами в боковой панели.
/// </summary>
public partial class MainWindow : Window
{
    // ---------- Помощники (классы из папки Helpers) ----------
    private readonly SystemCleaner _очистильщик = new();
    private readonly ProgramUninstaller _деинсталлятор = new();
    private readonly PhotoManager _фотоМенеджер = new();

    // ---------- Данные страниц ----------
    /// <summary>Категории очистки с чекбоксами (привязаны к ItemsControl).</summary>
    private readonly System.Collections.ObjectModel.ObservableCollection<CleanCategory> Категории = new();

    /// <summary>Все программы без фильтра (для поиска держим отдельно).</summary>
    private List<ProgramInfo> _всеПрограммы = new();

    /// <summary>Текущая просматриваемая папка с фото.</summary>
    private string _текущаяПапкаФото = PhotoManager.ПапкаПоУмолчанию;

    /// <summary>Ссылка на загруженные настройки (короткое имя).</summary>
    private AppSettings Настройки => App.НастройкиГлавная;

    /// <summary>Флаг: события Checked игнорируем во время начальной загрузки.</summary>
    private bool _настройкиГотовы;

    public MainWindow()
    {
        InitializeComponent();

        // Строим список категорий очистки (чекбоксы генерируются из коллекции)
        foreach (string код in new[] { "TempUser", "TempSystem", "Prefetch", "ShaderCache", "RecycleBin", "Browsers", "Downloads" })
        {
            Категории.Add(new CleanCategory { Код = код, Название = Loc.НазваниеКатегории(код) });
        }
        СписокКатегорий.ItemsSource = Категории;

        // Статус администратора в сайдбаре
        ТекстАдминСтатус.Text = App.ЗапущенКакАдминистратор()
            ? "✔ Права администратора получены"
            : "⚠ Запустите программу от имени администратора!";
        ТекстАдминСтатус.Foreground = App.ЗапущенКакАдминистратор()
            ? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))   // зелёный
            : new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));  // оранжевый

        // Восстанавливаем значения настроек в контролах страницы "Настройки"
        ЗагрузитьНастройкиВИнтерфейс();
        _настройкиГотовы = true; // теперь события переключателей можно обрабатывать

        // Применяем перевод интерфейса ко всем элементам (Tag = ключ словаря Loc)
        Loaded += (_, _) => ПеревестиВесьИнтерфейс();

        // Фото сканируем сразу при запуске (асинхронно, UI не блокируется).
        // ContentRendered = момент, когда окно уже отрисовано и визуальное дерево построено.
        ContentRendered += async (_, _) => await ЗагрузитьФотоAsync(_текущаяПапкаФото);
    }

    // =====================================================================
    // ЛОКАЛИЗАЦИЯ
    // Каждый локализуемый элемент в XAML помечен Tag="Ключ".
    // Рекурсивный обход дерева подставляет текст из словаря Loc по этому ключу.
    // =====================================================================

    /// <summary>Обходит ОДНО дерево (visual или logical) и переводит элементы по Tag.</summary>
    private void ApplyLocalization(DependencyObject корень, Func<DependencyObject, IEnumerable>? детиФункция)
    {
        // Hyperlink НЕ является FrameworkElement (это ContentElement), проверяем его отдельно
        if (корень is Hyperlink ссылкаГлобальная)
        {
            // У Hyperlink нет свойства Tag, поэтому текст берём из словаря по ключу "GitHubLink"
            // (ссылка на GitHub у нас одна).
            SetHyperlinkText(ссылкаГлобальная, Loc.Get("GitHubLink"));
        }

        if (корень is FrameworkElement fe)
        {
            if (fe.Tag is string ключ && ключ.Length > 0)
            {
                switch (fe)
                {
                    case TextBlock tb: tb.Text = Loc.Get(ключ); break;
                    case Button btn: btn.Content = Loc.Get(ключ); break;
                    case CheckBox cb: cb.Content = Loc.Get(ключ); break;
                    case Expander exp: exp.Header = Loc.Get(ключ); break;
                    // Навигационные RadioButton'ы тоже переводим (у них GroupName="Разделы")
                    case RadioButton rb: rb.Content = Loc.Get(ключ); break;
                    // Placeholder поля поиска показываем прямо текстом в TextBox
                    case TextBox txt when ключ == "SearchPlaceholder":
                        if (txt.Text.Length == 0 || txt.Text == Loc.Get("SearchPlaceholder"))
                            txt.Text = Loc.Get(ключ);
                        break;
                }
            }
        }

        // Параметр может быть null (например, для логического дерева у некоторых элементов) - защищаемся
        if (детиФункция == null) return;
        foreach (object ребёнок in детиФункция(корень))
            if (ребёнок is DependencyObject dep)
                ApplyLocalization(dep, детиФункция);
    }

    /// <summary>Визуальное дерево (видно только отрендеренным элементам).</summary>
    private static IEnumerable VisualChildren(DependencyObject d)
    {
        int count = VisualTreeHelper.GetChildrenCount(d);
        var list = new List<DependencyObject>(count);
        for (int i = 0; i < count; i++) list.Add(VisualTreeHelper.GetChild(d, i));
        return list;
    }

    /// <summary>Логическое дерево (доступно всегда, даже для свёрнутых вкладок).</summary>
    private static IEnumerable DetniLogicheskogoDereva(DependencyObject d) => LogicalTreeHelper.GetChildren(d);

    /// <summary>Меняет текст внутри Hyperlink (у него вместо Content - Inlines).</summary>
    private static void SetHyperlinkText(Hyperlink ссылка, string текст)
    {
        ссылка.Inlines.Clear();
        ссылка.Inlines.Add(new System.Windows.Documents.Run(текст));
    }

    /// <summary>Полный перевод интерфейса (вызывается при запуске и смене языка).</summary>
    private void ПеревестиВесьИнтерфейс()
    {
        // Скрытые разделы (Visibility=Collapsed) отсутствуют в visual tree,
        // поэтому сначала проходим по ЛОГИЧЕСКОМУ дереву всего окна...
        ApplyLocalization(this, DetniLogicheskogoDereva);
        // ...а затем по видимой части визуального дерева (шаблоны ItemsControl)
        ApplyLocalization(this, VisualChildren);

        // Колонки DataGrid живут вне обоих деревьев - обновляем их напрямую
        string[] ключиКолонок = { "ColName", "ColPublisher", "ColVersion", "ColDate", "ColSize" };
        var колонки = ТаблицаПрограмм.Columns;
        for (int i = 0; i < колонки.Count && i < ключиКолонок.Length; i++)
            колонки[i].Header = Loc.Get(ключиКолонок[i]);

        // Подписи категорий очистки (модели) и заголовок Expander'а загрузок
        foreach (var к in Категории) к.Название = Loc.НазваниеКатегории(к.Код);

        // Элементы, которые могут отсутствовать в обоих деревьях (колонки до рендера, шаблоны),
        // переводим напрямую по именам:
        КнопкаСканировать.Content = Loc.Get("ScanButton");
        КнопкаОчистить.Content = Loc.Get("CleanButton");
        КнопкаВыбратьВсеКатегории.Content = Loc.Get("SelectAllCats");
        РаскрышкаЗагрузки.Header = Loc.Get("DownloadsList");
        КнопкаОбновитьСписок.Content = Loc.Get("RefreshList");
        КнопкаУдалитьПрограмму.Content = Loc.Get("UninstallButton");
        КнопкаВыбратьВсеФото.Content = Loc.Get("SelectAll");
        КнопкаСнятьВыделение.Content = Loc.Get("DeselectAll");
        КнопкаУдалитьФото.Content = Loc.Get("DeleteSelected");
        КнопкаОбновитьФото.Content = Loc.Get("Update");
        КнопкаВыбратьПапку.Content = Loc.Get("ChooseFolder");
        КнопкаСохранить.Content = Loc.Get("SaveSettings");
        КнопкаСбросить.Content = Loc.Get("ResetSettings");
        КнопкаОбновления.Content = Loc.Get("CheckUpdates");
        НастройкаАвтозапуск.Content = Loc.Get("StartWithWindows");
        НастройкаПодтверждения.Content = Loc.Get("ShowConfirmations");
        НастройкаТочкаВосстановления.Content = Loc.Get("CreateRestorePoint");
        НастройкаОчисткаКорзины.Content = Loc.Get("EmptyRecycleAuto");
        ТекстМинРазмер.Text = Loc.Get("MinDownloadSize");
        ЯзыкRu.Content = Loc.Get("LangRu");
        ЯзыкEn.Content = Loc.Get("LangEn");
        ЯзыкTr.Content = Loc.Get("LangTr");
        ТемаDark.Content = Loc.Get("ThemeDark");
        ТемаLight.Content = Loc.Get("ThemeLight");
        ТемаBlue.Content = Loc.Get("ThemeBlue");
        НавигацияОчистка.Content = Loc.Get("NavClean");
        НавигацияПрограммы.Content = Loc.Get("NavPrograms");
        НавигацияФото.Content = Loc.Get("NavPhotos");
        НавигацияНастройки.Content = Loc.Get("NavSettings");
        НавигацияОПрограмме.Content = Loc.Get("NavAbout");
        ПредпросмотрПодсказка.Text = Loc.Get("PreviewHint");
        ТекстНетФото.Text = Loc.Get("NoPhotos");
        SetHyperlinkText(СсылкаGitHub, Loc.Get("GitHubLink"));
        // Placeholder поля поиска подставляем, только если пользователь ещё ничего не ввёл
        if (ПоискПрограмм.Text.Length == 0 || ЯвляетсяPlaceholder(ПоискПрограмм.Text))
            ПоискПрограмм.Text = Loc.Get("SearchPlaceholder");
    }

    /// <summary>Проверяет, является ли текст поля именно переводом-подсказкой (placeholder).</summary>
    private bool ЯвляетсяPlaceholder(string текст) =>
        текст == Loc.Get("SearchPlaceholder");

    // =====================================================================
    // ПЕРЕКЛЮЧЕНИЕ РАЗДЕЛОВ (навигация)
    // =====================================================================
    private void ПереключитьРаздел(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return; // во время InitializeComponent игнорируем

        // Прячем все разделы...
        РазделОчистка.Visibility = Visibility.Collapsed;
        РазделПрограммы.Visibility = Visibility.Collapsed;
        РазделФото.Visibility = Visibility.Collapsed;
        РазделНастройки.Visibility = Visibility.Collapsed;
        РазделОПрограмме.Visibility = Visibility.Collapsed;

        // ...и показываем выбранный
        if (sender == НавигацияОчистка) РазделОчистка.Visibility = Visibility.Visible;
        else if (sender == НавигацияПрограммы)
        {
            РазделПрограммы.Visibility = Visibility.Visible;
            // При первом открытии автоматически грузим список программ
            if (_всеПрограммы.Count == 0) _ = ОбновитьСписокПрограммAsync();
        }
        else if (sender == НавигацияФото) РазделФото.Visibility = Visibility.Visible;
        else if (sender == НавигацияНастройки) РазделНастройки.Visibility = Visibility.Visible;
        else if (sender == НавигацияОПрограмме) РазделОПрограмме.Visibility = Visibility.Visible;
    }

    // =====================================================================
    // РАЗДЕЛ 1: ОЧИСТКА СИСТЕМЫ
    // =====================================================================

    /// <summary>Возвращает путь категории очистки (null - для Корзины и браузеров).</summary>
    private static string? ПутьКатегории(string код) => код switch
    {
        "TempUser" => SystemCleaner.ПутьTempПользователя,
        "TempSystem" => SystemCleaner.ПутьTempСистемы,
        "Prefetch" => SystemCleaner.ПутьPrefetch,
        "ShaderCache" => SystemCleaner.ПутьShaderCache,
        "Downloads" => SystemCleaner.ПутьЗагрузки,
        _ => null,
    };

    /// <summary>Кнопка «Сканировать»: считает размер мусора по ОТМЕЧЕННЫМ категориям.</summary>
    private async void Сканировать_Click(object sender, RoutedEventArgs e)
    {
        КнопкаСканировать.IsEnabled = false;
        КнопкаОчистить.IsEnabled = false;
        ПрогрессОчистки.Visibility = Visibility.Visible;
        ПрогрессОчистки.Value = 0;
        ТекстПрогресса.Text = Loc.Get("Scanning");
        ТекстРезультатаОчистки.Text = "";
        _очистильщик.ПропущеноФайлов = 0;

        long всегоБайт = 0;
        try
        {
            // Если ничего не отмечено - отмечаем всё, чтобы кнопка была полезной
            if (!Категории.Any(c => c.Отмечено))
                foreach (var к in Категории) к.Отмечено = true;

            // Сбрасываем старые результаты
            foreach (var к in Категории) { к.Найден = false; к.РазмерБайт = 0; к.КоличествоФайлов = 0; }

            var активные = Категории.Where(c => c.Отмечено).ToList();
            int обработано = 0;

            foreach (var к in активные)
            {
                switch (к.Код)
                {
                    case "RecycleBin":
                        // Точный размер корзины без COM-интероп не получить - просто помечаем категорию
                        к.Найден = true;
                        break;

                    case "Browsers":
                    {
                        // Несколько папок браузеров - суммируем все
                        int кол = 0; long байт = 0;
                        foreach (string путь in SystemCleaner.ПутиКэшБраузеров)
                        {
                            var r = await _очистильщик.СканироватьПапкуAsync(путь);
                            кол += r.файлов; байт += r.байт;
                        }
                        к.КоличествоФайлов = кол; к.РазмерБайт = байт; к.Найден = true;
                        break;
                    }

                    case "Downloads":
                    {
                        // Показываем список больших файлов (> порога из настроек)
                        long порог = (long)Настройки.МинимальныйРазмерФайлаЗагрузокМБ * 1024 * 1024;
                        var файлы = await _очистильщик.ПолучитьБольшиеФайлыЗагрузокAsync(порог);
                        СписокБольшихФайлов.ItemsSource = файлы;
                        к.КоличествоФайлов = файлы.Count;
                        к.РазмерБайт = файлы.Sum(f => f.РазмерБайт);
                        к.Найден = true;
                        РаскрышкаЗагрузки.IsExpanded = файлы.Count > 0;
                        break;
                    }

                    default:
                    {
                        var r = await _очистильщик.СканироватьПапкуAsync(ПутьКатегории(к.Код)!);
                        к.КоличествоФайлов = r.файлов; к.РазмерБайт = r.байт; к.Найден = true;
                        break;
                    }
                }

                всегоБайт += к.РазмерБайт;
                обработано++;
                ПрогрессОчистки.Value = 100.0 * обработано / активные.Count;
                // Отдаём управление UI-потоку, чтобы прогресс-бар успевал перерисоваться
                await Dispatcher.Yield(DispatcherPriority.Background);
            }

            ТекстПрогресса.Text = string.Format(Loc.Get("TotalFound"), Utils.ФорматироватьРазмер(всегоБайт));
        }
        catch (Exception ex)
        {
            ПоказатьОшибку(ex);
        }
        finally
        {
            КнопкаСканировать.IsEnabled = true;
            КнопкаОчистить.IsEnabled = true;
            ПрогрессОчистки.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>Кнопка «Очистить»: удаляет найденный мусор (с подтверждением).</summary>
    private async void Очистить_Click(object sender, RoutedEventArgs e)
    {
        if (Категории.All(c => !c.Отмечено))
        {
            ПоказатьПредупреждение(Loc.Get("NothingSelected"));
            return;
        }

        var активные = Категории.Where(c => c.Отмечено && c.Найден).ToList();
        if (активные.Count == 0)
        {
            ПоказатьПредупреждение(Loc.Get("NothingToClean"));
            return;
        }

        long планировалось = активные.Sum(a => a.РазмерБайт);

        // ПОДТВЕРЖДЕНИЕ перед удалением (если включено в настройках)
        if (Настройки.ПоказыватьПодтверждения)
        {
            var ответ = MessageBox.Show(
                string.Format(Loc.Get("ConfirmClean"), Utils.ФорматироватьРазмер(планировалось)),
                Loc.Get("Question"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ответ != MessageBoxResult.Yes) return;
        }

        КнопкаСканировать.IsEnabled = false;
        КнопкаОчистить.IsEnabled = false;
        ПрогрессОчистки.Visibility = Visibility.Visible;
        ПрогрессОчистки.Value = 0;
        ТекстПрогресса.Text = Loc.Get("Cleaning");

        long освобождено = 0;
        try
        {
            // Заглушка точки восстановления (по заданию функционал можно не реализовывать)
            if (Настройки.СоздаватьТочкуВосстановления)
                System.Diagnostics.Debug.WriteLine("Точка восстановления: функция-заглушка.");

            int шаг = 0;
            foreach (var к in активные)
            {
                switch (к.Код)
                {
                    case "RecycleBin":
                        await _очистильщик.ОчиститьКорзинуAsync();
                        break;

                    case "Browsers":
                        foreach (string путь in SystemCleaner.ПутиКэшБраузеров)
                        {
                            var r = await _очистильщик.ОчиститьПапкуAsync(путь);
                            освобождено += r.байт;
                        }
                        break;

                    case "Downloads":
                        // Удаляем ТОЛЬКО большие файлы из списка, а всю папку - нет!
                        if (СписокБольшихФайлов.ItemsSource is List<BigFile> большие)
                        {
                            foreach (var ф in большие.ToList())
                            {
                                try
                                {
                                    var info = new FileInfo(ф.ПутьКФайлу);
                                    if (info.Exists) { long s = info.Length; info.Delete(); освобождено += s; }
                                }
                                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException) { }
                            }
                            большие.Clear();
                        }
                        break;

                    default:
                    {
                        var r = await _очистильщик.ОчиститьПапкуAsync(ПутьКатегории(к.Код)!);
                        освобождено += r.байт;
                        break;
                    }
                }

                к.Найден = false; к.РазмерБайт = 0; к.КоличествоФайлов = 0;
                шаг++;
                ПрогрессОчистки.Value = 100.0 * шаг / активные.Count;
                await Dispatcher.Yield(DispatcherPriority.Background);
            }

            // Опция "очищать корзину автоматически" (если корзина не была в списке категорий)
            if (Настройки.ОчищатьКорзинуАвтоматически && !активные.Any(a => a.Код == "RecycleBin"))
                await _очистильщик.ОчиститьКорзинуAsync();

            // Итоговая надпись "Освобождено: X MB/GB"
            string итог = string.Format(Loc.Get("FreedSpace"), Utils.ФорматироватьРазмер(освобождено));
            ТекстРезультатаОчистки.Text = итог;
            ТекстПрогресса.Text = Loc.Get("CleanDone");

            // Предупреждаем, если часть файлов пришлось пропустить (нет прав / заняты браузером)
            if (_очистильщик.ПропущеноФайлов > 0)
                ПоказатьПредупреждение(Loc.Get("NeedAdmin"));
        }
        catch (Exception ex)
        {
            ПоказатьОшибку(ex);
        }
        finally
        {
            КнопкаСканировать.IsEnabled = true;
            КнопкаОчистить.IsEnabled = true;
            ПрогрессОчистки.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>Кнопка «Выбрать все категории» (повторное нажатие снимает выделение).</summary>
    private void ВыбратьВсеКатегории_Click(object sender, RoutedEventArgs e)
    {
        bool всеОтмечены = Категории.All(c => c.Отмечено);
        foreach (var к in Категории) к.Отмечено = !всеОтмечены;
    }

    // =====================================================================
    // РАЗДЕЛ 2: УДАЛЕНИЕ ПРОГРАММ
    // =====================================================================

    /// <summary>Кнопка «Обновить список».</summary>
    private async void ОбновитьСписок_Click(object sender, RoutedEventArgs e) =>
        await ОбновитьСписокПрограммAsync();

    private async Task ОбновитьСписокПрограммAsync()
    {
        КнопкаОбновитьСписок.IsEnabled = false;
        ТекстСтатусПрограмм.Text = Loc.Get("LoadingPrograms");
        try
        {
            _всеПрограммы = await _деинсталлятор.ПолучитьПрограммыAsync();
            ПрименитьФильтрПрограмм();
            ТекстСтатусПрограмм.Text = string.Format(Loc.Get("ProgramsLoaded"), _всеПрограммы.Count);
        }
        catch (Exception ex)
        {
            ПоказатьОшибку(ex);
        }
        finally
        {
            КнопкаОбновитьСписок.IsEnabled = true;
        }
    }

    /// <summary>Поле поиска: фильтрация таблицы в реальном времени.</summary>
    private void ПоискПрограмм_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Пока пользователь не начал печатать (placeholder) - фильтр пустой
        string текст = ПоискПрограмм.Text;
        if (текст == Loc.Get("SearchPlaceholder")) текст = "";
        ПрименитьФильтрПрограмм(текст);
    }

    private void ПрименитьФильтрПрограмм(string фильтр = "")
    {
        var view = new System.Windows.Data.ListCollectionView(_всеПрограммы);
        // Фильтр: программа попадает в таблицу, если её данные содержат строку поиска
        view.Filter = obj => obj is ProgramInfo п && п.ПодходитПодФильтр(фильтр);
        ТаблицаПрограмм.ItemsSource = view;
    }

    /// <summary>Кнопка «Удалить»: запускает деинсталлятор выбранной программы.</summary>
    private void УдалитьПрограмму_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ТаблицаПрограмм.SelectedItem is not ProgramInfo программа)
            {
                ПоказатьПредупреждение(Loc.Get("SelectProgramFirst"));
                return;
            }

            // Подтверждение удаления
            if (Настройки.ПоказыватьПодтверждения)
            {
                var ответ = MessageBox.Show(
                    string.Format(Loc.Get("ConfirmUninstall"), программа.Имя),
                    Loc.Get("Question"), MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (ответ != MessageBoxResult.Yes) return;
            }

            string? ошибка = _деинсталлятор.УдалитьПрограмму(программа);
            if (ошибка is not null) ПоказатьОшибкуТекст(ошибка);
            else ПоказатьИнформацию(Loc.Get("Info"), Loc.Get("UninstallStarted"));
        }
        catch (Exception ex)
        {
            ПоказатьОшибку(ex);
        }
    }

    // =====================================================================
    // РАЗДЕЛ 3: УПРАВЛЕНИЕ ФОТО
    // =====================================================================

    /// <summary>Асинхронно сканирует папку и заполняет сетку миниатюр.</summary>
    private async Task ЗагрузитьФотоAsync(string папка)
    {
        СеткаФото.ItemsSource = null;
        ТекстНетФото.Visibility = Visibility.Collapsed;
        try
        {
            var фото = await _фотоМенеджер.СканироватьПапкуAsync(папка);
            _текущаяПапкаФото = папка;

            if (фото.Count == 0)
            {
                ТекстНетФото.Visibility = Visibility.Visible;
                return;
            }

            СеткаФото.ItemsSource = фото;

            // Миниатюры грузим ПО ЧАСТЯМИ через очередь Dispatcher (Background priority):
            // интерфейс остаётся отзывчивым, а картинки появляются постепенно.
            foreach (var ф in фото)
            {
                _ = Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        // Разрешение читается быстро: DelayCreation декодирует только заголовок файла
                        PhotoManager.ЗаполнитьИнформациюОРазмере(ф);
                        ф.Миниатюра = PhotoManager.СоздатьМиниатюру(ф.ПутьКФайлу);
                    }
                    catch (Exception ex)
                    {
                        // Повреждённый файл - оставляем заглушку, не падаем
                        System.Diagnostics.Debug.WriteLine($"Миниатюра не загружена: {ex.Message}");
                    }
                }, DispatcherPriority.Background);
            }
        }
        catch (Exception ex)
        {
            ПоказатьОшибку(ex);
        }
    }

    /// <summary>Клик по карточке фото -> предпросмотр полного изображения справа.</summary>
    private void СеткаFoto_PreviewLeftDown(object sender, MouseButtonEventArgs e)
    {
        // Ищем FrameworkElement с DataContext = PhotoInfo над точкой клика
        DependencyObject? элемент = e.OriginalSource as DependencyObject;
        while (элемент is not null)
        {
            if (элемент is FrameworkElement fe && fe.DataContext is PhotoInfo фото)
            {
                ПоказатьПредпросмотр(фото);
                return; // нашлись - выходим
            }
            // Поднимаемся вверх: сначала по визуальному дереву, потом по логическому
            элемент = VisualTreeHelper.GetParent(элемент) ?? LogicalTreeHelper.GetParent(элемент);
        }
    }

    private void ПоказатьПредпросмотр(PhotoInfo фото)
    {
        try
        {
            // Заодно запоминаем Tag-ключи, чтобы при смене языка текст обновился автоматически
            ПредпросмотрФайл.Tag = "FileName";
            ПредпросмотрРазмер.Tag = "FileSize";
            ПредпросмотрРазрешение.Tag = "FileResolution";
            ПредпросмотрДата.Tag = "FileDate";
            ПредпросмотрПапка.Tag = "FolderPath";
            ПредпросмотрФайл.Text = string.Format(Loc.Get("FileName"), фото.ИмяФайла);
            ПредпросмотрРазмер.Text = string.Format(Loc.Get("FileSize"), фото.РазмерЧеловек);
            ПредпросмотрРазрешение.Text = string.Format(Loc.Get("FileResolution"), фото.Разрешение);
            ПредпросмотрДата.Text = string.Format(Loc.Get("FileDate"), фото.ДатаИзменения.ToString("dd.MM.yyyy HH:mm"));
            ПредпросмотрПапка.Text = string.Format(Loc.Get("FolderPath"), фото.Папка);
            ПредпросмотрПодсказка.Visibility = Visibility.Collapsed;
            КартинкаПредпросмотра.Source = PhotoManager.ЗагрузитьПолноеИзображение(фото.ПутьКФайлу);
        }
        catch (Exception ex)
        {
            КартинкаПредпросмотра.Source = null;
            ПоказатьОшибку(ex);
        }
    }

    private void ВыбратьВсеФото_Click(object sender, RoutedEventArgs e)
    {
        if (СеткаФото.ItemsSource is IEnumerable<PhotoInfo> фото)
            foreach (var ф in фото) ф.Выбрано = true;
    }

    private void СнятьВыделение_Click(object sender, RoutedEventArgs e)
    {
        if (СеткаФото.ItemsSource is IEnumerable<PhotoInfo> фото)
            foreach (var ф in фото) ф.Выбрано = false;
    }

    private async void ОбновитьФото_Click(object sender, RoutedEventArgs e) =>
        await ЗагрузитьФотоAsync(_текущаяПапкаФото);

    /// <summary>Кнопка «Выбрать папку» - диалог выбора другой папки с фото.</summary>
    private async void ВыбратьПапку_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // OpenFolderDialog - встроенный диалог выбора папки (.NET 8+ / WPF)
            var диалог = new Microsoft.Win32.OpenFolderDialog
            {
                Title = Loc.Get("ChooseFolder"),
                InitialDirectory = Directory.Exists(_текущаяПапкаФото) ? _текущаяПапкаФото : @"C:\",
            };
            if (диалог.ShowDialog(this) == true)
                await ЗагрузитьФотоAsync(диалог.FolderName);
        }
        catch (Exception ex)
        {
            ПоказатьОшибку(ex);
        }
    }

    /// <summary>«Удалить выбранные» - перемещает фото в КОРЗИНУ (не удаляет навсегда!).</summary>
    private async void УдалитьВыбранныеФото_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (СеткаФото.ItemsSource is not IEnumerable<PhotoInfo> все) return;
            var выбранные = все.Where(f => f.Выбрано).ToList();
            if (выбранные.Count == 0)
            {
                ПоказатьПредупреждение(Loc.Get("SelectProgramFirst"));
                return;
            }

            if (Настройки.ПоказыватьПодтверждения)
            {
                var ответ = MessageBox.Show(
                    string.Format(Loc.Get("ConfirmDeletePhotos"), выбранные.Count),
                    Loc.Get("Question"), MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (ответ != MessageBoxResult.Yes) return;
            }

            // Удаление в Корзину - медленная операция, выносим в фоновый поток
            int удалено = await Task.Run(() =>
            {
                int count = 0;
                foreach (var ф in выбранные)
                    if (_фотоМенеджер.УдалитьВКорзину(ф.ПутьКФайлу) is null) count++;
                return count;
            });

            ПоказатьИнформацию(Loc.Get("Info"), string.Format(Loc.Get("PhotosDeleted"), удалено));
            await ЗагрузитьФотоAsync(_текущаяПапкаФото); // обновляем сетку
        }
        catch (Exception ex)
        {
            ПоказатьОшибку(ex);
        }
    }

    // =====================================================================
    // РАЗДЕЛ 4: НАСТРОЙКИ
    // =====================================================================

    /// <summary>Переносит значения из объекта настроек в контролы страницы.</summary>
    private void ЗагрузитьНастройкиВИнтерфейс()
    {
        _настройкиГотовы = false; // пока ставим галочки - события игнорируем

        НастройкаАвтозапуск.IsChecked = Настройки.Автозапуск;
        НастройкаПодтверждения.IsChecked = Настройки.ПоказыватьПодтверждения;
        НастройкаТочкаВосстановления.IsChecked = Настройки.СоздаватьТочкуВосстановления;
        НастройкаОчисткаКорзины.IsChecked = Настройки.ОчищатьКорзинуАвтоматически;
        ПолеМинРазмер.Text = Настройки.МинимальныйРазмерФайлаЗагрузокМБ.ToString();

        switch (Настройки.Язык)
        {
            case "en": ЯзыкEn.IsChecked = true; break;
            case "tr": ЯзыкTr.IsChecked = true; break;
            default: ЯзыкRu.IsChecked = true; break;
        }
        switch (Настройки.Тема)
        {
            case "light": ТемаLight.IsChecked = true; break;
            case "blue": ТемаBlue.IsChecked = true; break;
            default: ТемаDark.IsChecked = true; break;
        }

        _настройкиГотовы = true;
    }

    /// <summary>Переключение языка происходит мгновенно (без перезапуска).</summary>
    private void Язык_Изменён(object sender, RoutedEventArgs e)
    {
        if (!_настройкиГотовы) return;
        string код = sender == ЯзыкEn ? "en" : sender == ЯзыкTr ? "tr" : "ru";
        Loc.УстановитьЯзык(код);
        ПеревестиВесьИнтерфейс();
    }

    /// <summary>Переключение темы применяется сразу (кисти DynamicResource обновляются).</summary>
    private void Тема_Изменена(object sender, RoutedEventArgs e)
    {
        if (!_настройкиГотовы) return;
        string код = sender == ТемаLight ? "light" : sender == ТемаBlue ? "blue" : "dark";
        SettingsManager.ПрименитьТему(код);
    }

    /// <summary>Разрешаем вводить в числовое поле только цифры (валидация ввода).</summary>
    private void ТолькоЦифры(object sender, TextCompositionEventArgs e)
    {
        // Если хотя бы один символ не цифра - блокируем ввод
        e.Handled = !e.Text.All(char.IsDigit);
    }

    /// <summary>Кнопка «Сохранить настройки»: валидация + запись в appsettings.json.</summary>
    private void СохранитьНастройки_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // --- Валидация числового поля: должно быть 1 <= мин < максимум ---
            const int максимумМБ = 10240; // верхняя граница порога = 10 ГБ
            bool естьЧисло = int.TryParse(ПолеМинРазмер.Text, out int мин);
            if (!естьЧисло || мин < 1)
            {
                ПоказатьОшибкуТекст(Loc.Get("InvalidNumber"));
                return;
            }
            // Проверка требования "min < max"
            if (мин >= максимумМБ)
            {
                ПоказатьОшибкуТекст(Loc.Get("RangeCheckError"));
                return;
            }

            // --- Собираем значения из контролов в объект настроек ---
            Настройки.ПоказыватьПодтверждения = НастройкаПодтверждения.IsChecked == true;
            Настройки.СоздаватьТочкуВосстановления = НастройкаТочкаВосстановления.IsChecked == true;
            Настройки.ОчищатьКорзинуАвтоматически = НастройкаОчисткаКорзины.IsChecked == true;
            Настройки.МинимальныйРазмерФайлаЗагрузокМБ = мин;
            Настройки.Язык = Loc.ТекущийЯзык;
            Настройки.Тема = ТемаLight.IsChecked == true ? "light"
                           : ТемаBlue.IsChecked == true ? "blue" : "dark";

            // --- Автозапуск: создаём или удаляем ярлык в папке Shell:Startup ---
            bool нуженАвтозапуск = НастройкаАвтозапуск.IsChecked == true;
            if (нуженАвтозапуск != Настройки.Автозапуск)
            {
                string? ошибкаАвто = SettingsManager.ПрименитьАвтозапуск(нуженАвтозапуск);
                if (ошибкаАвто is not null)
                {
                    ПоказатьОшибкуТекст(ошибкаАвто);
                    return; // не сохраняем, если автозапуск не применился
                }
                Настройки.Автозапуск = нуженАвтозапуск;
            }

            string? ошибка = SettingsManager.Сохранить(Настройки);
            if (ошибка is not null) ПоказатьОшибкуТекст(ошибка);
            else ПоказатьИнформацию(Loc.Get("Info"), Loc.Get("SettingsSaved"));
        }
        catch (Exception ex)
        {
            ПоказатьОшибку(ex);
        }
    }

    /// <summary>Кнопка «Сбросить к заводским».</summary>
    private void СброситьНастройки_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Настройки.ПоказыватьПодтверждения)
            {
                var ответ = MessageBox.Show(Loc.Get("SettingsReset") + "?",
                    Loc.Get("Warning"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (ответ != MessageBoxResult.Yes) return;
            }

            // Если был включён автозапуск - убираем ярлык
            if (Настройки.Автозапуск) SettingsManager.ПрименитьАвтозапуск(false);

            App.НастройкиГлавная = AppSettings.Заводские();
            Loc.УстановитьЯзык(Настройки.Язык);
            SettingsManager.ПрименитьТему(Настройки.Тема);
            ПеревестиВесьИнтерфейс();
            ЗагрузитьНастройкиВИнтерфейс();
            SettingsManager.Сохранить(Настройки);
            ПоказатьИнформацию(Loc.Get("Info"), Loc.Get("SettingsReset"));
        }
        catch (Exception ex)
        {
            ПоказатьОшибку(ex);
        }
    }

    // =====================================================================
    // РАЗДЕЛ 5: О ПРОГРАММЕ
    // =====================================================================

    private void GitHub_Click(object sender, RoutedEventArgs e)
    {
        // Заглушка: ссылка на GitHub. Открываем в браузере по умолчанию.
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/shikdik/clean",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            ПоказатьОшибкуТекст(string.Format(Loc.Get("ErrorOccurred"), ex.Message));
        }
    }

    private void ПроверитьОбновления_Click(object sender, RoutedEventArgs e)
    {
        // Заглушка проверки обновлений
        ПоказатьИнформацию(Loc.Get("Info"), Loc.Get("NoUpdates"));
    }

    // =====================================================================
    // МЕЛКИЕ ПОМОЩНИКИ ДИАЛОГОВ (красивые MessageBox с иконками)
    // =====================================================================
    private void ПоказатьИнформацию(string заголовок, string текст) =>
        MessageBox.Show(текст, заголовок, MessageBoxButton.OK, MessageBoxImage.Information);

    private void ПоказатьПредупреждение(string текст) =>
        MessageBox.Show(текст, Loc.Get("Warning"), MessageBoxButton.OK, MessageBoxImage.Warning);

    private void ПоказатьОшибкуТекст(string текст) =>
        MessageBox.Show(текст, Loc.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);

    private void ПоказатьОшибку(Exception ex) =>
        MessageBox.Show(string.Format(Loc.Get("ErrorOccurred"), ex.Message),
                        Loc.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
}
