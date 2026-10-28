# Runs every control set in every first-show mode against each built version in out\, without
# building. Run it in an interactive session (a minimised window can't show in session 0).
#   pwsh matrix.ps1 [-Versions 2.5.1-sc,2.6.4-sc] [-Results results.txt]
param(
    [string[]]$Versions = @('2.5.1-sc', '2.6.4-sc'),
    [string]$Results = (Join-Path $PSScriptRoot 'results.txt')
)

$sets = 'titlebar-bare,titlebar-button,titlebar-control,extend,extend-only,navview,splitview,scroll,listview,gridview,infobar,none'
$shows = 'minimized', 'minimized', 'placement', 'activate', 'noactivate', 'hidden', 'hidden-then-minimized'
Remove-Item $Results -ErrorAction SilentlyContinue
foreach ($v in $Versions -split ',') {
    foreach ($show in $shows) {
        & (Join-Path $PSScriptRoot 'run.ps1') -NoBuild -Version $v -Show $show -Sets $sets | Out-File -Append $Results
    }
}
'done' | Out-File -Append $Results
