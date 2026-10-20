$ErrorActionPreference = 'Stop'
$airRenderFolder='C:/Users/xlfhf/Documents/Codex/2026-10-17/dbslx/work/broadcaster-word-render'
New-Item -ItemType Directory -Force -Path $airRenderFolder | Out-Null
$airWord=New-Object -ComObject Word.Application
$airWord.Visible=$false
$airWord.DisplayAlerts=0
try {
    $airDoc=$airWord.Documents.Open('C:/Users/xlfhf/Documents/Codex/2026-10-17/dbslx/outputs/SpecialItemDesignReport.docx',$false,$true)
    try {
        $airDoc.ExportAsFixedFormat((Join-Path $airRenderFolder 'report.pdf'),17)
        Write-Output ('Pages: '+$airDoc.ComputeStatistics(2))
    } finally { $airDoc.Close(0) }
} finally { $airWord.Quit() }
& 'C:/Users/xlfhf/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/poppler/Library/bin/pdftoppm.exe' -scale-to 1600 -png (Join-Path $airRenderFolder 'report.pdf') (Join-Path $airRenderFolder 'page')
if($LASTEXITCODE -ne 0){throw 'Page rendering failed'}

