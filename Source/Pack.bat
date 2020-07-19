@echo off
echo.
echo Packing %2
echo.
@del linq2db.LINQPad.%2
@del linq2db.LINQPad.%2.zip

IF %2 EQU lpx (
	rem xcopy /s /y ..\Redist\IBM\*.dll %1
	rem remove resource satellite assemblies
	rd /S /Q %1\cs
	rd /S /Q %1\de
	rd /S /Q %1\es
	rd /S /Q %1\fr
	rd /S /Q %1\it
	rd /S /Q %1\ja
	rd /S /Q %1\ko
	rd /S /Q %1\pl
	rd /S /Q %1\pt-BR
	rd /S /Q %1\ru
	rd /S /Q %1\tr
	rd /S /Q %1\zh-Hans
	rd /S /Q %1\zh-Hant
	del /Q %1\*.pdb
	"C:\Program Files\7-Zip\7z.exe" -r a linq2db.LINQPad.%2.zip %1\*.*
)
IF %2 EQU lpx6 ("C:\Program Files\7-Zip\7z.exe" a linq2db.LINQPad.%2.zip %1\linq2db.LINQPad.dll %1\Connection.png %1\FailedConnection.png %1\linq2db.LINQPad.deps.json)


ren linq2db.LINQPad.%2.zip linq2db.LINQPad.%2

