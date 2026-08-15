# Generates ArcGIS Online feature-layer CSV files from the app's seed JSON data.
# Outputs: utilities_feature_layer.csv (126 facilities) and
#          branches_feature_layer.csv (11 branches, new schema with Category).
# Uses UTF-8 (no BOM) so ArcGIS Online imports non-ASCII names correctly.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$seedDir = Join-Path $root 'Tourist_Project_MVC\SeedData'
$outDir = $root

function Csv-Field([string]$value) {
    if ($null -eq $value) { return '' }
    if ($value.Contains('"') -or $value.Contains(',') -or $value.Contains("`n") -or $value.Contains("`r")) {
        return '"' + $value.Replace('"', '""') + '"'
    }
    return $value
}

function Write-CsvFile([string]$path, [string[]]$header, [System.Collections.IEnumerable]$rows) {
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add(($header -join ','))
    foreach ($r in $rows) {
        $cells = foreach ($h in $header) { Csv-Field ([string]$r.$h) }
        $lines.Add(($cells -join ','))
    }
    $content = [string]::Join("`r`n", $lines.ToArray()) + "`r`n"
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($path, $content, $utf8NoBom)
    Write-Output ("Wrote " + $lines.Count + " lines -> " + $path)
}

# ---------- Utilities ----------
$utilities = Get-Content -Raw -Encoding UTF8 (Join-Path $seedDir 'utilities.json') | ConvertFrom-Json
$utilityRows = foreach ($u in $utilities) {
    [pscustomobject]@{
        Id            = [int]$u.Id
        Name          = [string]$u.Name
        Type          = [string]$u.Type
        Address       = [string]$u.Address
        City          = [string]$u.City
        ContactNumber = [string]$u.ContactNumber
        OpenHours     = [string]$u.OpenHours
        latitude      = ([double]$u.lat).ToString([System.Globalization.CultureInfo]::InvariantCulture)
        longitude     = ([double]$u.lng).ToString([System.Globalization.CultureInfo]::InvariantCulture)
    }
}
Write-CsvFile (Join-Path $outDir 'utilities_feature_layer.csv') `
    @('Id','Name','Type','Address','City','ContactNumber','OpenHours','latitude','longitude') $utilityRows

# ---------- Branches (join with sponsors for the new Category field) ----------
$branches = Get-Content -Raw -Encoding UTF8 (Join-Path $seedDir 'branches.json') | ConvertFrom-Json
$sponsors = Get-Content -Raw -Encoding UTF8 (Join-Path $seedDir 'sponsors.json') | ConvertFrom-Json
$sponsorById = @{}
foreach ($s in $sponsors) { $sponsorById[[int]$s.Id] = $s }

$branchRows = foreach ($b in $branches) {
    $sponsor = $sponsorById[[int]$b.SponsorId]
    $category = $b.Category
    if ([string]::IsNullOrWhiteSpace($category) -and $null -ne $sponsor) { $category = $sponsor.Type }
    [pscustomobject]@{
        Id            = [int]$b.Id
        SponsorId     = [int]$b.SponsorId
        Name          = [string]$b.Name
        Address       = [string]$b.Address
        ContactNumber = if ($null -eq $b.ContactNumber) { '' } else { [int]$b.ContactNumber }
        Category      = [string]$category
        latitude      = ([double]$b.lat).ToString([System.Globalization.CultureInfo]::InvariantCulture)
        longitude     = ([double]$b.lng).ToString([System.Globalization.CultureInfo]::InvariantCulture)
    }
}
Write-CsvFile (Join-Path $outDir 'branches_feature_layer.csv') `
    @('Id','SponsorId','Name','Address','ContactNumber','Category','latitude','longitude') $branchRows

Write-Output 'Done.'
