namespace Stay.Core;

/// <summary>
/// The advertising, the AI hooks, the telemetry and the noise — every switch, on one list.
///
/// This is Quiet for Windows' catalogue, brought here whole rather than rewritten, because somebody on an old
/// Windows 10 PC should not have to find and download a second app to turn off a Start menu advert. It is the
/// same author's work under the same licence, and the two lists are kept the same on purpose: a switch fixed in
/// one is fixed in both.
///
/// Everything here is a registry value with a receipt, exactly like the security list. Nothing is removed and
/// nothing is irreversible.
/// </summary>
public static class Junk
{
    public const string Ads = "Ads and suggestions";
    public const string AI = "AI features";
    public const string Telemetry = "Telemetry and tracking";
    public const string Noise = "Windows noise";
    public const string AppsGroup = "Apps you did not ask for";

    public static readonly IReadOnlyList<string> Groups = new[] { Ads, AI, Telemetry, Noise };

    private const string CDM = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
    private const string Advanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string EdgePolicy = @"SOFTWARE\Policies\Microsoft\Edge";
    private const string WindowsAI = @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI";
    private const string WindowsAIUser = @"Software\Policies\Microsoft\Windows\WindowsAI";

    private static RegEdit U(string key, string name, long v) => new(Hive.CurrentUser, key, name, RegValue.DWord(v));
    private static RegEdit M(string key, string name, long v) => new(Hive.LocalMachine, key, name, RegValue.DWord(v));
    private static RegEdit MS(string key, string name, string v) => new(Hive.LocalMachine, key, name, RegValue.Str(v));
    private static RegEdit UGone(string key, string name) => new(Hive.CurrentUser, key, name, RegValue.Absent);

