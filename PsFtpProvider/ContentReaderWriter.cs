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

using Microsoft.PowerShell.Commands;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Provider;
using System.Net.FtpClient;
using System.Text;

namespace PsFtpProvider
{
	internal class ContentReaderWriter : IContentReader, IContentWriter
	{
		public enum Mode { Read, Write, Append }

		private CacheNode item;

		private Mode mode;

		private Encoding encoding;

		private FtpClient client;

		private Stream stream;

		public ContentReaderWriter(CacheNode item, Mode mode, ContentReaderWriterDynamicParameters parameters, FtpClient client)
		{
			this.item = item;

			this.mode = mode;

			this.client = client;

			var encoding = parameters != null ? parameters.Encoding : FileSystemCmdletProviderEncoding.Byte;

			if (encoding != FileSystemCmdletProviderEncoding.Byte)
			{
				this.encoding = new FileSystemContentWriterDynamicParameters() { Encoding = encoding }.EncodingType;
			}
			else
			{
				this.encoding = null;
			}
		}

		public void Truncate()
		{
			if (stream == null)
			{
				if (mode != Mode.Write)
				{
					throw new InvalidOperationException("Cannot write to a non-writable stream.");
				}

				stream = client.OpenWrite(item.Item.FullName, FtpDataType.Binary);
			}

			stream.SetLength(0);
		}

		#region IContentReader members

		public IList Read(long readCount)
		{
			if (stream == null)
			{
				if (mode != Mode.Read)
				{
					throw new InvalidOperationException("Cannot read from a non-readable stream.");
				}

				stream = client.OpenRead(item.Item.FullName, FtpDataType.Binary);
			}

			var buffer = new byte[4096];

			if (readCount <= 0 || encoding != null)
			{
				readCount = long.MaxValue;
			}

			var read = stream.Read(buffer, 0, (int)Math.Min(readCount, buffer.Length));
			var result = new byte[read];

			if (read == 0)
			{
				return result;
			}

			Array.Copy(buffer, result, read);

			if (encoding == null)
			{
				return result;
			}

			return new[] { encoding.GetString(result) };
		}

		#endregion

		#region IContentWriter members

		public IList Write(IList content)
		{
			if (content.Count <= 0)
			{
				return content;
			}

			if (stream == null)
			{
				switch (mode)
				{
					case Mode.Write:
						stream = client.OpenWrite(item.Item.FullName, FtpDataType.Binary);
						break;

					case Mode.Append:
						stream = client.OpenAppend(item.Item.FullName, FtpDataType.Binary);
						break;

					default:
						throw new InvalidOperationException("Cannot write to a non-writable stream.");
				}
			}

			if (content[0] is PSObject)
			{
				content = content.Cast<PSObject>().Select(obj => obj.BaseObject).ToArray();
			}

			byte[] bytes;

			if (content[0] is string)
			{
				if (encoding == null)
				{
					encoding = Encoding.UTF8;
				}

				bytes = content.Cast<string>().SelectMany(str => encoding.GetBytes(str + "\n")).ToArray();
			}
			else if (content[0] is byte)
			{
				bytes = content.Cast<byte>().ToArray();
			}
			else
			{
				throw new ArgumentOutOfRangeException("content");
			}

			stream.Write(bytes, 0, bytes.Length);

			return bytes;
		}

		#endregion

		#region Common members

		public void Seek(long offset, SeekOrigin origin)
		{
			if (mode == Mode.Write && offset == 0 && origin == SeekOrigin.End)
			{
				mode = Mode.Append;
				return;
			}

			throw new InvalidOperationException("Seeking is not supported.");
		}

		public void Close()
		{
			if (stream == null)
			{
				return;
			}

			stream.Close();

			if (mode == Mode.Write || mode == Mode.Append)
			{
				item.Parent.MarkDirty();
			}
		}

		public void Dispose()
		{
			if (stream == null)
			{
				return;
			}

			stream.Dispose();

			if (mode == Mode.Write || mode == Mode.Append)
			{
				item.Parent.MarkDirty();
			}
		}

		#endregion
	}

	internal class ContentReaderWriterDynamicParameters
	{
		private FileSystemCmdletProviderEncoding encoding;

		[Parameter]
		public FileSystemCmdletProviderEncoding Encoding
		{
			get
			{
				return encoding == FileSystemCmdletProviderEncoding.Unknown ? FileSystemCmdletProviderEncoding.Byte : encoding;
			}
			set
			{
				encoding = value;
			}
		}
	}
}
