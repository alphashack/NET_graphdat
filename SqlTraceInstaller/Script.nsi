;##################################################################
;# The Graphdat SqlTrace installer - built using NCIS, thanks guys.
;# http://www.graphdat.com for more details.
;##################################################################

;Set compressor before outputting anything
SetCompressor /SOLID LZMA

;--------------------
;libraries to include
	!include "InstallOptions.nsh"
	!include "LogicLib.nsh"
	!include "MUI2.nsh"
 
;-------------
;hack for getting the file version
	!macro GetVersionLocal file basedef
		!verbose push
		!verbose 1
		!tempfile _GetVersionLocal_nsi
		!tempfile _GetVersionLocal_exe
		!appendfile "${_GetVersionLocal_nsi}" 'Outfile "${_GetVersionLocal_exe}"$\nRequestexecutionlevel user$\n'
		!appendfile "${_GetVersionLocal_nsi}" 'Section$\n!define D "$"$\n!define N "${D}\n"$\n'
		!appendfile "${_GetVersionLocal_nsi}" 'GetDLLVersion "${file}" $2 $4$\n'
		!appendfile "${_GetVersionLocal_nsi}" 'IntOp $1 $2 / 0x00010000$\nIntOp $2 $2 & 0x0000FFFF$\n'
		!appendfile "${_GetVersionLocal_nsi}" 'IntOp $3 $4 / 0x00010000$\nIntOp $4 $4 & 0x0000FFFF$\n'
		!appendfile "${_GetVersionLocal_nsi}" 'FileOpen $0 "${_GetVersionLocal_nsi}" w$\nStrCpy $9 "${N}"$\n'
		!appendfile "${_GetVersionLocal_nsi}" 'FileWrite $0 "!define ${basedef}1 $1$9"$\nFileWrite $0 "!define ${basedef}2 $2$9"$\n'
		!appendfile "${_GetVersionLocal_nsi}" 'FileWrite $0 "!define ${basedef}3 $3$9"$\nFileWrite $0 "!define ${basedef}4 $4$9"$\n'
		!appendfile "${_GetVersionLocal_nsi}" 'FileClose $0$\nSectionend$\n'
		!system '"${NSISDIR}\makensis" -NOCD -NOCONFIG "${_GetVersionLocal_nsi}"' = 0
		!system '"${_GetVersionLocal_exe}" /S' = 0
		!delfile "${_GetVersionLocal_exe}"
		!undef _GetVersionLocal_exe
		!include "${_GetVersionLocal_nsi}"
		!delfile "${_GetVersionLocal_nsi}"
		!undef _GetVersionLocal_nsi
		!verbose pop
	!macroend

;------------
;product info
	!define PRODUCT_NAME      "Graphdat SqlTrace Service"
	!define PUBLISHER         "Alphashack"
	!define WEBSITE_URL       "http://www.graphdat.com/"
	!define PRODUCT_UNINSTALL "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}"
	!define PROGRAM_NAME      "GraphdatAgentSqlTrace.exe"
	!define SERVICE_NAME	  "Graphdat-SqlTrace"

	!insertmacro GetVersionLocal "..\GraphdatSqlTrace\bin\Debug\${PROGRAM_NAME}" MyVer_
	!define PRODUCT_VERSION   "${MyVer_1}.${MyVer_2}.${MyVer_3}.${MyVer_4}"

	Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"

;--------------
;installer info
	!define CONFIGURATION_DIALOG "configuration_dialog.ini"
	!define CONTINUE_INSTALL_DIALOG "continue_install_dialog.ini"
	!define HEADER_IMAGE "contrib\Graphics\Header\graphdat.bmp"

;-----------------------
;Installer's VersionInfo
	VIProductVersion                   "${PRODUCT_VERSION}"
	VIAddVersionKey "CompanyName"      "${PUBLISHER}"
	VIAddVersionKey "ProductName"      "${PRODUCT_NAME}" 
	VIAddVersionKey "ProductVersion"   "${PRODUCT_VERSION}"
	VIAddVersionKey "FileDescription"  "${PRODUCT_NAME}"
	VIAddVersionKey "FileVersion"      "${PRODUCT_VERSION}"
	VIAddVersionKey "LegalCopyright"   "${PUBLISHER}"

;-----------------------
;modern UI configuration
	!define MUI_HEADERIMAGE
	!define MUI_HEADERIMAGE_BITMAP "${HEADER_IMAGE}"
	!define MUI_HEADERIMAGE_UNBITMAP "${HEADER_IMAGE}"
	!define MUI_ABORTWARNING

