param(
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor',
    [string]$ResponseFile = 'Game/Library/Bee/artifacts/1900b0aE.dag/Assembly-CSharp.rsp',
    [string]$OutputFolder = 'reports/save-refactor/S06D2B1L1D/physical-closure-semantic'
)

$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$sdk = Get-ChildItem -LiteralPath (Join-Path $UnityEditor 'Data/DotNetSdk/sdk') -Directory |
    Sort-Object { [Version]$_.Name } -Descending | Select-Object -First 1
if ($null -eq $sdk) { throw 'The selected Unity Editor has no bundled .NET SDK.' }
$roslyn = Join-Path $sdk.FullName 'Roslyn/bincore'
$analysis = Join-Path $roslyn 'Microsoft.CodeAnalysis.dll'
$csharp = Join-Path $roslyn 'Microsoft.CodeAnalysis.CSharp.dll'
Add-Type -Path $analysis
Add-Type -Path $csharp
$hostReferences = @(Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' | ForEach-Object { $_.FullName })
$references = @($analysis, $csharp) + $hostReferences
$helper = Join-Path $PSScriptRoot 'CartQueryClosureAudit.cs'
$helperHash = (Get-FileHash -LiteralPath $helper -Algorithm SHA256).Hash
$driverHash = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
# The bundled SDK targets .NET 8 while newer PowerShell hosts provide newer
# framework identities. Suppress only that helper-host unification warning;
# project compilation diagnostics below remain unsuppressed and fail closed.
Add-Type -Path $helper -ReferencedAssemblies $references -CompilerOptions '/nowarn:1701,1702'
$rsp = if ([IO.Path]::IsPathRooted($ResponseFile)) { $ResponseFile } else { Join-Path $repository $ResponseFile }
$output = if ([IO.Path]::IsPathRooted($OutputFolder)) { $OutputFolder } else { Join-Path $repository $OutputFolder }
[CartQueryClosureAudit]::Run($repository, $rsp, $output)
if ((Get-FileHash -LiteralPath $helper -Algorithm SHA256).Hash -ne $helperHash -or
    (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash -ne $driverHash) {
    throw 'Audit tool source changed; discard this analysis.'
}
$provenance = @(
    "Driver SHA256=$driverHash",
    "Helper SHA256=$helperHash",
    "Roslyn SHA256=$((Get-FileHash -LiteralPath $analysis -Algorithm SHA256).Hash)",
    "CSharp Roslyn SHA256=$((Get-FileHash -LiteralPath $csharp -Algorithm SHA256).Hash)",
    "PowerShell=$($PSVersionTable.PSVersion)",
    "Host CLR=$([Environment]::Version)",
    'Response parser adds -noconfig and -nostdlib+; explicit Unity references are required.',
    'Only helper-host CS1701/1702 unification warnings are suppressed; project compiler errors fail closed.'
)
[IO.File]::WriteAllText((Join-Path $output 'tool-provenance.txt'), [string]::Join("`n", $provenance) + "`n", [Text.UTF8Encoding]::new($false))
