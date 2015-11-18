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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Net.FtpClient;

namespace PsFtpProvider
{
	public class FtpDriveInfo : PSDriveInfo
	{
		private Cache cache;

		internal FtpDriveInfo(Site site, ProviderInfo provider)
			: base
			(
				site.Name, provider, "", "FTP Provider for " + site.Hostname,
				site.Credential != null ? new PSCredential(site.Credential.UserName, site.Credential.SecurePassword) : null,
				true
			)
		{
			cache = new Cache(site);
		}

		public void ClearCache()
		{
			cache.Clear();
		}

		internal bool IsItemContainer(string path)
		{
			try
			{
				var item = cache.GetItem(path);
				return item.Item.Type == FtpFileSystemObjectType.Directory;
			}
			catch
			{
				return false;
			}
		}

		internal IEnumerable<FtpListItem> GetChildItems(string path)
		{
			return cache.GetChildItems(path).Select(cacheNode => cacheNode.Item);
		}

		internal bool HasChildItems(string path)
		{
			try
			{
				return GetChildItems(path).Any();
			}
			catch
			{
				return false;
			}
		}

		internal FtpListItem NewFile(string path)
		{
			return cache.CreateFile(path).Item;
		}

		internal FtpListItem NewDirectory(string path)
		{
			return cache.CreateDirectory(path).Item;
		}

		internal FtpListItem GetItem(string path)
		{
			return cache.GetItem(path).Item;
		}

		internal void RemoveItem(string path, bool recurse)
		{
			var item = GetItem(path);

			switch (item.Type)
			{
				case FtpFileSystemObjectType.File:
					cache.DeleteFile(item.FullName);
					break;

				case FtpFileSystemObjectType.Directory:
					cache.DeleteDirectory(item.FullName, recurse);
					break;

				default:
					throw new PSInvalidOperationException("Item is neither a file nor a directory.");
			}
		}

		internal ContentReader GetContentReader(string path, ContentReaderDynamicParameters parameters)
		{
			var item = cache.GetItem(path);
			if (item.Item.Type != FtpFileSystemObjectType.File)
			{
				throw new ArgumentOutOfRangeException(nameof(path), "Item is not a file.");
			}

			return new ContentReader(item, parameters, cache.Client);
		}

		internal ContentWriter GetContentWriter(string path, ContentWriterDynamicParameters parameters)
		{
			CacheNode item;

			try
			{
				item = cache.GetItem(path);
			}
			catch
			{
				item = cache.CreateFile(path);
			}

			if (item.Item.Type != FtpFileSystemObjectType.File)
			{
				throw new ArgumentOutOfRangeException(nameof(path), "Cannot create a new file with that name because a non-file item of that name already exists.");
			}

			return new ContentWriter(item, parameters, cache.Client);
		}
	}

	internal class NewFtpDriveDynamicParameters
	{
		[Parameter(Mandatory = true), ValidateNotNullOrEmpty]
		public string Hostname { get; set; }

		[Parameter(Mandatory = true)]
		public ushort Port { get; set; }

		[Parameter]
		public FtpEncryptionMode EncryptionMode { get; set; }
	}

	internal class Site
	{
		public string Name { get; }

		public string Hostname { get; }

		public ushort Port { get; }

		public NetworkCredential Credential { get; }

		public FtpEncryptionMode EncryptionMode { get; }

		public Site(string name, string hostname, ushort port, NetworkCredential credential, FtpEncryptionMode encryptionMode)
		{
			Name = name;
			Hostname = hostname;
			Port = port;
			Credential = credential;
			EncryptionMode = encryptionMode;
		}
	}
}
