#define AppVersion "0.2.0-preview.9"
#ifndef Payload
  #define Payload "..\artifacts\preview-win-x64"
#endif
#ifdef TestInstall
  #define Identity "iStart-QA"
#else
  #define Identity "iStart"
#endif

[Setup]
AppId={#Identity}
AppName=iStart 个人开始菜单
AppVersion={#AppVersion}
AppPublisher=morrowfactory
AppPublisherURL=https://github.com/morrowfactory/istart
DefaultDirName={localappdata}\Programs\{#Identity}
DefaultGroupName={#Identity}
PrivilegesRequired=lowest
MinVersion=10.0.19041
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
DisableWelcomePage=no
DisableProgramGroupPage=yes
OutputDir=..\artifacts\setup
OutputBaseFilename={#Identity}-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
SetupLogging=yes
UninstallDisplayIcon={app}\IStart.App.exe
CloseApplications=yes
RestartApplications=no
Uninstallable=yes

[Languages]
Name: "zhcn"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Messages]
WelcomeLabel1=欢迎安装 iStart
WelcomeLabel2=把常用软件、文件和文件夹集中整理，让桌面更清爽。%n%n这是试用版。默认保留 Windows 开始菜单，安装后可在设置中选择启用自定义入口。当前仅 Win11 26200 完成基本入口验证。%n%n安装仅用于当前账户，无需管理员权限；收藏数据留在本机。
FinishedHeadingLabel=iStart 已安装
FinishedLabel=安装已完成。首次打开将显示本机软件候选；之后参考通过 iStart 打开的记录推荐。%n%n开始按钮接入需在设置中启用，通过兼容检查后生效。随时可从菜单或托盘切回原生菜单。%n%n卸载默认保留个人收藏与最近记录。

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; Flags: unchecked
#ifndef TestInstall
Name: "startup"; Description: "登录 Windows 后在托盘启动 iStart"; Flags: unchecked
Name: "custommenu"; Description: "首次启动尝试接入开始按钮（仅 Win11 26200，失败保留原生菜单）"; Flags: unchecked
#endif

[Files]
Source: "{#Payload}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\iStart"; Filename: "{app}\IStart.App.exe"
Name: "{group}\卸载 iStart"; Filename: "{uninstallexe}"
Name: "{autodesktop}\iStart"; Filename: "{app}\IStart.App.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\IStart.App.exe"; Parameters: "{code:LaunchArguments}"; Description: "打开 iStart，开始整理"; Flags: nowait postinstall skipifsilent

#ifndef TestInstall
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "iStart"; ValueType: string; ValueData: """{app}\IStart.App.exe"" --background"; Tasks: startup; Flags: uninsdeletevalue
#endif

[Code]
function LaunchArguments(Param: String): String;
begin
  Result := '';
#ifndef TestInstall
  if WizardIsTaskSelected('custommenu') then Result := '--enable-menu';
  if WizardIsTaskSelected('startup') then Result := Result + ' --startup-selected';
#endif
end;

procedure InitializeWizard;
begin
  WizardForm.SelectDirLabel.Caption := '选择安装位置。个人收藏单独保存在当前账户的数据目录，升级与卸载不会主动删除。';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    WizardForm.StatusLabel.Caption := '正在安装程序文件，请稍候…';
#ifndef TestInstall
  if (CurStep = ssPostInstall) and not WizardIsTaskSelected('startup') then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'iStart');
#endif
end;

procedure CurInstallProgressChanged(CurProgress, MaxProgress: Integer);
begin
  if MaxProgress > 0 then
    WizardForm.StatusLabel.Caption := Format('正在安装 · %d%%', [CurProgress * 100 div MaxProgress]);
end;

function InitializeUninstall: Boolean;
var
  ExitCode: Integer;
begin
  { 仅向当前用户的 iStart 发送正常退出请求，不结束其他进程。 }
#ifdef TestInstall
  Result := True;
#else
  Result := Exec(ExpandConstant('{app}\IStart.App.exe'), '--exit', '', SW_HIDE, ewWaitUntilTerminated, ExitCode);
  Result := Result and (ExitCode = 0);
  if not Result then MsgBox('iStart 尚未退出。请从托盘退出后重试卸载，个人收藏保持不变。', mbError, MB_OK);
#endif
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExitCode: Integer;
begin
  Result := '';
#ifndef TestInstall
  if FileExists(ExpandConstant('{app}\IStart.App.exe')) then
    if not Exec(ExpandConstant('{app}\IStart.App.exe'), '--exit', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) or (ExitCode <> 0) then
      Result := 'iStart 尚未退出。请从托盘退出后重试安装。';
#endif
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
#ifndef TestInstall
  { 也清理安装后从应用设置启用的启动项，不仅限于安装时选中的任务。 }
  if CurUninstallStep = usUninstall then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'iStart');
#endif
end;
