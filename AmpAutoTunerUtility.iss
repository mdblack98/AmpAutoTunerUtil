; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

#define MyAppName "AmpAutoTunerUtility"
#define MyAppVersion "0.99"
#define MyAppPublisher "W9MDB"
#define MyAppURL "https://www.qrz.com/db/W9MDB"
#define MyAppExeName "AmpAutoTunerUtility.exe"
#define MyAppIcoName "Amp.ico"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{F501C93E-950F-4733-B280-815D7B530A5E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={commonpf}\{#MyAppName}
DisableProgramGroupPage=yes
OutputDir=C:\Users\mdbla\Dropbox\Projects\AmpAutoTunerUtil\AmpAutoTunerUtil\TunerUtil\Install
OutputBaseFilename=AmpAutoTunerUtility{#MyAppVersion}
;SetupIconFile=C:\Users\mdbla\Dropbox\Projects\TunerUtil\TunerUtil\Amp.ico
Compression=lzma
SolidCompression=yes
DisableDirPage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "C:\Users\mdbla\Dropbox\Projects\TunerUtil\TunerUtil\bin\Release\AmpAutoTunerUtility.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\mdbla\Dropbox\Projects\TunerUtil\TunerUtil\bin\Release\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{commonprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

