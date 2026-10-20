$ErrorActionPreference = 'Stop'
$renderFolder = 'C:/Users/xlfhf/Documents/Codex/2026-10-17/dbslx/work/balance-review-render'
New-Item -ItemType Directory -Force -Path $renderFolder | Out-Null
$planWord = New-Object -ComObject Word.Application
$planWord.Visible = $false
$planWord.DisplayAlerts = 0
try {
    $planDoc = $planWord.Documents.Open('C:/Users/xlfhf/Documents/Codex/2026-10-17/dbslx/outputs/CombatBalanceReview.docx', $false, $true)
    try { $planDoc.ExportAsFixedFormat((Join-Path $renderFolder 'report.pdf'), 17) }
    finally { $planDoc.Close(0) }
} finally { $planWord.Quit() }
& 'C:/Users/xlfhf/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/poppler/Library/bin/pdftoppm.exe' -scale-to 1600 -png (Join-Path $renderFolder 'report.pdf') (Join-Path $renderFolder 'page')
if ($LASTEXITCODE -ne 0) { throw 'PDF page rendering failed' }
