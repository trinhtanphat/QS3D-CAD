#ifndef AppVersion
  #define AppVersion "0.1.0-preview.2"
#endif
#ifndef FileVersion
  #define FileVersion "0.1.0.2"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

[Setup]
AppId={{7ACFB5A5-0C21-4CC9-B8B8-7A0CB63A49B8}
AppName=QS3D CAD
AppVersion={#AppVersion}
AppPublisher=QS3D
AppPublisherURL=https://github.com/trinhtanphat/QS3D-CAD
AppSupportURL=https://github.com/trinhtanphat/QS3D-CAD/issues
DefaultDirName={autopf}\QS3D CAD
DefaultGroupName=QS3D CAD
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=QS3D-CAD-Setup-win-x64
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
WizardStyle=modern
SetupLogging=yes
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\QS3D.CAD.exe
VersionInfoVersion={#FileVersion}
VersionInfoTextVersion={#AppVersion}
VersionInfoCompany=QS3D
VersionInfoDescription=QS3D CAD Windows Installer
VersionInfoProductName=QS3D CAD
VersionInfoProductVersion={#FileVersion}
VersionInfoProductTextVersion={#AppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\QS3D CAD"; Filename: "{app}\QS3D.CAD.exe"
Name: "{autodesktop}\QS3D CAD"; Filename: "{app}\QS3D.CAD.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\QS3D.CAD.exe"; Description: "Launch QS3D CAD"; Flags: nowait postinstall skipifsilent
