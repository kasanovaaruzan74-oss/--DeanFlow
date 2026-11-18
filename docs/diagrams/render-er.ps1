Add-Type -AssemblyName System.Drawing

$outputPath = Join-Path $PSScriptRoot 'deanflow-er.png'
$bitmap = [System.Drawing.Bitmap]::new(1800, 1200)
$g = [System.Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$g.Clear([System.Drawing.Color]::FromArgb(245, 247, 250))

$titleFont = [System.Drawing.Font]::new('Segoe UI', 30, [System.Drawing.FontStyle]::Bold)
$legendFont = [System.Drawing.Font]::new('Segoe UI', 16)
$headFont = [System.Drawing.Font]::new('Segoe UI', 19, [System.Drawing.FontStyle]::Bold)
$tableFont = [System.Drawing.Font]::new('Segoe UI', 14, [System.Drawing.FontStyle]::Bold)
$fieldFont = [System.Drawing.Font]::new('Segoe UI', 14)
$smallFont = [System.Drawing.Font]::new('Segoe UI', 12)
$titleBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(23, 43, 67))
$legendBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(78, 96, 114))
$headBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(22, 40, 60))
$tableBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(98, 116, 135))
$fieldBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(37, 55, 74))
$edgePen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(101, 120, 140), 2)
$borderPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(183, 195, 208), 1.5)
$dividerPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(198, 208, 218), 1)
$centerFormat = [System.Drawing.StringFormat]::new()
$centerFormat.Alignment = [System.Drawing.StringAlignment]::Center

function Draw-Line([int]$x1, [int]$y1, [int]$x2, [int]$y2) {
    $g.DrawLine($edgePen, $x1, $y1, $x2, $y2)
}

function Draw-Card([int]$x, [int]$y, [int]$width, [int]$height, [string]$heading, [string]$table, [string[]]$fields) {
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $radius = 14
    $path.AddArc($x, $y, $radius, $radius, 180, 90)
    $path.AddArc($x + $width - $radius, $y, $radius, $radius, 270, 90)
    $path.AddArc($x + $width - $radius, $y + $height - $radius, $radius, $radius, 0, 90)
    $path.AddArc($x, $y + $height - $radius, $radius, $radius, 90, 90)
    $path.CloseFigure()

    $shadow = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(20, 24, 42, 61))
    $cardBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
    $headerBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(237, 242, 246))
    $g.FillPath($shadow, $path)
    $g.FillPath($cardBrush, $path)
    $g.SetClip($path)
    $g.FillRectangle($headerBrush, $x, $y, $width, 74)
    $g.ResetClip()
    $g.DrawString($heading, $headFont, $headBrush, [single]($x + 22), [single]($y + 12))
    $g.DrawString($table, $tableFont, $tableBrush, [single]($x + 22), [single]($y + 41))
    $g.DrawLine($dividerPen, $x + 18, $y + 76, $x + $width - 18, $y + 76)
    for ($i = 0; $i -lt $fields.Count; $i++) {
        $g.DrawString($fields[$i], $fieldFont, $fieldBrush, [single]($x + 22), [single]($y + 87 + 23 * $i))
    }
    $g.DrawPath($borderPen, $path)
    $path.Dispose()
    $shadow.Dispose()
    $cardBrush.Dispose()
    $headerBrush.Dispose()
}

function Draw-Cardinality([string]$text, [int]$x, [int]$y) {
    $g.DrawString($text, $headFont, $headBrush, [single]$x, [single]$y)
}

$g.DrawString('DeanFlow ақпараттық жүйесінің ER диаграммасы', $titleFont, $titleBrush, [single]900, [single]24, $centerFormat)
$g.DrawString('PK — бастапқы кілт · FK — сыртқы кілт · 1 → 0..N — бір жазбаға бірнеше жазба байланысады', $legendFont, $legendBrush, [single]900, [single]67, $centerFormat)

# Байланыс сызықтары кесте карточкаларының артында салынады.
Draw-Line 340 260 400 260; Draw-Line 400 260 400 475; Draw-Line 400 475 440 475
Draw-Line 550 340 550 455
Draw-Line 700 255 790 255; Draw-Line 790 255 790 470; Draw-Line 790 470 850 470
Draw-Line 1060 340 1060 430
Draw-Line 1280 340 1280 395; Draw-Line 1280 395 1320 395; Draw-Line 1320 395 1320 430
Draw-Line 640 555 850 555
Draw-Line 900 750 900 815; Draw-Line 900 815 190 815; Draw-Line 190 815 190 850
Draw-Line 1040 750 1040 815; Draw-Line 1040 815 540 815; Draw-Line 540 815 540 850
Draw-Line 1160 750 1160 850
Draw-Line 1270 750 1270 815; Draw-Line 1270 815 1240 815; Draw-Line 1240 815 1240 850
Draw-Line 1350 700 1600 700; Draw-Line 1600 700 1600 815; Draw-Line 1600 815 1590 815; Draw-Line 1590 815 1590 850

