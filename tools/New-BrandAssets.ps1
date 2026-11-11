$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetDirectory = Join-Path (Split-Path $PSScriptRoot) 'src/StillFrame/Assets'
New-Item -ItemType Directory -Path $assetDirectory -Force | Out-Null
function New-BrandBitmap([int]$size) {
    $scale = 4
    $bitmap = [Drawing.Bitmap]::new($size * $scale, $size * $scale, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.ScaleTransform($size * $scale / 256.0, $size * $scale / 256.0)
        $path = [Drawing.Drawing2D.GraphicsPath]::new()
        $path.AddArc(4,4,108,108,180,90); $path.AddArc(144,4,108,108,270,90)
        $path.AddArc(144,144,108,108,0,90); $path.AddArc(4,144,108,108,90,90); $path.CloseFigure()
        $gradient = [Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Point]::new(24,12),[Drawing.Point]::new(230,248),[Drawing.ColorTranslator]::FromHtml('#548B78'),[Drawing.ColorTranslator]::FromHtml('#2C6557'))
        $g.FillPath($gradient,$path)
        $stroke = if ($size -le 24) { 18 } else { 14 }
        $pen = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#F9F5E5'),$stroke)
        $pen.StartCap = $pen.EndCap = [Drawing.Drawing2D.LineCap]::Round
        $pen.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
        foreach ($points in @(@(62,98,62,62,98,62),@(158,62,194,62,194,98),@(194,158,194,194,158,194),@(98,194,62,194,62,158))) {
            $g.DrawLines($pen,[Drawing.PointF[]]@([Drawing.PointF]::new($points[0],$points[1]),[Drawing.PointF]::new($points[2],$points[3]),[Drawing.PointF]::new($points[4],$points[5])))
        }
        $g.DrawLine($pen,88,128,104,128); $g.DrawLine($pen,152,128,168,128)
        $dot = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#E9CC8D'))
        $g.FillEllipse($dot,116,116,24,24)
        $dot.Dispose(); $pen.Dispose(); $gradient.Dispose(); $path.Dispose()
    } finally { $g.Dispose() }
    $result = [Drawing.Bitmap]::new($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $resize = [Drawing.Graphics]::FromImage($result)
    try { $resize.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic; $resize.DrawImage($bitmap,0,0,$size,$size) } finally { $resize.Dispose(); $bitmap.Dispose() }
    return ,$result
}
$frames = @()
foreach ($size in @(16,20,24,32,40,48,64,128,256)) {
    $bitmap = New-BrandBitmap $size
    try {
        $memory = [IO.MemoryStream]::new()
        $bitmap.Save($memory,[Drawing.Imaging.ImageFormat]::Png)
        $frames += @{ Size=$size; Bytes=$memory.ToArray() }; $memory.Dispose()
        if ($size -in @(16,32,64,256)) { $bitmap.Save((Join-Path $assetDirectory "icon-$size.png"),[Drawing.Imaging.ImageFormat]::Png) }
    } finally { $bitmap.Dispose() }
}
$file = [IO.File]::Create((Join-Path $assetDirectory 'StillFrame.ico'))
$writer = [IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
} finally { $writer.Dispose(); $file.Dispose() }
$svg = @'
<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256"><defs><linearGradient id="sage" x2="1" y2="1"><stop stop-color="#548B78"/><stop offset="1" stop-color="#2C6557"/></linearGradient></defs><rect x="4" y="4" width="248" height="248" rx="54" fill="url(#sage)"/><path d="M62 98V62H98 M158 62H194V98 M194 158V194H158 M98 194H62V158 M88 128H104 M152 128H168" fill="none" stroke="#F9F5E5" stroke-width="14" stroke-linecap="round" stroke-linejoin="round"/><circle cx="128" cy="128" r="12" fill="#E9CC8D"/></svg>
'@
[IO.File]::WriteAllText((Join-Path $assetDirectory 'StillFrame.svg'),$svg,[Text.UTF8Encoding]::new($false))
Write-Output 'Brand assets generated: 9 ICO resolutions, PNG and SVG sources.'
