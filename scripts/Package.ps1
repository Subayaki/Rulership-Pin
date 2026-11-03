param([switch]$SkipPublish)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$projectFile=Join-Path $projectRoot 'Rulership-Pin.csproj'
$output=Join-Path $projectRoot 'artifacts/Portable'
if(-not $SkipPublish) {
 dotnet publish $projectFile -c Release -r win-x64 --self-contained true -o $output
 if($LASTEXITCODE -ne 0) {throw 'Publish failed'}
}
[xml]$project=Get-Content -LiteralPath $projectFile -Raw
$version=[string]$project.Project.PropertyGroup.Version
if($version -notmatch '^\d+\.\d+\.\d+$') {throw 'Version must have major.minor.patch format'}
$files=Get-ChildItem -LiteralPath $output -Recurse -File | Where-Object {
 $_.Name -notlike 'companion-settings.json*' -and $_.Name -ne 'update-manifest.json' -and $_.Extension -ne '.pdb' -and $_.Name -notlike '*error*.txt' -and
 -not [System.IO.Path]::GetRelativePath($output,$_.FullName).StartsWith('update-history')
}
$hashes=[ordered]@{}
foreach($file in $files) {$relative=[System.IO.Path]::GetRelativePath($output,$file.FullName).Replace('\','/');$hashes[$relative]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash}
$manifestPath=Join-Path $output 'update-manifest.json'
@{Version=$version;Files=$hashes} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
$archivePath=Join-Path $projectRoot 'Rulership-Pin-Portable-win-x64.zip'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$stream=[System.IO.File]::Open($archivePath,[System.IO.FileMode]::Create)
$zip=[System.IO.Compression.ZipArchive]::new($stream,[System.IO.Compression.ZipArchiveMode]::Create)
try {
 foreach($file in @($files)+(Get-Item -LiteralPath $manifestPath)) {
  $relative=[System.IO.Path]::GetRelativePath($output,$file.FullName).Replace('\','/')
  [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,'Rulership_pin_custom/'+$relative,[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
 }
} finally {$zip.Dispose();$stream.Dispose()}
$hash=(Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  Rulership-Pin-Portable-win-x64.zip" | Set-Content -LiteralPath ($archivePath+'.sha256') -Encoding ascii
Write-Output "Packaged v$version with $($files.Count) verified files in Rulership_pin_custom."
