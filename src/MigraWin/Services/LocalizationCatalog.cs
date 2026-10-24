namespace MigraWin.Services;

internal static class LocalizationCatalog
{
    private static readonly string[] Codes =
        ["it", "en", "zh", "ja", "ko", "es", "tr", "ru", "el", "fr", "de"];

    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Load()
    {
        var result = Codes.ToDictionary(
            code => code,
            _ => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(StringComparer.Ordinal));
        foreach (var rawLine in Data.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = line.Split('\t');
            if (columns.Length < 2)
            {
                continue;
            }

            var source = columns[0];
            for (var index = 1; index < Math.Min(columns.Length, Codes.Length); index++)
            {
                if (!string.IsNullOrWhiteSpace(columns[index]))
                {
                    ((Dictionary<string, string>)result[Codes[index]])[source] = columns[index];
                }
            }
        }

        return result;
    }

    private const string Data = """
Panoramica	Overview	概览	概要	개요	Resumen	Genel bakış	Обзор	Επισκόπηση	Vue d'ensemble	Übersicht
Inventario	Inventory	清单	インベントリ	인벤토리	Inventario	Envanter	Инвентарь	Απογραφή	Inventaire	Inventar
Backup	Backup	备份	バックアップ	백업	Copia de seguridad	Yedekleme	Резервная копия	Αντίγραφο ασφαλείας	Sauvegarde	Sicherung
Archivio	Archive	归档	アーカイブ	아카이브	Archivo	Arşiv	Архив	Αρχείο	Archive	Archiv
Confronto	Compare	比较	比較	비교	Comparar	Karşılaştır	Сравнение	Σύγκριση	Comparer	Vergleichen
Ripristino	Restore	还原	復元	복원	Restaurar	Geri yükle	Восстановление	Επαναφορά	Restaurer	Wiederherstellen
Pacchetti	Packages	软件包	パッケージ	패키지	Paquetes	Paketler	Пакеты	Πακέτα	Paquets	Pakete
Attività	Activity	活动	アクティビティ	활동	Actividad	Etkinlik	Действия	Δραστηριότητα	Activité	Aktivität
Impostazioni	Settings	设置	設定	설정	Configuración	Ayarlar	Настройки	Ρυθμίσεις	Paramètres	Einstellungen
Backup e migrazione	Backup and migration	备份与迁移	バックアップと移行	백업 및 마이그레이션	Copia de seguridad y migración	Yedekleme ve geçiş	Резервное копирование и миграция	Αντίγραφο και μετεγκατάσταση	Sauvegarde et migration	Sicherung und Migration
Solo locale	Local only	仅本地	ローカルのみ	로컬 전용	Solo local	Yalnızca yerel	Только локально	Μόνο τοπικά	Local uniquement	Nur lokal
Verifica autenticata attiva	Authenticated verification active	身份验证保护已启用	認証済み検証が有効	인증된 검증 활성화	Verificación autenticada activa	Kimliği doğrulanmış doğrulama açık	Проверка подлинности активна	Ενεργή επαληθευμένη επαλήθευση	Vérification authentifiée active	Authentifizierte Prüfung aktiv
Elaborazione locale	Local processing	本地处理	ローカル処理	로컬 처리	Procesamiento local	Yerel işleme	Локальная обработка	Τοπική επεξεργασία	Traitement local	Lokale Verarbeitung
Annulla	Cancel	取消	キャンセル	취소	Cancelar	İptal	Отмена	Άκυρο	Annuler	Abbrechen
Annulla operazione	Cancel operation	取消操作	操作をキャンセル	작업 취소	Cancelar operación	İşlemi iptal et	Отменить операцию	Ακύρωση λειτουργίας	Annuler l'opération	Vorgang abbrechen
Lingua	Language	语言	言語	언어	Idioma	Dil	Язык	Γλώσσα	Langue	Sprache
Segui il sistema	System	跟随系统	システム設定	시스템 설정	Seguir el sistema	Sistemi izle	Как в системе	Όπως στο σύστημα	Système	System
Chiaro	Light	浅色	ライト	라이트	Claro	Açık	Светлая	Φωτεινό	Clair	Hell
Scuro	Dark	深色	ダーク	다크	Oscuro	Koyu	Тёмная	Σκοτεινό	Sombre	Dunkel
Aggiornamenti	Updates	更新	更新	업데이트	Actualizaciones	Güncellemeler	Обновления	Ενημερώσεις	Mises à jour	Updates
Controlla	Check	检查	確認	확인	Comprobar	Denetle	Проверить	Έλεγχος	Vérifier	Prüfen
Scarica e installa	Download and install	下载并安装	ダウンロードしてインストール	다운로드 및 설치	Descargar e instalar	İndir ve yükle	Скачать и установить	Λήψη και εγκατάσταση	Télécharger et installer	Herunterladen und installieren
Aspetto	Appearance	外观	外観	모양	Apariencia	Görünüm	Внешний вид	Εμφάνιση	Apparence	Darstellung
Backup predefinito	Backup defaults	默认备份	バックアップの既定値	기본 백업	Valores de copia predeterminados	Varsayılan yedekleme	Параметры резервного копирования	Προεπιλογές αντιγράφου	Sauvegarde par défaut	Sicherungsvorgaben
Impostazioni e configurazioni di base	Base settings and configuration	基础设置与配置	基本設定と構成	기본 설정 및 구성	Configuración básica	Temel ayarlar ve yapılandırma	Базовые параметры и конфигурация	Βασικές ρυθμίσεις και διαμόρφωση	Paramètres et configuration de base	Basiseinstellungen und Konfiguration
Dati, progetti e cartelle personali	Data, projects and personal folders	数据、项目和个人文件夹	データ、プロジェクト、個人用フォルダー	데이터, 프로젝트 및 개인 폴더	Datos, proyectos y carpetas personales	Veriler, projeler ve kişisel klasörler	Данные, проекты и личные папки	Δεδομένα, έργα και προσωπικοί φάκελοι	Données, projets et dossiers personnels	Daten, Projekte und persönliche Ordner
Copia archivistica dei file installati	Archival copy of installed files	已安装文件的存档副本	インストール済みファイルのアーカイブコピー	설치된 파일의 보관 사본	Copia de archivo de archivos instalados	Yüklü dosyaların arşiv kopyası	Архивная копия установленных файлов	Αρχειακό αντίγραφο εγκατεστημένων αρχείων	Copie d'archive des fichiers installés	Archivkopie der installierten Dateien
Includi applicazioni Microsoft Store e MSIX	Include Microsoft Store and MSIX apps	包含 Microsoft Store 和 MSIX 应用	Microsoft Store と MSIX アプリを含める	Microsoft Store 및 MSIX 앱 포함	Incluir aplicaciones Microsoft Store y MSIX	Microsoft Store ve MSIX uygulamalarını dahil et	Включить приложения Microsoft Store и MSIX	Συμπερίληψη εφαρμογών Microsoft Store και MSIX	Inclure les applications Microsoft Store et MSIX	Microsoft Store- und MSIX-Apps einbeziehen
Includi script e strumenti personali	Include personal scripts and tools	包含个人脚本和工具	個人用スクリプトとツールを含める	개인 스크립트 및 도구 포함	Incluir scripts y herramientas personales	Kişisel betikleri ve araçları dahil et	Включить личные скрипты и инструменты	Συμπερίληψη προσωπικών σεναρίων και εργαλείων	Inclure les scripts et outils personnels	Persönliche Skripte und Tools einbeziehen
Dati locali	Local data	本地数据	ローカルデータ	로컬 데이터	Datos locales	Yerel veriler	Локальные данные	Τοπικά δεδομένα	Données locales	Lokale Daten
Apri cartella rapporti	Open reports folder	打开报告文件夹	レポートフォルダーを開く	보고서 폴더 열기	Abrir carpeta de informes	Rapor klasörünü aç	Открыть папку отчётов	Άνοιγμα φακέλου αναφορών	Ouvrir le dossier des rapports	Berichtsordner öffnen
Il tuo ambiente di lavoro, pronto per il prossimo PC.	Your work environment, ready for the next PC.	你的工作环境，已为新电脑准备就绪。	次の PC に備えた作業環境。	새 PC를 위한 작업 환경이 준비되었습니다.	Tu entorno de trabajo, listo para el próximo equipo.	Çalışma ortamınız yeni bilgisayara hazır.	Ваша рабочая среда готова к переходу на новый ПК.	Το περιβάλλον εργασίας σας είναι έτοιμο για τον επόμενο υπολογιστή.	Votre environnement de travail, prêt pour le prochain PC.	Ihre Arbeitsumgebung, bereit für den nächsten PC.
Scegli applicazioni, impostazioni e dati. MigraWin prepara un archivio autonomo, ne verifica l'integrità e ricostruisce solo ciò che approvi.	Choose apps, settings and data. MigraWin creates a self-contained archive, verifies its integrity and restores only what you approve.	选择应用、设置和数据。MigraWin 创建独立归档、验证完整性，并仅还原你批准的内容。	アプリ、設定、データを選択します。MigraWin は自己完結型アーカイブを作成し、整合性を検証して、承認した項目だけを復元します。	앱, 설정 및 데이터를 선택하세요. MigraWin은 독립형 아카이브를 만들고 무결성을 검증하며 승인한 항목만 복원합니다.	Elige aplicaciones, ajustes y datos. MigraWin crea un archivo autónomo, verifica su integridad y restaura solo lo que apruebas.	Uygulamaları, ayarları ve verileri seçin. MigraWin bağımsız bir arşiv oluşturur, bütünlüğünü doğrular ve yalnızca onayladığınız öğeleri geri yükler.	Выберите приложения, параметры и данные. MigraWin создаёт автономный архив, проверяет его целостность и восстанавливает только одобренное.	Επιλέξτε εφαρμογές, ρυθμίσεις και δεδομένα. Το MigraWin δημιουργεί αυτόνομο αρχείο, επαληθεύει την ακεραιότητά του και επαναφέρει μόνο όσα εγκρίνετε.	Choisissez les applications, paramètres et données. MigraWin crée une archive autonome, vérifie son intégrité et restaure uniquement ce que vous approuvez.	Wählen Sie Apps, Einstellungen und Daten. MigraWin erstellt ein eigenständiges Archiv, prüft dessen Integrität und stellt nur das Wiederher, was Sie freigeben.
Analizza questo PC	Analyze this PC	分析此电脑	この PC を分析	이 PC 분석	Analizar este equipo	Bu bilgisayarı analiz et	Проанализировать компьютер	Ανάλυση αυτού του υπολογιστή	Analyser ce PC	Diesen PC analysieren
Apri archivio	Open archive	打开归档	アーカイブを開く	아카이브 열기	Abrir archivo	Arşivi aç	Открыть архив	Άνοιγμα αρχείου	Ouvrir l'archive	Archiv öffnen
App rilevate	Apps found	已发现应用	検出されたアプリ	검색된 앱	Aplicaciones detectadas	Bulunan uygulamalar	Найдено приложений	Εφαρμογές που βρέθηκαν	Applications détectées	Gefundene Apps
Selezionate	Selected	已选择	選択済み	선택됨	Seleccionadas	Seçilen	Выбрано	Επιλεγμένα	Sélectionnées	Ausgewählt
Giochi esclusi	Games excluded	已排除游戏	除外されたゲーム	제외된 게임	Juegos excluidos	Hariç tutulan oyunlar	Игры исключены	Εξαιρούμενα παιχνίδια	Jeux exclus	Spiele ausgeschlossen
Ultimo backup	Last backup	上次备份	前回のバックアップ	마지막 백업	Última copia	Son yedekleme	Последняя копия	Τελευταίο αντίγραφο	Dernière sauvegarde	Letzte Sicherung
Nessun backup	No backup	没有备份	バックアップなし	백업 없음	Sin copias	Yedek yok	Нет резервных копий	Δεν υπάρχει αντίγραφο	Aucune sauvegarde	Keine Sicherung
Inventario del PC	PC inventory	电脑清单	PC インベントリ	PC 인벤토리	Inventario del equipo	Bilgisayar envanteri	Инвентарь ПК	Απογραφή υπολογιστή	Inventaire du PC	PC-Inventar
Protezioni sempre attive	Always-on protections	持续保护	常時有効な保護	상시 보호	Protecciones siempre activas	Her zaman açık korumalar	Постоянная защита	Πάντα ενεργή προστασία	Protections toujours actives	Immer aktive Schutzfunktionen
Attività recente	Recent activity	最近活动	最近のアクティビティ	최근 활동	Actividad reciente	Son etkinlik	Последние действия	Πρόσφατη δραστηριότητα	Activité récente	Letzte Aktivität
Scegli cosa migrare	Choose what to migrate	选择要迁移的内容	移行する内容を選択	마이그레이션할 항목 선택	Elige qué migrar	Geçirilecekleri seçin	Выберите, что перенести	Επιλέξτε τι θα μεταφερθεί	Choisissez quoi migrer	Auswählen, was migriert wird
Aggiungi portabile	Add portable app	添加便携应用	ポータブルアプリを追加	포터블 앱 추가	Agregar aplicación portátil	Taşınabilir uygulama ekle	Добавить портативную программу	Προσθήκη φορητής εφαρμογής	Ajouter une application portable	Portable App hinzufügen
Seleziona risultati	Select results	选择结果	結果を選択	결과 선택	Seleccionar resultados	Sonuçları seç	Выбрать результаты	Επιλογή αποτελεσμάτων	Sélectionner les résultats	Ergebnisse auswählen
Deseleziona tutto	Deselect all	全部取消选择	すべて選択解除	모두 선택 해제	Desmarcar todo	Tüm seçimleri kaldır	Снять выбор	Αποεπιλογή όλων	Tout désélectionner	Alle abwählen
Tutte le applicazioni	All apps	所有应用	すべてのアプリ	모든 앱	Todas las aplicaciones	Tüm uygulamalar	Все приложения	Όλες οι εφαρμογές	Toutes les applications	Alle Apps
Reinstallabili o supportate	Reinstallable or supported	可重新安装或受支持	再インストール可能または対応	재설치 가능 또는 지원됨	Reinstalables o compatibles	Yeniden yüklenebilir veya desteklenen	Переустанавливаемые или поддерживаемые	Εγκαταστάσιμα ή υποστηριζόμενα	Réinstallables ou pris en charge	Neu installierbar oder unterstützt
Solo rilevate	Detected only	仅已检测	検出のみ	검색만 됨	Solo detectadas	Yalnızca algılananlar	Только обнаруженные	Μόνο ανιχνευμένα	Détectées uniquement	Nur erkannt
Nella selezione	In selection	在选择中	選択内	선택 항목	En la selección	Seçimde	В выборе	Στην επιλογή	Dans la sélection	In der Auswahl
Script personali	Personal scripts	个人脚本	個人用スクリプト	개인 스크립트	Scripts personales	Kişisel betikler	Личные скрипты	Προσωπικά σενάρια	Scripts personnels	Persönliche Skripte
Carica profilo	Load profile	加载配置	プロファイルを読み込む	프로필 불러오기	Cargar perfil	Profili yükle	Загрузить профиль	Φόρτωση προφίλ	Charger le profil	Profil laden
Salva profilo	Save profile	保存配置	プロファイルを保存	프로필 저장	Guardar perfil	Profili kaydet	Сохранить профиль	Αποθήκευση προφίλ	Enregistrer le profil	Profil speichern
Esporta	Export	导出	エクスポート	내보내기	Exportar	Dışa aktar	Экспорт	Εξαγωγή	Exporter	Exportieren
Importa	Import	导入	インポート	가져오기	Importar	İçe aktar	Импорт	Εισαγωγή	Importer	Importieren
Livello di supporto	Support level	支持级别	サポートレベル	지원 수준	Nivel de compatibilidad	Destek düzeyi	Уровень поддержки	Επίπεδο υποστήριξης	Niveau de prise en charge	Unterstützungsgrad
Componenti	Components	组件	コンポーネント	구성 요소	Componentes	Bileşenler	Компоненты	Στοιχεία	Composants	Komponenten
Impostazioni e preferenze	Settings and preferences	设置与首选项	設定と環境設定	설정 및 기본 설정	Ajustes y preferencias	Ayarlar ve tercihler	Параметры и предпочтения	Ρυθμίσεις και προτιμήσεις	Paramètres et préférences	Einstellungen und Präferenzen
Dati, progetti e cartelle personalizzate	Data, projects and custom folders	数据、项目和自定义文件夹	データ、プロジェクト、カスタムフォルダー	데이터, 프로젝트 및 사용자 지정 폴더	Datos, proyectos y carpetas personalizadas	Veriler, projeler ve özel klasörler	Данные, проекты и пользовательские папки	Δεδομένα, έργα και προσαρμοσμένοι φάκελοι	Données, projets et dossiers personnalisés	Daten, Projekte und benutzerdefinierte Ordner
Pacchetto installabile offline	Offline installer package	离线安装包	オフライン インストーラー	오프라인 설치 패키지	Paquete de instalación sin conexión	Çevrimdışı yükleyici paketi	Пакет автономной установки	Πακέτο εγκατάστασης εκτός σύνδεσης	Package d'installation hors ligne	Offline-Installationspaket
Includi configurazioni che possono contenere segreti	Include configurations that may contain secrets	包含可能含有密钥的配置	シークレットを含む可能性のある構成を含める	비밀 정보가 포함될 수 있는 구성 포함	Gestionar configuraciones que pueden contener secretos	Gizli bilgi içerebilecek yapılandırmaları dahil et	Включить конфигурации, которые могут содержать секреты	Συμπερίληψη διαμορφώσεων που μπορεί να περιέχουν μυστικά	Inclure les configurations pouvant contenir des secrets	Konfigurationen mit möglichen Geheimnissen einbeziehen
Cartelle dati aggiuntive	Additional data folders	其他数据文件夹	追加のデータフォルダー	추가 데이터 폴더	Carpetas de datos adicionales	Ek veri klasörleri	Дополнительные папки данных	Πρόσθετοι φάκελοι δεδομένων	Dossiers de données supplémentaires	Zusätzliche Datenordner
Esclusioni persistenti	Persistent exclusions	永久排除项	永続的な除外	영구 제외	Exclusiones persistentes	Kalıcı hariç tutmalar	Постоянные исключения	Μόνιμες εξαιρέσεις	Exclusions persistantes	Dauerhafte Ausschlüsse
Installer associato	Associated installer	关联安装程序	関連付けられたインストーラー	연결된 설치 관리자	Instalador asociado	İlişkili yükleyici	Связанный установщик	Συσχετισμένο πρόγραμμα εγκατάστασης	Programme d'installation associé	Zugeordnetes Installationsprogramm
Scegli installer…	Choose installer…	选择安装程序…	インストーラーを選択…	설치 관리자 선택…	Elegir instalador…	Yükleyici seç…	Выбрать установщик…	Επιλογή προγράμματος εγκατάστασης…	Choisir l'installateur…	Installationsprogramm wählen…
Apri sito ufficiale	Open official website	打开官方网站	公式サイトを開く	공식 웹사이트 열기	Abrir sitio oficial	Resmî siteyi aç	Открыть официальный сайт	Άνοιγμα επίσημου ιστότοπου	Ouvrir le site officiel	Offizielle Website öffnen
Componi il backup	Build the backup	配置备份	バックアップを構成	백업 구성	Configurar la copia	Copiyi oluştur	Создание резервной копии	Δημιουργία αντιγράφου	Configurer la sauvegarde	Sicherung erstellen
Profilo di contenuto	Content profile	内容配置	コンテンツ プロファイル	콘텐츠 프로필	Perfil de contenido	İçerik profili	Профиль содержимого	Προφίλ περιεχομένου	Profil de contenu	Inhaltsprofil
Solo inventario	Inventory only	仅清单	インベントリのみ	인벤토리만	Solo inventario	Yalnızca envanter	Только инвентарь	Μόνο απογραφή	Inventaire uniquement	Nur Inventar
Solo impostazioni	Settings only	仅设置	設定のみ	설정만	Solo ajustes	Yalnızca ayarlar	Только параметры	Μόνο ρυθμίσεις	Paramètres uniquement	Nur Einstellungen
Solo dati	Data only	仅数据	データのみ	데이터만	Solo datos	Yalnızca veriler	Только данные	Μόνο δεδομένα	Données uniquement	Nur Daten
Solo pacchetti	Packages only	仅软件包	パッケージのみ	패키지만	Solo paquetes	Yalnızca paketler	Только пакеты	Μόνο πακέτα	Paquets uniquement	Nur Pakete
Impostazioni e dati	Settings and data	设置和数据	設定とデータ	설정 및 데이터	Ajustes y datos	Ayarlar ve veriler	Параметры и данные	Ρυθμίσεις και δεδομένα	Paramètres et données	Einstellungen und Daten
Completo supportato	Full supported	完整支持	サポート対象をすべて	지원되는 항목 전체	Todos los compatibles	Desteklenenlerin tamamı	Все поддерживаемое	Πλήρες υποστηριζόμενο	Complet pris en charge	Vollständig unterstützt
Identità del backup	Backup identity	备份标识	バックアップ識別情報	백업 ID	Identidad de la copia	Yedek kimliği	Идентификатор копии	Ταυτότητα αντιγράφου	Identité de la sauvegarde	Sicherungsidentität
Nome profilo	Profile name	配置名称	プロファイル名	프로필 이름	Nombre del perfil	Profil adı	Имя профиля	Όνομα προφίλ	Nom du profil	Profilname
Repository di destinazione	Destination repository	目标仓库	保存先リポジトリ	대상 저장소	Repositorio de destino	Hedef depo	Целевой репозиторий	Αποθετήριο προορισμού	Dépôt de destination	Ziel-Repository
Modalità temporale	Time mode	时间模式	時間モード	시간 모드	Modo temporal	Zaman modu	Режим по времени	Λειτουργία χρόνου	Mode temporel	Zeitmodus
Incrementale con deduplicazione	Incremental with deduplication	增量并去重	増分・重複排除	증분 및 중복 제거	Incremental con deduplicación	Yinelenenleri ayıklayarak artımlı	Инкрементально с дедупликацией	Αυξητικό με αφαίρεση διπλοτύπων	Incrémentale avec déduplication	Inkrementell mit Deduplizierung
Differenziale dalla copia più recente	Differential from the latest backup	基于最新备份的差分	最新バックアップからの差分	최신 백업 기준 차분	Diferencial desde la copia más reciente	En son yedeğe göre fark	Дифференциально от последней копии	Διαφορικό από το τελευταίο αντίγραφο	Différentielle depuis la dernière sauvegarde	Differentiell zur letzten Sicherung
Completa logica	Full logical	完整逻辑备份	完全論理バックアップ	전체 논리 백업	Completa lógica	Tam mantıksal	Полная логическая	Πλήρες λογικό	Complète logique	Vollständig logisch
Protezione dell'archivio	Archive protection	归档保护	アーカイブ保護	아카이브 보호	Protección del archivo	Arşiv koruması	Защита архива	Προστασία αρχείου	Protection de l'archive	Archivschutz
Password (minimo 10 caratteri)	Password (minimum 10 characters)	密码（至少 10 个字符）	パスワード（10 文字以上）	암호(최소 10자)	Contraseña (mínimo 10 caracteres)	Parola (en az 10 karakter)	Пароль (не менее 10 символов)	Κωδικός (τουλάχιστον 10 χαρακτήρες)	Mot de passe (10 caractères minimum)	Kennwort (mindestens 10 Zeichen)
Conferma password	Confirm password	确认密码	パスワードを確認	암호 확인	Confirmar contraseña	Parolayı doğrula	Подтвердите пароль	Επιβεβαίωση κωδικού	Confirmer le mot de passe	Kennwort bestätigen
Conservazione	Retention	保留策略	保持	보존	Retención	Saklama	Хранение	Διατήρηση	Conservation	Aufbewahrung
Esecuzione pianificata	Scheduled run	计划执行	スケジュール実行	예약 실행	Ejecución programada	Zamanlanmış çalıştırma	Запуск по расписанию	Προγραμματισμένη εκτέλεση	Exécution planifiée	Geplante Ausführung
Attiva backup giornaliero	Enable daily backup	启用每日备份	毎日のバックアップを有効化	일일 백업 활성화	Activar copia diaria	Günlük yedeği etkinleştir	Включить ежедневную копию	Ενεργοποίηση καθημερινού αντιγράφου	Activer la sauvegarde quotidienne	Tägliche Sicherung aktivieren
Ora locale	Local time	本地时间	現地時刻	현지 시간	Hora local	Yerel saat	Местное время	Τοπική ώρα	Heure locale	Ortszeit
Piano corrente	Current plan	当前计划	現在のプラン	현재 계획	Plan actual	Geçerli plan	Текущий план	Τρέχον σχέδιο	Plan actuel	Aktueller Plan
Crea e verifica backup	Create and verify backup	创建并验证备份	バックアップを作成して検証	백업 생성 및 검증	Crear y verificar copia	Yedeği oluştur ve doğrula	Создать и проверить копию	Δημιουργία και επαλήθευση αντιγράφου	Créer et vérifier la sauvegarde	Sicherung erstellen und prüfen
Chiave di recupero	Recovery key	恢复密钥	回復キー	복구 키	Clave de recuperación	Kurtarma anahtarı	Ключ восстановления	Κλειδί ανάκτησης	Clé de récupération	Wiederherstellungsschlüssel
Copia chiave	Copy key	复制密钥	キーをコピー	키 복사	Copiar clave	Anahtarı kopyala	Копировать ключ	Αντιγραφή κλειδιού	Copier la clé	Schlüssel kopieren
Archivio dei backup	Backup archive	备份归档	バックアップ アーカイブ	백업 아카이브	Archivo de copias	Yedek arşivi	Архив резервных копий	Αρχείο αντιγράφων	Archive des sauvegardes	Sicherungsarchiv
Apri un repository	Open a repository	打开仓库	リポジトリを開く	저장소 열기	Abrir un repositorio	Depoyu aç	Открыть репозиторий	Άνοιγμα αποθετηρίου	Ouvrir un dépôt	Repository öffnen
Cartella repository	Repository folder	仓库文件夹	リポジトリ フォルダー	저장소 폴더	Carpeta del repositorio	Depo klasörü	Папка репозитория	Φάκελος αποθετηρίου	Dossier du dépôt	Repository-Ordner
Password o chiave di recupero	Password or recovery key	密码或恢复密钥	パスワードまたは回復キー	암호 또는 복구 키	Contraseña o clave de recuperación	Parola veya kurtarma anahtarı	Пароль или ключ восстановления	Κωδικός ή κλειδί ανάκτησης	Mot de passe ou clé de récupération	Kennwort oder Wiederherstellungsschlüssel
Apri repository	Open repository	打开仓库	リポジトリを開く	저장소 열기	Abrir repositorio	Depoyu aç	Открыть репозиторий	Άνοιγμα αποθετηρίου	Ouvrir le dépôt	Repository öffnen
Apri file…	Open file…	打开文件…	ファイルを開く…	파일 열기…	Abrir archivo…	Dosya aç…	Открыть файл…	Άνοιγμα αρχείου…	Ouvrir le fichier…	Datei öffnen…
Punti di ripristino	Restore points	还原点	復元ポイント	복원 지점	Puntos de restauración	Geri yükleme noktaları	Точки восстановления	Σημεία επαναφοράς	Points de restauration	Wiederherstellungspunkte
Verifica integrità	Verify integrity	验证完整性	整合性を検証	무결성 확인	Verificar integridad	Bütünlüğü doğrula	Проверить целостность	Επαλήθευση ακεραιότητας	Vérifier l'intégrité	Integrität prüfen
Prepara il ripristino	Prepare restore	准备还原	復元を準備	복원 준비	Preparar restauración	Geri yüklemeyi hazırla	Подготовить восстановление	Προετοιμασία επαναφοράς	Préparer la restauration	Wiederherstellung vorbereiten
Esporta archivio autonomo…	Export standalone archive…	导出自包含归档…	自己完結アーカイブをエクスポート…	독립 실행형 아카이브 내보내기…	Exportar archivo autónomo…	Bağımsız arşivi dışa aktar…	Экспортировать автономный архив…	Εξαγωγή αυτόνομου αρχείου…	Exporter une archive autonome…	Eigenständiges Archiv exportieren…
Elimina questo backup	Delete this backup	删除此备份	このバックアップを削除	이 백업 삭제	Eliminar esta copia	Bu yedeği sil	Удалить эту копию	Διαγραφή αυτού του αντιγράφου	Supprimer cette sauvegarde	Diese Sicherung löschen
Nuova password	New password	新密码	新しいパスワード	새 암호	Nueva contraseña	Yeni parola	Новый пароль	Νέος κωδικός	Nouveau mot de passe	Neues Kennwort
Conferma nuova password	Confirm new password	确认新密码	新しいパスワードを確認	새 암호 확인	Confirmar nueva contraseña	Yeni parolayı doğrula	Подтвердите новый пароль	Επιβεβαίωση νέου κωδικού	Confirmer le nouveau mot de passe	Neues Kennwort bestätigen
Cambia password	Change password	更改密码	パスワードを変更	암호 변경	Cambiar contraseña	Parolayı değiştir	Изменить пароль	Αλλαγή κωδικού	Changer le mot de passe	Kennwort ändern
Confronto tra copie	Compare backups	比较备份	バックアップを比較	백업 비교	Comparar copias	Yedekleri karşılaştır	Сравнить копии	Σύγκριση αντιγράφων	Comparer les sauvegardes	Sicherungen vergleichen
Da	From	从	開始	원본	Desde	Başlangıç	От	Από	De	Von
A	To	至	終了	대상	A	Bitiş	До	Προς	À	Bis
Confronta	Compare	比较	比較	비교	Comparar	Karşılaştır	Сравнить	Σύγκριση	Comparer	Vergleichen
Rapporto	Report	报告	レポート	보고서	Informe	Rapor	Отчёт	Αναφορά	Rapport	Bericht
Variazioni rilevate	Detected changes	检测到的更改	検出された変更	감지된 변경 사항	Cambios detectados	Algılanan değişiklikler	Обнаруженные изменения	Ανιχνευμένες αλλαγές	Modifications détectées	Erkannte Änderungen
Ripristino selettivo	Selective restore	选择性还原	選択的復元	선택적 복원	Restauración selectiva	Seçmeli geri yükleme	Выборочное восстановление	Επιλεκτική επαναφορά	Restauration sélective	Selektive Wiederherstellung
Contenuto da ripristinare	Content to restore	要还原的内容	復元する内容	복원할 콘텐츠	Contenido a restaurar	Geri yüklenecek içerik	Содержимое для восстановления	Περιεχόμενο για επαναφορά	Contenu à restaurer	Wiederherzustellender Inhalt
Mappature dei percorsi	Path mappings	路径映射	パスのマッピング	경로 매핑	Asignaciones de rutas	Yol eşlemeleri	Сопоставления путей	Αντιστοιχίσεις διαδρομών	Mappages des chemins	Pfadzuordnungen
Conflitti	Conflicts	冲突	競合	충돌	Conflictos	Çakışmalar	Конфликты	Συγκρούσεις	Conflits	Konflikte
Mantieni i file esistenti	Keep existing files	保留现有文件	既存ファイルを保持	기존 파일 유지	Conservar archivos existentes	Mevcut dosyaları koru	Сохранить существующие файлы	Διατήρηση υπαρχόντων αρχείων	Conserver les fichiers existants	Vorhandene Dateien behalten
Sostituisci con copia preventiva	Replace with rollback copy	替换并保留回滚副本	ロールバック コピーで置換	롤백 사본으로 교체	Reemplazar con copia de reversión	Geri alma kopyasıyla değiştir	Заменить с резервной копией	Αντικατάσταση με αντίγραφο επαναφοράς	Remplacer avec copie de restauration	Mit Rollback-Kopie ersetzen
Salva entrambi	Keep both	保留两者	両方保持	둘 다 유지	Conservar ambos	İkisini de sakla	Сохранить оба	Διατήρηση και των δύο	Conserver les deux	Beide behalten
Avvia gli installer ripristinati	Run restored installers	运行已还原的安装程序	復元したインストーラーを実行	복원된 설치 관리자 실행	Ejecutar instaladores restaurados	Geri yüklenen yükleyicileri çalıştır	Запустить восстановленные установщики	Εκτέλεση αποκατεστημένων εγκαταστατών	Exécuter les installateurs restaurés	Wiederhergestellte Installationsprogramme starten
Rianalizza conflitti	Recheck conflicts	重新检查冲突	競合を再確認	충돌 다시 확인	Volver a analizar conflictos	Çakışmaları yeniden denetle	Повторно проверить конфликты	Επανέλεγχος συγκρούσεων	Réanalyser les conflits	Konflikte erneut prüfen
Avvia ripristino	Start restore	开始还原	復元を開始	복원 시작	Iniciar restauración	Geri yüklemeyi başlat	Начать восстановление	Έναρξη επαναφοράς	Démarrer la restauration	Wiederherstellung starten
Torna all'archivio	Back to archive	返回归档	アーカイブに戻る	아카이브로 돌아가기	Volver al archivo	Arşive dön	Вернуться к архиву	Επιστροφή στο αρχείο	Retour à l'archive	Zurück zum Archiv
Libreria dei pacchetti	Package library	软件包库	パッケージ ライブラリ	패키지 라이브러리	Biblioteca de paquetes	Paket kitaplığı	Библиотека пакетов	Βιβλιοθήκη πακέτων	Bibliothèque de paquets	Paketbibliothek
Importa installer	Import installer	导入安装程序	インストーラーをインポート	설치 관리자 가져오기	Importar instalador	Yükleyiciyi içe aktar	Импортировать установщик	Εισαγωγή προγράμματος εγκατάστασης	Importer un installateur	Installationsprogramm importieren
Rimuovi dal catalogo	Remove from catalog	从目录中移除	カタログから削除	카탈로그에서 제거	Quitar del catálogo	Katalogdan kaldır	Удалить из каталога	Αφαίρεση από κατάλογο	Retirer du catalogue	Aus Katalog entfernen
Usa per l'app selezionata	Use for selected app	用于所选应用	選択したアプリに使用	선택한 앱에 사용	Usar para la aplicación seleccionada	Seçili uygulama için kullan	Использовать для выбранного приложения	Χρήση για την επιλεγμένη εφαρμογή	Utiliser pour l'application sélectionnée	Für ausgewählte App verwenden
Attività e rapporti	Activity and reports	活动与报告	アクティビティとレポート	활동 및 보고서	Actividad e informes	Etkinlik ve raporlar	Действия и отчёты	Δραστηριότητα και αναφορές	Activité et rapports	Aktivität und Berichte
Cronologia sessione	Session history	会话历史	セッション履歴	세션 기록	Historial de la sesión	Oturum geçmişi	История сеанса	Ιστορικό συνεδρίας	Historique de session	Sitzungsverlauf
Ultimo rapporto	Latest report	最新报告	最新のレポート	최신 보고서	Último informe	Son rapor	Последний отчёт	Τελευταία αναφορά	Dernier rapport	Letzter Bericht
Aggiorna	Refresh	刷新	更新	새로 고침	Actualizar	Yenile	Обновить	Ανανέωση	Actualiser	Aktualisieren
Rilevato	Detected	已检测	検出済み	검색됨	Detectado	Algılandı	Обнаружено	Ανιχνεύθηκε	Détectée	Erkannt
Archiviabile	Archivable	可归档	アーカイブ可能	보관 가능	Archivable	Arşivlenebilir	Можно архивировать	Αρχειοθετήσιμο	Archivable	Archivierbar
Reinstallabile online	Online reinstallable	可在线重新安装	オンライン再インストール可能	온라인 재설치 가능	Reinstalable en línea	Çevrimiçi yeniden yüklenebilir	Онлайн-переустановка	Επανεγκατάσταση μέσω διαδικτύου	Réinstallation en ligne	Online neu installierbar
Reinstallabile offline	Offline reinstallable	可离线重新安装	オフライン再インストール可能	오프라인 재설치 가능	Reinstalable sin conexión	Çevrimdışı yeniden yüklenebilir	Офлайн-переустановка	Επανεγκατάσταση εκτός σύνδεσης	Réinstallation hors ligne	Offline neu installierbar
Migrazione verificata	Verified migration	已验证迁移	検証済み移行	검증된 마이그레이션	Migración verificada	Doğrulanmış geçiş	Проверенная миграция	Επαληθευμένη μετεγκατάσταση	Migration vérifiée	Verifizierte Migration
Script personale	Personal script	个人脚本	個人用スクリプト	개인 스크립트	Script personal	Kişisel betik	Личный скрипт	Προσωπικό σενάριο	Script personnel	Persönliches Skript
Non selezionato	Not selected	未选择	未選択	선택 안 됨	No seleccionado	Seçilmedi	Не выбрано	Δεν έχει επιλεγεί	Non sélectionné	Nicht ausgewählt
Nel backup	In backup	在备份中	バックアップに含む	백업에 포함	En la copia	Yedekte	В резервной копии	Στο αντίγραφο	Dans la sauvegarde	In der Sicherung
Gioco escluso	Game excluded	游戏已排除	除外されたゲーム	제외된 게임	Juego excluido	Oyun hariç tutuldu	Игра исключена	Εξαιρούμενο παιχνίδι	Jeu exclu	Spiel ausgeschlossen
Da calcolare	To calculate	待计算	計算予定	계산 예정	Por calcular	Hesaplanacak	Будет рассчитано	Προς υπολογισμό	À calculer	Zu berechnen
Firma rilevata	Signature detected	已检测到签名	署名を検出	서명 감지됨	Firma detectada	İmza algılandı	Подпись обнаружена	Εντοπίστηκε υπογραφή	Signature détectée	Signatur erkannt
Firma non verificata	Unverified signature	签名未验证	未検証の署名	확인되지 않은 서명	Firma no verificada	Doğrulanmamış imza	Непроверенная подпись	Μη επαληθευμένη υπογραφή	Signature non vérifiée	Unbestätigte Signatur
Lingua dell'applicazione	Application language	应用程序语言	アプリの言語	앱 언어	Idioma de la aplicación	Uygulama dili	Язык приложения	Γλώσσα εφαρμογής	Langue de l'application	App-Sprache
La scelta viene applicata subito e memorizzata per i prossimi avvii.	The choice is applied immediately and saved for future launches.	选择会立即应用，并保存供下次启动使用。	選択はすぐに適用され、次回以降の起動用に保存されます。	선택한 언어가 즉시 적용되고 다음 실행을 위해 저장됩니다.	La opción se aplica de inmediato y se guarda para futuros inicios.	Seçim hemen uygulanır ve sonraki açılışlar için kaydedilir.	Выбор применяется сразу и сохраняется для следующих запусков.	Η επιλογή εφαρμόζεται αμέσως και αποθηκεύεται για τις επόμενες εκκινήσεις.	Le choix s'applique immédiatement et est enregistré pour les prochains démarrages.	Die Auswahl wird sofort angewendet und für künftige Starts gespeichert.
{0} applicazioni · {1} stimati	{0} apps · {1} estimated	{0} 个应用 · 预计 {1}	{0} 個のアプリ · 推定 {1}	앱 {0}개 · 예상 {1}	{0} aplicaciones · {1} estimados	{0} uygulama · tahmini {1}	Приложений: {0} · оценка {1}	{0} εφαρμογές · εκτίμηση {1}	{0} applications · {1} estimés	{0} Apps · geschätzt {1}
Seleziona almeno un'applicazione nell'inventario.	Select at least one app in the inventory.	请在清单中至少选择一个应用。	インベントリで少なくとも 1 つのアプリを選択してください。	인벤토리에서 앱을 하나 이상 선택하세요.	Selecciona al menos una aplicación del inventario.	Envanterden en az bir uygulama seçin.	Выберите хотя бы одно приложение в инвентаре.	Επιλέξτε τουλάχιστον μία εφαρμογή στην απογραφή.	Sélectionnez au moins une application dans l'inventaire.	Wählen Sie mindestens eine App im Inventar aus.
La password deve contenere almeno 10 caratteri.	The password must contain at least 10 characters.	密码至少需要 10 个字符。	パスワードは 10 文字以上必要です。	암호는 10자 이상이어야 합니다.	La contraseña debe tener al menos 10 caracteres.	Parola en az 10 karakter olmalıdır.	Пароль должен содержать не менее 10 символов.	Ο κωδικός πρέπει να έχει τουλάχιστον 10 χαρακτήρες.	Le mot de passe doit contenir au moins 10 caractères.	Das Kennwort muss mindestens 10 Zeichen enthalten.
Le password non coincidono.	Passwords do not match.	密码不一致。	パスワードが一致しません。	암호가 일치하지 않습니다.	Las contraseñas no coinciden.	Parolalar eşleşmiyor.	Пароли не совпадают.	Οι κωδικοί δεν ταιριάζουν.	Les mots de passe ne correspondent pas.	Die Kennwörter stimmen nicht überein.
Scegli una destinazione.	Choose a destination.	选择目标位置。	保存先を選択してください。	대상을 선택하세요.	Elige un destino.	Bir hedef seçin.	Выберите назначение.	Επιλέξτε προορισμό.	Choisissez une destination.	Wählen Sie ein Ziel.
L'orario pianificato deve usare il formato HH:mm.	The scheduled time must use HH:mm format.	计划时间必须使用 HH:mm 格式。	スケジュール時刻は HH:mm 形式で指定してください。	예약 시간은 HH:mm 형식이어야 합니다.	La hora programada debe usar el formato HH:mm.	Zamanlanmış saat HH:mm biçiminde olmalıdır.	Запланированное время должно иметь формат HH:mm.	Η προγραμματισμένη ώρα πρέπει να έχει μορφή HH:mm.	L'heure planifiée doit utiliser le format HH:mm.	Die geplante Zeit muss das Format HH:mm verwenden.
Pronto per la verifica e il backup.	Ready for verification and backup.	已准备好验证和备份。	検証とバックアップの準備ができました。	검증 및 백업 준비가 완료되었습니다.	Listo para verificar y crear la copia.	Doğrulama ve yedekleme için hazır.	Готово к проверке и резервному копированию.	Έτοιμο για επαλήθευση και δημιουργία αντιγράφου.	Prêt pour la vérification et la sauvegarde.	Bereit für Prüfung und Sicherung.
Nessuna icona locale disponibile.	No local icon available.	没有可用的本地图标。	ローカル アイコンはありません。	사용 가능한 로컬 아이콘이 없습니다.	No hay icono local disponible.	Yerel simge kullanılamıyor.	Локальный значок недоступен.	Δεν υπάρχει διαθέσιμο τοπικό εικονίδιο.	Aucune icône locale disponible.	Kein lokales Symbol verfügbar.
Icona dell'installazione.	Icon from installed app.	来自已安装应用的图标。	インストール済みアプリのアイコン。	설치된 앱의 아이콘.	Icono de la aplicación instalada.	Yüklü uygulama simgesi.	Значок установленного приложения.	Εικονίδιο εγκατεστημένης εφαρμογής.	Icône de l'application installée.	Symbol der installierten App.
Inventario applicazioni	Application inventory	应用程序清单	アプリのインベントリ	애플리케이션 인벤토리	Inventario de aplicaciones	Uygulama envanteri	Инвентарь приложений	Απογραφή εφαρμογών	Inventaire des applications	Anwendungsinventar
Configura backup	Configure backup	配置备份	バックアップを設定	백업 구성	Configurar copia	Yedeklemeyi yapılandır	Настройка резервного копирования	Ρύθμιση αντιγράφου	Configurer la sauvegarde	Sicherung konfigurieren
Libreria pacchetti	Package library	软件包库	パッケージ ライブラリ	패키지 라이브러리	Biblioteca de paquetes	Paket kitaplığı	Библиотека пакетов	Βιβλιοθήκη πακέτων	Bibliothèque de paquets	Paketbibliothek
Nessun aggiornamento disponibile	No update available	没有可用更新	利用可能な更新はありません	사용 가능한 업데이트 없음	No hay actualizaciones disponibles	Güncelleme yok	Обновлений нет	Δεν υπάρχει διαθέσιμη ενημέρωση	Aucune mise à jour disponible	Keine Aktualisierung verfügbar
MigraWin {0} è aggiornato.	MigraWin {0} is up to date.	MigraWin {0} 已是最新版本。	MigraWin {0} は最新です。	MigraWin {0}이(가) 최신 상태입니다.	MigraWin {0} está actualizado.	MigraWin {0} güncel.	MigraWin {0} обновлён.	Το MigraWin {0} είναι ενημερωμένο.	MigraWin {0} est à jour.	MigraWin {0} ist aktuell.
Controllo delle release GitHub…	Checking GitHub releases…	正在检查 GitHub 发布版本…	GitHub リリースを確認しています…	GitHub 릴리스 확인 중…	Comprobando versiones de GitHub…	GitHub sürümleri denetleniyor…	Проверка выпусков GitHub…	Έλεγχος εκδόσεων GitHub…	Vérification des versions GitHub…	GitHub-Versionen werden geprüft…
Download dell'aggiornamento…	Downloading update…	正在下载更新…	更新をダウンロードしています…	업데이트 다운로드 중…	Descargando actualización…	Güncelleme indiriliyor…	Загрузка обновления…	Λήψη ενημέρωσης…	Téléchargement de la mise à jour…	Update wird heruntergeladen…
Avvio dell'installer…	Starting installer…	正在启动安装程序…	インストーラーを起動しています…	설치 관리자 시작 중…	Iniciando instalador…	Yükleyici başlatılıyor…	Запуск установщика…	Εκκίνηση προγράμματος εγκατάστασης…	Démarrage de l'installateur…	Installationsprogramm wird gestartet…
1  Manifest e schemi versionati	1 Versioned manifest and schemas
2  Acquisizione in area temporanea	2 Staging-area capture
3  Verifica di identità e hash	3 Identity and hash verification
4  Pubblicazione atomica nel repository	4 Atomic repository publication
Aggiungi cartella…	Add folder…
Analizza PC	Analyze PC
ANTEPRIMA	PREVIEW
Applicazioni	Applications
Apri il sito ufficiale dell'applicazione	Open the application's official website
Archivio locale	Local archive
Archivio verificato prima del commit	Archive verified before commit
Associa le origini ai percorsi del nuovo PC.	Map sources to paths on the new PC.
Avanzamento, errori e rapporti esportabili in Markdown e JSON.	Progress, errors and reports exportable as Markdown and JSON.
Avvio come utente normale. Le operazioni che richiedono privilegi elevati devono passare da un installer o da un'azione esplicita del sistema.	Runs as a standard user. Operations requiring elevation must go through an installer or an explicit system action.
Azzera	Reset
Cache e file temporanei sono esclusi in questa versione.	Cache and temporary files are excluded in this version.
Cartella dati locale	Local data folder
Cerca applicazioni	Search applications
Cerca per nome, produttore o versione	Search by name, publisher or version
Cerca per nome, produttore o versione…	Search by name, publisher or version…
Come viene letto	How it is read
Conferma nuova password repository	Confirm new repository password
Conferma password backup	Confirm backup password
Contenuto ultimo rapporto	Latest report content
Contenuto, recuperabilità e conservazione restano scelte separate.	Content, recoverability and retention remain separate choices.
Controllo finale	Final review
Dal sito ufficiale	From the official website
Dimensione logica	Logical size
Entrambe le modalità producono un manifest autonomo; gli oggetti identici vengono conservati una sola volta.	Both modes produce a self-contained manifest; identical objects are stored only once.
Escludi cartella…	Exclude folder…
Esegue il profilo ogni giorno mentre l'utente Windows è disponibile.	Runs the profile daily while the Windows user is available.
File logici	Logical files
Filtro applicazioni	Application filter
I file sostituiti vengono copiati nella cartella di rollback locale. Installazioni e attivazioni esterne possono non essere interamente annullabili.	Replaced files are copied to the local rollback folder. External installations and activations may not be fully reversible.
I manifest restano cifrati finché non inserisci la chiave corretta.	Manifests remain encrypted until you enter the correct key.
Il confronto viene salvato in Markdown insieme agli altri rapporti.	The comparison is saved as Markdown with the other reports.
Il programma non nasconde un limite dietro un'etichetta di successo.	The program does not hide a limitation behind a success label.
Il tema segue Windows per impostazione iniziale; le risorse ad alto contrasto hanno precedenza.	The theme follows Windows initially; high-contrast resources take precedence.
Immagini promozionali fornite dalla pagina ufficiale.	Promotional images provided by the official page.
Importa installer posseduti, controlla hash e firma, associandoli alle applicazioni corrette.	Import installers you own, verify hash and signature, and associate them with the correct applications.
Importa un pacchetto e associalo a un'applicazione selezionata.	Import a package and associate it with a selected application.
Includi nel backup	Include in backup
Individua applicazioni, versioni e contenuti che differiscono tra due punti di ripristino.	Find applications, versions and content that differ between two restore points.
Installer importati	Imported installers
Installer, script e programmi rilevati non vengono avviati.	Detected installers, scripts and programs are not started.
Integrità non significa autorizzazione	Integrity does not mean authorization
L'avvio è interattivo e richiede una tua conferma successiva nel programma di installazione.	Launch is interactive and requires your later confirmation in the installer.
L'eliminazione rimuove il manifest e solo gli oggetti non raggiungibili dagli altri backup.	Deletion removes the manifest and only objects that are no longer referenced by other backups.
La copia appena verificata e quella protetta non vengono mai eliminate automaticamente.	The newly verified copy and the protected copy are never deleted automatically.
La copia dei file installati può occupare molto spazio, soprattutto per applicazioni e strumenti Node.js.	Copying installed files can use substantial space, especially for applications and Node.js tools.
La destinazione non viene cancellata senza una scelta esplicita.	The destination is not deleted without an explicit choice.
La password protegge la chiave dei contenuti; non viene mai salvata nel profilo.	The password protects the content key and is never saved in the profile.
La password viene protetta con DPAPI per l'utente corrente. Non viene scritta nell'attività né esportata nel profilo.	The password is protected with DPAPI for the current user. It is not written to the task or exported in the profile.
La scansione legge metadati dei pacchetti installati. Non avvia programmi, script o installer.	The scan reads installed package metadata. It does not start programs, scripts or installers.
La scelta viene applicata a tutte le applicazioni, poi puoi personalizzarla nell'inventario.	The choice is applied to all applications and can then be customized in the inventory.
La selezione è per applicazione e per componente. I giochi non possono essere inclusi automaticamente.	Selection is by application and component. Games cannot be included automatically.
Le operazioni non presidiate restano visibili.	Unattended operations remain visible.
Locale · cifrato · verificato	Local · encrypted · verified
Mantieni gli ultimi 10 backup	Keep the last 10 backups
Mantieni gli ultimi 20 backup	Keep the last 20 backups
Mantieni gli ultimi 50 backup	Keep the last 50 backups
Mostrata solo alla creazione del repository. Conservala fuori dal disco di backup.	Shown only when the repository is created. Keep it off the backup disk.
Nessun installer importato	No installers imported
Nessun profilo salvato	No saved profile
Nessuna inclusione automatica	No automatic inclusion
Nessuna modifica durante l'analisi	No changes during analysis
Non applicare una politica automatica	Do not apply an automatic retention policy
Nuova password repository	New repository password
Oggetti cifrati, manifest autenticato e nessuna copia parziale visibile.	Encrypted objects, authenticated manifest and no visible partial copy.
Ogni punto è autonomo nel manifest; gli oggetti condivisi sono protetti dalla deduplicazione.	Each point is self-contained in the manifest; shared objects are protected by deduplication.
Ora del backup pianificato	Scheduled backup time
Password backup	Backup password
Percorso di destinazione	Destination path
Politica di conservazione	Retention policy
Politica per i conflitti	Conflict policy
Preferenze locali senza account e senza invio di contenuti a servizi esterni.	Local preferences without an account and without sending content to external services.
Prima del commit	Before commit
Profili, catalogo pacchetti e rapporti dell'ultima operazione restano su questo PC. Le password non vengono archiviate.	Profiles, package catalog and reports from the last operation stay on this PC. Passwords are not stored.
Profilo salvato	Saved profile
Questi valori vengono applicati a ogni nuova analisi e ai programmi portabili aggiunti; il profilo Completo supportato li rispetta. Le modifiche manuali nella singola applicazione restano valide fino alla scansione successiva.	These values apply to every new scan and to added portable programs; the Full supported profile respects them. Manual changes to individual applications remain until the next scan.
Qui puoi personalizzare esattamente quali componenti includere.	Customize exactly which components to include.
Rapporti: Documenti\MigraWin\Reports	Reports: Documents\MigraWin\Reports
Repository cifrato	Encrypted repository
Rileva script nella cartella utente e strumenti locali come PM2, n8n, Pi Agent e proxy Codex. I processi non vengono avviati.	Detects scripts in the user folder and local tools such as PM2, n8n, Pi Agent and the Codex proxy. Processes are not started.
Rimuovi pianificazione	Remove schedule
Ripristino selettivo e rollback	Selective restore and rollback
Scegli applicazioni e componenti. Le modifiche avvengono solo dopo la conferma del piano.	Choose applications and components. Changes occur only after plan confirmation.
Se non vedi un componente, torna all'inventario e personalizza l'applicazione.	If a component is missing, return to the inventory and customize the application.
Seleziona un'applicazione	Select an application
Sfoglia…	Browse…
Snapshot di arrivo	Destination snapshot
Snapshot di partenza	Source snapshot
Tema dell'applicazione	Application theme
Un file di configurazione può contenere token o password. MigraWin non dichiara trasferibile un segreto non verificato.	A configuration file may contain tokens or passwords. MigraWin does not claim an unverified secret is transferable.
Un hash conferma che il file non è cambiato rispetto all'importazione. La firma viene rilevata, ma l'esecuzione richiede sempre la tua approvazione.	A hash confirms that the file has not changed since import. The signature is detected, but execution always requires your approval.
Una copia preventiva protegge i file sostituiti sulla destinazione.	A rollback copy protects files replaced at the destination.
Una voce rimossa significa che non compare nella copia di arrivo. Non implica che sia stata cancellata dal PC dopo il punto di ripristino.	A removed entry means it does not appear in the destination copy. It does not imply deletion from the PC after the restore point.
Pronto per analizzare il computer.	Ready to analyze the computer.
Nessuna pianificazione configurata.	No schedule configured.
Controllo aggiornamenti non ancora eseguito.	Update check has not run yet.
Apri un repository e confronta due punti di ripristino.	Open a repository and compare two restore points.
Carica un backup per analizzare la destinazione.	Load a backup to analyze the destination.
Versione {0} disponibile	Version {0} available
Disponibile MigraWin {0}. L'installazione richiede una tua conferma.	MigraWin {0} is available. Installation requires your confirmation.
Controllo aggiornamenti non disponibile: {0}	Update check unavailable: {0}
Download installer: {0}	Installer download: {0}
Aggiornamento non completato: {0}	Update not completed: {0}
""";
}
