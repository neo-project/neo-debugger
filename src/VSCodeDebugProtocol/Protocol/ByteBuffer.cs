using System;
using System.Text;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Protocol
{
	internal class ByteBuffer
	{
		private byte[] buffer;

		private Encoding encoding;

		public int Length => buffer.Length;

		public ByteBuffer(Encoding encoding)
		{
			this.encoding = encoding;
			buffer = new byte[0];
		}

		public void AppendData(byte[] newData, int length)
		{
			byte[] dst = new byte[buffer.Length + length];
			Buffer.BlockCopy(buffer, 0, dst, 0, buffer.Length);
			Buffer.BlockCopy(newData, 0, dst, buffer.Length, length);
			buffer = dst;
		}

		public void RemoveData(int byteCount)
		{
			byte[] dst = new byte[buffer.Length - byteCount];
			Buffer.BlockCopy(buffer, byteCount, dst, 0, buffer.Length - byteCount);
			buffer = dst;
		}

		public string PeekString()
		{
			return encoding.GetString(buffer);
		}

		public string PopString(int byteCount)
		{
			byte[] array = new byte[byteCount];
			Buffer.BlockCopy(buffer, 0, array, 0, byteCount);
			byte[] dst = new byte[buffer.Length - byteCount];
			Buffer.BlockCopy(buffer, byteCount, dst, 0, buffer.Length - byteCount);
			buffer = dst;
			return encoding?.GetString(array);
		}
	}
}
