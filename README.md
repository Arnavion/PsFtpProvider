This project is no longer maintained.

---

PsFtpProvider is a PowerShell provider for FTP sites.

It allows you to interact with FTP sites using the commandlets you use for working with local files, such as `Set-Location (cd)`, `Get-ChildItem (dir)` and `Get-/Set-Content`. Sites are loaded by default from Filezilla's `sitemanager.xml` if present, and exposed as drives you can `cd` into.


### Build

- PowerShell 5.0

	```powershell
	dotnet publish -f net47
	```

- PowerShell Core (Windows)

	```powershell
	dotnet publish -f netcoreapp2.2 -r win-x64
	```

- PowerShell Core (Linux)

	```powershell
	dotnet publish -f netcoreapp2.2 -r linux-x64
	```


### Use

* Import the provider into a PS session

	```powershell
	# PowerShell 5.0
	Import-Module ./bin/Debug/net47/publish/PsFtpProvider.psd1

	# PowerShell Core (Windows)
	Import-Module ./bin/Debug/netcoreapp2.0/win-x64/publish/PsFtpProvider.psd1

	# PowerShell Core (Linux)
	Import-Module ./bin/Debug/netcoreapp2.0/linux-x64/publish/PsFtpProvider.psd1
	```

	or copy the publish directory's contents to your module path so that it's loaded at startup

	```powershell
	# PowerShell 5.0
	Copy-Item -Recurse ./bin/Debug/net47/publish/ ~/Documents/WindowsPowerShell/Modules/PsFtpProvider

	# PowerShell Core (Windows)
	Copy-Item -Recurse ./bin/Debug/netcoreapp2.0/win-x64/publish/ ~/Documents/PowerShell/Modules/PsFtpProvider

	# PowerShell Core (Linux)
	Copy-Item -Recurse ./bin/Debug/netcoreapp2.0/linux-x64/publish/ ~/.local/share/powershell/Modules/PsFtpProvider
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
		gc ./file.bin

		# Get a text file's contents
		gc ./file.txt -Encoding UTF8
		```

	* Download a file

		```powershell
		# Can't use Copy-Item because it doesn't support the source and target being different providers.

		# Download a binary file
		[System.IO.File]::WriteAllBytes('C:\file.bin', $(gc './file.bin'))

		# Download a text file
		Set-Content C:\file.txt $(gc './file.txt' -Encoding UTF8) -Encoding UTF8

		# You don't need to cd to the site drive first. Fully qualified paths work too.
		[System.IO.File]::WriteAllBytes('C:\file.bin', $(gc 'MyFtpSite:/file.bin'))
		Set-Content C:\file.txt $(gc 'MyFtpSite:/file.txt' -Encoding UTF8) -Encoding UTF8
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
		Set-Content './file.bin' -Encoding Byte $(gc -Encoding Byte 'C:\file.bin')

		# Upload a text file. Get-Content will return one string per line and Set-Content will add a `n at the end of each.
		# If the original file did not have `n newlines, this will cause the file on the server to be different from the local file.
		# If you don't want this to happen, use binary mode as in the above example
		Set-Content './file.txt' -Encoding UTF8 $(gc -Encoding UTF8 'C:\file.txt')

		# Upload a text file. Let PS guess the encoding of the input file. Output encoding still defaults to UTF8 and each line is terminated with a `n.
		Set-Content './file.txt' $(gc 'C:\file.txt')
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

PsFtp drives cache the directory structure to avoid making repeated calls to the FTP server. For example, if you run `dir` in a directory, it will cache the list of children to make future invocations of `dir` faster. If you then create a file `a.txt` using another FTP client in the same directory, running `dir` will continue to show the old directory listing.

In this case, you can use the `Clear-FtpDriveCache` commandlet to clear the cache for the drive.

```powershell
# Clear the current drive's cache
Clear-FtpDriveCache

# Clear the cache of a particular drive
Clear-FtpDriveCache $(Get-PSDrive MyFtpSite)
```

In some cases, this cache self-heals. For example, in the above example, `dir` would not show the new file until you cleared the cache and ran `dir` again. However, if you didn't clear the cache and tried to do ```gc a.txt```, PsFtpProvider would try to look up `a.txt` in the cache, fail to find it, and refresh the cache automatically to see if it exists now. After that, you would start seeing `a.txt` in the output of `dir`.


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

FTP implementation is provided by [FluentFTP](https://github.com/robinrodricks/FluentFTP) which uses the MIT license.
