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
    public static string Splash_NfcDetected => Get("Splash_NfcDetected");
    public static string Splash_StatCardId => Get("Splash_StatCardId");

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
    public static string MainWindow_CardInserted(string uid, string title, string platform, string process) =>
        Format("MainWindow_CardInserted", uid, title, platform, process);
    public static string MainWindow_CardRemoved(string uid, string title) => Format("MainWindow_CardRemoved", uid, title);
    public static string MainWindow_InvalidCard(string uid, string errors) => Format("MainWindow_InvalidCard", uid, errors);

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
    public static string Settings_PlatformsLabel => Get("Settings_PlatformsLabel");
    public static string Settings_PlatformsDescription => Get("Settings_PlatformsDescription");
    public static string Settings_PlatformEnabled(string platform) => Format("Settings_PlatformEnabled", platform);
    public static string Settings_PlatformDisabled(string platform) => Format("Settings_PlatformDisabled", platform);
    public static string Settings_NfcEnabled => Get("Settings_NfcEnabled");
    public static string Settings_NfcDescription => Get("Settings_NfcDescription");
    public static string Settings_NfcEnabledOn => Get("Settings_NfcEnabledOn");
    public static string Settings_NfcEnabledOff => Get("Settings_NfcEnabledOff");
    public static string Settings_NfcCardPasswordLabel => Get("Settings_NfcCardPasswordLabel");
    public static string Settings_NfcCardPasswordDescription => Get("Settings_NfcCardPasswordDescription");
    public static string Settings_NfcCardPasswordSaved => Get("Settings_NfcCardPasswordSaved");
    public static string Settings_NfcCardPasswordRemoved => Get("Settings_NfcCardPasswordRemoved");
    public static string Settings_UpdatesLabel => Get("Settings_UpdatesLabel");
    public static string Settings_UpdatesDescription => Get("Settings_UpdatesDescription");
    public static string Settings_CheckForUpdatesCheckbox => Get("Settings_CheckForUpdatesCheckbox");
    public static string Settings_CheckForUpdatesNowButton => Get("Settings_CheckForUpdatesNowButton");
    public static string Settings_CheckForUpdatesEnabledOn => Get("Settings_CheckForUpdatesEnabledOn");
    public static string Settings_CheckForUpdatesEnabledOff => Get("Settings_CheckForUpdatesEnabledOff");
    public static string Settings_UpdateStatusChecking => Get("Settings_UpdateStatusChecking");
    public static string Settings_UpdateStatusUpToDate(string version) => Format("Settings_UpdateStatusUpToDate", version);
    public static string Settings_UpdateStatusAvailable(string version) => Format("Settings_UpdateStatusAvailable", version);
    public static string Settings_UpdateStatusFailed => Get("Settings_UpdateStatusFailed");
    public static string Settings_InstallUpdateButton => Get("Settings_InstallUpdateButton");
    public static string Settings_UpdateDownloading(int percent) => Format("Settings_UpdateDownloading", percent);
    public static string Settings_UpdateDownloadFailed => Get("Settings_UpdateDownloadFailed");
    public static string Settings_UpdateInstallStarting => Get("Settings_UpdateInstallStarting");
    public static string Settings_AboutLabel => Get("Settings_AboutLabel");
    public static string Settings_AboutDescription => Get("Settings_AboutDescription");
    public static string Settings_AboutCreatedBy => Get("Settings_AboutCreatedBy");

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
    public static string Tray_UpdateAvailable(string version) => Format("Tray_UpdateAvailable", version);
    public static string Tray_UpdateBalloonTitle => Get("Tray_UpdateBalloonTitle");
    public static string Tray_UpdateBalloonText(string version) => Format("Tray_UpdateBalloonText", version);

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
    public static string LS_FetchingCover(string platform) => Format("LS_FetchingCover", platform);
    public static string LS_NoCoverFound(string platform) => Format("LS_NoCoverFound", platform);
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
    public static string LS_TargetTypeDrive => Get("LS_TargetTypeDrive");
    public static string LS_TargetTypeNfc => Get("LS_TargetTypeNfc");
    public static string LS_Nfc_NoReaderDetected => Get("LS_Nfc_NoReaderDetected");
    public static string LS_Nfc_ReaderDetected(string readerName) => Format("LS_Nfc_ReaderDetected", readerName);
    public static string LS_Nfc_WaitingForCard => Get("LS_Nfc_WaitingForCard");
    public static string LS_Nfc_CardDetected(string uid, string cardType) => Format("LS_Nfc_CardDetected", uid, cardType);
    public static string LS_Nfc_CardTypeUnknown => Get("LS_Nfc_CardTypeUnknown");
    public static string LS_Drive_WriteProgress(int percent) => Format("LS_Drive_WriteProgress", percent);
    public static string LS_Nfc_ReadingCard => Get("LS_Nfc_ReadingCard");
    public static string LS_Nfc_CardContentGame(string title, string platform) => Format("LS_Nfc_CardContentGame", title, platform);
    public static string LS_Nfc_CardContentEmpty => Get("LS_Nfc_CardContentEmpty");
    public static string LS_Nfc_CardContentLocked => Get("LS_Nfc_CardContentLocked");
    public static string LS_Nfc_CardContentInvalid => Get("LS_Nfc_CardContentInvalid");
    public static string LS_Nfc_CardContentReadFailed => Get("LS_Nfc_CardContentReadFailed");
    public static string LS_Nfc_EraseButton => Get("LS_Nfc_EraseButton");
    public static string LS_Nfc_EraseConfirm => Get("LS_Nfc_EraseConfirm");
    public static string LS_Nfc_EraseMagicConfirm => Get("LS_Nfc_EraseMagicConfirm");
    public static string LS_Nfc_Erasing => Get("LS_Nfc_Erasing");
    public static string LS_Nfc_EraseSuccess => Get("LS_Nfc_EraseSuccess");
    public static string LS_Nfc_EraseFailed => Get("LS_Nfc_EraseFailed");
    public static string LS_Nfc_ReplaceGameConfirm(string title) => Format("LS_Nfc_ReplaceGameConfirm", title);
    public static string LS_Nfc_RefreshReaderButton => Get("LS_Nfc_RefreshReaderButton");
    public static string LS_Nfc_WriteSuccess(string title, string uid) => Format("LS_Nfc_WriteSuccess", title, uid);
    public static string LS_Nfc_WriteFailed => Get("LS_Nfc_WriteFailed");
    public static string LS_Nfc_NoCardPresent => Get("LS_Nfc_NoCardPresent");
    public static string LS_Nfc_CoverNotWritten => Get("LS_Nfc_CoverNotWritten");
    public static string LS_Nfc_FormatButton => Get("LS_Nfc_FormatButton");
    public static string LS_Nfc_FormatConfirm => Get("LS_Nfc_FormatConfirm");
    public static string LS_Nfc_FormatFailed => Get("LS_Nfc_FormatFailed");
    public static string LS_Nfc_Checking => Get("LS_Nfc_Checking");
    public static string LS_Nfc_WriteProgress(int current, int total) => Format("LS_Nfc_WriteProgress", current, total);
    public static string LS_Nfc_ProtectCheckbox => Get("LS_Nfc_ProtectCheckbox");
    public static string LS_Nfc_ProtectNoPasswordHint => Get("LS_Nfc_ProtectNoPasswordHint");
    public static string LS_Nfc_ProtectConfirm => Get("LS_Nfc_ProtectConfirm");
    public static string LS_Nfc_Protecting => Get("LS_Nfc_Protecting");
    public static string LS_Nfc_ProtectSuccess(string uid) => Format("LS_Nfc_ProtectSuccess", uid);
    public static string LS_Nfc_ProtectFailed => Get("LS_Nfc_ProtectFailed");

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

    // Core: Nfc
    public static string Core_Nfc_UnsupportedCardType => Get("Core_Nfc_UnsupportedCardType");
    public static string Core_Nfc_TooLarge(string required, string available) => Format("Core_Nfc_TooLarge", required, available);
    public static string Core_Nfc_AuthenticationFailed(int sector) => Format("Core_Nfc_AuthenticationFailed", sector);
    public static string Core_Nfc_ExistingData => Get("Core_Nfc_ExistingData");
    public static string Core_Nfc_ReaderCommunicationFailure(string error) => Format("Core_Nfc_ReaderCommunicationFailure", error);
}
