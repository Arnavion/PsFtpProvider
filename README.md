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

	* Download a file

		```powershell
		# Can't use Copy-Item because it doesn't support the source and target being different providers.

		# Download a binary file
		[System.IO.File]::WriteAllBytes('C:\file.bin', $(cat './file.bin'))

		# Download a text file
		Set-Content C:\file.txt $(cat './file.txt' -Encoding UTF8) -Encoding UTF8

		# You don't need to cd to the site drive first. Fully qualified paths work too.
		[System.IO.File]::WriteAllBytes('C:\file.bin', $(cat 'MyFtpSite:/file.bin'))
		Set-Content C:\file.txt $(cat 'MyFtpSite:/file.txt' -Encoding UTF8) -Encoding UTF8
		```

	* Set a file's contents

		```powershell
		# Set a binary file's contents
		Set-Content ./file.bin [System.Text.Encoding]::UTF8.GetBytes('aaa')

		# Set a text file's contents in the given encoding
		Set-Content ./file.txt 'aaa' -Encoding UTF8

		# Set a text file's contents without specifying the encoding. Defaults to UTF-8.
		Set-Content ./file.txt 'aaa'
		```

	* Upload a file

		```powershell
		# Upload a binary file
		Set-Content './file.bin' -Encoding Byte $(Get-Content -Encoding Byte 'C:\file.bin')

		# Upload a text file. Get-Content will return one string per line and Set-Content will add a `n at the end of each.
		# If you don't want this, use binary mode as in the above example
		Set-Content './file.txt' -Encoding UTF8 $(Get-Content -Encoding UTF8 'C:\file.txt')

		# Upload a text file. Let PS guess the encoding of the input file. Output encoding still defaults to UTF8 and each line is terminated with a `n.
		Set-Content './file.txt' $(Get-Content 'C:\file.txt')
		```

	* Append to a file

		```powershell
		# Append to a binary file
		Add-Content ./file.bin [System.Text.Encoding]::UTF8.GetBytes('aaa')

		# Append to a text file in the given encoding
		Add-Content ./file.txt 'aaa' -Encoding UTF8

		# Append to a text file without specifying the encoding. Defaults to UTF-8.
		Add-Content ./file.txt 'aaa'
		```


### Caveats of the drive cache

PsFtpProvider drives cache the directory structure to avoid making repeated calls to the FTP server. For example, if you dir in a directory, it will cache the list of children to make future invocations of dir faster. If you then create a file a.txt using another FTP client in the same directory, running dir will continue to show the old directory listing.

In this case, you can use the Clear-FtpDriveCache commandlet to clear the cache for the drive.

```powershell
# Clear the current drive's cache
Clear-FtpDriveCache

# Clear the cache of a particular drive
Clear-FtpDriveCache $(Get-PSDrive MyFtpSite)
```

In some cases, this cache self-heals. For example, in the above example, dir would not show the new file until you cleared the cache and ran dir again. However, if you didn't clear the cache and tried to do ```cat a.txt```, PsFtpProvider would try to look up a.txt in the cache, fail to find it, and refresh the cache automatically to see if it exists now. After that, you would see the file in the output of dir.


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
