# Genera la matriz de 38 pruebas y el log de pruebas automaticas a partir del TRX real.
# Uso: dotnet test -c Release --logger "trx;LogFileName=resultados.trx" --results-directory docs/evidencias/trx
#      powershell -File tools\generar-matriz-pruebas.ps1
# El "resultado obtenido" sale solo del TRX: no se escribe a mano.
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$trxRuta = Join-Path $raiz 'docs\evidencias\trx\resultados.trx'
$casosRuta = Join-Path $PSScriptRoot 'casos-aceptacion.csv'
$capturas = Join-Path $raiz 'docs\evidencias\capturas'
$destino = Join-Path $raiz 'docs\matrices\matriz-38-pruebas.md'
$logRuta = Join-Path $raiz 'docs\evidencias\logs\log-pruebas-automaticas.txt'

[xml]$trx = Get-Content -LiteralPath $trxRuta -Encoding UTF8
$ns = New-Object System.Xml.XmlNamespaceManager($trx.NameTable)
$ns.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')

$defs = @{}
foreach ($d in $trx.SelectNodes('//t:UnitTest', $ns)) {
    $m = $d.SelectSingleNode('t:TestMethod', $ns)
    $defs[$d.id] = "$($m.className -replace '^LogiMarket\.Tests\.','').$($m.name)"
}
$pruebas = foreach ($r in $trx.SelectNodes('//t:UnitTestResult', $ns)) {
    $clase = ($defs[$r.testId]) -replace '\.[^.]+$', ''
    [pscustomobject]@{
        Nombre   = $r.testName -replace '^LogiMarket\.Tests\.', ''
        Clase    = $clase
        Resultado= $r.outcome
        Inicio   = [datetime]$r.startTime
        Duracion = [timespan]$r.duration
    }
}
$total = @($pruebas).Count
$ok = @($pruebas | Where-Object Resultado -eq 'Passed').Count

# Log legible de las pruebas automaticas
$lineas = @("LogiMarket 360 - log de pruebas automaticas (fuente: docs/evidencias/trx/resultados.trx)",
            "Total: $total   Superadas: $ok   Falladas: $($total - $ok)", "")
$lineas += $pruebas | Sort-Object Nombre | ForEach-Object {
    '{0} [{1}] {2} ({3} ms)' -f $_.Inicio.ToString('yyyy-MM-dd HH:mm:ss'), $_.Resultado, $_.Nombre, [int]$_.Duracion.TotalMilliseconds }
New-Item -ItemType Directory -Force (Split-Path $logRuta) | Out-Null
[IO.File]::WriteAllLines($logRuta, $lineas, (New-Object Text.UTF8Encoding($true)))

# Logs por caso (salida estandar real de las pruebas capturada en el TRX)
$nombresLog = @{ P19 = 'P19_decorator'; P21 = 'P21_flyweight'; P22 = 'P22_proxy'; P36 = 'P36_concurrencia'; P37 = 'P37_outbox' }
foreach ($id in $nombresLog.Keys) {
    $out = @("Caso $id - salida de las pruebas (fuente: TRX)", '')
    foreach ($r in ($trx.SelectNodes('//t:UnitTestResult', $ns) | Sort-Object testName)) {
        if ($r.testName -match "(^|\.)${id}_") {
            $so = $r.SelectSingleNode('t:Output/t:StdOut', $ns)
            $out += "## $($r.testName -replace '^LogiMarket\.Tests\.','') -> $($r.outcome)"
            $out += if ($so) { ($so.InnerText.Trim() -split "`r?`n") } else { '(sin salida)' }
            $out += ''
        }
    }
    [IO.File]::WriteAllLines((Join-Path $raiz "docs\evidencias\logs\$($nombresLog[$id]).txt"), $out, (New-Object Text.UTF8Encoding($true)))
}

