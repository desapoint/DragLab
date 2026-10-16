param(
    [string]$CaptureRoot = '',
    [string]$Contract = '',
    [string]$Python = 'python'
)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$CaptureRoot) { $CaptureRoot = Join-Path $taskRoot 'reports/render-pipeline-20261016' }
if (!$Contract) { $Contract = Join-Path $PSScriptRoot 'render-pipeline-capture-contract.json' }
$taskStarted = [DateTime]::UtcNow
& $Python (Join-Path $PSScriptRoot 'Validate-RenderPipelineImages.py') $CaptureRoot --contract $Contract
$taskValidationExit = $LASTEXITCODE
if ($taskValidationExit -notin @(0,1,2)) { throw "Visual validator could not run: $taskValidationExit" }
$taskResults = Join-Path $CaptureRoot 'validation/results.json'
if (!(Test-Path -LiteralPath $taskResults) -or (Get-Item -LiteralPath $taskResults).LastWriteTimeUtc -lt $taskStarted) {
    throw 'Validator did not produce a current report; retained results must not be treated as this run.'
}
# Always rebuild the gallery so failed/inconclusive results are visible too.
& $Python (Join-Path $PSScriptRoot 'Compare-RenderPipelineImages.py') $CaptureRoot
$taskGalleryExit = $LASTEXITCODE
if ($taskGalleryExit -ne 0) { exit 1 }
Write-Output "Visual gate exit: $taskValidationExit (0 pass, 1 failure, 2 inconclusive). Report: $CaptureRoot/validation/index.html"
exit $taskValidationExit
