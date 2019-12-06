using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Protocol
{
	internal class RequestResponder : IRequestResponder
	{
		private DebugProtocol protocol;

		private string command;

		private int seq;

		private bool isClosed;

		public object Arguments
		{
			get;
			private set;
		}

		public string Command => command;

		internal RequestResponder(DebugProtocol protocol, string command, int seq, object args)
		{
			this.protocol = protocol;
			this.command = command;
			this.seq = seq;
			Arguments = args;
		}

		private void Close()
		{
			if (isClosed)
			{
				throw new InvalidOperationException("Cannot modify a RequestResponder once the response has been sent.");
			}
			isClosed = true;
		}

		public void SetError(ProtocolException exception)
		{
			ProtocolResponse protocolResponse = new ProtocolResponse(seq, command);
			protocolResponse.Success = false;
			protocolResponse.Body = new ErrorResponse(exception);
			protocolResponse.Message = exception.Message;
			protocol.SendMessageCore(protocolResponse);
			Close();
		}

		public void SetResponse(ResponseBody response)
		{
			ProtocolResponse protocolResponse = new ProtocolResponse(seq, command);
			protocolResponse.Success = true;
			protocolResponse.Body = response;
			protocol.SendMessageCore(protocolResponse);
			Close();
		}
	}
	internal class RequestResponder<TArgs> : RequestResponder, IRequestResponder<TArgs>, IRequestResponder where TArgs : class, new()
	{
		TArgs IRequestResponder<TArgs>.Arguments => (TArgs)base.Arguments;

		internal RequestResponder(DebugProtocol protocol, string command, int seq, TArgs args)
			: base(protocol, command, seq, args)
		{
		}
	}
	internal class RequestResponder<TArgs, TResponse> : RequestResponder<TArgs>, IRequestResponder<TArgs, TResponse>, IRequestResponder<TArgs>, IRequestResponder where TArgs : class, new()where TResponse : ResponseBody
	{
		internal RequestResponder(DebugProtocol protocol, string command, int seq, TArgs args)
			: base(protocol, command, seq, args)
		{
		}

		void IRequestResponder<TArgs, TResponse>.SetResponse(TResponse response)
		{
			SetResponse(response);
		}
	}
}
