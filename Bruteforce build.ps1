
Get-ChildItem -Recurse -Filter *.csproj | ForEach-Object { dotnet clean $_.FullName }
Get-ChildItem -Recurse -Filter *.csproj | ForEach-Object { dotnet build $_.FullName }