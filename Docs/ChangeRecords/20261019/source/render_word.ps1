$ErrorActionPreference='Stop'
$designDir=Split-Path $PSScriptRoot -Parent
$renderDir=Join-Path $designDir 'evidence/render-word'
New-Item -ItemType Directory -Force -Path $renderDir | Out-Null
$wordApp=New-Object -ComObject Word.Application
$wordApp.Visible=$false
$wordApp.DisplayAlerts=0
try {
 $designDoc=$wordApp.Documents.Open((Join-Path $designDir 'ChangeReport_20261019.docx'),$false,$true)
 try {$designDoc.ExportAsFixedFormat((Join-Path $renderDir 'report.pdf'),17)} finally {$designDoc.Close(0)}
 & 'C:/Users/xlfhf/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/poppler/Library/bin/pdftoppm.exe' -scale-to 1600 -png (Join-Path $renderDir 'report.pdf') (Join-Path $renderDir 'page')
 if ($LASTEXITCODE -ne 0) {throw 'PDF render failed'}
} finally {$wordApp.Quit()}
