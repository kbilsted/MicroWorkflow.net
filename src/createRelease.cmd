cd C:\src\GreenFeetWorkFlow\src
copy ..\README.md .\Product\GreenFeetWorkFlow\README.md
dotnet build
dotnet pack --include-source   -p:PackageVersion=1.3.2.0  -o:.\releases
cd releases
# dotnet nuget push  *.symbols.nupkg -s https://api.nuget.org/v3/index.json --skip-duplicate --api-key MYKEYHERE 
# del *
cd ..