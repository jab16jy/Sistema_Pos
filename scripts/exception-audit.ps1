#requires -Version 7.0

[CmdletBinding()]
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [switch]$AsJson,
    [switch]$FailOnHigh,
    [switch]$IncludeTests,
    [switch]$IncludeAtl
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$patterns = @(
    @{ Id = 'bare-catch'; Regex = 'catch\s*\{'; Severity = 'high'; Recommendation = 'Reemplaza catch vacío por manejo explícito o justifica el best-effort.' },
    @{ Id = 'fire-and-forget'; Regex = '_\s*=\s*[^;]+Async\(|new\s+RelayCommand\([^\n;]*async\s+_?\s*=>|new\s+RelayCommand\([^\n;]*=>\s*(?!RunSafe)[^;]*Async\('; Severity = 'medium'; Recommendation = 'Usa RunSafe, AsyncRelayCommand o captura explícita de excepciones.' },
    @{ Id = 'async-void'; Regex = '\basync\s+void\b'; Severity = 'high'; Recommendation = 'Limita async void a eventos UI y encapsula await en try/catch.' },
    @{ Id = 'blocking-await'; Regex = 'GetAwaiter\(\)\.GetResult\(\)|\.Wait\('; Severity = 'high'; Recommendation = 'Evita bloqueo síncrono sobre Task en UI; usa await o comando asíncrono seguro.' },
    @{ Id = 'shutdown-mode'; Regex = 'ShutdownMode|ShowDialog\(|\.Close\('; Severity = 'info'; Recommendation = 'Revisa el orden de cierre/reapertura en flujos WPF sensibles.' },
    @{ Id = 'required-service-catch'; Regex = 'GetRequiredService<[^>]+>\([^\)]*\);?\s*}\s*catch|GetRequiredService<'; Severity = 'info'; Recommendation = 'Confirma si la dependencia es obligatoria u opcional y evita silencios al fallar.' }
)

$include = @('*.cs', '*.xaml.cs')
$scanRoots = @((Join-Path $Root 'src'))
if ($IncludeTests) {
    $scanRoots += (Join-Path $Root 'tests')
}
if ($IncludeAtl) {
    $scanRoots += (Join-Path $Root '.atl')
}

$files = foreach ($scanRoot in $scanRoots) {
    if (Test-Path -LiteralPath $scanRoot) {
        Get-ChildItem -LiteralPath $scanRoot -Recurse -File -Include $include |
            Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
    }
}

$findings = foreach ($file in $files) {
    foreach ($pattern in $patterns) {
        $matches = Select-String -Path $file.FullName -Pattern $pattern.Regex -AllMatches
        foreach ($match in $matches) {
            if ($pattern.Id -eq 'fire-and-forget' -and $match.Line -match 'RunSafe\(') {
                continue
            }

            [pscustomobject]@{
                Severity = $pattern.Severity
                Rule = $pattern.Id
                File = $file.FullName.Replace($Root, '.').TrimStart('.', '\')
                Line = $match.LineNumber
                Snippet = $match.Line.Trim()
                Recommendation = $pattern.Recommendation
            }
        }
    }
}

$ordered = $findings |
    Sort-Object @{ Expression = { switch ($_.Severity) { 'high' { 0 } 'medium' { 1 } 'info' { 2 } default { 3 } } } }, File, Line

if ($AsJson) {
    $ordered | ConvertTo-Json -Depth 4
}
elseif (-not $ordered) {
    'No se encontraron patrones de auditoría.'
}
else {
    $ordered | Format-Table Severity, Rule, File, Line, Snippet -AutoSize
    ""
    "Resumen: $($ordered.Count) hallazgo(s)."
    "Altos: $(@($ordered | Where-Object Severity -eq 'high').Count) | Medios: $(@($ordered | Where-Object Severity -eq 'medium').Count) | Info: $(@($ordered | Where-Object Severity -eq 'info').Count)"
}

if ($FailOnHigh -and ($ordered | Where-Object Severity -eq 'high')) {
    exit 1
}
