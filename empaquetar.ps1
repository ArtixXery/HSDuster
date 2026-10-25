# Compila el plugin y arma dist\RomperCartas-<versión>.zip listo para descomprimir en la carpeta Plugins de HDT.
# Uso: .\empaquetar.ps1
$ErrorActionPreference = 'Stop'
$raiz = $PSScriptRoot
$proyecto = Join-Path $raiz 'src\RomperCartas\RomperCartas.csproj'

dotnet build $proyecto -c Release
if ($LASTEXITCODE -ne 0) { throw 'La compilación falló' }

$version = ([xml](Get-Content $proyecto)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$temp = Join-Path $raiz 'dist\tmp'
Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force "$temp\RomperCartas\datos" | Out-Null

Copy-Item (Join-Path $raiz 'src\RomperCartas\bin\Release\RomperCartas.dll') $temp
Copy-Item (Join-Path $raiz 'datos\*.json') "$temp\RomperCartas\datos"

$zip = Join-Path $raiz "dist\RomperCartas-$version.zip"
Remove-Item $zip -ErrorAction SilentlyContinue
Compress-Archive -Path "$temp\*" -DestinationPath $zip
Remove-Item $temp -Recurse -Force
Write-Host "Listo: $zip"
