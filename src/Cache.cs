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
using System.Net.FtpClient;

namespace PsFtpProvider
{
	internal class Cache
	{
		private readonly Site site;

		private FtpClient _client;
		public FtpClient Client
		{
			get
			{
				if (_client == null)
				{
					_client = new FtpClient() { Host = site.Hostname, Port = site.Port, Credentials = site.Credential, EncryptionMode = site.EncryptionMode };
				}

				return _client;
			}
		}

		private CacheDirectoryNode root;

		public Cache(Site site)
		{
			this.site = site;

			Clear();
		}

		public void Clear() =>
			root = new CacheDirectoryNode(new FtpListItem() { FullName = "/", Type = FtpFileSystemObjectType.Directory }, null, Client);

		public CacheNode GetItem(string path)
		{
			path = GetValidPath(path);

			var components = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
			CacheNode current = root;
			foreach (var component in components)
			{
				var currentDirectory = current as CacheDirectoryNode;
				if (currentDirectory == null)
				{
					break;
				}

				var child = currentDirectory.GetChild(component);
				if (child == null)
				{
					throw new ArgumentOutOfRangeException(nameof(path), $"Item { component } does not exist under { current.Item.FullName }");
				}

				current = child;
			}

			if (current.Item.FullName != path)
			{
				// Found a file with the same name as what should've been a directory.
				throw new ArgumentOutOfRangeException(nameof(path), $"{ current.Item.FullName } is not a directory.");
			}

			return current;
		}

		public List<CacheNode> GetChildItems(string path)
		{
			var item = GetItem(path);

			var directory = item as CacheDirectoryNode;
			if (directory == null)
			{
				throw new ArgumentOutOfRangeException(nameof(path), $"{ path } is not a directory.");
			}

			return directory.GetChildren();
		}

		public CacheNode CreateDirectory(string path)
		{
			path = GetValidPath(path);

			var components = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
			CacheDirectoryNode current = root;
			foreach (var component in components)
			{
				var child = current.GetChild(component);
				if (child == null)
				{
					child = current.CreateDirectory(component);
				}
				else if (child is CacheDirectoryNode)
				{
					current = (CacheDirectoryNode)child;
				}
				else
				{
					throw new ArgumentOutOfRangeException(nameof(path), $"Cannot create a directory named { component } because a file of that name already exists.");
				}
			}

			return current;
		}

		public CacheNode CreateFile(string path)
		{
			path = GetValidPath(path);

			var lastSlashPos = path.LastIndexOf('/');
			var parentPath = path.Substring(0, lastSlashPos);
			var fileName = path.Substring(lastSlashPos + 1);

			var parentPathComponents = parentPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
			CacheDirectoryNode current = root;
			foreach (var component in parentPathComponents)
			{
				var child = current.GetChild(component);
				if (child == null)
				{
					child = current.CreateDirectory(component);
				}
				else if (child is CacheDirectoryNode)
				{
					current = (CacheDirectoryNode)child;
				}
				else
				{
					throw new ArgumentOutOfRangeException(nameof(path), $"Cannot create a directory named { component } because a file of that name already exists.");
				}
			}

			return current.CreateFile(fileName);
		}

		public void DeleteFile(string path)
		{
			path = GetValidPath(path);

			var lastSlashPos = path.LastIndexOf('/');
			var parentPath = path.Substring(0, lastSlashPos);
			var fileName = path.Substring(lastSlashPos + 1);

			var parentPathComponents = parentPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
			CacheDirectoryNode current = root;
			foreach (var component in parentPathComponents)
			{
				var child = current.GetChild(component);
				if (child == null || !(child is CacheDirectoryNode))
				{
					throw new ArgumentOutOfRangeException(nameof(path), $"Directory { component } does not exist.");
				}

				current = (CacheDirectoryNode)child;
			}

			current.DeleteFile(fileName);
		}

		public void DeleteDirectory(string path, bool recurse)
		{
			path = GetValidPath(path);

			var lastSlashPos = path.LastIndexOf('/');
			var parentPath = path.Substring(0, lastSlashPos);
			var directoryName = path.Substring(lastSlashPos + 1);

			var parentPathComponents = parentPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
			CacheDirectoryNode current = root;
			foreach (var component in parentPathComponents)
			{
				var child = current.GetChild(component);
				if (child == null || !(child is CacheDirectoryNode))
				{
					throw new ArgumentOutOfRangeException(nameof(path), $"Directory { component } does not exist.");
				}

				current = (CacheDirectoryNode)child;
			}

			current.DeleteDirectory(directoryName, recurse);
		}

