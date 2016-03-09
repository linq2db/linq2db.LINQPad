del linq2db.LINQPad.Driver.lpx
"C:\Program Files\7-Zip\7z.exe" a linq2db.LINQPad.Driver.zip %1\*.*
"C:\Program Files\7-Zip\7z.exe" d linq2db.LINQPad.Driver.zip %1\*.exe

ren linq2db.LINQPad.Driver.zip linq2db.LINQPad.Driver.lpx
xcopy /i/y %1*.dll "%programdata%\LINQPad\Drivers\DataContext\4.0\linq2db.LINQPad.Driver (f19f8aed7feff67e)\*.*"
xcopy /i/y %1*.pdb "%programdata%\LINQPad\Drivers\DataContext\4.0\linq2db.LINQPad.Driver (f19f8aed7feff67e)\*.*"
xcopy /i/y %1*.xml "%programdata%\LINQPad\Drivers\DataContext\4.0\linq2db.LINQPad.Driver (f19f8aed7feff67e)\*.*"
