#ifndef MyAppVersion
#define MyAppVersion "1.0.0"
#endif

#define MyAppName "TgWsProxy"
#define MyAppPublisher "TgWsProxyNet"
#define MyAppExeName "TgWsProxy.exe"
#define MyServiceName "TgWsProxy"
#define MyServiceDescription "Local Telegram MTProto-to-WebSocket proxy implemented as a .NET 10 Windows Service."

[Setup]
AppId={{2E3999AA-4AC3-4B4C-8E0C-5C4A589C3F4E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=TgWsProxyNet-Setup-{#MyAppVersion}-win-x64
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=no
RestartApplications=no

[Tasks]
Name: "startservice"; Description: "Start TgWsProxy service after installation"; Flags: checkedonce

[Files]
Source: "..\artifacts\publish\win-x64\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\artifacts\publish\win-x64\appsettings.json"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist
Source: "..\artifacts\publish\win-x64\*.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\artifacts\publish\win-x64\*.pdb"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\NOTICE"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\TgWsProxy README"; Filename: "{app}\README.md"; WorkingDir: "{app}"; Check: FileExists(ExpandConstant('{app}\README.md'))

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop {#MyServiceName}"; Flags: runhidden waituntilterminated; RunOnceId: "StopTgWsProxyService"; StatusMsg: "Stopping TgWsProxy service..."
Filename: "{sys}\sc.exe"; Parameters: "delete {#MyServiceName}"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteTgWsProxyService"; StatusMsg: "Deleting TgWsProxy service..."

[Code]
var
  SecretPage: TInputQueryWizardPage;

function IsHexChar(Value: Char): Boolean;
begin
  Result := ((Value >= '0') and (Value <= '9')) or
            ((Value >= 'a') and (Value <= 'f')) or
            ((Value >= 'A') and (Value <= 'F'));
end;

function IsHex32(Value: String): Boolean;
var
  I: Integer;
begin
  Result := Length(Value) = 32;

  if not Result then
  begin
    Exit;
  end;

  for I := 1 to Length(Value) do
  begin
    if not IsHexChar(Value[I]) then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

function ServiceExists(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(
    ExpandConstant('{sys}\sc.exe'),
    'query {#MyServiceName}',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode) and (ResultCode = 0);
end;

procedure StopServiceIfNeeded();
var
  ResultCode: Integer;
begin
  if ServiceExists() then
  begin
    Exec(
      ExpandConstant('{sys}\sc.exe'),
      'stop {#MyServiceName}',
      '',
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode);

    Sleep(2000);
  end;
end;

procedure PatchConfiguration();
var
  FileName: String;
  Lines: TArrayOfString;
  Secret: String;
  I: Integer;
  Count: Integer;
begin
  FileName := ExpandConstant('{app}\appsettings.json');
  Secret := SecretPage.Values[0];

  if LoadStringsFromFile(FileName, Lines) then
  begin
    Count := GetArrayLength(Lines);

    for I := 0 to Count - 1 do
    begin
      StringChangeEx(Lines[I], 'CHANGE_ME_32_HEX_CHARS', Secret, True);
    end;

    if not SaveStringsToUTF8File(FileName, Lines, False) then
    begin
      MsgBox('Failed to save appsettings.json.', mbError, MB_OK);
    end;
  end
  else
  begin
    MsgBox('Failed to load appsettings.json.', mbError, MB_OK);
  end;
end;

procedure InstallOrUpdateService();
var
  ResultCode: Integer;
  ExePath: String;
  Params: String;
begin
  ExePath := ExpandConstant('{app}\{#MyAppExeName}');

  if ServiceExists() then
  begin
    Params := 'config {#MyServiceName} binPath= "' + ExePath + '" start= auto DisplayName= "{#MyAppName}"';
  end
  else
  begin
    Params := 'create {#MyServiceName} binPath= "' + ExePath + '" start= auto DisplayName= "{#MyAppName}"';
  end;

  if not Exec(ExpandConstant('{sys}\sc.exe'), Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    MsgBox('Failed to configure Windows Service.', mbError, MB_OK);
    Exit;
  end;

  Exec(
    ExpandConstant('{sys}\sc.exe'),
    'description {#MyServiceName} "{#MyServiceDescription}"',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode);

  if WizardIsTaskSelected('startservice') then
  begin
    Exec(
      ExpandConstant('{sys}\sc.exe'),
      'start {#MyServiceName}',
      '',
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode);
  end;
end;

procedure InitializeWizard();
begin
  SecretPage := CreateInputQueryPage(
    wpSelectDir,
    'Telegram proxy secret',
    'Enter internal 32-character hex secret.',
    'Use the internal secret without the Telegram dd prefix. ' +
    'Telegram Desktop must use dd + this value.');

  SecretPage.Add('Internal secret:', False);
  SecretPage.Values[0] := 'CHANGE_ME_32_HEX_CHARS';
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  if CurPageID = SecretPage.ID then
  begin
    if not IsHex32(SecretPage.Values[0]) then
    begin
      MsgBox('Secret must contain exactly 32 hex characters. Example: d032632e7eb3e05c579ea4fc59ae011d', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    StopServiceIfNeeded();
  end;

  if CurStep = ssPostInstall then
  begin
    PatchConfiguration();
    InstallOrUpdateService();
  end;
end;
