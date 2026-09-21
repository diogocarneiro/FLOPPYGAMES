using System.Globalization;
using System.Resources;

namespace FloppyGames.Core.Localization;

/// <summary>
/// Acesso fortemente tipado (escrito à mão, não gerado) às strings traduzidas em
/// <c>Strings.resx</c> (neutro = Francês, o idioma por omissão) e satélites
/// <c>Strings.en/pt/es/it.resx</c>. Resolve sempre contra <see cref="CultureInfo.CurrentUICulture"/>.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Manager =
        new("FloppyGames.Core.Localization.Strings", typeof(Strings).Assembly);

    private static string Get(string key) => Manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    private static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    // Splash
    public static string Splash_Preparing => Get("Splash_Preparing");
    public static string Splash_FloppyDetected => Get("Splash_FloppyDetected");
    public static string Splash_UsbDetected => Get("Splash_UsbDetected");
    public static string Splash_StatSupport => Get("Splash_StatSupport");
    public static string Splash_StatBuild => Get("Splash_StatBuild");
    public static string Splash_StatUpdated => Get("Splash_StatUpdated");
    public static string Splash_StatPlaytime => Get("Splash_StatPlaytime");
    public static string Splash_StatLastSession => Get("Splash_StatLastSession");
    public static string Splash_StatAchievements => Get("Splash_StatAchievements");
    public static string Splash_Checking => Get("Splash_Checking");
    public static string Splash_Installed => Get("Splash_Installed");
    public static string Splash_NotInstalled => Get("Splash_NotInstalled");
    public static string Splash_UnknownDate => Get("Splash_UnknownDate");
    public static string Splash_PlaytimeUnavailable => Get("Splash_PlaytimeUnavailable");
    public static string Splash_LaunchingSteam => Get("Splash_LaunchingSteam");
    public static string Splash_LaunchingEpic => Get("Splash_LaunchingEpic");
    public static string Splash_LaunchingGog => Get("Splash_LaunchingGog");
    public static string Splash_LaunchingGeneric => Get("Splash_LaunchingGeneric");
    public static string Splash_GameRunning(string title) => Format("Splash_GameRunning", title);

    // MainWindow (Agent)
    public static string MainWindow_Subtitle => Get("MainWindow_Subtitle");
    public static string MainWindow_Watching => Get("MainWindow_Watching");
    public static string MainWindow_MediaInserted(string drive, string title, string platform, string process) =>
        Format("MainWindow_MediaInserted", drive, title, platform, process);
    public static string MainWindow_MediaRemoved(string drive, string title) => Format("MainWindow_MediaRemoved", drive, title);
    public static string MainWindow_InvalidGameIni(string drive, string errors) => Format("MainWindow_InvalidGameIni", drive, errors);
    public static string MainWindow_Launching(string title) => Format("MainWindow_Launching", title);
    public static string MainWindow_LaunchConfirmed(string title) => Format("MainWindow_LaunchConfirmed", title);
    public static string MainWindow_LaunchFailed(string title, string reason) => Format("MainWindow_LaunchFailed", title, reason);
    public static string MainWindow_GameStopped(string title) => Format("MainWindow_GameStopped", title);

    // Settings window
    public static string Settings_Autostart => Get("Settings_Autostart");
    public static string Settings_FloppySound => Get("Settings_FloppySound");
    public static string Settings_CrtEffect => Get("Settings_CrtEffect");
    public static string Settings_LanguageLabel => Get("Settings_LanguageLabel");
    public static string Settings_LanguageRestartNote => Get("Settings_LanguageRestartNote");
    public static string Settings_LogsFolder => Get("Settings_LogsFolder");
    public static string Settings_OpenButton => Get("Settings_OpenButton");
    public static string Settings_SteamApiKeyLabel => Get("Settings_SteamApiKeyLabel");
    public static string Settings_SteamApiKeyDescription => Get("Settings_SteamApiKeyDescription");
    public static string Settings_SaveButton => Get("Settings_SaveButton");
    public static string Settings_GetApiKeyLink => Get("Settings_GetApiKeyLink");
    public static string Settings_AutostartEnabled => Get("Settings_AutostartEnabled");
    public static string Settings_AutostartDisabled => Get("Settings_AutostartDisabled");
    public static string Settings_ApiKeyRemoved => Get("Settings_ApiKeyRemoved");
    public static string Settings_ApiKeySaved => Get("Settings_ApiKeySaved");
    public static string Settings_FloppySoundEnabled => Get("Settings_FloppySoundEnabled");
    public static string Settings_FloppySoundDisabled => Get("Settings_FloppySoundDisabled");
    public static string Settings_CrtEffectEnabled => Get("Settings_CrtEffectEnabled");
    public static string Settings_CrtEffectDisabled => Get("Settings_CrtEffectDisabled");

    // Tray icon
    public static string Tray_OpenFloppyGames => Get("Tray_OpenFloppyGames");
    public static string Tray_OpenLogsFolder => Get("Tray_OpenLogsFolder");
    public static string Tray_OpenLabelStudio => Get("Tray_OpenLabelStudio");
    public static string Tray_Settings => Get("Tray_Settings");
    public static string Tray_Exit => Get("Tray_Exit");
    public static string Tray_Idle => Get("Tray_Idle");
    public static string Tray_Launching => Get("Tray_Launching");
    public static string Tray_OneRunning => Get("Tray_OneRunning");
    public static string Tray_ManyRunning(int count) => Format("Tray_ManyRunning", count);

    // LabelStudioLauncher
    public static string LabelStudioLauncher_NotInstalled => Get("LabelStudioLauncher_NotInstalled");

    // Launch failure reasons
    public static string LaunchFailed_Timeout => Get("LaunchFailed_Timeout");
    public static string LaunchFailed_MediaRemoved => Get("LaunchFailed_MediaRemoved");
    public static string LaunchFailed_UnexpectedError => Get("LaunchFailed_UnexpectedError");

    // Label Studio MainWindow
    public static string LS_Step1 => Get("LS_Step1");
    public static string LS_Platform => Get("LS_Platform");
    public static string LS_InstalledGames => Get("LS_InstalledGames");
    public static string LS_Step2 => Get("LS_Step2");
    public static string LS_TitleField => Get("LS_TitleField");
    public static string LS_ProcessField => Get("LS_ProcessField");
    public static string LS_DescriptionField => Get("LS_DescriptionField");
    public static string LS_AdvancedOptions => Get("LS_AdvancedOptions");
    public static string LS_WatchTimeoutField => Get("LS_WatchTimeoutField");
    public static string LS_LaunchDelayField => Get("LS_LaunchDelayField");
    public static string LS_GracefulShutdownField => Get("LS_GracefulShutdownField");
    public static string LS_Step3 => Get("LS_Step3");
    public static string LS_Step4 => Get("LS_Step4");
    public static string LS_TargetDrive => Get("LS_TargetDrive");
    public static string LS_RefreshButton => Get("LS_RefreshButton");
    public static string LS_WriteButton => Get("LS_WriteButton");
    public static string LS_PrintButton => Get("LS_PrintButton");
    public static string LS_ChooseLocalCoverButton => Get("LS_ChooseLocalCoverButton");
    public static string LS_LoadingLibrary(string platform) => Format("LS_LoadingLibrary", platform);
    public static string LS_NoGamesFound(string platform) => Format("LS_NoGamesFound", platform);
    public static string LS_GamesFoundOne => Get("LS_GamesFoundOne");
    public static string LS_GamesFoundMany(int count) => Format("LS_GamesFoundMany", count);
    public static string LS_LoadLibraryFailed => Get("LS_LoadLibraryFailed");
    public static string LS_IdentifierLabel => Get("LS_IdentifierLabel");
    public static string LS_IdentifierLabelSteam => Get("LS_IdentifierLabelSteam");
    public static string LS_IdentifierLabelEpic => Get("LS_IdentifierLabelEpic");
    public static string LS_IdentifierLabelGog => Get("LS_IdentifierLabelGog");
    public static string LS_FetchingSteamCover => Get("LS_FetchingSteamCover");
    public static string LS_NoAutoCoverForPlatform(string platform) => Format("LS_NoAutoCoverForPlatform", platform);
    public static string LS_NoCoverFoundSteam => Get("LS_NoCoverFoundSteam");
    public static string LS_ChooseCoverDialogTitle => Get("LS_ChooseCoverDialogTitle");
    public static string LS_ImagesFilterWord => Get("LS_ImagesFilterWord");
    public static string LS_FilledFromCatalog => Get("LS_FilledFromCatalog");
    public static string LS_ImageReadFailed => Get("LS_ImageReadFailed");
    public static string LS_ChooseTargetDrive => Get("LS_ChooseTargetDrive");
    public static string LS_TitleProcessRequired => Get("LS_TitleProcessRequired");
    public static string LS_WatchTimeoutInvalid => Get("LS_WatchTimeoutInvalid");
    public static string LS_LaunchDelayInvalid => Get("LS_LaunchDelayInvalid");
    public static string LS_WriteCancelled => Get("LS_WriteCancelled");
    public static string LS_WriteSuccess(string title, string drive) => Format("LS_WriteSuccess", title, drive);
    public static string LS_WriteFailed => Get("LS_WriteFailed");

    // LabelPrintWindow
    public static string LPW_PrintButton => Get("LPW_PrintButton");
    public static string LPW_ExportPngButton => Get("LPW_ExportPngButton");
    public static string LPW_PrintedSuccess => Get("LPW_PrintedSuccess");
    public static string LPW_ExportedSuccess(string path) => Format("LPW_ExportedSuccess", path);
    public static string LPW_PngImageWord => Get("LPW_PngImageWord");

    // Core: FloppyMediaWriter
    public static string Core_MediaWriter_NotRemovable => Get("Core_MediaWriter_NotRemovable");
    public static string Core_MediaWriter_InsufficientSpace(string required, string free) =>
        Format("Core_MediaWriter_InsufficientSpace", required, free);
    public static string Core_MediaWriter_ExistingGameIni => Get("Core_MediaWriter_ExistingGameIni");

    // Core: GameIniParser
    public static string Core_Parser_UnknownPlatform(string raw) => Format("Core_Parser_UnknownPlatform", raw);
    public static string Core_Parser_MissingRequiredField(string field) => Format("Core_Parser_MissingRequiredField", field);
    public static string Core_Parser_MustBePositiveInt(string field, string raw) => Format("Core_Parser_MustBePositiveInt", field, raw);
    public static string Core_Parser_OptionsMustBePositiveInt(string field, string raw) =>
        Format("Core_Parser_OptionsMustBePositiveInt", field, raw);
    public static string Core_Parser_OptionsMustBeNonNegativeInt(string field, string raw) =>
        Format("Core_Parser_OptionsMustBeNonNegativeInt", field, raw);
    public static string Core_Parser_OptionsMustBeBool(string field, string raw) =>
        Format("Core_Parser_OptionsMustBeBool", field, raw);
}