Draw-Cardinality '1' 350 239; Draw-Cardinality '0..N' 402 425
Draw-Cardinality '1' 555 342; Draw-Cardinality '0..N' 555 428
Draw-Cardinality '1' 708 239; Draw-Cardinality '0..N' 792 448
Draw-Cardinality '1' 1067 342; Draw-Cardinality '0..N' 1067 405
Draw-Cardinality '1' 1287 342; Draw-Cardinality '0..N' 1325 405
Draw-Cardinality '1' 650 535; Draw-Cardinality '0..N' 810 535
Draw-Cardinality '1' 905 770; Draw-Cardinality '0..N' 192 824
Draw-Cardinality '1' 1045 770; Draw-Cardinality '0..N' 545 824
Draw-Cardinality '1' 1165 770; Draw-Cardinality '0..N' 1164 824
Draw-Cardinality '1' 1275 770; Draw-Cardinality '0..N' 1243 824
Draw-Cardinality '1' 1360 674; Draw-Cardinality '0..N' 1602 824

Draw-Card 40 180 300 160 'Рөлдер' 'roles' @('PK role_id', 'role_name UNIQUE', 'description')
Draw-Card 400 180 300 160 'Бөлімдер' 'departments' @('PK department_id', 'department_name UNIQUE', 'short_name · is_active')
Draw-Card 760 180 300 160 'Құжат түрлері' 'document_types' @('PK document_type_id', 'type_name', 'code UNIQUE · is_active')
Draw-Card 1120 180 300 160 'Құжат мәртебелері' 'document_statuses' @('PK status_id', 'status_name', 'status_code UNIQUE')
Draw-Card 260 455 380 215 'Пайдаланушылар' 'users' @('PK user_id', 'FK role_id · FK department_id', 'full_name · email UNIQUE', 'is_active · created_at')
Draw-Card 850 430 520 320 'Құжаттар' 'documents' @('PK document_id · registration_number UNIQUE', 'FK document_type_id · FK status_id', 'FK department_id · FK created_by_user_id', 'title · description · direction · priority', 'registered_at · due_date · completed_at', 'is_archived')
Draw-Card 40 850 300 220 'Орындаушылар' 'document_executors' @('PK assignment_id', 'FK document_id · FK user_id', 'FK assigned_by_user_id', 'due_date · completed_at')
Draw-Card 390 850 300 220 'Келісімдер' 'document_approvals' @('PK approval_id', 'FK document_id', 'FK approver_user_id', 'approval_order · approval_status', 'assigned_at · decision_at')
Draw-Card 740 850 300 220 'Құжат тарихы' 'document_history' @('PK history_id', 'FK document_id', 'FK user_id', 'action_type · description', 'action_date')
Draw-Card 1090 850 300 220 'Пікірлер' 'document_comments' @('PK comment_id', 'FK document_id', 'FK user_id', 'comment_text', 'created_at')
Draw-Card 1440 850 300 220 'Тіркемелер' 'attachments' @('PK attachment_id', 'FK document_id', 'FK uploaded_by_user_id', 'file_name · file_type', 'file_size · uploaded_at')

$g.DrawString('Негізгі кестелер: Data/Entities.cs және Data/DeanFlowDbContext.cs · Оқу демосында құпиясөз хэші мен файл жолдары әдейі көрсетілмеді', $smallFont, $legendBrush, [single]900, [single]1110, $centerFormat)
$bitmap.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)

$centerFormat.Dispose()
$edgePen.Dispose()
$borderPen.Dispose()
$dividerPen.Dispose()
$titleFont.Dispose()
$legendFont.Dispose()
$headFont.Dispose()
$tableFont.Dispose()
$fieldFont.Dispose()
$smallFont.Dispose()
$titleBrush.Dispose()
$legendBrush.Dispose()
$headBrush.Dispose()
$tableBrush.Dispose()
$fieldBrush.Dispose()
$g.Dispose()
$bitmap.Dispose()

Write-Output "ER диаграммасы сақталды: $outputPath"
