param ($manifestPath, $version)

$manifest = [xml](Get-Content $manifestPath);
$manifest.PackageManifest.Metadata.Identity.Version = $version
$manifest.Save($manifestPath)