;------------------
;custom form fields
	!define CHK_EDIT_CONFIGURATION_FILE "Field 2"
	!define CHK_VIEW_README_FILE "Field 3"

;-----
;Pages

	;installer
	!insertmacro MUI_PAGE_DIRECTORY
	Page custom ConfigurationPage ValidateConfigurationPage
	Page custom ContinueInstallPage
	!insertmacro MUI_PAGE_INSTFILES

	;uninstaller  
	!insertmacro MUI_UNPAGE_CONFIRM
	!insertmacro MUI_UNPAGE_INSTFILES

;---------
;Languages
	!insertmacro MUI_LANGUAGE "English"

;----------------
;general settings
	
	;name of the installer
	OutFile "GraphdatSqlTraceSetup.exe"

	;the default installation folder
	InstallDir "$PROGRAMFILES\${PUBLISHER}\${PRODUCT_NAME}"
  
	;get installation folder from registry if available
	InstallDirRegKey HKLM "${PRODUCT_UNINSTALL}" "UninstallString"

	;request application privileges for Windows Vista
	RequestExecutionLevel admin

;-------------
;reserve files
	
	;Things that need to be extracted on first (keep these lines before any File command!)
	ReserveFile "${CONFIGURATION_DIALOG}"
	ReserveFile "${HEADER_IMAGE}"

;-----------------
;Installer Section

	Section "-default files"
		
		;if the installer is already there, need to uninstall it first
		${If} ${FileExists} "$INSTDIR\${PROGRAM_NAME}"
			DetailPrint "Stopping the service..."
			nsExec::Exec 'net stop "${SERVICE_NAME}"'
			Sleep 500
		
			DetailPrint "Uninstalling the service..."
			nsExec::Exec 'c:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe /uninstall "$INSTDIR\${PROGRAM_NAME}"'
			Sleep 500
		${EndIf}

		;set output path to the installation directory.
		SetOutPath $INSTDIR
		File "..\GraphdatSqlTrace\bin\Debug\${PROGRAM_NAME}"
		File "..\GraphdatSqlTrace\bin\Debug\GraphdatAgentConnect.dll"
		File "..\GraphdatSqlTrace\bin\Debug\MsgPack.dll"
		File "..\GraphdatSqlTrace\bin\Debug\GraphdatAgentSqlQueryHelper.dll"
		File "..\GraphdatSqlTrace\lib\Gehtsoft.PCRE.dll"

		File "c:\Program Files (x86)\Microsoft Visual Studio 10.0\VSTSDB\Microsoft.Data.Schema.ScriptDom.dll"
		File "c:\Program Files (x86)\Microsoft Visual Studio 10.0\VSTSDB\Microsoft.Data.Schema.ScriptDom.Sql.dll"
		File "C:\Program Files\Microsoft SQL Server\100\SDK\Assemblies\Microsoft.SqlServer.ConnectionInfo.dll"
		File "C:\Program Files\Microsoft SQL Server\100\SDK\Assemblies\Microsoft.SqlServer.ConnectionInfoExtended.dll"
		File "C:\Windows\assembly\GAC_MSIL\Microsoft.SqlServer.Diagnostics.STrace\10.0.0.0__89845dcd8080cc91\Microsoft.SqlServer.Diagnostics.STrace.dll"
		File "C:\Program Files\Microsoft SQL Server\100\SDK\Assemblies\Microsoft.SqlServer.Dmf.dll"
		File "C:\Program Files\Microsoft SQL Server\100\SDK\Assemblies\Microsoft.SqlServer.Management.Sdk.Sfc.dll"
		File "C:\Windows\assembly\GAC_MSIL\Microsoft.SqlServer.Management.SmoMetadataProvider\10.0.0.0__89845dcd8080cc91\Microsoft.SqlServer.Management.SmoMetadataProvider.dll"
		File "C:\Windows\assembly\GAC_MSIL\Microsoft.SqlServer.Management.SqlParser\10.0.0.0__89845dcd8080cc91\Microsoft.SqlServer.Management.SqlParser.dll"
		File "C:\Program Files\Microsoft SQL Server\100\SDK\Assemblies\Microsoft.SqlServer.ServiceBrokerEnum.dll"
		File "C:\Program Files\Microsoft SQL Server\100\SDK\Assemblies\Microsoft.SqlServer.Smo.dll"
		File "C:\Windows\assembly\GAC_MSIL\Microsoft.SqlServer.SqlClrProvider\10.0.0.0__89845dcd8080cc91\Microsoft.SqlServer.SqlClrProvider.dll"
		File "C:\Program Files\Microsoft SQL Server\100\SDK\Assemblies\Microsoft.SqlServer.SqlEnum.dll"

		File "C:\Windows\assembly\GAC_MSIL\Microsoft.SqlServer.Instapi\10.0.0.0__89845dcd8080cc91\Microsoft.SqlServer.InstApi.dll"

		; Write the uninstall keys for Windows
		WriteRegStr   HKLM "${PRODUCT_UNINSTALL}" "DisplayName"     "${PRODUCT_NAME}"
		WriteRegStr   HKLM "${PRODUCT_UNINSTALL}" "DisplayIcon"     "$INSTDIR\${PROGRAM_NAME}"
		WriteRegStr   HKLM "${PRODUCT_UNINSTALL}" "DisplayVersion"  "${PRODUCT_VERSION}"
		WriteRegStr   HKLM "${PRODUCT_UNINSTALL}" "HelpLink"        "${WEBSITE_URL}"
		WriteRegStr   HKLM "${PRODUCT_UNINSTALL}" "InstallLocation" "$INSTDIR"
		WriteRegStr   HKLM "${PRODUCT_UNINSTALL}" "URLInfoAbout"    "${WEBSITE_URL}"
		WriteRegStr   HKLM "${PRODUCT_UNINSTALL}" "URLUpdateInfo"   "${WEBSITE_URL}"
		WriteRegStr   HKLM "${PRODUCT_UNINSTALL}" "UninstallString" "$INSTDIR\uninstall.exe"
		WriteRegStr   HKLM "${PRODUCT_UNINSTALL}" "Publisher"       "${PUBLISHER}"
		WriteRegDWORD HKLM "${PRODUCT_UNINSTALL}" "NoModify"        "1"
		WriteRegDWORD HKLM "${PRODUCT_UNINSTALL}" "NoRepair"        "1"

		WriteUninstaller "$INSTDIR\uninstall.exe"

	SectionEnd

	Section "-PostInst"

		;write the installation path into the registry
		WriteRegStr HKCU "SOFTWARE\${PRODUCT_NAME}" "Install_Dir" "$INSTDIR"
		WriteRegStr HKLM "SOFTWARE\${PRODUCT_NAME}" "Install_Dir" "$INSTDIR"

		;installing service
		DetailPrint "Installing the service..."
		nsExec::Exec 'c:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe /install "$INSTDIR\${PROGRAM_NAME}"'
		Sleep 500

		;start the service
		DetailPrint "Starting up the service..."
		nsExec::Exec 'net start "${SERVICE_NAME}"'
		Sleep 500

	SectionEnd

