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
		private Site site;

		private FtpClient _client;
		public FtpClient Client
		{
			get
			{
				if (_client == null)
				{
					_client = new FtpClient() { Host = site.Hostname, Port = site.Port, Credentials = site.Credential };
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

		public void Clear()
		{
			root = new CacheDirectoryNode(new FtpListItem() { FullName = "/", Type = FtpFileSystemObjectType.Directory }, null, Client);
		}

		public CacheNode GetItem(string path)
		{
			path = GetValidPath(path);

			var components = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
			CacheNode current = root;
			foreach (var component in components)
			{
				var currentDirectory = current as CacheDirectoryNode;
				if (currentDirectory != null)
				{
					var child = currentDirectory.GetChild(component);
					if (child == null)
					{
						throw new ArgumentOutOfRangeException("path", string.Format("Item {0} does not exist under {1}", component, current.Item.FullName));
					}

					current = child;
				}
				else
				{
					break;
				}
			}

			if (current.Item.FullName != path)
			{
				// Found a file with the same name as what should've been a directory.
				throw new ArgumentOutOfRangeException("path", string.Format("{0} is not a directory.", current.Item.FullName));
			}

			return current;
		}

		public List<CacheNode> GetChildItems(string path)
		{
			var item = GetItem(path);

			var directory = item as CacheDirectoryNode;
			if (directory == null)
			{
				throw new ArgumentOutOfRangeException("path", string.Format("{0} is not a directory.", path));
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
					throw new ArgumentOutOfRangeException("path", string.Format("Cannot create a directory named {0} because a file of that name already exists.", component));
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
					throw new ArgumentOutOfRangeException("path", string.Format("Cannot create a directory named {0} because a file of that name already exists.", component));
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
					throw new ArgumentOutOfRangeException("path", string.Format("Directory {0} does not exist.", component));
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
					throw new ArgumentOutOfRangeException("path", string.Format("Directory {0} does not exist.", component));
				}

				current = (CacheDirectoryNode)child;
			}

			current.DeleteDirectory(directoryName, recurse);
		}

		private string GetValidPath(string path)
		{
			path = path.Replace('\\', '/');

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

		public CacheDirectoryNode Parent { get; private set; }

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
					var listing = client.GetListing(Item.FullName);

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
									throw new ArgumentOutOfRangeException("child", "Found child of unexpected type " + child.Type);
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

		private FtpClient client;

		public CacheDirectoryNode(FtpListItem item, CacheDirectoryNode parent, FtpClient client)
			:base(item, parent)
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

		public List<CacheNode> GetChildren()
		{
			return Children.Values.ToList();
		}

		public CacheNode CreateFile(string name)
		{
			if (Children.ContainsKey(name))
			{
				// Item of that name already exists. Refresh once to be sure.
				MarkDirty();

				if (Children.ContainsKey(name))
				{
					throw new ArgumentOutOfRangeException("name", string.Format("Cannot create file named {0} because an item of that name already exists.", name));
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
					throw new ArgumentOutOfRangeException("name", string.Format("Cannot create directory named {0} because an item of that name already exists.", name));
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
					throw new ArgumentOutOfRangeException("name", string.Format("Cannot delete file named {0} because it doesn't exist.", name));
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
					throw new ArgumentOutOfRangeException("name", string.Format("Cannot delete directory named {0} because it doesn't exist.", name));
				}
			}

			client.DeleteDirectory(Item.FullName + "/" + name, recurse);

			MarkDirty();
		}

		public void MarkDirty()
		{
			needsRefresh = true;
		}
	}
}
