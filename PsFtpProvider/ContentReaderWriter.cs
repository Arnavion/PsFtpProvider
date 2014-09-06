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
	internal abstract class ContentReaderWriterBase
	{
		protected CacheNode Item { get; private set; }

		protected FtpClient Client { get; private set; }

		protected Encoding Encoding { get; set; }

		protected Stream Stream { get; set; }

		public ContentReaderWriterBase(CacheNode item, ContentReaderWriterDynamicParameters parameters, FtpClient client)
		{
			Item = item;

			Client = client;

			var encoding = parameters != null ? parameters.Encoding : FileSystemCmdletProviderEncoding.Byte;

			if (encoding != FileSystemCmdletProviderEncoding.Byte)
			{
				Encoding = new FileSystemContentWriterDynamicParameters() { Encoding = encoding }.EncodingType;
			}
		}

		public virtual void Seek(long offset, SeekOrigin origin)
		{
			throw new InvalidOperationException("Seeking is not supported.");
		}

		public virtual void Close()
		{
			if (Stream == null)
			{
				return;
			}

			Stream.Close();
		}

		public virtual void Dispose()
		{
			if (Stream == null)
			{
				return;
			}

			Stream.Dispose();
		}
	}

	internal class ContentReader : ContentReaderWriterBase, IContentReader
	{
		public ContentReader(CacheNode item, ContentReaderWriterDynamicParameters parameters, FtpClient client)
			: base(item, parameters, client)
		{
		}

		public IList Read(long readCount)
		{
			if (Stream == null)
			{
				Stream = Client.OpenRead(Item.Item.FullName, FtpDataType.Binary);
			}

			var buffer = new byte[4096];

			if (readCount <= 0 || Encoding != null)
			{
				readCount = long.MaxValue;
			}

			var read = Stream.Read(buffer, 0, (int)Math.Min(readCount, buffer.Length));
			var result = new byte[read];

			if (read == 0)
			{
				return result;
			}

			Array.Copy(buffer, result, read);

			if (Encoding == null)
			{
				return result;
			}

			return new[] { Encoding.GetString(result) };
		}
	}

	internal class ContentWriter : ContentReaderWriterBase, IContentWriter
	{
		private enum Mode { Write, Append }

		private Mode mode = Mode.Write;

		public ContentWriter(CacheNode item, ContentReaderWriterDynamicParameters parameters, FtpClient client)
			: base(item, parameters, client)
		{
		}

		public IList Write(IList content)
		{
			if (Stream == null)
			{
				switch (mode)
				{
					case Mode.Write:
						Stream = Client.OpenWrite(Item.Item.FullName, FtpDataType.Binary);
						break;

					case Mode.Append:
						Stream = Client.OpenAppend(Item.Item.FullName, FtpDataType.Binary);
						break;

					default:
						throw new InvalidOperationException(string.Format("Unknown mode {0}", mode));
				}
			}

			if (content.Count <= 0)
			{
				return content;
			}

			if (content[0] is PSObject)
			{
				content = content.Cast<PSObject>().Select(obj => obj.BaseObject).ToArray();
			}

			byte[] bytes;

			if (content[0] is string)
			{
				if (Encoding == null)
				{
					Encoding = Encoding.UTF8;
				}

				bytes = content.Cast<string>().SelectMany(str => Encoding.GetBytes(str + "\n")).ToArray();
			}
			else if (content[0] is byte)
			{
				bytes = content.Cast<byte>().ToArray();
			}
			else
			{
				throw new ArgumentOutOfRangeException("content");
			}

			Stream.Write(bytes, 0, bytes.Length);

			return bytes;
		}

		public void Truncate()
		{
			if (Stream == null)
			{
				Stream = Client.OpenWrite(Item.Item.FullName, FtpDataType.Binary);
			}

			Stream.SetLength(0);
		}

		public override void Seek(long offset, SeekOrigin origin)
		{
			if (mode == Mode.Write && offset == 0 && origin == SeekOrigin.End)
			{
				mode = Mode.Append;
				return;
			}

			base.Seek(offset, origin);
		}

		public override void Close()
		{
			base.Close();

			if (Stream == null)
			{
				return;
			}

			if (mode == Mode.Write || mode == Mode.Append)
			{
				Item.Parent.MarkDirty();
			}
		}

		public override void Dispose()
		{
			base.Dispose();

			if (Stream == null)
			{
				return;
			}

			if (mode == Mode.Write || mode == Mode.Append)
			{
				Item.Parent.MarkDirty();
			}
		}
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
