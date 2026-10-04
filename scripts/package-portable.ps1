[CmdletBinding()]
param([string]$OutputDirectory, [switch]$PackageOnly, [string]$PublishDirectory)

$ErrorActionPreference = 'Stop'
$packageRepositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$packageVersion = ([xml](Get-Content -LiteralPath (Join-Path $packageRepositoryRoot 'src/Directory.Build.props') -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if ($packageVersion -notmatch '^\d+\.\d+\.\d+(?:-[a-zA-Z0-9.]+)?$') { throw 'Invalid package version.' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $packageRepositoryRoot 'artifacts/releases' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$packageName = "TokenFish-$packageVersion-win-x64"
$packageArchivePath = Join-Path $OutputDirectory "$packageName.zip"
if (Test-Path -LiteralPath $packageArchivePath) { throw "Archive already exists: $packageArchivePath. Choose another output directory." }
if ($PackageOnly -and -not $PublishDirectory) { throw 'PackageOnly requires PublishDirectory.' }
if (-not $PackageOnly -and $PublishDirectory) { throw 'PublishDirectory is only supported with PackageOnly.' }

Push-Location $packageRepositoryRoot
try {
    if (-not $PackageOnly) {
        & dotnet build TokenFish.sln -p:Platform=x64
        if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
        & dotnet test TokenFish.sln -p:Platform=x64 --no-build --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
        $PublishDirectory = Join-Path $packageRepositoryRoot ('artifacts/package-stage/' + [guid]::NewGuid().ToString('N'))
        & dotnet publish src/TokenFish.App/TokenFish.App.csproj -c Release -p:Platform=x64 -p:PublishProfile=win-x64 "-p:PublishDir=$PublishDirectory/"
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    }
    $PublishDirectory = (Resolve-Path -LiteralPath $PublishDirectory).Path
    foreach ($packageRequiredFile in @('TokenFish.App.exe','TokenFish.App.dll','tools/claude/TokenFish.ClaudeBridge.exe')) {
        if (-not (Test-Path -LiteralPath (Join-Path $PublishDirectory $packageRequiredFile) -PathType Leaf)) { throw "Missing $packageRequiredFile" }
    }
    $packageForbiddenFiles = Get-ChildItem -LiteralPath $PublishDirectory -Recurse -File | Where-Object {
        $_.Extension -in @('.pdb','.db','.sqlite','.sqlite3','.log','.dmp','.key','.pem','.pfx') -or
        $_.Name -match '(^\.env($|\.)|settings\.json(\.|$)|auth\.json(\.|$)|state\.json$|claude-status.*\.json$|history\.json$|snapshot.*\.json$|credential|secret)' -or
        $_.FullName -match '[\\/](AppData|TokenFishData|auth|sessions|provider-snapshots|diagnostics)[\\/]'
    }
    if ($packageForbiddenFiles) { throw 'Publish contains personal or debug artifacts; inspect the publish directory.' }
    foreach ($packageBinary in (Get-ChildItem -LiteralPath $PublishDirectory -Recurse -File | Where-Object { $_.Name -like 'TokenFish*' -and $_.Extension -in @('.exe','.dll') })) {
        $packageBytes = [IO.File]::ReadAllBytes($packageBinary.FullName)
        foreach ($packageEncoding in @([Text.Encoding]::UTF8,[Text.Encoding]::Unicode)) {
            if ($packageEncoding.GetString($packageBytes).IndexOf($packageRepositoryRoot,[StringComparison]::OrdinalIgnoreCase) -ge 0) { throw "Development path found in $($packageBinary.Name)." }
        }
    }
    Copy-Item -LiteralPath (Join-Path $packageRepositoryRoot 'docs/releases/PORTABLE-README.txt') -Destination (Join-Path $PublishDirectory 'README.txt')
    $packageManifest = Get-ChildItem -LiteralPath $PublishDirectory -Recurse -File | Sort-Object FullName | ForEach-Object {
        $packageRelativePath = $_.FullName.Substring($PublishDirectory.Length).TrimStart('\','/').Replace('\','/')
        "$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())  $packageRelativePath"
    }
    [IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
    [IO.File]::WriteAllLines((Join-Path $OutputDirectory "$packageName.files.sha256"), [string[]]$packageManifest)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($PublishDirectory,$packageArchivePath,[IO.Compression.CompressionLevel]::Optimal,$false)
    $packageZip = [IO.Compression.ZipFile]::OpenRead($packageArchivePath)
    try {
        foreach ($packageEntry in @('TokenFish.App.exe','TokenFish.App.dll','tools/claude/TokenFish.ClaudeBridge.exe','README.txt')) {
            if (-not $packageZip.GetEntry($packageEntry)) { throw "Missing archive entry: $packageEntry" }
        }
        if ($packageZip.Entries.Count -ne $packageManifest.Count) { throw 'Archive file count differs from verified publish.' }
    }
    finally { $packageZip.Dispose() }
    $packageDigest = (Get-FileHash -LiteralPath $packageArchivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText((Join-Path $OutputDirectory "$packageName.zip.sha256"), "$packageDigest  $packageName.zip`n")
    Write-Output "Portable candidate: $packageArchivePath"
    Write-Output "SHA-256: $packageDigest"
}
finally { Pop-Location }
