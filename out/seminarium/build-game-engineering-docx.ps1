$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourcePath = Join-Path $root "game-engineering-course.md"
$outputPath = Join-Path $root "game-engineering-course.docx"
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("godmist-docx-" + [System.Guid]::NewGuid().ToString("N"))

function Escape-XmlText([string]$text) {
    if ($null -eq $text) { return "" }
    $escaped = [System.Security.SecurityElement]::Escape($text)
    if ($null -eq $escaped) { return "" }
    return $escaped
}

function New-ParagraphXml([string]$text, [string]$style = "Normal") {
    $safe = Escape-XmlText $text
    return "<w:p><w:pPr><w:pStyle w:val=`"$style`"/></w:pPr><w:r><w:t xml:space=`"preserve`">$safe</w:t></w:r></w:p>"
}

function Convert-MarkdownToDocumentXml([string[]]$lines) {
    $paragraphs = New-Object System.Collections.Generic.List[string]

    foreach ($rawLine in $lines) {
        $line = $rawLine.TrimEnd()

        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        if ($line.StartsWith("# ")) {
            $paragraphs.Add((New-ParagraphXml $line.Substring(2) "Title"))
            continue
        }

        if ($line.StartsWith("## ")) {
            $paragraphs.Add((New-ParagraphXml $line.Substring(3) "Heading1"))
            continue
        }

        if ($line.StartsWith("### ")) {
            $paragraphs.Add((New-ParagraphXml $line.Substring(4) "Heading2"))
            continue
        }

        if ($line.StartsWith("- ")) {
            $paragraphs.Add((New-ParagraphXml ("• " + $line.Substring(2)) "ListParagraph"))
            continue
        }

        $paragraphs.Add((New-ParagraphXml $line "Normal"))
    }

    $body = ($paragraphs -join "")
    return @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:wpc="http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas"
 xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
 xmlns:o="urn:schemas-microsoft-com:office:office"
 xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"
 xmlns:m="http://schemas.openxmlformats.org/officeDocument/2006/math"
 xmlns:v="urn:schemas-microsoft-com:vml"
 xmlns:wp14="http://schemas.microsoft.com/office/word/2010/wordprocessingDrawing"
 xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"
 xmlns:w10="urn:schemas-microsoft-com:office:word"
 xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
 xmlns:w14="http://schemas.microsoft.com/office/word/2010/wordml"
 xmlns:wpg="http://schemas.microsoft.com/office/word/2010/wordprocessingGroup"
 xmlns:wpi="http://schemas.microsoft.com/office/word/2010/wordprocessingInk"
 xmlns:wne="http://schemas.microsoft.com/office/word/2006/wordml"
 xmlns:wps="http://schemas.microsoft.com/office/word/2010/wordprocessingShape"
 mc:Ignorable="w14 wp14">
  <w:body>
    $body
    <w:sectPr>
      <w:pgSz w:w="11906" w:h="16838"/>
      <w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440" w:header="708" w:footer="708" w:gutter="0"/>
    </w:sectPr>
  </w:body>
</w:document>
"@
}

function Get-StylesXml() {
    return @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:style w:type="paragraph" w:default="1" w:styleId="Normal">
    <w:name w:val="Normal"/>
    <w:qFormat/>
    <w:rPr>
      <w:rFonts w:ascii="Calibri" w:hAnsi="Calibri" w:eastAsia="Calibri" w:cs="Calibri"/>
      <w:sz w:val="22"/>
      <w:lang w:val="pl-PL"/>
    </w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="Title">
    <w:name w:val="Title"/>
    <w:basedOn w:val="Normal"/>
    <w:qFormat/>
    <w:pPr>
      <w:jc w:val="center"/>
      <w:spacing w:after="240"/>
    </w:pPr>
    <w:rPr>
      <w:b/>
      <w:sz w:val="32"/>
      <w:lang w:val="pl-PL"/>
    </w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="Heading1">
    <w:name w:val="heading 1"/>
    <w:basedOn w:val="Normal"/>
    <w:next w:val="Normal"/>
    <w:qFormat/>
    <w:pPr>
      <w:spacing w:before="240" w:after="120"/>
    </w:pPr>
    <w:rPr>
      <w:b/>
      <w:sz w:val="28"/>
      <w:lang w:val="pl-PL"/>
    </w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="Heading2">
    <w:name w:val="heading 2"/>
    <w:basedOn w:val="Normal"/>
    <w:next w:val="Normal"/>
    <w:qFormat/>
    <w:pPr>
      <w:spacing w:before="160" w:after="80"/>
    </w:pPr>
    <w:rPr>
      <w:b/>
      <w:sz w:val="24"/>
      <w:lang w:val="pl-PL"/>
    </w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="ListParagraph">
    <w:name w:val="List Paragraph"/>
    <w:basedOn w:val="Normal"/>
    <w:qFormat/>
    <w:pPr>
      <w:ind w:left="360"/>
      <w:spacing w:after="40"/>
    </w:pPr>
    <w:rPr>
      <w:sz w:val="22"/>
      <w:lang w:val="pl-PL"/>
    </w:rPr>
  </w:style>
</w:styles>
"@
}

function Write-Utf8File([string]$path, [string]$content) {
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($path, $content, $utf8NoBom)
}

New-Item -ItemType Directory -Path $tempDir | Out-Null
New-Item -ItemType Directory -Path (Join-Path $tempDir "_rels") | Out-Null
New-Item -ItemType Directory -Path (Join-Path $tempDir "word") | Out-Null
New-Item -ItemType Directory -Path (Join-Path $tempDir "word\_rels") | Out-Null

$lines = Get-Content -Path $sourcePath -Encoding UTF8
$documentXml = Convert-MarkdownToDocumentXml $lines
$stylesXml = Get-StylesXml

$contentTypes = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
  <Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
</Types>
"@

$rels = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
</Relationships>
"@

$documentRels = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"/>
"@

Write-Utf8File (Join-Path $tempDir "[Content_Types].xml") $contentTypes
Write-Utf8File (Join-Path $tempDir "_rels\.rels") $rels
Write-Utf8File (Join-Path $tempDir "word\document.xml") $documentXml
Write-Utf8File (Join-Path $tempDir "word\styles.xml") $stylesXml
Write-Utf8File (Join-Path $tempDir "word\_rels\document.xml.rels") $documentRels

$zipPath = [System.IO.Path]::ChangeExtension($outputPath, ".zip")
if (Test-Path $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
if (Test-Path $outputPath) { Remove-Item -LiteralPath $outputPath -Force }

Compress-Archive -Path (Join-Path $tempDir "*") -DestinationPath $zipPath -Force
Move-Item -LiteralPath $zipPath -Destination $outputPath -Force
Remove-Item -LiteralPath $tempDir -Recurse -Force

Write-Output "Created: $outputPath"
