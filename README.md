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

	* List all items in current directory

		```powershell
		dir
		```

	* List all items in current directory recursively.

		```powershell
		# If not supported by the server, this returns the same results as without the -Recurse switch
		dir -Recurse
		```

	* Get a file's contents

		```powershell
		# Get a binary file's contents
		cat ./file.bin

		# Get a text file's contents
		cat ./file.txt -Encoding UTF8
		```

	* Save a file to a local drive

		```powershell
		# Can't use Copy-Item because it doesn't support the source and target being different providers.

		# Save a binary file
		[System.IO.File]::WriteAllBytes('C:\file.bin', $(cat './file.bin'))

		# Save a text file
		Set-Content C:\file.txt $(cat './file.txt' -Encoding UTF8) -Encoding UTF8

		# You don't need to cd to the site drive first. Fully qualified paths work too.
		[System.IO.File]::WriteAllBytes('C:\file.bin', $(cat 'MyFtpSite:/file.bin'))
		Set-Content C:\file.txt $(cat 'MyFtpSite:/file.txt' -Encoding UTF8) -Encoding UTF8
		```

	* Write a file

		```powershell
		# Write a binary file
		Set-Content ./file.bin [System.Text.Encoding]::UTF8.GetBytes('aaa')

		# Write a text file in the given encoding
		Set-Content ./file.txt 'aaa' -Encoding UTF8

		# Write a text file without specifying the encoding. Defaults to UTF-8.
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
