; Inno Setup Script for RevitMCP and forks (e.g. BimCore)
; Can be parametrized via ISCC command line:
; iscc /DMcpName=BimCore /DMcpTitle="BimCore MCP" /DAppVersion=1.0.0 installer\RevitMCP.iss

#ifndef McpName
  #define McpName "RevitMCP"
#endif

#ifndef McpTitle
  #define McpTitle "Revit MCP"
#endif

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#ifndef AppPublisher
  #define AppPublisher "Delures"
#endif

#ifndef AddInGuid
  #define AddInGuid "{D4A8E24E-721D-40EF-8A35-1317A3EE3691}"
#endif

#ifndef DistDir
  #define DistDir "..\dist"
#endif

[Setup]
AppId={{D4A8E24E-721D-40EF-8A35-1317A3EE3691}
AppName={#McpTitle}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\{#McpName}
DisableDirPage=yes
DefaultGroupName={#McpTitle}
DisableProgramGroupPage=yes
OutputBaseFilename={#McpName}-Setup-{#AppVersion}
OutputDir=..\dist-installer
Compression=lzma2/max
SolidCompression=yes
PrivilegesRequired=lowest
CloseApplications=force
CloseApplicationsFilter=*.exe
RestartApplications=no
WizardStyle=modern
UninstallDisplayName={#McpTitle}

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Files]
; CLI application binaries
Source: "{#DistDir}\bin\cli\*"; DestDir: "{app}\bin\cli"; Flags: ignoreversion recursesubdirs createallsubdirs

; Revit Plugins for all built versions
Source: "{#DistDir}\bin\2019\*"; DestDir: "{app}\bin\2019"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#DistDir}\bin\2020\*"; DestDir: "{app}\bin\2020"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#DistDir}\bin\2021\*"; DestDir: "{app}\bin\2021"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#DistDir}\bin\2022\*"; DestDir: "{app}\bin\2022"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#DistDir}\bin\2023\*"; DestDir: "{app}\bin\2023"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#DistDir}\bin\2024\*"; DestDir: "{app}\bin\2024"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#DistDir}\bin\2025\*"; DestDir: "{app}\bin\2025"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#DistDir}\bin\2026\*"; DestDir: "{app}\bin\2026"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#DistDir}\bin\2027\*"; DestDir: "{app}\bin\2027"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Code]

// Terminate a background process via taskkill
procedure KillProcess(const ProcessName: string);
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/F /T /IM ' + ProcessName, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

// Check whether a process is running
function IsProcessRunning(const ProcessName: string): Boolean;
var
  WbemServices: Variant;
  WbemObjectSet: Variant;
begin
  Result := False;
  try
    WbemServices := CreateOleObject('WbemScripting.SWbemLocator');
    WbemServices := WbemServices.ConnectServer('.', 'root\CIMV2');
    WbemObjectSet := WbemServices.ExecQuery('SELECT * FROM Win32_Process WHERE Name = "' + ProcessName + '"');
    Result := (WbemObjectSet.Count > 0);
  except
    Result := False;
  end;
end;

// Pre-install checks and preparation
function InitializeSetup(): Boolean;
begin
  Result := True;

  // Kill background server CLI processes so they do not lock the files
  KillProcess('{#McpName}.Cli.exe');
  KillProcess('RevitMCP.Cli.exe');

  // If Revit is running, ask the user to save the project and close it
  while IsProcessRunning('Revit.exe') do
  begin
    if MsgBox('Autodesk Revit is running.' + #13#10#13#10 +
              'Please save your work and close Revit before continuing the installation.',
              mbConfirmation, MB_OKCANCEL) = IDCANCEL then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

// Pre-uninstall checks and preparation
function InitializeUninstall(): Boolean;
begin
  Result := True;

  // Kill background CLI processes
  KillProcess('{#McpName}.Cli.exe');
  KillProcess('RevitMCP.Cli.exe');

  // If Revit is running, ask the user to close it
  while IsProcessRunning('Revit.exe') do
  begin
    if MsgBox('Autodesk Revit is running.' + #13#10#13#10 +
              'Please close Revit before removing the add-in.',
              mbConfirmation, MB_OKCANCEL) = IDCANCEL then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

// Generate the .addin manifest
procedure CreateAddinManifest(const Year: string);
var
  AddinDir, ManifestPath, DllPath, Content: string;
begin
  AddinDir := ExpandConstant('{userappdata}\Autodesk\Revit\Addins\' + Year);
  DllPath := ExpandConstant('{app}\bin\' + Year + '\RevitMCP.Plugin.dll');

  if not FileExists(DllPath) then
    Exit;

  if not DirExists(AddinDir) then
    ForceDirectories(AddinDir);

  ManifestPath := AddinDir + '\{#McpName}.addin';

  Content := '<?xml version="1.0" encoding="utf-8" standalone="no"?>' + #13#10 +
             '<RevitAddIns>' + #13#10 +
             '  <AddIn Type="Application">' + #13#10 +
             '    <Name>{#McpTitle} Server</Name>' + #13#10 +
             '    <Assembly>' + DllPath + '</Assembly>' + #13#10 +
             '    <AddInId>{#AddInGuid}</AddInId>' + #13#10 +
             '    <FullClassName>RevitMCP.Plugin.RevitMcpApp</FullClassName>' + #13#10 +
             '    <VendorId>{#McpName}</VendorId>' + #13#10 +
             '    <VendorDescription>{#McpTitle} Model Context Protocol Server</VendorDescription>' + #13#10 +
             '  </AddIn>' + #13#10 +
             '</RevitAddIns>';

  SaveStringToFile(ManifestPath, Content, False);
end;

// Post-install: register the manifest in every detected Revit version
procedure CurStepChanged(CurStep: TSetupStep);
var
  Years: array[0..8] of string;
  I: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    Years[0] := '2019';
    Years[1] := '2020';
    Years[2] := '2021';
    Years[3] := '2022';
    Years[4] := '2023';
    Years[5] := '2024';
    Years[6] := '2025';
    Years[7] := '2026';
    Years[8] := '2027';

    for I := 0 to 8 do
    begin
      if DirExists(ExpandConstant('{app}\bin\' + Years[I])) then
      begin
        CreateAddinManifest(Years[I]);
      end;
    end;
  end;
end;

// Uninstall: clean up manifests and ask what to do with user data
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Answer: Integer;
  KeepUserData: Boolean;
  Years: array[0..8] of string;
  I: Integer;
  AddinPath: string;
begin
  if CurUninstallStep = usUninstall then
  begin
    // 1. Always remove .addin manifests from every Revit version
    Years[0] := '2019';
    Years[1] := '2020';
    Years[2] := '2021';
    Years[3] := '2022';
    Years[4] := '2023';
    Years[5] := '2024';
    Years[6] := '2025';
    Years[7] := '2026';
    Years[8] := '2027';

    for I := 0 to 8 do
    begin
      AddinPath := ExpandConstant('{userappdata}\Autodesk\Revit\Addins\' + Years[I] + '\{#McpName}.addin');
      if FileExists(AddinPath) then
        DeleteFile(AddinPath);
    end;

    // 2. Ask directly and politely whether to keep user data
    Answer := MsgBox(
      'Keep the assistant memory and your accumulated work?' + #13#10 +
      '(activity history, accumulated knowledge and generated tools)' + #13#10#13#10 +
      'Choose Yes if you plan to install the add-in again.',
      mbConfirmation, MB_YESNO or MB_DEFBUTTON1);

    KeepUserData := (Answer = IDYES);

    if not KeepUserData then
    begin
      // Remove the working data directories
      DelTree(ExpandConstant('{app}\models'), True, True, True);
      DelTree(ExpandConstant('{app}\knowledge'), True, True, True);
      DelTree(ExpandConstant('{app}\tools'), True, True, True);
      DelTree(ExpandConstant('{app}\logs'), True, True, True);
      DelTree(ExpandConstant('{app}\sessions'), True, True, True);
      RemoveDir(ExpandConstant('{app}'));
    end;
  end;
end;