    public static readonly IReadOnlyList<Guard> Items = new List<Guard>
    {
        // ---- Ads and suggestions ----
        new("ads.start", Ads, "Suggestions in Start",
            "Start shows tips, promoted apps and a \"recommended\" area Microsoft fills for you.",
            "Turns off suggestions and the recommended section in Start.",
            "Recently added apps and recent files stop appearing in Start's recommended area.",
            new[] {
                U(CDM, "SubscribedContent-338388Enabled", 0),
                U(CDM, "SystemPaneSuggestionsEnabled", 0),
                U(Advanced, "Start_IrisRecommendations", 0),
                U(@"Software\Policies\Microsoft\Windows\Explorer", "HideRecommendedSection", 1),
            }, DefaultOn: true),

        new("ads.settings", Ads, "Promotions in Settings",
            "The Settings app carries offers for Microsoft 365, OneDrive, Game Pass and account nudges.",
            "Turns off suggested content and account notifications inside Settings.",
            "None. Settings still shows every setting.",
            new[] {
                U(CDM, "SubscribedContent-338393Enabled", 0),
                U(CDM, "SubscribedContent-353694Enabled", 0),
                U(CDM, "SubscribedContent-353696Enabled", 0),
                U(CDM, "SubscribedContent-353698Enabled", 0),
                U(@"Software\Microsoft\Windows\CurrentVersion\SystemSettings\AccountNotifications", "EnableAccountNotifications", 0),
                M(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableConsumerAccountStateContent", 1),
            }, DefaultOn: true),

        new("ads.settings_home", Ads, "The Settings home page",
            "Settings opens on a \"Home\" page that is mostly your Microsoft account, promotions and recommendations.",
            "Hides the Home page. Settings opens on System instead.",
            "The Home page is gone until you switch this back. Every setting on it lives elsewhere in Settings too.",
            new[] {
                MS(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "SettingsPageVisibility", "hide:home"),
            }, DefaultOn: true),

        new("ads.tips", Ads, "Tips, nags and welcome screens",
            "Windows shows tips, \"finish setting up your device\" prompts, a welcome page after updates, backup reminders and \"suggested\" notifications.",
            "Turns all of those off.",
            "You will not be told about new features after an update. Nothing else.",
            new[] {
                U(CDM, "SubscribedContent-338389Enabled", 0),
                U(CDM, "SoftLandingEnabled", 0),
                U(CDM, "SubscribedContent-310093Enabled", 0),
                U(@"SOFTWARE\Microsoft\Windows\CurrentVersion\UserProfileEngagement", "ScoobeSystemSettingEnabled", 0),
                U(@"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.Suggested", "Enabled", 0),
                U(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.BackupReminder", "Enabled", 0),
                U(@"Software\Microsoft\Windows\CurrentVersion\Mobility", "OptedIn", 0),
                U(Advanced, "Start_AccountNotifications", 0),
            }, DefaultOn: true),

        new("ads.lockscreen", Ads, "Lock screen tips and offers",
            "The lock screen picture comes with \"fun facts\", tips and offers laid over it.",
            "Turns off the overlay. The picture stays.",
            "None.",
            new[] {
                U(CDM, "SubscribedContent-338387Enabled", 0),
                U(CDM, "RotatingLockScreenOverlayEnabled", 0),
            }, DefaultOn: true),

        new("ads.autoinstall", Ads, "Apps that install themselves",
            "Windows quietly installs promoted apps and games it thinks you might like, and pins them to Start.",
            "Stops the automatic installs.",
            "None you would notice.",
            new[] {
                U(CDM, "SilentInstalledAppsEnabled", 0),
                U(CDM, "PreInstalledAppsEnabled", 0),
                U(CDM, "OemPreInstalledAppsEnabled", 0),
            }, DefaultOn: true),

        new("ads.explorer", Ads, "Notices inside File Explorer",
            "OneDrive and other sync providers can show notices and offers at the top of File Explorer windows.",
            "Turns those notices off.",
            "A sync provider cannot tell you about its own offers inside File Explorer. It can still notify you normally.",
            new[] { U(Advanced, "ShowSyncProviderNotifications", 0) }, DefaultOn: true),

        new("ads.advertising_id", Ads, "Advertising ID and tailored experiences",
            "Windows gives every user an advertising ID so apps can show ads tailored to you, and uses diagnostic data to tailor tips and offers.",
            "Turns off the advertising ID and tailored experiences.",
            "Ads in apps are no longer tailored to you. There are still ads.",
            new[] {
                U(@"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0),
                U(@"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0),
            }, DefaultOn: true),

        new("ads.bing", Ads, "Bing in the Start search box",
            "Typing in Start's search box sends what you type to Bing and shows web results, plus a daily picture and \"search highlights\".",
            "Keeps search on this PC and turns the highlights off.",
            "Searching Start finds only files, apps and settings on this PC. Web search stays in the browser.",
            new[] {
                U(@"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1),
                U(@"Software\Microsoft\Windows\CurrentVersion\SearchSettings", "IsDynamicSearchBoxEnabled", 0),
            }, DefaultOn: true, NeedsSignOut: true),

        new("ads.edge", Ads, "Edge promotions and nags",
            "Edge fills the new tab page with news and sponsored links, pushes shopping offers, Rewards, an Acrobat button, and asks to be your default browser.",
            "Turns off the new tab feed, the shopping assistant, Rewards, the default-browser campaign, recommendations and the Acrobat button, by policy.",
            "Edge's new tab page becomes a plain page with a search box. Edge will say some settings are \"managed by your organization\"; that is these.",
            new[] {
                M(EdgePolicy, "NewTabPageContentEnabled", 0),
                M(EdgePolicy, "NewTabPageHideDefaultTopSites", 1),
                M(EdgePolicy, "EdgeShoppingAssistantEnabled", 0),
                M(EdgePolicy, "ShowMicrosoftRewards", 0),
                M(EdgePolicy, "UserFeedbackAllowed", 0),
                M(EdgePolicy, "ShowRecommendationsEnabled", 0),
                M(EdgePolicy, "WalletDonationEnabled", 0),
                M(EdgePolicy, "DefaultBrowserSettingsCampaignEnabled", 0),
                M(EdgePolicy, "SpotlightExperiencesAndRecommendationsEnabled", 0),
                M(EdgePolicy, "ShowAcrobatSubscriptionButton", 0),
            }, DefaultOn: true),

        // ---- AI features ----
        new("ai.copilot", AI, "Copilot",
            "Copilot sits on the taskbar, answers the Windows key + C, and Microsoft keeps adding it to more places.",
            "Removes the taskbar button and turns Copilot off for this user and for every user, by policy.",
            "The Copilot button and the Windows key + C shortcut stop working. The Copilot app itself is listed under Apps.",
            new[] {
                U(Advanced, "ShowCopilotButton", 0),
                U(@"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1),
                M(@"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1),
            }, DefaultOn: true, NeedsSignOut: true),

        new("ai.recall", AI, "Recall",
            "Recall takes a screenshot of what is on your screen every few seconds and keeps them so you can search your past.",
            "Turns Recall off and stops snapshots being saved, for this user and by policy for the PC.",
            "You cannot search what was on your screen. On a PC without a Copilot+ chip it was never available; the switch keeps it from arriving.",
            new[] {
                new RegEdit(Hive.CurrentUser, WindowsAIUser, "DisableAIDataAnalysis", RegValue.DWord(1)),
                M(WindowsAI, "DisableAIDataAnalysis", 1),
                M(WindowsAI, "AllowRecallEnablement", 0),
                M(WindowsAI, "TurnOffSavingSnapshots", 1),
            }, DefaultOn: true),

        new("ai.clicktodo", AI, "Click to Do",
            "Click to Do reads whatever is on screen when you hold the Windows key and click, and offers actions on it.",
            "Turns Click to Do off for this user and by policy.",
            "Windows key + click does nothing special.",
            new[] {
                new RegEdit(Hive.CurrentUser, WindowsAIUser, "DisableClickToDo", RegValue.DWord(1)),
                M(WindowsAI, "DisableClickToDo", 1),
            }, DefaultOn: true),

        new("ai.paint", AI, "AI in Paint",
            "Paint has Cocreator, Image Creator, generative fill and erase, and background removal, some of which send your picture to Microsoft.",
            "Turns those features off by policy.",
            "Paint is the old Paint again. Cropping, drawing and layers still work.",
            new[] {
                M(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableCocreator", 1),
                M(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableGenerativeFill", 1),
                M(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableImageCreator", 1),
                M(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableGenerativeErase", 1),
                M(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableRemoveBackground", 1),
            }, DefaultOn: true),

        new("ai.notepad", AI, "AI in Notepad",
            "Notepad has Rewrite and Summarize, which send the text to Microsoft.",
            "Turns them off by policy.",
            "Notepad edits text and nothing else, which is what it was for.",
            new[] { M(@"SOFTWARE\Policies\WindowsNotepad", "DisableAIFeatures", 1) }, DefaultOn: true),

        new("ai.edge", AI, "AI in Edge",
            "Edge has a Copilot sidebar that can read the page you are on, summarise your history, and write in text boxes for you.",
            "Turns off the sidebar, page context sharing, history search, inline compose and Bing chat on the new tab page, by policy.",
            "No Copilot in Edge. Edge will say these settings are \"managed by your organization\".",
            new[] {
                M(EdgePolicy, "CopilotCDPPageContext", 0),
                M(EdgePolicy, "CopilotPageContext", 0),
                M(EdgePolicy, "HubsSidebarEnabled", 0),
                M(EdgePolicy, "EdgeEntraCopilotPageContext", 0),
                M(EdgePolicy, "EdgeHistoryAISearchEnabled", 0),
                M(EdgePolicy, "ComposeInlineEnabled", 0),
                M(EdgePolicy, "NewTabPageBingChatEnabled", 0),
            }, DefaultOn: true),

        new("ai.service", AI, "AI service starting on its own",
            "The Windows AI fabric service starts at boot whether or not anything uses it.",
            "Sets the service to start only when something asks for it.",
            "None. It still starts when an app needs it. Takes effect after a restart.",
            new[] { M(@"SYSTEM\CurrentControlSet\Services\WSAIFabricSvc", "Start", 3) }, DefaultOn: true, NeedsRestart: true),

        // ---- Telemetry and tracking ----
        new("tel.diagnostics", Telemetry, "Diagnostic data",
            "Windows sends diagnostic data about how you use it. Home and Pro can send \"required\" or \"optional\"; the default is optional.",
            "Sets diagnostic data to required only, by policy.",
            "Nothing you would notice. Windows Insider builds need optional data and will say so.",
            new[] {
                M(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", 0),
                M(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0),
            }, DefaultOn: true),

        new("tel.feedback", Telemetry, "Feedback requests",
            "Windows asks how it is doing, in notifications, on a schedule.",
            "Sets the feedback frequency to never and hides feedback notifications.",
            "Windows stops asking. Feedback Hub still works if you open it.",
            new[] {
                U(@"SOFTWARE\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0),
                UGone(@"SOFTWARE\Microsoft\Siuf\Rules", "PeriodInNanoSeconds"),
                M(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "DoNotShowFeedbackNotifications", 1),
            }, DefaultOn: true),

        new("tel.activity", Telemetry, "Activity history",
            "Windows keeps a history of the apps, files and sites you use and can send it to Microsoft.",
            "Stops the history being published or uploaded, by policy.",
            "Nothing visible on Windows 11, which no longer shows a timeline.",
            new[] {
                M(@"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0),
                M(@"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0),
            }, DefaultOn: true),

        new("tel.typing", Telemetry, "Typing, inking and speech data",
            "Windows learns from what you type and write to improve suggestions, sends typing data to improve recognition, and can use online speech recognition.",
            "Turns off the collection and the online speech recognition.",
            "Word suggestions stop learning your words. Dictation that needs the cloud will not work; the on-device kind still does.",
            new[] {
                U(@"Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", 1),
                U(@"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 1),
                U(@"Software\Microsoft\InputPersonalization\TrainedDataStore", "HarvestContacts", 0),
                U(@"Software\Microsoft\Personalization\Settings", "AcceptedPrivacyPolicy", 0),
                U(@"Software\Microsoft\Input\TIPC", "Enabled", 0),
                U(@"Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy", "HasAccepted", 0),
            }, DefaultOn: true),

        new("tel.launch", Telemetry, "App launch tracking",
            "Windows counts how often you open each app to rank Start and search results.",
            "Turns the counting off.",
            "Start and search stop putting your most-used apps first.",
            new[] { U(Advanced, "Start_TrackProgs", 0) }, DefaultOn: true),

        new("tel.edge", Telemetry, "Edge diagnostics and personalization",
            "Edge sends browsing data to personalise ads, news and search, and sends diagnostic data about the browser.",
            "Turns both off by policy.",
            "None you would notice. Edge will say these settings are \"managed by your organization\".",
            new[] {
                M(EdgePolicy, "PersonalizationReportingEnabled", 0),
                M(EdgePolicy, "DiagnosticData", 0),
            }, DefaultOn: true),

        new("tel.ceip", Telemetry, "Customer Experience Improvement Program",
            "An older program that reports how Windows components are used.",
            "Opts the PC out, by policy.",
            "None.",
            new[] { M(@"SOFTWARE\Policies\Microsoft\SQMClient\Windows", "CEIPEnable", 0) }, DefaultOn: true),

        // ---- Windows noise ----
        new("noise.widgets", Noise, "Widgets and the weather button",
            "The weather in the taskbar corner opens a board of news, ads and widgets.",
            "Turns the board off by policy, which takes the button with it. Windows no longer lets anyone, administrators included, write the taskbar's own widgets switch, so the policy is the way.",
            "No weather in the taskbar. The Widgets packages themselves are under Apps.",
            new[] { M(@"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0) }, DefaultOn: true, NeedsSignOut: true),

        new("noise.chat", Noise, "Chat and Meet Now",
            "Older Windows 11 puts a Teams chat button on the taskbar; Windows 10 has Meet Now. Recent builds have neither, and this keeps it that way.",
            "Hides both.",
            "None.",
            new[] {
                U(Advanced, "TaskbarMn", 0),
                U(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "HideSCAMeetNow", 1),
            }, DefaultOn: true),

        new("noise.gamebar", Noise, "Game Bar recording",
            "Game Bar records gameplay in the background so you can save the last minutes, and answers the Windows key + G.",
            "Turns background recording and captures off.",
            "Windows key + G stops recording. Games still run; Xbox features that need the Game Bar overlay may complain.",
            new[] {
                U(@"System\GameConfigStore", "GameDVR_Enabled", 0),
                U(@"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0),
                M(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0),
            }, DefaultOn: true),

        new("noise.phonelink", Noise, "Phone Link panel in Start",
            "Start can show a panel with your phone's battery, messages and photos.",
            "Turns the panel off.",
            "Start no longer shows your phone. Phone Link itself still works.",
            new[] { U(@"Software\Microsoft\Windows\CurrentVersion\Start\Companions\Microsoft.YourPhone_8wekyb3d8bbwe", "IsEnabled", 0) }, DefaultOn: true),

        new("noise.spotlight", Noise, "\"Learn about this picture\" on the desktop",
            "When the desktop background is Windows Spotlight, an icon sits on the desktop linking to Bing.",
            "Turns off the Spotlight collection on the desktop and hides the icon.",
            "The desktop keeps a picture; it stops changing every day. Off by default because it is a wallpaper choice.",
            new[] {
                U(@"Software\Policies\Microsoft\Windows\CloudContent", "DisableSpotlightCollectionOnDesktop", 1),
                U(@"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel", "{2cc5ca98-6485-489a-920e-b3e88a6ccce3}", 1),
            }, DefaultOn: false),

        new("noise.onedrive", Noise, "OneDrive in File Explorer's sidebar",
            "File Explorer pins OneDrive to the sidebar whether or not you use it.",
            "Unpins it for this user.",
            "OneDrive is a folder in your user folder instead. Off by default because many people use OneDrive.",
            new[] { U(@"Software\Classes\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}", "System.IsPinnedToNameSpaceTree", 0) }, DefaultOn: false),

        new("noise.edge_background", Noise, "Edge running when it is closed",
            "Edge starts at boot (\"startup boost\") and keeps running after you close it so it opens faster.",
            "Turns both off by policy.",
            "Edge opens a little slower. Extensions that want to run with Edge closed cannot.",
            new[] {
                M(EdgePolicy, "StartupBoostEnabled", 0),
                M(EdgePolicy, "BackgroundModeEnabled", 0),
            }, DefaultOn: true),

        new("noise.suggested_actions", Noise, "Suggested actions on copied text",
            "Copy a phone number or a date and Windows pops up a bar offering to call it or make an event.",
            "Turns the bar off.",
            "None.",
            new[] { U(@"Software\Microsoft\Windows\CurrentVersion\SmartActionPlatform\SmartClipboard", "Disabled", 1) }, DefaultOn: true),

        new("noise.onedrive_start", Noise, "OneDrive starting at sign-in",
            "OneDrive starts with Windows and sits in the tray whether or not you use it.",
            "Removes its entry from the list of programs that start at sign-in. OneDrive itself stays installed.",
            "OneDrive does not sync until you open it. Off by default because many people want it running.",
            new[] { UGone(@"Software\Microsoft\Windows\CurrentVersion\Run", "OneDrive") }, DefaultOn: false, NeedsSignOut: true),

        new("noise.delivery", Noise, "Sending updates to other PCs",
            "Delivery Optimization uploads Windows updates from this PC to other PCs, on your network and on the internet.",
            "Sets it to download only.",
            "Updates arrive from Microsoft alone; on a home connection that is no slower. Other PCs cannot fetch updates from this one.",
            new[] { new RegEdit(Hive.NetworkService, @"Software\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Settings", "DownloadMode", RegValue.DWord(0)) },
            DefaultOn: true),
    };

    public static Guard? Find(string id) => Items.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<Guard> InGroup(string group) => Items.Where(i => i.Group == group);
}
