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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Provider;
using System.Text;
using FluentFTP;

namespace PsFtpProvider
{
	internal abstract class ContentReaderWriterBase
	{
		protected const int ByteBufferSize = 4096;

		protected CacheNode Item { get; }

		protected FtpClient Client { get; }

		protected Encoding Encoding { get; }

		protected Stream Stream { get; set; }

		public ContentReaderWriterBase(CacheNode item, ContentReaderWriterDynamicParametersBase parameters, FtpClient client)
		{
			Item = item;

			Client = client;

			var encoding = parameters?.Encoding ?? FileSystemCmdletProviderEncoding.Byte;

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
		private readonly Decoder decoder;
		private readonly bool raw;

		public ContentReader(CacheNode item, ContentReaderDynamicParameters parameters, FtpClient client)
			: base(item, parameters, client)
		{
			decoder = Encoding?.GetDecoder();
			raw = parameters?.Raw ?? false;
		}

		public IList Read(long readCount)
		{
			if (Stream == null)
			{
				Stream = Client.OpenRead(Item.Item.FullName, FtpDataType.Binary);
			}

			if (decoder == null)
			{
				if (raw)
				{
					var bytes = ReadRawBytes();
					if (bytes.Length == 0)
					{
						return new byte[] { };
					}

					return new[] { bytes };
				}

				if (readCount == 1)
				{
					var b = ReadByte();
					if (b == null)
					{
						return new byte[0];
					}

					return new byte[] { (byte)b };
				}

				return ReadBytes(readCount);
			}
			else
			{
				string result;

				if (raw)
				{
					result = ReadRawString();
				}
				else
				{
					result = ReadString();
				}

				if (result == null)
				{
					return new string[0];
				}

				return new[] { result };
			}
		}

		private byte[] ReadRawBytes()
		{
			var result = new List<byte[]>();

			var buffer = new byte[ByteBufferSize];

			for (; ; )
			{
				var read = Stream.Read(buffer, 0, buffer.Length);

				if (read == 0)
				{
					break;
				}

				var copy = new byte[read];
				Array.Copy(buffer, copy, copy.Length);
				result.Add(copy);
			}

			var totalBytes = result.Sum(bytes => bytes.Length);
			var resultArray = new byte[totalBytes];

			var index = 0;
			foreach (var bytes in result)
			{
				Buffer.BlockCopy(bytes, 0, resultArray, index, bytes.Length);
				index += bytes.Length;
			}

			return resultArray;
		}

		private byte? ReadByte()
		{
			var b = Stream.ReadByte();

			if (b == -1)
			{
				return null;
			}

			return (byte)b;
		}

		private byte[] ReadBytes(long readCount)
		{
			if (readCount <= 0)
			{
				readCount = long.MaxValue;
			}

			if (readCount > ByteBufferSize)
			{
				readCount = ByteBufferSize;
			}

			var buffer = new byte[(int)readCount];

			var read = Stream.Read(buffer, 0, buffer.Length);

			if (read == buffer.Length)
			{
				return buffer;
			}

			var slice = new byte[read];

			Buffer.BlockCopy(buffer, 0, slice, 0, read);

			return slice;
		}

		private string ReadRawString()
		{
			var result = new StringBuilder();

			var buffer = new byte[ByteBufferSize];

			for (; ; )
			{
				var read = Stream.Read(buffer, 0, buffer.Length);

				var numChars = decoder.GetCharCount(buffer, 0, read, read == 0);
				if (read == 0 && numChars == 0)
				{
					break;
				}

				var chars = new char[numChars];
				decoder.GetChars(buffer, 0, read, chars, 0, read == 0);
				result.Append(chars);
			}

			var resultString = result.ToString();

			if (resultString == "")
			{
				return null;
			}

			return resultString;
		}

		private string ReadString()
		{
			var result = new StringBuilder();

			var buffer = new byte[ByteBufferSize];

			for (; ; )
			{
				for (var i = 0; i < buffer.Length; i++)
				{
					var b = Stream.ReadByte();

					if (b == -1 && i == 0 && result.Length == 0)
					{
						return null;
					}

					if (b == '\r' || b == '\n' || b == -1)
					{
						var numChars = decoder.GetCharCount(buffer, 0, i, true);
						var chars = new char[numChars];
						decoder.GetChars(buffer, 0, i, chars, 0, true);
						result.Append(chars);

						return result.ToString();
					}

					buffer[i] = (byte)b;
				}

				// Ran out of buffer space. Put whatever we got so far into result, leave half-read characters in decoder, and restart reading into the buffer from index 0
				{
					var numChars = decoder.GetCharCount(buffer, 0, buffer.Length, false);
					var chars = new char[numChars];
					decoder.GetChars(buffer, 0, buffer.Length, chars, 0, false);
					result.Append(chars);
				}
			}
		}
	}

	internal class ContentWriter : ContentReaderWriterBase, IContentWriter
	{
		private enum Mode { Write, Append }

		private Mode mode = Mode.Write;

		private Encoder encoder;

		public ContentWriter(CacheNode item, ContentWriterDynamicParameters parameters, FtpClient client)
			: base(item, parameters, client)
		{
			encoder = Encoding?.GetEncoder();
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
						throw new InvalidOperationException($"Unknown mode { mode }");
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

			if (content[0] is string)
			{
				if (encoder == null)
				{
					encoder = Encoding.UTF8.GetEncoder();
				}

				foreach (string str in content)
				{
					var chars = (str + "\n").ToCharArray();
					var numBytes = encoder.GetByteCount(chars, 0, chars.Length, false);

					var bytes = new byte[Math.Min(numBytes, ByteBufferSize)];
					var convertedChars = 0;

					var completed = false;
					while (!completed)
					{
						int charsUsed;
						int bytesUsed;

						encoder.Convert(chars, convertedChars, chars.Length - convertedChars, bytes, 0, bytes.Length, false, out charsUsed, out bytesUsed, out completed);
						convertedChars += charsUsed;

						Stream.Write(bytes, 0, bytesUsed);
					}
				}
			}
			else if (content[0] is byte)
			{
				var bytes = content as byte[] ?? content.Cast<byte>().ToArray();

				var bytesWritten = 0;
				while (bytesWritten < bytes.Length)
				{
					var written = Math.Min(bytes.Length - bytesWritten, ByteBufferSize);
					Stream.Write(bytes, bytesWritten, written);
					bytesWritten += written;
				}
			}
			else
			{
				throw new ArgumentOutOfRangeException(nameof(content));
			}

			return content;
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

	internal abstract class ContentReaderWriterDynamicParametersBase
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

	internal class ContentReaderDynamicParameters : ContentReaderWriterDynamicParametersBase
	{
		[Parameter]
		public SwitchParameter Raw { get; set; }
	}

	internal class ContentWriterDynamicParameters : ContentReaderWriterDynamicParametersBase
	{
	}
}
