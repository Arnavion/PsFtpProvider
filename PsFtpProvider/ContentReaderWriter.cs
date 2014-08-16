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
using System.Text;

namespace PsFtpProvider
{
	internal class ContentReaderWriter : IContentReader, IContentWriter
	{
		public enum Mode { Read, Write }

		private Mode mode;

		private CacheDirectoryNode parent;

		private Stream stream;

		private Encoding encoding;

		public ContentReaderWriter(Stream stream, Mode mode, ContentReaderWriterDynamicParameters parameters, CacheDirectoryNode parent)
		{
			this.parent = parent;

			this.mode = mode;

			this.stream = stream;

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
			stream.SetLength(0);
		}

		#region IContentReader members

		public IList Read(long readCount)
		{
			var buffer = new byte[4096];

			if (readCount <= 0 || encoding != null)
			{
				readCount = long.MaxValue;
			}

			var read = stream.Read(buffer, 0, (int)Math.Min(readCount, buffer.Length));
			var result = new byte[read];
			Array.Copy(buffer, result, read);

			if (read == 0)
			{
				return result;
			}

			if (encoding == null || result.Length == 0)
			{
				return result;
			}
			else
			{
				return new[] { encoding.GetString(result) };
			}
		}

		#endregion

		#region IContentWriter members

		public IList Write(IList content)
		{
			if (content.Count <= 0)
			{
				return content;
			}

			byte[] bytes;
			if (content[0] is string && content.Count == 1)
			{
				if (encoding == null)
				{
					encoding = Encoding.UTF8;
				}

				bytes = encoding.GetBytes((string)content[0]);
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

		public void Close()
		{
			stream.Close();
		}

		public void Seek(long offset, SeekOrigin origin)
		{
			stream.Seek(offset, origin);
		}

		public void Dispose()
		{
			stream.Dispose();

			if (mode == Mode.Write)
			{
				parent.MarkDirty();
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
