!include "XML.nsh"
!include "Locate.nsh"
;!include MUI2.nsh
LoadLanguageFile "${NSISDIR}\Contrib\Language files\Russian.nlf"
; Переменные.
!define TEMP1 $R0 ;Temp variable
; Наименование инсталятора.
Name "утилита импорта данных"
; Директория инсталяции.
InstallDir "$PROGRAMFILES64\Directum Company\DirectumRX\ImportData"
; Наименование инсталятора.
OutFile "Setup.exe"

ReserveFile "${NSISDIR}\Plugins\x86-unicode\InstallOptions.dll"
ReserveFile "AssemblyInterfacesPage.ini"
ReserveFile "ConfigParamPage.ini"
ReserveFile "FinishPage.ini"
ReserveFile "ShortcutPage.ini"

VIProductVersion 3.6.27.22
VIAddVersionKey FileVersion 3.6.27.22
VIAddVersionKey ProductVersion 3.6.27.22
XPStyle on
; Управление страницами.
Page directory 
Page custom CreateShortcutPage LeaveShortcutPage
Page custom CreateConfigParamPage LeaveConfigParamPage
Page custom CreateAssemblyInterfacesParamPage LeaveAssemblyInterfacesParamPage
Page instfiles
Page custom CreateFinishPage


Section "Components"
 SetOutPath $INSTDIR
 File /r "..\src\ImportData\bin\Debug\*.*" 
 File /r "run.bat"
  ;Get Install Options dialog user input
	; Обновление xml файла.
	Call UpdateConfig
	Call UpdateAssemblyInterfaces
SectionEnd

Function .onInit
  InitPluginsDir
  File /oname=$PLUGINSDIR\AssemblyInterfacesPage.ini "AssemblyInterfacesPage.ini"
  File /oname=$PLUGINSDIR\ConfigParamPage.ini "ConfigParamPage.ini"
	File /oname=$PLUGINSDIR\FinishPage.ini "FinishPage.ini"
	File /oname=$PLUGINSDIR\ShortcutPage.ini "ShortcutPage.ini"
FunctionEnd

; Генерация страницы с параметрами конфигурационного файла.
Function CreateConfigParamPage
	; Поиск и установка пути к _configSettings
	Call SetPathConfig
  Push ${TEMP1}
    InstallOptions::dialog "$PLUGINSDIR\ConfigParamPage.ini"
    Pop ${TEMP1}  
  Pop ${TEMP1}
FunctionEnd
; Выход из страницы с параметрами конфигурационного файла. 
Function LeaveConfigParamPage	
	;MessageBox MB_YESNO "Проверить подключение к серверу?" IDNO done	
	ReadINIStr ${TEMP1} "$PLUGINSDIR\ConfigParamPage.ini" "Field 2" "State"
  StrCmp ${TEMP1} "" 0 done
    MessageBox MB_ICONQUESTION|MB_YESNO "Не выбран конфигурационный файл. Вы хотите продолжить?" IDYES done
    Abort
  done:
FunctionEnd

; Генерация страницы с параметрами интерфейсной сборки.
Function CreateAssemblyInterfacesParamPage
	; Поиск и установка пути к Sungero.Domain.interfaces.dll
	Call SetPathAssemblyInterfaces
  Push ${TEMP1}
    InstallOptions::dialog "$PLUGINSDIR\AssemblyInterfacesPage.ini"
    Pop ${TEMP1}  
  Pop ${TEMP1}
FunctionEnd
; Выход из страницы с параметрами интерфейсной сборки. 
Function LeaveAssemblyInterfacesParamPage	
	ReadINIStr ${TEMP1} "$PLUGINSDIR\AssemblyInterfacesPage.ini" "Field 2" "State"
	StrCmp ${TEMP1} "" 0 done
    MessageBox MB_ICONQUESTION|MB_YESNO "Не выбран файл интерфейсной сборки. Вы хотите продолжить?" IDYES done
	Abort
  done:
FunctionEnd

; Создание страницы с ярлыками.
Function CreateShortcutPage
	Push ${TEMP1}
    InstallOptions::dialog "$PLUGINSDIR\ShortcutPage.ini"
    Pop ${TEMP1}  
  Pop ${TEMP1}
