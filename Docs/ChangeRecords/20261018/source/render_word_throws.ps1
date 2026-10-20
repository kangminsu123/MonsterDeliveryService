$ErrorActionPreference = 'Stop'
$reportDir = Split-Path $PSScriptRoot -Parent
$docPath = Join-Path $reportDir 'ChangeReport_20261018.docx'
$renderDir = Join-Path $reportDir 'evidence/render-throws'
New-Item -ItemType Directory -Force -Path $renderDir | Out-Null
$pdfPath = Join-Path $renderDir 'report.pdf'
$wordApp = New-Object -ComObject Word.Application
$wordApp.Visible = $false
$wordApp.DisplayAlerts = 0
$document = $null
try {
    $document = $wordApp.Documents.Open($docPath, $false, $true)
    $document.ExportAsFixedFormat($pdfPath, 17)
    $document.Close(0)
    $document = $null
} finally {
    if ($null -ne $document) { $document.Close(0) }
    $wordApp.Quit()
}
& 'C:/Users/xlfhf/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/poppler/Library/bin/pdftoppm.exe' -scale-to 1600 -png $pdfPath (Join-Path $renderDir 'page')
if ($LASTEXITCODE -ne 0) { throw 'Poppler rasterization failed' }

