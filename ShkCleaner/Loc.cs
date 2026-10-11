namespace ShkCleaner;

/// <summary>
/// Простая система локализации: словарь ключ -> перевод для трёх языков.
/// Метод Apply(Window) проходит по всему дереву визуальных элементов и
/// заменяет текст в Tag (ключ локали), а также обновляет заголовки колонок,
/// подписи чекбоксов и т.д. Это позволяет переключать язык без ResourceDictionary.
/// </summary>
public static class Loc
{
    /// <summary>Текущий код языка: "ru", "en" или "tr".</summary>
    public static string ТекущийЯзык { get; private set; } = "ru";

    // Словарь переводов. Ключ - английское имя, значения по языкам.
    private static readonly Dictionary<string, (string ru, string en, string tr)> Переводы = new()
    {
        // ---- Навигация ----
        ["NavTitle"] = ("SHK CLEANER", "SHK CLEANER", "SHK CLEANER"),
        ["NavClean"] = ("🧹 Очистка системы", "🧹 System Cleanup", "🧹 Sistem Temizliği"),
        ["NavPrograms"] = ("🗑️ Удаление программ", "🗑️ Uninstall Programs", "🗑️ Program Kaldır"),
        ["NavPhotos"] = ("🖼️ Управление фото", "🖼️ Photo Manager", "🖼️ Fotoğraf Yönetimi"),
        ["NavSettings"] = ("⚙️ Настройки", "⚙️ Settings", "⚙️ Ayarlar"),
        ["NavAbout"] = ("ℹ️ О программе", "ℹ️ About", "ℹ️ Hakkında"),

        // ---- Страница очистки ----
        ["CleanHeader"] = ("Очистка системы", "System Cleanup", "Sistem Temizliği"),
        ["CleanSubHeader"] = ("Найдите и удалите временные файлы, кэш и другой системный мусор.",
                             "Find and remove temporary files, cache and other system junk.",
                             "Geçici dosyaları, önbelleği ve diğer sistem çöpünü bulun ve silin."),
        ["ScanButton"] = ("Сканировать", "Scan", "Tara"),
        ["CleanButton"] = ("Очистить", "Clean", "Temizle"),
        ["SelectAllCats"] = ("Выбрать все категории", "Select all categories", "Tüm kategorileri seç"),
        ["WhatToClean"] = ("Что очищать:", "What to clean:", "Neyi temizle:"),
        ["DownloadsList"] = ("Большие файлы в папке «Загрузки» (>100 МБ):",
                             "Large files in Downloads (>100 MB):",
                             "İndirilenler klasöründeki büyük dosyalar (>100 MB):"),
        ["ScanHint"] = ("Нажмите «Сканировать», чтобы рассчитать размер", "Press \"Scan\" to calculate size", "\"Tara\" ile boyutu hesaplayın"),
        ["FoundFiles"] = ("Найдено файлов: {0} • Размер: {1}", "Files found: {0} • Size: {1}", "Bulunan dosya: {0} • Boyut: {1}"),
        ["TotalFound"] = ("Всего можно освободить: {0}", "Total space to free: {0}", "Toplam boşaltılabilir: {0}"),
        ["FreedSpace"] = ("Освобождено: {0}", "Freed: {0}", "Boşaltıldı: {0}"),
        ["Scanning"] = ("Идёт сканирование…", "Scanning…", "Taranıyor…"),
        ["Cleaning"] = ("Идёт очистка…", "Cleaning…", "Temizleniyor…"),
        ["ScanDone"] = ("Сканирование завершено", "Scan complete", "Tarama tamamlandı"),
        ["CleanDone"] = ("Очистка завершена", "Cleaning complete", "Temizlik tamamlandı"),
        ["NothingSelected"] = ("Не выбрано ни одной категории для очистки.", "No cleanup category selected.", "Temizlik kategorisi seçilmedi."),
        ["ConfirmClean"] = ("Вы уверены, что хотите удалить найденные временные файлы ({0})?\nФайлы будут удалены безвозвратно.",
                            "Are you sure you want to delete the found temporary files ({0})?\nThey will be deleted permanently.",
                            "Bulunan geçici dosyaları ({0}) silmek istediğinize emin misiniz?\nKalıcı olarak silinecek."),
        ["NeedAdmin"] = ("Для полной очистки требуются права администратора.\nНекоторые файлы были пропущены (Access Denied).",
                         "Administrator rights are required for full cleanup.\nSome files were skipped (Access Denied).",
                         "Tam temizlik için yönetici hakları gerekir.\nBazı dosyalar atlandı (Erişim Reddedildi)."),
        ["NothingToClean"] = ("Нечего очищать — сначала выполните сканирование.", "Nothing to clean — run a scan first.", "Temizlenecek bir şey yok — önce tarama yapın."),

        // Категории очистки
        ["CatTempUser"] = ("Временные файлы пользователя (%TEMP%)", "User temporary files (%TEMP%)", "Kullanıcı geçici dosyaları (%TEMP%)"),
        ["CatTempSystem"] = ("Временные файлы системы (C:\\Windows\\Temp)", "System temp files (C:\\Windows\\Temp)", "Sistem geçici dosyaları (C:\\Windows\\Temp)"),
        ["CatPrefetch"] = ("Prefetch (C:\\Windows\\Prefetch)", "Prefetch (C:\\Windows\\Prefetch)", "Prefetch (C:\\Windows\\Prefetch)"),
        ["CatShaderCache"] = ("Кэш DirectX Shader Cache", "DirectX Shader Cache", "DirectX Shader Önbelleği"),
        ["CatRecycleBin"] = ("Корзина", "Recycle Bin", "Geri Dönüşüm Kutusu"),
        ["CatBrowsers"] = ("Кэш браузеров (Chrome, Edge, Firefox)", "Browser cache (Chrome, Edge, Firefox)", "Tarayıcı önbelleği (Chrome, Edge, Firefox)"),
        ["CatDownloads"] = ("Загрузки (файлы больше 100 МБ)", "Downloads (files over 100 MB)", "İndirilenler (100 MB üzeri dosyalar)"),

        // ---- Страница программ ----
        ["ProgramsHeader"] = ("Удаление программ", "Uninstall Programs", "Program Kaldırma"),
        ["ProgramsSubHeader"] = ("Все установленные программы из реестра Windows.", "All programs installed from the Windows registry.", "Windows kayıt defterindeki tüm programlar."),
        ["SearchPlaceholder"] = ("🔍 Поиск программы…", "🔍 Search programs…", "🔍 Program ara…"),
        ["RefreshList"] = ("Обновить список", "Refresh list", "Listeyi yenile"),
        ["UninstallButton"] = ("Удалить", "Uninstall", "Kaldır"),
        ["ColName"] = ("Название программы", "Program name", "Program adı"),
        ["ColPublisher"] = ("Издатель", "Publisher", "Yayıncı"),
        ["ColVersion"] = ("Версия", "Version", "Sürüm"),
        ["ColDate"] = ("Дата установки", "Install date", "Kurulum tarihi"),
        ["ColSize"] = ("Размер", "Size", "Boyut"),
        ["LoadingPrograms"] = ("Загрузка списка программ…", "Loading program list…", "Program listesi yükleniyor…"),
        ["ProgramsLoaded"] = ("Найдено программ: {0}", "Programs found: {0}", "{0} program bulundu"),
        ["ConfirmUninstall"] = ("Вы уверены, что хотите удалить [{0}]?", "Are you sure you want to uninstall [{0}]?", "[{0}] kaldırmak istediğinize emin misiniz?"),
        ["SelectProgramFirst"] = ("Сначала выберите программу из списка.", "Select a program from the list first.", "Önce listeden bir program seçin."),
        ["NoUninstallString"] = ("Для этой программы не найдена команда удаления в реестре.", "No uninstall command found in the registry for this program.", "Bu program için kayıt defterinde kaldırma komutu bulunamadı."),
        ["UninstallStarted"] = ("Деинсталлятор программы запущен.", "The program's uninstaller has been started.", "Programın kaldırıcısı başlatıldı."),

        // ---- Страница фото ----
        ["PhotosHeader"] = ("Управление фото", "Photo Manager", "Fotoğraf Yönetimi"),
        ["PhotosSubHeader"] = ("Просмотр и удаление фотографий (удаление — в Корзину).", "Browse and delete photos (deleted to Recycle Bin).", "Fotoğraflara göz atın ve silin (Geri Dönüşüm Kutusuna)."),
        ["SelectAll"] = ("Выбрать все", "Select all", "Tümünü seç"),
        ["DeselectAll"] = ("Снять выделение", "Deselect all", "Seçimi kaldır"),
        ["DeleteSelected"] = ("Удалить выбранные", "Delete selected", "Seçilenleri sil"),
        ["Update"] = ("Обновить", "Refresh", "Yenile"),
        ["ChooseFolder"] = ("Выбрать папку", "Choose folder", "Klasör seç"),
        ["PhotosLoaded"] = ("Найдено фото: {0}", "Photos found: {0}", "{0} fotoğraf bulundu"),
        ["NoPhotos"] = ("Изображения не найдены. Поддерживаются JPG, PNG, WEBP, BMP, GIF.", "No images found. Supported: JPG, PNG, WEBP, BMP, GIF.", "Görüntü bulunamadı. Desteklenen: JPG, PNG, WEBP, BMP, GIF."),
        ["ConfirmDeletePhotos"] = ("Вы уверены, что хотите удалить выбранные фото: {0} шт.?\nОни будут перемещены в Корзину.",
                                   "Are you sure you want to delete {0} selected photo(s)?\nThey will be moved to the Recycle Bin.",
                                   "{0} seçili fotoğrafı silmek istediğinize emin misiniz?\nGeri Dönüşüm Kutusuna taşınacak."),
        ["PhotosDeleted"] = ("Фото перемещено в Корзину: {0}", "Photos moved to Recycle Bin: {0}", "Geri Dönüşüm Kutusuna taşınan foto: {0}"),
        ["PreviewTitle"] = ("Предпросмотр", "Preview", "Önizleme"),
        ["PreviewHint"] = ("Кликните по фото, чтобы увидеть предпросмотр", "Click a photo to preview it", "Önizleme için bir fotoğrafa tıklayın"),
        ["FileName"] = ("Файл: {0}", "File: {0}", "Dosya: {0}"),
        ["FileSize"] = ("Размер: {0}", "Size: {0}", "Boyut: {0}"),
        ["FileResolution"] = ("Разрешение: {0}", "Resolution: {0}", "Çözünürlük: {0}"),
        ["FileDate"] = ("Изменён: {0}", "Modified: {0}", "Değiştirilme: {0}"),
        ["FolderPath"] = ("Папка: {0}", "Folder: {0}", "Klasör: {0}"),
        ["ScanningPhotos"] = ("Сканирование папки…", "Scanning folder…", "Klasör taranıyor…"),

        // ---- Страница настроек ----
        ["SettingsHeader"] = ("Настройки", "Settings", "Ayarlar"),
        ["SettingsSubHeader"] = ("Автозапуск, язык, тема и другие параметры.", "Startup, language, theme and more.", "Başlangıç, dil, tema ve diğer ayarlar."),
        ["GroupStartup"] = ("Автозапуск", "Startup", "Başlangıç"),
        ["StartWithWindows"] = ("Запускать вместе с Windows", "Run at Windows startup", "Windows ile birlikte başlat"),
        ["GroupLanguage"] = ("Язык интерфейса", "Interface language", "Arayüz dili"),
        ["LangRu"] = ("Русский", "Русский (Russian)", "Русский"),
        ["LangEn"] = ("English", "English", "İngilizce (English)"),
        ["LangTr"] = ("Türkçe", "Türkçe", "Türkçe"),
        ["GroupTheme"] = ("Тема оформления", "Color theme", "Renk teması"),
        ["ThemeDark"] = ("Тёмная тема", "Dark theme", "Koyu tema"),
        ["ThemeLight"] = ("Светлая тема", "Light theme", "Açık tema"),
        ["ThemeBlue"] = ("Синяя тема", "Blue theme", "Mavi tema"),
        ["GroupExtra"] = ("Дополнительные настройки", "Additional settings", "Ek ayarlar"),
        ["ShowConfirmations"] = ("Показывать подтверждения перед удалением", "Show confirmations before deleting", "Silmeden önce onay göster"),
        ["CreateRestorePoint"] = ("Автоматически создавать точку восстановления перед очисткой", "Automatically create a restore point before cleaning", "Temizlikten önce otomatik geri yükleme noktası oluştur"),
        ["EmptyRecycleAuto"] = ("Очищать корзину автоматически после очистки системы", "Empty Recycle Bin automatically after cleanup", "Temizlikten sonra Geri Dönüşüm Kutusunu otomatik boşalt"),
        ["MinDownloadSize"] = ("Минимальный размер «большого» файла в Загрузках (МБ):", "Minimum \"large file\" size in Downloads (MB):", "İndirilenlerdeki \"büyük dosya\" için asgari boyut (MB):"),
        ["SaveSettings"] = ("Сохранить настройки", "Save settings", "Ayarları kaydet"),
        ["ResetSettings"] = ("Сбросить к заводским", "Reset to defaults", "Fabrika ayarlarına sıfırla"),
        ["SettingsSaved"] = ("Настройки сохранены в appsettings.json", "Settings saved to appsettings.json", "Ayarlar appsettings.json dosyasına kaydedildi"),
        ["SettingsReset"] = ("Настройки сброшены к значениям по умолчанию", "Settings reset to defaults", "Ayarlar varsayılan değerlere sıfırlandı"),
        ["InvalidNumber"] = ("Некорректное число. Введите значение от 1 до 10240 МБ.", "Invalid number. Enter a value between 1 and 10240 MB.", "Geçersiz sayı. 1-10240 MB arasında bir değer girin."),
        ["RangeCheckError"] = ("Минимальный размер должен быть меньше максимального (проверьте поле).", "Minimum size must be less than maximum (check the field).", "Asgari boyut azami boyuttan küçük olmalıdır."),
        ["AutostartError"] = ("Не удалось изменить ярлык автозапуска.", "Failed to change the autostart shortcut.", "Otomatik başlatma kısayolu değiştirilemedi."),

        // ---- О программе ----
        ["AboutHeader"] = ("О программе", "About", "Hakkında"),
        ["AppName"] = ("SHK CLEANER", "SHK CLEANER", "SHK CLEANER"),
        ["AppVersion"] = ("Версия: 1.0.0", "Version: 1.0.0", "Sürüm: 1.0.0"),
        ["AppDescription"] = ("Программа для очистки вашего компьютера от временных файлов и ненужных программ.",
                              "An application to clean your computer from temporary files and unwanted programs.",
                              "Bilgisayarınızı geçici dosyalardan ve gereksiz programlardan temizleyen uygulama."),
        ["GitHubLink"] = ("🔗 GitHub: github.com/shk/cleaner", "🔗 GitHub: github.com/shk/cleaner", "🔗 GitHub: github.com/shk/cleaner"),
        ["CheckUpdates"] = ("Проверить обновления", "Check for updates", "Güncellemeleri kontrol et"),
        ["Copyright"] = ("© 2026 SHK. Все права защищены.", "© 2026 SHK. All rights reserved.", "© 2026 SHK. Tüm hakları saklıdır."),
        ["NoUpdates"] = ("У вас установлена последняя версия (1.0.0).", "You have the latest version (1.0.0).", "En son sürüme sahipsiniz (1.0.0)."),

        // ---- Новые фичи: карточки статистики, предпросмотр программ ----
        ["StatTotalJunk"] = ("Всего мусора", "Total junk", "Toplam çöp"),
        ["StatFilesFound"] = ("Найдено файлов", "Files found", "Dosya bulundu"),
        ["StatCategories"] = ("Категорий", "Categories", "Kategori"),
        ["StatFreed"] = ("Освобождено", "Freed", "Boşaltıldı"),
        ["ProgramPreview"] = ("Программа", "Program", "Program"),
        ["PublisherLabel"] = ("Издатель: {0}", "Publisher: {0}", "Yayıncı: {0}"),
        ["VersionLabel"] = ("Версия: {0}", "Version: {0}", "Sürüm: {0}"),
        ["InstallDateLabel"] = ("Установлена: {0}", "Installed: {0}", "Kurulum: {0}"),
        ["SizeLabel"] = ("Размер: {0}", "Size: {0}", "Boyut: {0}"),
        ["UninstallCommand"] = ("Команда удаления:", "Uninstall command:", "Kaldırma komutu:"),
        ["ProgramHint"] = ("Выберите программу в списке, чтобы увидеть подробности и значок.",
                           "Select a program in the list to see details and its icon.",
                           "Ayrıntıları ve simgesini görmek için listeden bir program seçin."),
        ["SilentUninstall"] = ("Тихое удаление доступно", "Silent uninstall available", "Sessiz kaldırma mevcut"),
        ["NoIcon"] = ("Значок не найден", "No icon found", "Simge bulunamadı"),

        // Общие диалоги
        ["Question"] = ("Вопрос", "Question", "Soru"),
        ["Warning"] = ("Внимание", "Warning", "Uyarı"),
        ["Error"] = ("Ошибка", "Error", "Hata"),
        ["Info"] = ("Информация", "Information", "Bilgi"),
        ["ErrorOccurred"] = ("Произошла ошибка: {0}", "An error occurred: {0}", "Bir hata oluştu: {0}"),
    };

    /// <summary>Возвращает перевод по ключу для текущего языка (если нет — для русского).</summary>
    public static string Get(string ключ)
    {
        if (!Переводы.TryGetValue(ключ, out var п)) return ключ;
        return ТекущийЯзык switch
        {
            "en" => п.en,
            "tr" => п.tr,
            _ => п.ru,
        };
    }

    /// <summary>Меняет текущий язык интерфейса.</summary>
    public static void УстановитьЯзык(string кодЯзыка)
    {
        ТекущийЯзык = кодЯзыка.ToLowerInvariant() switch
        {
            "en" or "tr" => кодЯзыка.ToLowerInvariant(),
            _ => "ru",
        };
    }

    /// <summary>Названия категорий очистки по их кодам (для построения списка).</summary>
    public static string НазваниеКатегории(string код) => код switch
    {
        "TempUser" => Get("CatTempUser"),
        "TempSystem" => Get("CatTempSystem"),
        "Prefetch" => Get("CatPrefetch"),
        "ShaderCache" => Get("CatShaderCache"),
        "RecycleBin" => Get("CatRecycleBin"),
        "Browsers" => Get("CatBrowsers"),
        "Downloads" => Get("CatDownloads"),
        _ => код,
    };
}
