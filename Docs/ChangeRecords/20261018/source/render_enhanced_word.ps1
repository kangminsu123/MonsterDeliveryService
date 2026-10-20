$ErrorActionPreference = 'Stop'
$usageDir = Split-Path $PSScriptRoot -Parent
$wordApp = New-Object -ComObject Word.Application
$wordApp.Visible = $false
$wordApp.DisplayAlerts = 0
try {
    foreach ($report in @(@{name='ItemUsageReport_20261018';folder='render-enhanced-current'},@{name='ChangeReport_20261018';folder='render-enhanced-changes'})) {
        $renderDir = Join-Path $usageDir ('evidence/' + $report.folder)
        New-Item -ItemType Directory -Force -Path $renderDir | Out-Null
        $docPath = Join-Path $usageDir ($report.name + '.docx')
        $pdfPath = Join-Path $renderDir 'report.pdf'
        $document = $wordApp.Documents.Open($docPath, $false, $true)
        try { $document.ExportAsFixedFormat($pdfPath, 17) } finally { $document.Close(0) }
        & 'C:/Users/xlfhf/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/poppler/Library/bin/pdftoppm.exe' -scale-to 1600 -png $pdfPath (Join-Path $renderDir 'page')
        if ($LASTEXITCODE -ne 0) { throw 'Poppler rasterization failed' }
    }
} finally { $wordApp.Quit() }
