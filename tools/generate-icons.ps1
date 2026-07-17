Add-Type -AssemblyName System.Drawing

function New-Icon {
	param([string]$Path, [bool]$Maskable)

	$size = 512
	$bmp = New-Object System.Drawing.Bitmap($size, $size)
	$g = [System.Drawing.Graphics]::FromImage($bmp)
	$g.SmoothingMode = 'AntiAlias'
	$g.TextRenderingHint = 'AntiAliasGridFit'

	# Fondo azul marino con degradado
	$bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
		(New-Object System.Drawing.Point(0,0)), (New-Object System.Drawing.Point($size,$size)),
		[System.Drawing.Color]::FromArgb(27,42,94), [System.Drawing.Color]::FromArgb(13,21,51))

	if ($Maskable) {
		$g.FillRectangle($bgBrush, 0, 0, $size, $size)
		$s = 0.72; $off = [int](($size * (1 - $s)) / 2)
	} else {
		$gp = New-Object System.Drawing.Drawing2D.GraphicsPath
		$r = 116; $d = $r * 2; $w = $size - 16
		$gp.AddArc(8, 8, $d, $d, 180, 90)
		$gp.AddArc(8 + $w - $d, 8, $d, $d, 270, 90)
		$gp.AddArc(8 + $w - $d, 8 + $w - $d, $d, $d, 0, 90)
		$gp.AddArc(8, 8 + $w - $d, $d, $d, 90, 90)
		$gp.CloseFigure()
		$g.FillPath($bgBrush, $gp)
		$s = 1.0; $off = 0
	}

	$m = New-Object System.Drawing.Drawing2D.Matrix
	$m.Translate($off, $off); $m.Scale($s, $s)
	$g.Transform = $m

	# Anillo abierto (azul -> verde)
	$ringBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
		(New-Object System.Drawing.Point(100,420)), (New-Object System.Drawing.Point(420,100)),
		[System.Drawing.Color]::FromArgb(43,179,216), [System.Drawing.Color]::FromArgb(126,217,87))
	$ringPen = New-Object System.Drawing.Pen($ringBrush, 26)
	$ringPen.StartCap = 'Round'; $ringPen.EndCap = 'Round'
	$g.DrawArc($ringPen, 96, 96, 320, 320, 55, 250)

	# Barras con degradado
	$bars = @(
		@{x=148; y=292; h=120; c1=@(24,153,201);  c2=@(47,196,178)},
		@{x=212; y=256; h=156; c1=@(31,174,158);  c2=@(78,203,113)},
		@{x=276; y=222; h=190; c1=@(53,189,141);  c2=@(111,212,78)},
		@{x=340; y=186; h=226; c1=@(76,200,111);  c2=@(142,224,62)}
	)
	foreach ($b in $bars) {
		$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
			(New-Object System.Drawing.Point($b.x, ($b.y + $b.h))), (New-Object System.Drawing.Point($b.x, $b.y)),
			[System.Drawing.Color]::FromArgb($b.c1[0],$b.c1[1],$b.c1[2]),
			[System.Drawing.Color]::FromArgb($b.c2[0],$b.c2[1],$b.c2[2]))
		$g.FillRectangle($brush, $b.x, $b.y, 46, $b.h)
		$brush.Dispose()
	}

	# Línea de tendencia blanca con puntos y flecha
	$white = [System.Drawing.Brushes]::White
	$linePen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 16)
	$linePen.StartCap = 'Round'; $linePen.EndCap = 'Round'; $linePen.LineJoin = 'Round'
	$pts = @(
		(New-Object System.Drawing.Point(150,272)), (New-Object System.Drawing.Point(212,216)),
		(New-Object System.Drawing.Point(258,244)), (New-Object System.Drawing.Point(306,186)),
		(New-Object System.Drawing.Point(352,142)))
	$g.DrawLines($linePen, $pts)
	$arrow = @(
		(New-Object System.Drawing.Point(318,128)), (New-Object System.Drawing.Point(376,118)),
		(New-Object System.Drawing.Point(366,176)))
	$g.FillPolygon($white, $arrow)
	foreach ($p in @(@(150,272), @(212,216), @(258,244))) {
		$g.FillEllipse($white, $p[0]-14, $p[1]-14, 28, 28)
	}

	# Sello $
	$badgeBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(21,34,82))
	$g.FillEllipse($badgeBrush, 292, 272, 156, 156)
	$badgePen = New-Object System.Drawing.Pen($ringBrush, 12)
	$g.DrawEllipse($badgePen, 292, 272, 156, 156)
	$font = New-Object System.Drawing.Font('Arial', 72, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
	$fmt = New-Object System.Drawing.StringFormat
	$fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'
	$g.DrawString('$', $font, $white, (New-Object System.Drawing.RectangleF(292, 272, 156, 156)), $fmt)

	$g.Dispose()
	$bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)

	# Version 192x192
	$small = New-Object System.Drawing.Bitmap($bmp, 192, 192)
	$small.Save(($Path -replace '512', '192'), [System.Drawing.Imaging.ImageFormat]::Png)
	$small.Dispose(); $bmp.Dispose()
}

$root = 'C:\Users\54221\FinanzasIA\FinanzasIA.Backoffice\wwwroot'
New-Icon -Path "$root\app-icon-512.png" -Maskable $false
New-Icon -Path "$root\app-icon-maskable-512.png" -Maskable $true
Write-Host 'Iconos PNG generados correctamente'
