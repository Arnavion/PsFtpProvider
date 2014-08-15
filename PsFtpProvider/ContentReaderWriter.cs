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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation.Provider;
using System.Text;

namespace PsFtpProvider
{
	internal class ContentReaderWriter : IContentReader, IContentWriter
	{
		public enum Mode { Read, Write }

		private Stream stream;

		public ContentReaderWriter(FtpDriveInfo drive, string path, Mode mode)
		{
			switch (mode)
			{
				case Mode.Read: stream = drive.OpenRead(path); break;
				case Mode.Write: stream = drive.OpenWrite(path); break;
				default: throw new ArgumentOutOfRangeException("mode");
			}
		}

		public void Truncate()
		{
			stream.SetLength(0);
		}

		#region IContentReader members

		public IList Read(long readCount)
		{
			var result = new List<byte[]>();

			var buffer = new byte[4096];

			if (readCount <= 0)
			{
				readCount = long.MaxValue;
			}

			while (readCount > 0)
			{
				var read = stream.Read(buffer, 0, (int)Math.Min(readCount, buffer.Length));
				if (read == 0)
				{
					break;
				}

				var copy = new byte[read];
				Array.Copy(buffer, copy, read);
				result.Add(copy);

				readCount -= read;
			}

			return result.SelectMany(bytes => bytes).ToArray();
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
				bytes = Encoding.UTF8.GetBytes((string)content[0]);
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
		}

		#endregion
	}
}
