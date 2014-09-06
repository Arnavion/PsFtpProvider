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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Provider;
using System.Net;
using System.Net.FtpClient;
using System.Xml.Linq;

namespace PsFtpProvider
{
	[CmdletProvider("PsFtp", ProviderCapabilities.Credentials)]
	public class FtpProvider : NavigationCmdletProvider, IContentCmdletProvider
	{
		private new FtpDriveInfo PSDriveInfo
		{
			get
			{
				return (FtpDriveInfo)base.PSDriveInfo;
			}
		}

		protected override Collection<PSDriveInfo> InitializeDefaultDrives()
		{
			try
			{
				var root = XElement.Load(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileZilla", "sitemanager.xml"));

				return new Collection<PSDriveInfo>(
					(
						from server in root.Element("Servers").Elements("Server")
						select new FtpDriveInfo
						(
							new Site
							(
								string.Join("", from textNode in server.Nodes().OfType<XText>() select textNode.Value.Trim()),
								server.Element("Host").Value, ushort.Parse(server.Element("Port").Value),
								new NetworkCredential(server.Element("User").Value, server.Element("Pass").Value)
							),
							ProviderInfo
						)
					).ToList<PSDriveInfo>()
				);
			}
			catch
			{
				return new Collection<PSDriveInfo>();
			}
		}

		#region NavigationCmdletProvider members

		protected override bool IsItemContainer(string path)
		{
			return PSDriveInfo.IsItemContainer(path);
		}

		#endregion

		#region ContainerCmdletProvider members

		protected override void GetChildItems(string path, bool recurse)
		{
			if (recurse)
			{
				throw new ArgumentOutOfRangeException("recurse", "recurse == true is not supported.");
			}

			foreach (var item in PSDriveInfo.GetChildItems(path))
			{
				WriteItemObject(item, item.FullName, item.Type == FtpFileSystemObjectType.Directory);
			}
		}

		protected override bool HasChildItems(string path)
		{
			return PSDriveInfo.HasChildItems(path);
		}

		protected override void NewItem(string path, string itemTypeName, object newItemValue)
		{
			FtpListItem newItem = null;

			switch (itemTypeName)
			{
				case "File":
					newItem = PSDriveInfo.NewFile(path);
					break;

				case "Directory":
					newItem = PSDriveInfo.NewDirectory(path);
					break;

				default:
					throw new ArgumentOutOfRangeException("itemTypeName");
			}

			WriteItemObject(newItem, path, newItem.Type == FtpFileSystemObjectType.Directory);
		}

		protected override void RemoveItem(string path, bool recurse)
		{
			PSDriveInfo.RemoveItem(path, recurse);
		}

		#endregion

		#region ItemCmdletProvider members

		protected override void GetItem(string path)
		{
			var item = PSDriveInfo.GetItem(path);
			WriteItemObject(item, item.FullName, item.Type == FtpFileSystemObjectType.Directory);
		}

		protected override bool IsValidPath(string path)
		{
			return PSDriveInfo != null;
		}

		protected override bool ItemExists(string path)
		{
			if (PSDriveInfo == null)
			{
				return false;
			}

			try
			{
				PSDriveInfo.GetItem(path);

				return true;
			}
			catch
			{
				return false;
			}
		}

		#endregion

		#region DriveCmdletProvider members

		protected override PSDriveInfo NewDrive(PSDriveInfo drive)
		{
			if (drive == null)
			{
				throw new ArgumentNullException();
			}

			var result = drive as FtpDriveInfo;
			if (result != null)
			{
				return result;
			}

			var dynamicParameters = DynamicParameters as NewFtpDriveDynamicParameters;
			if (dynamicParameters != null)
			{
				return new FtpDriveInfo
				(
					new Site
					(
						drive.Name,
						dynamicParameters.Hostname, dynamicParameters.Port,
						(drive.Credential != null && drive.Credential != PSCredential.Empty) ? new NetworkCredential(drive.Credential.UserName, drive.Credential.Password) : null
					),
					ProviderInfo
				);
			}

			throw new ArgumentOutOfRangeException("drive");
		}

		protected override object NewDriveDynamicParameters()
		{
			return new NewFtpDriveDynamicParameters();
		}

		protected override PSDriveInfo RemoveDrive(PSDriveInfo drive)
		{
			throw new InvalidOperationException("Removing drives is not allowed with this provider.");
		}

		#endregion

		#region IContentCmdletProvider members

		public void ClearContent(string path)
		{
			using (var writer = PSDriveInfo.GetContentWriter(path, DynamicParameters as ContentWriterDynamicParameters))
			{
				writer.Truncate();
			}
		}

		public object ClearContentDynamicParameters(string path)
		{
			return null;
		}

		public IContentReader GetContentReader(string path)
		{
			return PSDriveInfo.GetContentReader(path, DynamicParameters as ContentReaderDynamicParameters);
		}

		public object GetContentReaderDynamicParameters(string path)
		{
			return new ContentReaderDynamicParameters();
		}

		public IContentWriter GetContentWriter(string path)
		{
			return PSDriveInfo.GetContentWriter(path, DynamicParameters as ContentWriterDynamicParameters);
		}

		public object GetContentWriterDynamicParameters(string path)
		{
			return new ContentWriterDynamicParameters();
		}

		#endregion
	}
}
