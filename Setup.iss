[Setup]
AppId=DesktopBoxes-zhenghanrex
AppName=Desktop Boxes
AppVersion=1.0
DefaultDirName={localappdata}\Programs\Desktop Boxes
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
OutputDir=out
OutputBaseFilename=DesktopBoxes-Setup
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\DesktopBoxes.exe

[Files]
Source: "out\DesktopBoxes.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Measure-DesktopBoxes.ps1"; DestDir: "{app}"
Source: "README.md"; DestDir: "{app}"

[Icons]
Name: "{userprograms}\Desktop Boxes"; Filename: "{app}\DesktopBoxes.exe"
