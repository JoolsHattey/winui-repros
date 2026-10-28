# Builds the repro against a Windows App SDK version, runs it once per control set, and reports
# whether each run exited cleanly.
#   pwsh run.ps1 [-Version 2.6.4] [-Show minimized] [-Sets 'all,titlebar,navview'] [-NoBuild]
param(
    [string]$Version = '2.6.4',
    [string]$Show = 'minimized',
    [string[]]$Sets = @('none', 'titlebar', 'navview', 'splitview', 'scroll', 'listview', 'gridview', 'infobar', 'all'),
    [switch]$NoBuild,
    # Bundle the Windows App SDK, for a machine without that version's runtime installed.
    [switch]$SelfContained
)

$out = Join-Path $PSScriptRoot "out\$Version$(if ($SelfContained) { '-sc' })"
if (-not $NoBuild) {
    # Not `dotnet build -o`: it changes the embedded XAML's resource path, so App.xaml (and with it
    # XamlControlsResources) silently isn't found.
    Remove-Item -Recurse -Force (Join-Path $PSScriptRoot 'bin') -ErrorAction SilentlyContinue
    dotnet build $PSScriptRoot -c Release "-p:WinAppSdk=$Version" "-p:WindowsAppSDKSelfContained=$([bool]$SelfContained)" | Select-Object -Last 1
    $bin = Get-ChildItem (Join-Path $PSScriptRoot 'bin\Release') -Recurse -Filter MinimisedFirstLayout.exe | Select-Object -First 1
    Remove-Item -Recurse -Force $out -ErrorAction SilentlyContinue
    Copy-Item -Recurse $bin.DirectoryName $out
}
$exe = Join-Path $out 'MinimisedFirstLayout.exe'
foreach ($set in $Sets -split ',') {
    $controls = switch ($set) {
        'all' { 'titlebar,navview,splitview,scroll,listview,gridview,infobar' }
        'none' { ',' }
        default { $set }
    }
    $p = Start-Process $exe -ArgumentList '--show', $Show, '--controls', $controls -PassThru
    if (-not $p.WaitForExit(20000)) { $p.Kill(); $result = 'hung' }
    elseif ($p.ExitCode -eq 0) { $result = 'ok' }
    else { $result = 'exit 0x{0:x8}' -f $p.ExitCode }
    '{0,-8} {1,-10} {2,-40} {3}' -f $Version, $Show, $set, $result
}
