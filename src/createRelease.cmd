cd C:\src\GreenFeetWorkFlow\src
dotnet pack --include-source   -p:PackageVersion=0.0.0.x  -o:.\releases
cd releases
dotnet nuget push  *.symbols.nupkg -s https://api.nuget.org/v3/index.json --skip-duplicate --api-key MYKEYHERE 
