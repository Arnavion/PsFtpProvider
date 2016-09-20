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
using System.Text;
using System.Xml.Linq;

namespace PsFtpProvider
{
	[CmdletProvider("PsFtp", ProviderCapabilities.Credentials)]
	public class FtpProvider : NavigationCmdletProvider, IContentCmdletProvider
	{
		private new FtpDriveInfo PSDriveInfo =>
			(FtpDriveInfo)base.PSDriveInfo;

		protected override Collection<PSDriveInfo> InitializeDefaultDrives()
		{
			try
			{
				var root = XElement.Load(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileZilla", "sitemanager.xml"));

				return new Collection<PSDriveInfo>(
					(
						from server in root.Element("Servers").Elements("Server")
						let passwordElement = server.Element("Pass")
						let password = passwordElement.Attribute("encoding")?.Value == "base64" ? Encoding.UTF8.GetString(Convert.FromBase64String(passwordElement.Value)) : passwordElement.Value
						let protocol = int.Parse(server.Element("Protocol").Value)
						select new FtpDriveInfo
						(
							new Site
							(
								string.Join("", from textNode in server.Nodes().OfType<XText>() select textNode.Value.Trim()),
								server.Element("Host").Value, ushort.Parse(server.Element("Port").Value),
								new NetworkCredential(server.Element("User").Value, password),
								(protocol == 0 || protocol == 4) ? FtpEncryptionMode.Explicit :
								(protocol == 3) ? FtpEncryptionMode.Implicit :
								(protocol == 6) ? FtpEncryptionMode.None :
								FtpEncryptionMode.Explicit
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

		protected override bool IsItemContainer(string path) =>
			PSDriveInfo.IsItemContainer(path);

		#endregion

		#region ContainerCmdletProvider members

		protected override void GetChildItems(string path, bool recurse)
		{
			if (recurse)
			{
				throw new ArgumentOutOfRangeException(nameof(recurse), "recurse == true is not supported.");
			}

			foreach (var item in PSDriveInfo.GetChildItems(path))
			{
				WriteItemObject(item, item.FullName, item.Type == FtpFileSystemObjectType.Directory);
			}
		}

		protected override bool HasChildItems(string path) =>
			PSDriveInfo.HasChildItems(path);

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
					throw new ArgumentOutOfRangeException(nameof(itemTypeName));
			}

			WriteItemObject(newItem, path, newItem.Type == FtpFileSystemObjectType.Directory);
		}

		protected override void RemoveItem(string path, bool recurse) =>
			PSDriveInfo.RemoveItem(path, recurse);

		#endregion

		#region ItemCmdletProvider members

		protected override void GetItem(string path)
		{
			var item = PSDriveInfo.GetItem(path);
			WriteItemObject(item, item.FullName, item.Type == FtpFileSystemObjectType.Directory);
		}

		protected override bool IsValidPath(string path) =>
			PSDriveInfo != null;

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
				throw new ArgumentNullException(nameof(drive));
			}

			var result = drive as FtpDriveInfo;
			if (result != null)
			{
				return result;
			}

			var dynamicParameters = DynamicParameters as NewFtpDriveDynamicParameters;
			if (dynamicParameters == null)
			{
				throw new ArgumentOutOfRangeException(nameof(drive));
			}

			return new FtpDriveInfo
			(
				new Site
				(
					drive.Name,
					dynamicParameters.Hostname, dynamicParameters.Port,
					(NetworkCredential)drive.Credential,
					dynamicParameters.EncryptionMode
				),
				ProviderInfo
			);
		}

		protected override object NewDriveDynamicParameters() =>
			new NewFtpDriveDynamicParameters();

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

		public object ClearContentDynamicParameters(string path) =>
			null;

		public IContentReader GetContentReader(string path) =>
			PSDriveInfo.GetContentReader(path, DynamicParameters as ContentReaderDynamicParameters);

		public object GetContentReaderDynamicParameters(string path) =>
			new ContentReaderDynamicParameters();

		public IContentWriter GetContentWriter(string path) =>
			PSDriveInfo.GetContentWriter(path, DynamicParameters as ContentWriterDynamicParameters);

		public object GetContentWriterDynamicParameters(string path) =>
			new ContentWriterDynamicParameters();

		#endregion
	}
}