;--------------------------------
;installer functions
	Function .onInit
	
		;detect windows type (NT or 9x)
		ReadRegStr $R0 HKLM "SOFTWARE\Microsoft\Windows NT\CurrentVersion" CurrentVersion
		StrCmp $R0 "" 0 detection_NT

		;we are not NT.
		MessageBox MB_OK|MB_ICONSTOP "The Graphdat Agent does not support Windows 95/98/ME"
		Abort
  
	detection_NT:
	FunctionEnd

	LangString CONFIGURATION_PAGE_TITLE ${LANG_ENGLISH} "Graphdat SqlTrace Configuration"
	LangString CONFIGURATION_PAGE_SUBTITLE ${LANG_ENGLISH} "Configuring SqlTrace"

	Function ConfigurationPage 
		;Extract InstallOptions INI files
		!insertmacro INSTALLOPTIONS_EXTRACT "${CONFIGURATION_DIALOG}"
		!insertmacro MUI_HEADER_TEXT "$(CONFIGURATION_PAGE_TITLE)" "$(CONFIGURATION_PAGE_SUBTITLE)"
		!insertmacro INSTALLOPTIONS_DISPLAY "${CONFIGURATION_DIALOG}"
	FunctionEnd

	Function ValidateConfigurationPage
		SetOutPath $INSTDIR
		${Unless} ${FileExists} "$INSTDIR\${PROGRAM_NAME}.config"
			File "..\GraphdatSqlTrace\bin\Debug\${PROGRAM_NAME}.config"
		${EndUnless}
		File "..\GraphdatSqlTrace\bin\Debug\README.md"

		Var /GLOBAL editConfigurationFile
		!insertmacro INSTALLOPTIONS_READ $editConfigurationFile "${CONFIGURATION_DIALOG}" "${CHK_EDIT_CONFIGURATION_FILE}" "State"
		Push "$editConfigurationFile"
		
		Var /GLOBAL viewReadmeFile
		!insertmacro INSTALLOPTIONS_READ $viewReadmeFile "${CONFIGURATION_DIALOG}" "${CHK_VIEW_README_FILE}" "State"
		Push "$viewReadmeFile"
		
		${If} $editConfigurationFile = 1
			Exec 'notepad "$INSTDIR\${PROGRAM_NAME}.config"'
		${EndIf}

		${If} $viewReadmeFile = 1
			Exec 'notepad "$INSTDIR\README.md"'
		${EndIf}
	FunctionEnd

	LangString CONTINUE_INSTALL_PAGE_TITLE ${LANG_ENGLISH} "Graphdat SqlTrace Configuration"
	LangString CONTINUE_INSTALL_PAGE_SUBTITLE ${LANG_ENGLISH} "Configuring SqlTrace"

	Function ContinueInstallPage 
		;Extract InstallOptions INI files
		!insertmacro INSTALLOPTIONS_EXTRACT "${CONTINUE_INSTALL_DIALOG}"
		!insertmacro MUI_HEADER_TEXT "$(CONTINUE_INSTALL_PAGE_TITLE)" "$(CONTINUE_INSTALL_PAGE_SUBTITLE)"
		!insertmacro INSTALLOPTIONS_DISPLAY "${CONTINUE_INSTALL_DIALOG}"
	FunctionEnd