# Matriz
$casos = Import-Csv -LiteralPath $casosRuta -Delimiter '~' -Encoding UTF8
$md = New-Object System.Text.StringBuilder
[void]$md.AppendLine('# Matriz de 38 pruebas')
[void]$md.AppendLine('')
[void]$md.AppendLine("Generada por ``tools/generar-matriz-pruebas.ps1`` desde ``docs/evidencias/trx/resultados.trx``. Pruebas automatizadas: **$ok de $total superadas**.")
[void]$md.AppendLine('Resultado obtenido = salida real del ejecutor de pruebas. Una captura se cita como evidencia solo si el archivo existe en `docs/evidencias/capturas`; si no, aparece como *pendiente*.')
[void]$md.AppendLine('')
[void]$md.AppendLine('| Caso | Entrada / escenario | Resultado esperado | Resultado obtenido | Evidencia | Fecha | Archivo / metodo |')
[void]$md.AppendLine('|---|---|---|---|---|---|---|')
$sinPruebas = @()
foreach ($c in $casos) {
    if ($c.ID -eq 'P38') {
        $sel = @($pruebas | Where-Object { $_.Clase -like 'Arquitectura.*' })
    } else {
        $sel = @($pruebas | Where-Object { $_.Nombre -match "(^|\.)$($c.ID)_" })
    }
    if ($sel.Count -eq 0) { $sinPruebas += $c.ID }
    $pas = @($sel | Where-Object Resultado -eq 'Passed').Count
    $obt = if ($sel.Count -eq 0) { 'SIN PRUEBA AUTOMATIZADA' } elseif ($c.ID -eq 'P38') { "$pas/$($sel.Count) pruebas de arquitectura superadas; suite completa $ok/$total" } else { "$pas/$($sel.Count) pruebas superadas" }
    $ev = @()
    foreach ($n in ($c.Capturas -split '\|' | Where-Object { $_ })) {
        if (Test-Path (Join-Path $capturas $n)) { $ev += "capturas/$n" } else { $ev += "capturas/$n (pendiente)" }
    }
    foreach ($l in ($c.Logs -split '\|' | Where-Object { $_ })) {
        if (Test-Path (Join-Path $raiz "docs\evidencias\$l")) { $ev += $l } else { $ev += "$l (pendiente)" }
    }
    $fecha = if ($sel.Count) { ($sel | Measure-Object Inicio -Minimum).Minimum.ToString('yyyy-MM-dd') } else { '-' }
    $met = if ($sel.Count) { (($sel | ForEach-Object { '`' + (($_.Nombre -replace '\(.*$', '') -replace '^([^.]+)\.', '$1/') + '`' } | Select-Object -Unique) -join '<br>') } else { '-' }
    [void]$md.AppendLine("| $($c.ID) | $($c.Entrada) | $($c.Esperado) | $obt | $($ev -join '<br>') | $fecha | $met |")
}
[IO.File]::WriteAllText($destino, $md.ToString(), (New-Object Text.UTF8Encoding($false)))
Write-Host "Matriz: $destino"
Write-Host "Pruebas: $ok/$total superadas. Casos sin prueba automatizada: $(if ($sinPruebas) { $sinPruebas -join ', ' } else { 'ninguno' })"

# Matriz de 23 patrones
$pat = Import-Csv -LiteralPath (Join-Path $PSScriptRoot 'patrones.csv') -Delimiter '~' -Encoding UTF8
$m2 = New-Object System.Text.StringBuilder
[void]$m2.AppendLine('# Matriz de 23 patrones GoF')
[void]$m2.AppendLine('')
[void]$m2.AppendLine("Generada por ``tools/generar-matriz-pruebas.ps1`` (datos en ``tools/patrones.csv``). El resultado de la prueba sale del TRX; las rutas apuntan a los participantes reales del codigo. Suite: **$ok de $total superadas**.")
[void]$m2.AppendLine('')
[void]$m2.AppendLine('| N | Tipo | Patron | Problema que resuelve | Participantes reales | Proyecto / archivo | Prueba (caso) | Resultado | Captura |')
[void]$m2.AppendLine('|---|---|---|---|---|---|---|---|---|')
foreach ($p in $pat) {
    $sel = @($pruebas | Where-Object { $_.Nombre -match "(^|\.)$($p.Prueba)_" })
    $pas = @($sel | Where-Object Resultado -eq 'Passed').Count
    $cap = if ($p.Captura -eq '-') { 'log de pruebas' } elseif (Test-Path (Join-Path $capturas $p.Captura)) { "capturas/$($p.Captura)" } else { "capturas/$($p.Captura) (pendiente)" }
    [void]$m2.AppendLine("| $($p.N) | $($p.Tipo) | $($p.Patron) | $($p.Problema) | $($p.Participantes) | $($p.Archivo) | $($p.Prueba) | $pas/$($sel.Count) superadas | $cap |")
}
[IO.File]::WriteAllText((Join-Path $raiz 'docs\matrices\matriz-23-patrones.md'), $m2.ToString(), (New-Object Text.UTF8Encoding($false)))
Write-Host "Matriz de patrones: docs\matrices\matriz-23-patrones.md ($(@($pat).Count) patrones)"
