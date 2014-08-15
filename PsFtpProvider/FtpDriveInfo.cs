/**
 * PsFtpProvider
 *
 * https://github.com/Arnavion/PsFtpProvider
 *
 * Copyright 2014 Arnav Singh
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Net.FtpClient;

namespace PsFtpProvider
{
	internal class FtpDriveInfo : PSDriveInfo
	{
		public Site Site { get; private set; }

		private FtpClient _client;
		private FtpClient Client
		{
			get
			{
				if (_client == null)
				{
					_client = new FtpClient() { Host = Site.Hostname, Port = Site.Port, Credentials = Site.Credential };
				}

				return _client;
			}
		}

		internal FtpDriveInfo(Site site, ProviderInfo provider)
			: base
			(
				site.Name, provider, "\\", "FTP Provider for " + site.Hostname,
				site.Credential != null ? new PSCredential(site.Credential.UserName, site.Credential.SecurePassword) : null,
				true
			)
		{
			Site = site;
		}

		public bool IsItemContainer(string path)
		{
			return Client.DirectoryExists(path);
		}

		public FtpListItem[] GetChildItems(string path, bool recurse)
		{
			if (!recurse)
			{
				return Client.GetListing(path);
			}
			else
			{
				return Client.GetListing(path, FtpListOption.Recursive);
			}
		}

		public bool HasChildItems(string path)
		{
			return Client.GetListing(path).Length > 1;
		}

		public FtpListItem NewFile(string path)
		{
			using (Client.OpenWrite(path))
			{
			}

			return GetItem(path);
		}

		public FtpListItem NewDirectory(string path)
		{
			Client.CreateDirectory(path);
			return GetItem(path);
		}

		public FtpListItem GetItem(string path)
		{
			var parent = Path.GetDirectoryName(path);
			if (parent == null)
			{
				return new FtpListItem() { FullName = "/" };
			}

			var list = Client.GetListing(parent);

			return list.First(item => item.FullName == path.Replace('\\', '/'));
		}

		public void RemoveItem(string path, bool recurse)
		{
			var item = GetItem(path);

			switch (item.Type)
			{
				case FtpFileSystemObjectType.File:
					Client.DeleteFile(path);
					break;

				case FtpFileSystemObjectType.Directory:
					if (!recurse)
					{
						Client.DeleteDirectory(path, true);
					}
					else
					{
						Client.DeleteDirectory(path, true, FtpListOption.Recursive);
					}
					break;

				default:
					throw new PSInvalidOperationException("Item is neither a file nor a directory.");
			}
		}

		public Stream OpenRead(string path)
		{
			return Client.OpenRead(path, FtpDataType.Binary);
		}

		public Stream OpenWrite(string path)
		{
			return Client.OpenWrite(path, FtpDataType.Binary);
		}
	}

	internal class NewFtpDriveDynamicParameters
	{
		[Parameter(Mandatory = true), ValidateNotNullOrEmpty]
		public string Hostname { get; set; }

		[Parameter(Mandatory = true)]
		public ushort Port { get; set; }
	}

	internal class Site
	{
		public string Name { get; private set; }

		public string Hostname { get; private set; }

		public ushort Port { get; private set; }

		public NetworkCredential Credential { get; private set; }

		public Site(string name, string hostname, ushort port, NetworkCredential credential)
		{
			Name = name;
			Hostname = hostname;
			Port = port;
			Credential = credential;
		}
	}
}