;--------------------------------
;Uninstaller Section

	Section "Uninstall"

		;stopping and uninstalling service
		DetailPrint "Stopping the service..."
		nsExec::Exec 'net stop "${SERVICE_NAME}"'
		Sleep 500
		
		DetailPrint "Uninstalling the service..."
		nsExec::Exec 'c:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe /uninstall "$INSTDIR\${PROGRAM_NAME}"'
		Sleep 500

		;remove registry keys
		DeleteRegValue HKCU "Software\${PRODUCT_NAME}" "Install_Dir"
		DeleteRegValue HKLM "Software\${PRODUCT_NAME}" "Install_Dir"
		DeleteRegKey /ifempty HKCU "Software\${PRODUCT_NAME}"
		DeleteRegKey /ifempty HKLM "Software\${PRODUCT_NAME}"
		DeleteRegKey HKLM "${PRODUCT_UNINSTALL}"

		;remove files
		Delete "$INSTDIR\${PROGRAM_NAME}"
		Delete "$INSTDIR\uninstall.exe"
		Delete "$INSTDIR\${PROGRAM_NAME}.config"
		Delete "$INSTDIR\GraphdatAgentConnect.dll"
		Delete "$INSTDIR\MsgPack.dll"
		Delete "$INSTDIR\GraphdatAgentSqlQueryHelper.dll"
		Delete "$INSTDIR\README.md"
		Delete "$INSTDIR\GraphdatAgentSqlTrace.InstallLog"
		Delete "$INSTDIR\InstallUtil.InstallLog"
		Delete "$INSTDIR\Gehtsoft.PCRE.dll"

		Delete "$INSTDIR\Microsoft.Data.Schema.ScriptDom.dll"
		Delete "$INSTDIR\Microsoft.Data.Schema.ScriptDom.Sql.dll"
		Delete "$INSTDIR\Microsoft.SqlServer.ConnectionInfo.dll"
		Delete "$INSTDIR\Microsoft.SqlServer.ConnectionInfoExtended.dll"
		Delete "$INSTDIR\Microsoft.SqlServer.Diagnostics.STrace.dll"
		Delete "$INSTDIR\Microsoft.SqlServer.Dmf.dll"
		Delete "$INSTDIR\Microsoft.SqlServer.Management.Sdk.Sfc.dll"
		Delete "$INSTDIR\Microsoft.SqlServer.Management.SmoMetadataProvider.dll"
		Delete "$INSTDIR\Microsoft.SqlServer.Management.SqlParser.dll"
		Delete "$INSTDIR\Microsoft.SqlServer.ServiceBrokerEnum.dll"
		Delete "$INSTDIR\Microsoft.SqlServer.Smo.dll"
		Delete "$INSTDIR\Microsoft.SqlServer.SqlClrProvider.dll"
		Delete "$INSTDIR\Microsoft.SqlServer.SqlEnum.dll"

		Delete "$INSTDIR\Microsoft.SqlServer.InstApi.dll"

		Delete "$INSTDIR\*.trc"

		;remove directories used.
		RMDir "$INSTDIR"
		RMDir "$PROGRAMFILES\${PUBLISHER}"
  
	SectionEnd

; End of file
