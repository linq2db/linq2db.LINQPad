del linq2db.LINQPad.lpx
"C:\Program Files\7-Zip\7z.exe" -r a linq2db.LINQPad.zip %1\*.*
"C:\Program Files\7-Zip\7z.exe" -r d linq2db.LINQPad.zip %1\*.exe

ren linq2db.LINQPad.zip linq2db.LINQPad.lpx
xcopy /i/y/s %1*.dll "%programdata%\LINQPad\Drivers\DataContext\4.0\linq2db.LINQPad (f19f8aed7feff67e)\*.*"
xcopy /i/y/s %1*.pdb "%programdata%\LINQPad\Drivers\DataContext\4.0\linq2db.LINQPad (f19f8aed7feff67e)\*.*"
xcopy /i/y/s %1*.xml "%programdata%\LINQPad\Drivers\DataContext\4.0\linq2db.LINQPad (f19f8aed7feff67e)\*.*"
