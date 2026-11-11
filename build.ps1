param([switch]$SkipChecks)
$ErrorActionPreference = 'Stop'
$sdk = Join-Path $PSScriptRoot '.tools/dotnet/dotnet.exe'
if (!(Test-Path -LiteralPath $sdk)) { $sdk = (Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.tools/cli-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.tools/nuget'
$env:STILLFRAME_DATA_DIR = Join-Path $PSScriptRoot '.tools/check-data'
if (!$SkipChecks) {
    & $sdk run --project "$PSScriptRoot/tests/StillFrame.Checks" -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Checks failed' }
}
& "$PSScriptRoot/tools/New-BrandAssets.ps1"
$publish = Join-Path $PSScriptRoot 'artifacts/StillFrame-2.2.0-win-x64'
& $sdk publish "$PSScriptRoot/src/StillFrame" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
Copy-Item -LiteralPath "$PSScriptRoot/docs/使用指南.md" -Destination "$publish/使用说明.md" -Force
Copy-Item -LiteralPath "$PSScriptRoot/docs/常见问题.md" -Destination $publish -Force
Copy-Item -LiteralPath "$PSScriptRoot/LICENSE" -Destination "$publish/LICENSE.txt" -Force
Copy-Item -LiteralPath "$PSScriptRoot/docs/家中电脑验证清单.md" -Destination $publish -Force
Copy-Item -LiteralPath "$PSScriptRoot/docs/验证报告.md" -Destination $publish -Force
Copy-Item -LiteralPath "$PSScriptRoot/THIRD-PARTY-NOTICES.md" -Destination $publish -Force
$licenseDirectory = Join-Path $publish 'licenses'
New-Item -ItemType Directory -Path $licenseDirectory -Force | Out-Null
$runtimeConfiguration = Get-Content (Join-Path $publish 'StillFrame.runtimeconfig.json') -Raw | ConvertFrom-Json
foreach ($framework in $runtimeConfiguration.runtimeOptions.includedFrameworks) {
    $package = Join-Path $env:NUGET_PACKAGES ($framework.name.ToLowerInvariant() + '.runtime.win-x64/' + $framework.version)
    foreach ($licenseName in @('LICENSE','LICENSE.TXT','THIRD-PARTY-NOTICES.TXT')) {
        $licenseFile = Join-Path $package $licenseName
        if (Test-Path -LiteralPath $licenseFile) {
            Copy-Item -LiteralPath $licenseFile -Destination (Join-Path $licenseDirectory ($framework.name + '-' + $licenseName)) -Force
        }
    }
}
$archive = Join-Path $PSScriptRoot 'artifacts/StillFrame-2.2.0-win-x64.zip'
Compress-Archive -Path $publish -DestinationPath $archive -Force
Add-Type -AssemblyName System.IO.Compression
$sourceArchive = Join-Path $PSScriptRoot 'artifacts/StillFrame-2.2.0-source.zip'
$sourceFiles = @('README.md','CHANGELOG.md','LICENSE','THIRD-PARTY-NOTICES.md','build.ps1','.gitignore') | ForEach-Object { Get-Item -LiteralPath (Join-Path $PSScriptRoot $_) }
$sourceFiles += Get-ChildItem -LiteralPath "$PSScriptRoot/src","$PSScriptRoot/tests","$PSScriptRoot/docs","$PSScriptRoot/tools" -Recurse -File | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
$sourceStream = [IO.File]::Create($sourceArchive)
$sourceZip = [IO.Compression.ZipArchive]::new($sourceStream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($sourceFile in $sourceFiles) {
        $relative = $sourceFile.FullName.Substring($PSScriptRoot.Length + 1).Replace('\','/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($sourceZip, $sourceFile.FullName, $relative, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $sourceZip.Dispose(); $sourceStream.Dispose() }
$hashes = Get-FileHash -LiteralPath $archive,$sourceArchive -Algorithm SHA256
$hashes | ForEach-Object { $_.Hash + '  ' + [IO.Path]::GetFileName($_.Path) } | Set-Content -LiteralPath "$PSScriptRoot/artifacts/SHA256SUMS.txt"
$hashes | Format-List
Write-Output "Release: $archive"
