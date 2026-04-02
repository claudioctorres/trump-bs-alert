[Setup]
AppName=TrumpBsAlert
AppVersion=1.0.0
AppPublisher=Claudio Torres
AppPublisherURL=https://github.com/claudioctorres/trump-bs-alert
DefaultDirName={autopf}\TrumpBsAlert
DefaultGroupName=TrumpBsAlert
UninstallDisplayIcon={app}\appicon.ico
OutputDir=..\dist
OutputBaseFilename=TrumpBsAlertSetup
Compression=lzma2/ultra64
SolidCompression=yes
SetupIconFile=..\src\TrumpBsAlert\Resources\Images\tray_icon.ico
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
PrivilegesRequired=lowest
WizardStyle=modern
DisableProgramGroupPage=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Start with Windows"; GroupDescription: "Other:"

[Files]
Source: "..\src\TrumpBsAlert\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\TrumpBsAlert"; Filename: "{app}\TrumpBsAlert.exe"; IconFilename: "{app}\appicon.ico"
Name: "{group}\Uninstall TrumpBsAlert"; Filename: "{uninstallexe}"
Name: "{autodesktop}\TrumpBsAlert"; Filename: "{app}\TrumpBsAlert.exe"; IconFilename: "{app}\appicon.ico"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "TrumpBsAlert"; ValueData: """{app}\TrumpBsAlert.exe"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\TrumpBsAlert.exe"; Description: "Launch TrumpBsAlert"; Flags: nowait postinstall skipifsilent
