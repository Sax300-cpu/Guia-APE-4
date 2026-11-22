<#
 Ejecuta la autoprueba de la interfaz WinForms: arma un escenario real con los casos de uso, recorre las 12 pantallas
 por rol, comprueba el menu por permisos y guarda una imagen de cada pantalla.
 Usa una base temporal propia: NO toca la base de datos del usuario.

 Uso:   .\tools\ejecutar-autoprueba.ps1 [-Carpeta docs\evidencias\capturas-automaticas]
#>
param([string]$Carpeta = (Join-Path $PSScriptRoot "..\docs\evidencias\autoprueba-ui"))

$raiz = Resolve-Path (Join-Path $PSScriptRoot "..")
dotnet build (Join-Path $raiz "LogiMarket360.sln") -c Release | Out-Null
if ($LASTEXITCODE -ne 0) { throw "La compilacion fallo." }

$exe = Get-ChildItem (Join-Path $raiz "src\LogiMarket.WinForms\bin\Release") -Recurse -Filter LogiMarket.WinForms.exe | Select-Object -First 1
$null = New-Item -ItemType Directory -Force $Carpeta
$carpetaAbs = (Resolve-Path $Carpeta).Path
$db = Join-Path $env:TEMP ("autoprueba_" + [guid]::NewGuid().ToString("N") + ".db")

$p = Start-Process $exe.FullName -ArgumentList "--autoprueba", "--capturas", "`"$carpetaAbs`"", "--db", "`"$db`"" -PassThru
if (-not $p.WaitForExit(180000)) { Stop-Process -Id $p.Id -Force; throw "La autoprueba excedio el tiempo limite." }
Remove-Item "$db*" -Force -ErrorAction SilentlyContinue

Get-Content (Join-Path $carpetaAbs "autoprueba.txt")
"Codigo de salida: $($p.ExitCode)  (0 = correcto)"
exit $p.ExitCode
