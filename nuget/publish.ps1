$ErrorActionPreference = "Stop"

$nugetServer = "http://lightcodenuget.azurewebsites.net/nuget"
$apiKey = "0DB79AEE-1D2D-44F0-BDAD-863EEEC4BC0C"
$packageName = "LightCode.AlertService"
$projPath = "..\src\LightCode.AlertService\LightCode.AlertService.csproj"

function FormatVersion([string] $version) {
	$parsedVersion = [System.Version]::Parse($version)
	return "$($parsedVersion.Major).$($parsedVersion.Minor).$($parsedVersion.Build)"
}

function Get-ProjectVersion ([string] $path) {
	$filePath = Resolve-Path $path	
	[xml]$xml = Get-Content $filePath	
	$prjVersion = $xml.Project.PropertyGroup.AssemblyVersion
	
	if ([string]::IsNullOrEmpty($prjVersion))
	{
		$prjVersion = "0.0.0"
	}
	return $prjVersion
}

function Set-ProjectVersion ([string] $path, [string] $version) { 
	$filePath = Resolve-Path $path
	$xml = [xml](Get-Content $filePath)
	if ($null -eq $xml.Project.PropertyGroup.AssemblyVersion)
	{
		$child = $xml.CreateElement("AssemblyVersion")
		$xml.Project.PropertyGroup.AppendChild($child)		
	}
	$xml.Project.PropertyGroup.AssemblyVersion = $version
	
	if ($null -eq $xml.Project.PropertyGroup.FileVersion)
	{
		$child = $xml.CreateElement("FileVersion")
		$xml.Project.PropertyGroup.AppendChild($child)		
	}
	$xml.Project.PropertyGroup.FileVersion = $version
	$xml.Save($filePath) | Out-Null

	$specFile = $filePath -replace "csproj", "nuspec"
	$xmlSpec = [xml](Get-Content $specFile)
	$xmlSpec.package.metadata.version = $version
	$xmlSpec.Save($specFile) | Out-Null

}

function NextBuildVersion([string] $version) {
	$versionTokens = $version.split(".")
	$buildNumber = [System.Double]::Parse($versionTokens[$versionTokens.Count -1]) 
	$versionTokens[$versionTokens.Count -1] = $buildNumber +1
	$newVersion = [string]::join('.', $versionTokens)
	return $newVersion 
}

function ExistNuspecFile([string] $path) { 
	$specFile = $path -replace "csproj", "nuspec"
	$exist = Test-Path -Path $specFile -PathType Leaf
	return $exist
}

function CreateNuspecFile([string] $path) { 
	
	$description = Read-Host "File Nuspec non presente. Inserire una descrizione per il pacchetto nuget"

	$specFile = $path -replace "csproj", "nuspec"

	New-Item $specFile
	Set-Content $specFile "<?xml version=""1.0""?>"
	Add-Content $specFile "<package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">"
	Add-Content $specFile "  <metadata>"
	Add-Content $specFile "    <id>$packageName</id>"
	Add-Content $specFile "    <title>$packageName</title>"
	Add-Content $specFile "    <authors>LightCode srl</authors>"
	Add-Content $specFile "    <copyright>Copyright © 2021</copyright>"
	Add-Content $specFile "    <version>0.0.0</version>"
	Add-Content $specFile "    <requireLicenseAcceptance>false</requireLicenseAcceptance>"
	Add-Content $specFile "    <description>$description</description>"
	Add-Content $specFile "    <dependencies />"
	Add-Content $specFile "  </metadata>"
	Add-Content $specFile "</package>"
	return $exist
}



$changedFiles = $(git status --porcelain | Measure-Object | Select-Object -expand Count)
if ($changedFiles -gt 0)
{
	$confirmation = Read-Host "Ci sono $changedFiles files modificati. Vuoi procedere comunque con il rilascio del pacchetto? [y/n]"
	while($confirmation -ne "y")
	{
		if (($confirmation -eq 'n') -or ($confirmation -eq '')) {exit}
		$confirmation = Read-Host "Ci sono $changedFiles modificati. Vuoi procedere comunque con il rilascio del pacchetto? [y/N]"
	}
}

$prjVersion = FormatVersion(Get-ProjectVersion($projPath))	
$newVersion = NextBuildVersion($prjVersion)
$reqestedVersion =  Read-Host "La versione attuale è '$prjVersion'. Immettere la nuova versione o premere invio per accettare il default ($newVersion)"

if ($reqestedVersion -eq "")
{
	$reqestedVersion = $newVersion
}
else
{
	$parsedVersion = [Version]::new()
	if (-not [Version]::TryParse($reqestedVersion, [ref]$parsedVersion))
	{
		Write-Host ("Versione '$reqestedVersion' non valida")
		exit
	}
	$reqestedVersion = $parsedVersion.ToString()
}

$latestRelease = nuget list $packageName -source $nugetServer
if ($latestRelease -ne "No packages found.")
{
	$latestRelease = $latestRelease.Split(' ')[1]
	if ([System.Version]::Parse($latestRelease) -ge [System.Version]::Parse($reqestedVersion))
	{
		Write-Host "La versione richiesta ($reqestedVersion) deve essere maggiore dell'ultima versione presente su nuget ($latestRelease)"
		exit
	}	
}

if (-not (ExistNuspecFile $projPath))
{
	CreateNuspecFile $projPath
}

Write-Host "Updating version in .Net files"
Set-ProjectVersion -path $projPath -version $reqestedVersion

Write-Host "Build project..."
dotnet restore $projPath
dotnet  build $projPath

Write-Host "Packing nuget package..."
get-childitem | Where-Object {$_.extension -eq ".nupkg"} | ForEach-Object ($_) {remove-item $_.fullname}
nuget pack $projPath -Version $reqestedVersion

Write-Host "Pushing nuget package..."
$package = get-childitem | Where-Object {$_.extension -eq ".nupkg"}
nuget push -Source $nugetServer $package $apiKey

if ($changedFiles -eq 0)
{
	git add ..
	git commit -m "Pubblicazione pacchetto nuget versione $reqestedVersion"
	git tag $reqestedVersion
	git push
	git push origin $reqestedVersion
}
else 
{
	Write-Host " *** ci sono files modificati per cui non viene taggato l'ultimo commit ***"
}
