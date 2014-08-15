PsFtpProvider is a PowerShell provider for FTP sites.

It allows you to interact with FTP sites using the commandlets you use for working with local files, such as Set-Location (cd), Get-ChildItem (dir) and Get-/Set-Content. Sites are loaded by default from Filezilla's sitemanager.xml if present, and exposed as drives you can cd into.


### Build

```batchfile
REM In "Developer Command Prompt for VS 2013"
msbuild .\PsFtpProvider.sln
```


### Use

* Import the provider into a PS session

	```powershell
	cd PsFtpProvider\bin\Debug
	Import-Module .\PsFtpProvider.psd1
	```

* List all FTP sites provided as drives by this provider (defaults to any sites defined in Filezilla's sitemanager.xml)

	```powershell
	Get-PSDrive -PSProvider PsFtp
	```

* Add a new site (not persisted outside the current session)

	```powershell
	# With credentials
	New-PSDrive -PSProvider PsFtp -Name 'MyFTPSite' -Hostname 'ftp.example.com' -Port 21 -Root / -Credential $(Get-Credential)

	# Without credentials
	New-PSDrive -PSProvider PsFtp -Name 'MyFTPSite' -Hostname 'ftp.example.com' -Port 21 -Root /
	```

* Enter the site's directory

	```powershell
	# The drive's "Name" followed by a colon and slash is what you cd into, just like you would cd into C:\
	cd MyFtpSite:/
	```

* Browse around

	```powershell
	# List all items in current directory
	dir

	# List all items in current directory recursively.
	# If not supported by the server, this returns the same results as without the -Recurse switch
	dir -Recurse

	# Get a file's contents (bytes)
	cat ./file.txt

	# Save a file to local machine. Can't use Copy-Item because it doesn't support the source and target being different providers.
	[System.IO.File]::WriteAllBytes('C:\file.txt', $(cat 'MyFtpSite:/file.txt'))

	# Set a file's contents
	# If given a byte array, the bytes are used as-is. If given a string, the string is saved as UTF-8.
	Set-Content ./file.txt 'aaa'
	```


### Links

* [GitHub](https://github.com/Arnavion/PsFtpProvider)


### License

```
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
```

FTP implementation is provided by [System.Net.FtpClient](https://netftp.codeplex.com/) which uses the MIT license.
