﻿Param(
	[Parameter(Mandatory=$true)][string]$path,
	[Parameter(Mandatory=$true)][string]$version,
	[Parameter(Mandatory=$false)][string]$branch
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ($version) {

	$nsUri = 'http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd'
	$authors = 'Linq To DB Team'
	$ns = @{ns=$nsUri}
	$dotlessVersion = $version -replace '\.',''
	$commit = (git rev-parse HEAD)
	if (-not $branch) {
		$branch = (git rev-parse --abbrev-ref HEAD)
	}

	Get-ChildItem $path | ForEach {
		$xmlPath = Resolve-Path $_.FullName

		$xml = [xml] (Get-Content "$xmlPath")
		$xml.PreserveWhitespace = $true

		Select-Xml -Xml $xml -XPath '//ns:metadata/ns:version' -Namespace $ns |
		Select -expand node |
		ForEach { $_.InnerText = $version }

		$child = $xml.CreateElement('version', $nsUri)
		$child.InnerText = $version
		$xml.package.metadata.AppendChild($child)

		$child = $xml.CreateElement('releaseNotes', $nsUri)
		$child.InnerText = 'https://github.com/linq2db/linq2db.LINQPad/blob/master/release-notes.md#release-' + $dotlessVersion
		$xml.package.metadata.AppendChild($child)

		$child = $xml.CreateElement('copyright', $nsUri)
		$child.InnerText = 'Copyright © 2016-2025 ' + $authors
		$xml.package.metadata.AppendChild($child)

		$child = $xml.CreateElement('authors', $nsUri)
		$child.InnerText = $authors
		$xml.package.metadata.AppendChild($child)

		$child = $xml.CreateElement('owners', $nsUri)
		$child.InnerText = $authors
		$xml.package.metadata.AppendChild($child)

		$child = $xml.CreateElement('license', $nsUri)
		$attr = $xml.CreateAttribute('type')
		$attr.Value = 'file'
		$child.Attributes.Append($attr)
		$child.InnerText = 'MIT-LICENSE.txt'
		$xml.package.metadata.AppendChild($child)

		$child = $xml.CreateElement('file', $nsUri)
		$attr = $xml.CreateAttribute('src')
		$attr.Value = '..\MIT-LICENSE.txt'
		$child.Attributes.Append($attr)
		$xml.package.files.AppendChild($child)

		$child = $xml.CreateElement('projectUrl', $nsUri)
		$child.InnerText = 'https://linq2db.github.io'
		$xml.package.metadata.AppendChild($child)

		# add icon + icon file
		$child = $xml.CreateElement('icon', $nsUri)
		$child.InnerText = 'images\icon.png'
		$xml.package.metadata.AppendChild($child)

		$child = $xml.CreateElement('file', $nsUri)
		$attr = $xml.CreateAttribute('src')
		$attr.Value = 'icon.png'
		$child.Attributes.Append($attr)
		$attr = $xml.CreateAttribute('target')
		$attr.Value = 'images\icon.png'
		$child.Attributes.Append($attr)
		$xml.package.files.AppendChild($child)


		$child = $xml.CreateElement('requireLicenseAcceptance', $nsUri)
		$child.InnerText = 'false'
		$xml.package.metadata.AppendChild($child)

		$child = $xml.CreateElement('repository', $nsUri)
		$attr = $xml.CreateAttribute('type')
		$attr.Value = 'git'
		$child.Attributes.Append($attr)
		$attr = $xml.CreateAttribute('url')
		$attr.Value = 'https://github.com/linq2db/linq2db.LINQPad.git'
		$child.Attributes.Append($attr)
		$attr = $xml.CreateAttribute('branch')
		$attr.Value = $branch
		$child.Attributes.Append($attr)
		$attr = $xml.CreateAttribute('commit')
		$attr.Value = $commit
		$child.Attributes.Append($attr)
		$xml.package.metadata.AppendChild($child)

		Write-Host "Patched $xmlPath"
		$xml.Save($xmlPath)
	}
}
