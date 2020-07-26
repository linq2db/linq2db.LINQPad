powershell ..\Build\BuildNuspecs.ps1 -path *.nuspec -version 3.0.0-local1

rmdir built /S /Q
md built

..\Redist\NuGet Pack linq2db.LINQPad.nuspec -OutputDirectory built