		private string GetValidPath(string path)
		{
			path = path.Replace('\\', '/');

			path = path.TrimEnd('/');

			if (path.Length < 1 || path[0] != '/')
			{
				path = "/" + path;
			}

			return path;
		}
	}

	internal class CacheNode
	{
		public FtpListItem Item { get; set; }

		public CacheDirectoryNode Parent { get; }

		public CacheNode(FtpListItem item, CacheDirectoryNode parent)
		{
			Item = item;
			Parent = parent;
		}
	}

	internal class CacheDirectoryNode : CacheNode
	{
		private Dictionary<string, CacheNode> _children = new Dictionary<string, CacheNode>();
		private Dictionary<string, CacheNode> Children
		{
			get
			{
				if (needsRefresh)
				{
					var listing = client.GetListing(Item.FullName, FtpListOption.Modify | FtpListOption.Size);

					if (listing.Length == 1 && listing[0].FullName == Item.FullName)
					{
						// The item of this name used to be a directory but has now become a file!
						Parent.MarkDirty();
					}
					else
					{
						var listingDictionary = listing.ToDictionary(child => child.Name, child =>
						{
							switch (child.Type)
							{
								case FtpFileSystemObjectType.File:
									return new CacheNode(child, this);
								case FtpFileSystemObjectType.Directory:
									return new CacheDirectoryNode(child, this, client);
								default:
									throw new ArgumentOutOfRangeException(nameof(child), "Found child of unexpected type " + child.Type);
							}
						});

						_children = listingDictionary.ToDictionary(kvp => kvp.Key, kvp =>
						{
							var listingChild = kvp.Value;

							CacheNode existingChild;
							if (_children.TryGetValue(listingChild.Item.Name, out existingChild) && existingChild.Item.Type == listingChild.Item.Type)
							{
								existingChild.Item = listingChild.Item;
								return existingChild;
							}
							else
							{
								return listingChild;
							}
						});
					}

					needsRefresh = false;
				}

				return _children;
			}
		}

		private bool needsRefresh;

		private readonly FtpClient client;

		public CacheDirectoryNode(FtpListItem item, CacheDirectoryNode parent, FtpClient client)
			: base(item, parent)
		{
			this.client = client;

			MarkDirty();
		}

		public CacheNode GetChild(string name)
		{
			CacheNode child;

			if (!Children.TryGetValue(name, out child))
			{
				// If not found once, try refreshing once.
				MarkDirty();
				Children.TryGetValue(name, out child);
			}

			return child;
		}

		public List<CacheNode> GetChildren() =>
			Children.Values.ToList();

		public CacheNode CreateFile(string name)
		{
			if (Children.ContainsKey(name))
			{
				// Item of that name already exists. Refresh once to be sure.
				MarkDirty();

				if (Children.ContainsKey(name))
				{
					throw new ArgumentOutOfRangeException(nameof(name), $"Cannot create file named { name } because an item of that name already exists.");
				}
			}

			using (client.OpenWrite(Item.FullName + "/" + name))
			{
			}

			MarkDirty();

			return GetChild(name);
		}

		public CacheDirectoryNode CreateDirectory(string name)
		{
			if (Children.ContainsKey(name))
			{
				// Item of that name already exists. Refresh once to be sure.
				MarkDirty();

				if (Children.ContainsKey(name))
				{
					throw new ArgumentOutOfRangeException(nameof(name), $"Cannot create directory named { name } because an item of that name already exists.");
				}
			}

			client.CreateDirectory(Item.FullName + "/" + name);

			MarkDirty();

			return (CacheDirectoryNode)GetChild(name);
		}

		public void DeleteFile(string name)
		{
			if (!Children.ContainsKey(name))
			{
				// Item of that name already exists. Refresh once to be sure.
				MarkDirty();

				if (!Children.ContainsKey(name))
				{
					throw new ArgumentOutOfRangeException(nameof(name), $"Cannot delete file named { name } because it doesn't exist.");
				}
			}

			client.DeleteFile(Item.FullName + "/" + name);

			MarkDirty();
		}

		public void DeleteDirectory(string name, bool recurse)
		{
			if (!Children.ContainsKey(name))
			{
				// Item of that name already exists. Refresh once to be sure.
				MarkDirty();

				if (!Children.ContainsKey(name))
				{
					throw new ArgumentOutOfRangeException(nameof(name), $"Cannot delete directory named { name } because it doesn't exist.");
				}
			}

			client.DeleteDirectory(Item.FullName + "/" + name, recurse);

			MarkDirty();
		}

		public void MarkDirty() =>
			needsRefresh = true;
	}
}
