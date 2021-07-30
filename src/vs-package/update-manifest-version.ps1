$path = "$PSScriptRoot\source.extension.vsixmanifest"

$manifest = [xml](Get-Content $path);
$version = nbgv get-version -v version

$manifest.PackageManifest.Metadata.Identity.Version = $version
$manifest.Save($path)