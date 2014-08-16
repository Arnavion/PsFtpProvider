@{
	ModuleToProcess = 'PsFtpProvider.dll'

	Description = 'FTP provider for PowerShell'
	Copyright = 'Copyright 2014 Arnav Singh'
	ModuleVersion = '1.0'
	Author = 'Arnavion'

	RequiredAssemblies = @(
		'System.Net.FtpClient.dll'
	)

	TypesToProcess = @(
		'PsFtpProvider.Types.ps1xml'
	)

	NestedModules = @(
		'PsFtpProvider.psm1'
	)
}