FunctionEnd
; Уход со страницы с ярлыками.
Function LeaveShortcutPage	
	
FunctionEnd
; Генерация завершающей страницы.
Function CreateFinishPage
  Push ${TEMP1}
    InstallOptions::dialog "$PLUGINSDIR\FinishPage.ini"
    Pop ${TEMP1}  
  Pop ${TEMP1}
FunctionEnd

Function .onInstSuccess
  ReadINIStr ${TEMP1} "$PLUGINSDIR\FinishPage.ini" "Field 1" "State"
  StrCmp ${TEMP1} 0 +2
	Exec '"$INSTDIR\run.bat"'	
	; Создание ярлыков.
	ReadINIStr ${TEMP1} "$PLUGINSDIR\ShortcutPage.ini" "Field 1" "State"
  StrCmp ${TEMP1} 0 +4
	CreateDirectory "$SMPROGRAMS\ImportData"
	CreateShortCut "$SMPROGRAMS\ImportData\run.lnk" "$INSTDIR\run.bat"
	CreateShortCut "$DESKTOP\run.lnk" "$INSTDIR\run.bat"
FunctionEnd

; Обновить конфигурационный файл.
Function UpdateConfig
	ReadINIStr ${TEMP1} "$PLUGINSDIR\ConfigParamPage.ini" "Field 2" "State"
	CopyFiles "${TEMP1}" "$INSTDIR\_ConfigSettings.xml"	
FunctionEnd
; Установить путь к _configSettings
Function SetPathConfig
	${locate::Open} "$LOCALAPPDATA" `/L=F /M=*_ConfigSettings.xml` $0	
	StrCmp $0 0 0 loop
	MessageBox MB_OK "Error" IDOK close
  Var /GLOBAL count1	
	Var /GLOBAL pathConfig
	loop:
	${locate::Find} $0 $1 $2 $3 $4 $5 $6	
	StrCmp $1 "" +5 0
	IfFileExists "$2\_ConfigSettings.xml" 0 +3
	IntOp $count1 $count1 + 1
	StrCpy $pathConfig "$1"	
	Goto loop
	IntCmp $count1 1 is1 close morethan1
is1:  
  WriteINIStr "$PLUGINSDIR\ConfigParamPage.ini" "Field 2" "State" "$pathConfig"   
  Goto close
morethan1:	
  MessageBox MB_OKCANCEL "Найдено несколько конфигурационных файлов. Укажите путь к конфигурационному файлу вручную"
  Goto close
close:
	${locate::Close} $0
	${locate::Unload}	
FunctionEnd

; Обновить файл интерфейсной сборки.
Function UpdateAssemblyInterfaces
	ReadINIStr ${TEMP1} "$PLUGINSDIR\AssemblyInterfacesPage.ini" "Field 2" "State"
	CopyFiles "${TEMP1}" "$INSTDIR\Sungero.Domain.interfaces.dll"	
FunctionEnd
; Установить путь к Sungero.Domain.interfaces.dll
Function SetPathAssemblyInterfaces
	${locate::Open} "$LOCALAPPDATA" `/L=F /M=*Sungero.Domain.Interfaces.dll` $0	
	StrCmp $0 0 0 loop
	MessageBox MB_OK "Error" IDOK close
  Var /GLOBAL count2	
	Var /GLOBAL pathAssemblyInterfaces
	loop:
	${locate::Find} $0 $1 $2 $3 $4 $5 $6	
	StrCmp $1 "" +5 0
	IfFileExists "$2\Sungero.Domain.Interfaces.dll" 0 +3
	IntOp $count2 $count2 + 1
	StrCpy $pathAssemblyInterfaces "$1"	
	Goto loop
	IntCmp $count2 1 is1 close morethan1
is1:  
  WriteINIStr "$PLUGINSDIR\AssemblyInterfacesPage.ini" "Field 2" "State" "$pathAssemblyInterfaces"   
  Goto close
morethan1:	
  MessageBox MB_OKCANCEL "Найдено несколько файлов интерфейсной сборки. Укажите путь к конфигурационному файлу вручную"
  Goto close
close:
	${locate::Close} $0
	${locate::Unload}	
FunctionEnd