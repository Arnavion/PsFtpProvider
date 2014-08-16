<#
  PsFtpProvider

  https://github.com/Arnavion/PsFtpProvider

  Copyright 2014 Arnav Singh

  Licensed under the Apache License, Version 2.0 (the "License");
  you may not use this file except in compliance with the License.
  You may obtain a copy of the License at

     http://www.apache.org/licenses/LICENSE-2.0

  Unless required by applicable law or agreed to in writing, software
  distributed under the License is distributed on an "AS IS" BASIS,
  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
  See the License for the specific language governing permissions and
  limitations under the License.
#>

<#

.SYNOPSIS
Clears the cache of an FTP drive.


.DESCRIPTION
Clears the cache of an FTP drive.
If no drive is provided, the current drive's cache is cleared.


.PARAMETER Drive
The FTP drive whose cache should be cleared. If not provided, the current drive's cache is cleared.


.EXAMPLE
MyFtpSite:\> Clear-FtpDriveCache


.EXAMPLE
C:\> Clear-FtpDriveCache $(Get-PSDrive MyFtpSite)

#>
function Clear-FtpDriveCache {
	param(
		[PsFtpProvider.FtpDriveInfo] $Drive = $ExecutionContext.SessionState.Drive.Current
	)

	$Drive.ClearCache()
}
