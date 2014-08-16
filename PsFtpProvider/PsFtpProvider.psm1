function Clear-FtpDriveCache {
	param(
		[PsFtpProvider.FtpDriveInfo] $Drive = $ExecutionContext.SessionState.Drive.Current
	)

	$Drive.ClearCache()
}
