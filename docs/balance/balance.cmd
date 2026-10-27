@echo off
rem Regenera balance-realistic.xlsx y .md (esta misma carpeta) con el motor vigente y los abre al terminar.
rem Herramienta manual: nunca corre en pipelines ni en la suite completa (el test exige RFF_BALANCE=1).
setlocal
cd /d "%~dp0..\.."

rem Si Excel tiene abierto el reporte, cierra ese libro forzosamente (descarta cambios sin avisar; otros libros quedan intactos).
powershell.exe -NoProfile -NonInteractive -Command "$f = (Get-Location).Path + '\docs\balance\balance-realistic.xlsx'; try { $s = [IO.File]::Open($f, 'Open', 'ReadWrite', 'None'); $s.Close(); Write-Output 'Reporte sin lock en Excel.' } catch { $cerrado = $false; try { $xl = [Runtime.InteropServices.Marshal]::GetActiveObject('Excel.Application'); $wb = @($xl.Workbooks | Where-Object { $_.FullName -eq $f }); if ($wb.Count -gt 0) { $wb | ForEach-Object { $_.Close($false) }; $cerrado = $true; Write-Output 'Reporte cerrado forzosamente en Excel (sin guardar).' } } catch {} if (-not $cerrado) { taskkill /F /IM EXCEL.exe | Out-Null; Write-Output 'EXCEL.EXE cerrado forzosamente.' } }"

set RFF_BALANCE=1
dotnet test "Realistic Failures Solution\RealFenixFailures.TestUnits\RealFenixFailures.TestUnits.csproj" -c Debug --filter BalanceReport --logger "console;verbosity=minimal"
if errorlevel 1 (
    echo No se abre el reporte: la generacion fallo. Revisar la salida de arriba.
    exit /b 1
)
start "" "%~dp0balance-realistic.xlsx"
endlocal
