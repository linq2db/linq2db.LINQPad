@echo off
echo.
echo Packing %2
echo.
@del linq2db.LINQPad.%2
@del linq2db.LINQPad.%2.zip
@del %1\LINQPad.exe
@del %1\LINQPad.Runtime.dll
"C:\Program Files\7-Zip\7z.exe" -r a linq2db.LINQPad.%2.zip %1\*.*

ren linq2db.LINQPad.%2.zip linq2db.LINQPad.%2

