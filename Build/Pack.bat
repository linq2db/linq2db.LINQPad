ECHO OFF
ECHO Packing %2

DEL linq2db.LINQPad.%2
DEL linq2db.LINQPad.%2.zip

REM LINQPad 5 driver archive generation
IF %2 EQU lpx (
	REM xcopy /s /y ..\Redist\IBM\*.dll %1

	REM remove resource satellite assemblies
	RD /S /Q %1\cs
	RD /S /Q %1\de
	RD /S /Q %1\es
	RD /S /Q %1\fr
	RD /S /Q %1\it
	RD /S /Q %1\ja
	RD /S /Q %1\ko
	RD /S /Q %1\pl
	RD /S /Q %1\pt
	RD /S /Q %1\pt-BR
	RD /S /Q %1\ru
	RD /S /Q %1\tr
	RD /S /Q %1\zh-Hans
	RD /S /Q %1\zh-Hant

	REM remove not needed files
	DEL /Q %1\linq2db.*.xml
	DEL /Q %1\*.pdb

	"C:\Program Files\7-Zip\7z.exe" -r a linq2db.LINQPad.%2.zip %1\*.* %1\..\..\..\..\Build\Connection.png %1\..\..\..\..\Build\FailedConnection.png %1\..\..\..\..\Build\header.xml
)

REM LINQPad 7 driver archive generation
IF %2 EQU lpx6 ("C:\Program Files\7-Zip\7z.exe" a linq2db.LINQPad.%2.zip %1\linq2db.LINQPad.dll %1\..\..\..\..\Build\Connection.png %1\..\..\..\..\Build\FailedConnection.png %1\linq2db.LINQPad.deps.json)

REN linq2db.LINQPad.%2.zip linq2db.LINQPad.%2

