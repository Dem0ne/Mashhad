; Inno Setup 6 script for Mashhad (مشهد).
; Build with:  pwsh scripts/build.ps1 -Installer
; The extensions in [Registry] mirror src/Mashhad.Core/Media/MediaTypes.cs (a unit test checks they stay in sync).

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish\Mashhad"
#endif
#ifndef OutputDir
  #define OutputDir "..\dist"
#endif

[Setup]
AppId={{6E0B5B5C-6A0D-4C2B-9B7F-4D41534848A1}
AppName=Mashhad
AppVersion={#AppVersion}
AppVerName=Mashhad {#AppVersion}
AppPublisher=Mashhad
DefaultDirName={autopf}\Mashhad
DefaultGroupName=Mashhad
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\Mashhad.exe
SetupIconFile=..\src\Mashhad.App\Assets\Mashhad.ico
OutputDir={#OutputDir}
OutputBaseFilename=Mashhad-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
; Per-user by default (no admin prompt); the dialog lets the user choose "for all users".
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ChangesAssociations=yes
WizardStyle=modern
ShowLanguageDialog=auto

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "ar"; MessagesFile: "compiler:Languages\Arabic.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "associate"; Description: "{cm:AssocTask}"; GroupDescription: "{cm:AssocGroup}"

[CustomMessages]
en.AssocGroup=File types
ar.AssocGroup=أنواع الملفات
en.AssocTask=Register Mashhad for photos, videos and audio (choose it as default in Windows Settings)
ar.AssocTask=تسجيل «مشهد» للصور والفيديو والصوت (اختره افتراضيًا من إعدادات ويندوز)
en.OpenDefaults=Open Windows "Default apps" to make Mashhad the default
ar.OpenDefaults=فتح «التطبيقات الافتراضية» في ويندوز لجعل «مشهد» افتراضيًا
en.OpenWith=Open with Mashhad
ar.OpenWith=فتح بواسطة «مشهد»

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Mashhad"; Filename: "{app}\Mashhad.exe"
Name: "{autodesktop}\Mashhad"; Filename: "{app}\Mashhad.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Mashhad.exe"; Description: "{cm:LaunchProgram,Mashhad}"; Flags: nowait postinstall skipifsilent
Filename: "ms-settings:defaultapps"; Description: "{cm:OpenDefaults}"; Flags: shellexec nowait postinstall skipifsilent unchecked; Tasks: associate

[Registry]
; Application registration ("Open with" list, friendly name, supported types).
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "Mashhad"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\shell\open\command"; ValueType: string; ValueData: """{app}\Mashhad.exe"" ""%1"""
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\Mashhad.exe"; ValueType: string; ValueData: "{app}\Mashhad.exe"; Flags: uninsdeletekey

; Capabilities: this is what makes Mashhad appear in Settings > Apps > Default apps.
Root: HKA; Subkey: "Software\Mashhad\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "Mashhad"; Flags: uninsdeletekey; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "Photo viewer, video player and lossless video cutter"; Tasks: associate
Root: HKA; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "Mashhad"; ValueData: "Software\Mashhad\Capabilities"; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad"; Flags: uninsdeletekeyifempty

; ProgIDs (one per media family).
Root: HKA; Subkey: "Software\Classes\Mashhad.Image"; ValueType: string; ValueData: "Image (Mashhad)"; Flags: uninsdeletekey; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Mashhad.Image\DefaultIcon"; ValueType: string; ValueData: "{app}\Mashhad.exe,0"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Mashhad.Image\shell\open"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "Mashhad"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Mashhad.Image\shell\open\command"; ValueType: string; ValueData: """{app}\Mashhad.exe"" ""%1"""; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Mashhad.Video"; ValueType: string; ValueData: "Video (Mashhad)"; Flags: uninsdeletekey; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Mashhad.Video\DefaultIcon"; ValueType: string; ValueData: "{app}\Mashhad.exe,0"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Mashhad.Video\shell\open"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "Mashhad"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Mashhad.Video\shell\open\command"; ValueType: string; ValueData: """{app}\Mashhad.exe"" ""%1"""; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Mashhad.Audio"; ValueType: string; ValueData: "Audio (Mashhad)"; Flags: uninsdeletekey; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Mashhad.Audio\DefaultIcon"; ValueType: string; ValueData: "{app}\Mashhad.exe,0"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Mashhad.Audio\shell\open"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "Mashhad"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Mashhad.Audio\shell\open\command"; ValueType: string; ValueData: """{app}\Mashhad.exe"" ""%1"""; Tasks: associate

; Per-extension registration. We add ourselves to OpenWithProgids and to Capabilities only:
; Windows 10/11 let the *user* pick the default, so an existing default is never overwritten.
Root: HKA; Subkey: "Software\Classes\.jpg\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jpg"; ValueData: "Mashhad.Image"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".jpg"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.jpeg\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jpeg"; ValueData: "Mashhad.Image"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".jpeg"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.jpe\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jpe"; ValueData: "Mashhad.Image"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".jpe"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.jfif\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jfif"; ValueData: "Mashhad.Image"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".jfif"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.png\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".png"; ValueData: "Mashhad.Image"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".png"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.webp\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".webp"; ValueData: "Mashhad.Image"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".webp"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.bmp\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".bmp"; ValueData: "Mashhad.Image"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".bmp"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.tif\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tif"; ValueData: "Mashhad.Image"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".tif"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.tiff\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tiff"; ValueData: "Mashhad.Image"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".tiff"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.heic\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".heic"; ValueData: "Mashhad.Image"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".heic"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.heif\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".heif"; ValueData: "Mashhad.Image"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".heif"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.avif\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".avif"; ValueData: "Mashhad.Image"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".avif"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.jxl\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".jxl"; ValueData: "Mashhad.Image"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".jxl"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.gif\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".gif"; ValueData: "Mashhad.Image"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".gif"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.apng\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Image"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".apng"; ValueData: "Mashhad.Image"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".apng"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.mp4\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mp4"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".mp4"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.m4v\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".m4v"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".m4v"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.mkv\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mkv"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".mkv"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.webm\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".webm"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".webm"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.mov\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mov"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".mov"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.avi\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".avi"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".avi"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.wmv\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".wmv"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".wmv"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.flv\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".flv"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".flv"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.f4v\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".f4v"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".f4v"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.ts\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ts"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".ts"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.mts\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mts"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".mts"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.m2ts\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".m2ts"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".m2ts"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.mpg\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mpg"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".mpg"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.mpeg\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mpeg"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".mpeg"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.mpe\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mpe"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".mpe"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.m2v\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".m2v"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".m2v"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.vob\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".vob"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".vob"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.3gp\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".3gp"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".3gp"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.3g2\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".3g2"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".3g2"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.ogv\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ogv"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".ogv"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.ogm\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ogm"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".ogm"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.rm\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".rm"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".rm"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.rmvb\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".rmvb"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".rmvb"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.asf\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".asf"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".asf"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.divx\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".divx"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".divx"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.mxf\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mxf"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".mxf"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.y4m\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".y4m"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".y4m"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.nut\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".nut"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".nut"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.dv\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Video"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".dv"; ValueData: "Mashhad.Video"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".dv"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.mp3\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Audio"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mp3"; ValueData: "Mashhad.Audio"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".mp3"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.flac\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Audio"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".flac"; ValueData: "Mashhad.Audio"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".flac"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.m4a\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Audio"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".m4a"; ValueData: "Mashhad.Audio"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".m4a"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.aac\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Audio"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".aac"; ValueData: "Mashhad.Audio"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".aac"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.ogg\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Audio"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ogg"; ValueData: "Mashhad.Audio"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".ogg"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.oga\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Audio"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".oga"; ValueData: "Mashhad.Audio"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".oga"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.opus\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Audio"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".opus"; ValueData: "Mashhad.Audio"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".opus"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.wav\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Audio"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".wav"; ValueData: "Mashhad.Audio"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".wav"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.wma\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Audio"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".wma"; ValueData: "Mashhad.Audio"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".wma"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.ac3\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Audio"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ac3"; ValueData: "Mashhad.Audio"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".ac3"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.eac3\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Audio"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".eac3"; ValueData: "Mashhad.Audio"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".eac3"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.dts\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Audio"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".dts"; ValueData: "Mashhad.Audio"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".dts"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.mka\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Audio"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mka"; ValueData: "Mashhad.Audio"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".mka"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.aiff\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Audio"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".aiff"; ValueData: "Mashhad.Audio"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".aiff"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.aif\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Audio"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".aif"; ValueData: "Mashhad.Audio"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".aif"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.ape\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Audio"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ape"; ValueData: "Mashhad.Audio"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".ape"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.wv\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Audio"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".wv"; ValueData: "Mashhad.Audio"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".wv"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.tta\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Audio"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tta"; ValueData: "Mashhad.Audio"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".tta"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\.amr\OpenWithProgids"; ValueType: string; ValueName: "Mashhad.Audio"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Mashhad\Capabilities\FileAssociations"; ValueType: string; ValueName: ".amr"; ValueData: "Mashhad.Audio"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\Applications\Mashhad.exe\SupportedTypes"; ValueType: string; ValueName: ".amr"; ValueData: ""
