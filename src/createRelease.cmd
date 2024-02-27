cd C:\src\MicroWorkflow\src
copy ..\README.md .\Product\MicroWorkflow\README.md
dotnet build
dotnet pack --include-source   -p:PackageVersion=1.5.0.0  -o:.\releases
cd releases
# dotnet nuget push  *.symbols.nupkg -s https://api.nuget.org/v3/index.json --skip-duplicate --api-key MYKEYHERE 
# del *
cd ..