param(
    [string]$GameManaged='D:\Steam\steamapps\common\OxygenNotIncluded\OxygenNotIncluded_Data\Managed',
    [string]$Python='python'
)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$taskVersion=[regex]::Match([IO.File]::ReadAllText((Join-Path $taskRoot 'mod_info.yaml')),'(?m)^version:\s*([0-9]+\.[0-9]+\.[0-9]+)\s*$').Groups[1].Value
if(-not $taskVersion){throw 'Missing semantic version'}
& $Python (Join-Path $taskRoot 'tools/verify-assets.py')
if($LASTEXITCODE -ne 0){throw 'Asset check failed'}
& dotnet build (Join-Path $taskRoot 'CryogenicPantry.csproj') -c Release "-p:GameManaged=$GameManaged" -v:q -clp:ErrorsOnly
if($LASTEXITCODE -ne 0){throw 'Build failed'}
$taskStage=[IO.Path]::GetFullPath((Join-Path $taskRoot 'dist/package/BaiyeCryogenicPantry'))
if(-not $taskStage.StartsWith($taskRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Stage outside project'}
if(Test-Path -LiteralPath $taskStage){Remove-Item -LiteralPath $taskStage -Recurse -Force}
New-Item -ItemType Directory -Force -Path $taskStage | Out-Null
Copy-Item -LiteralPath (Join-Path $taskRoot 'bin/Release/netstandard2.1/BaiyeCryogenicPantry.dll') -Destination $taskStage
foreach($name in @('mod.yaml','mod_info.yaml','README.md','CHANGELOG.md')){Copy-Item -LiteralPath (Join-Path $taskRoot $name) -Destination $taskStage}
Copy-Item -LiteralPath (Join-Path $taskRoot 'docs/playtest.md') -Destination (Join-Path $taskStage 'PLAYTEST.md')
$taskAssets=@()
foreach($name in @('baiye_cryogenic_pantry','baiye_pantry_drinks'))
{
    foreach($file in @("$name.png","${name}_anim.bytes","${name}_build.bytes"))
    {
        $relative="anim/assets/$name/$file";$taskAssets+=$relative
        $dest=Join-Path $taskStage $relative;New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dest)|Out-Null
        Copy-Item -LiteralPath (Join-Path $taskRoot $relative) -Destination $dest
    }
}
$taskExpected=@('BaiyeCryogenicPantry.dll','mod.yaml','mod_info.yaml','README.md','CHANGELOG.md','PLAYTEST.md')+$taskAssets
$taskDlls=@(Get-ChildItem -LiteralPath $taskStage -Recurse -Filter '*.dll')
if($taskDlls.Count -ne 1 -or $taskDlls[0].Name -ne 'BaiyeCryogenicPantry.dll'){throw 'Unexpected DLL dependencies'}
$taskZip=Join-Path $taskRoot "dist/BaiyeCryogenicPantry-$taskVersion.zip"
if(Test-Path -LiteralPath $taskZip){Remove-Item -LiteralPath $taskZip -Force}
Compress-Archive -LiteralPath $taskStage -DestinationPath $taskZip
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskArchive=[IO.Compression.ZipFile]::OpenRead($taskZip)
try{
    $taskEntries=@($taskArchive.Entries|Where-Object{$_.Name})
    if($taskEntries.Count -ne $taskExpected.Count){throw 'Unexpected ZIP file count'}
    $taskSeen=@{}
    foreach($entry in $taskEntries){
        $path=$entry.FullName.Replace('\','/')
        if(-not $path.StartsWith('BaiyeCryogenicPantry/',[StringComparison]::Ordinal)){throw 'Unexpected ZIP root'}
        $relative=$path.Substring('BaiyeCryogenicPantry/'.Length)
        if($relative -notin $taskExpected -or $taskSeen.ContainsKey($relative)){throw "Unexpected/duplicate file $relative"}
        $taskSeen[$relative]=$true;$stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
        try{$digest=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()}
        if($digest -ne (Get-FileHash -LiteralPath (Join-Path $taskStage $relative) -Algorithm SHA256).Hash){throw "ZIP checksum mismatch $relative"}
    }
}finally{$taskArchive.Dispose()}
$taskHash=(Get-FileHash -LiteralPath $taskZip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($taskZip+'.sha256',"$taskHash  $([IO.Path]::GetFileName($taskZip))`n",[Text.UTF8Encoding]::new($false))
Write-Output "PASS package: $($taskExpected.Count) files, one own DLL, all ZIP contents/checksums verified."
Write-Output "Package: $taskZip"
Write-Output "SHA256: $taskHash"
