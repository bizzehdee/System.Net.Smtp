/*
 * Copyright (c) 2011, Darren Horrocks
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without modification, 
 * are permitted provided that the following conditions are met:
 * 
 * Redistributions of source code must retain the above copyright notice, this list 
 * of conditions and the following disclaimer.
 * Redistributions in binary form must reproduce the above copyright notice, this 
 * list of conditions and the following disclaimer in the documentation and/or 
 * other materials provided with the distribution.
 * Neither the name of Darren Horrocks/www.bizzeh.com nor the names of its 
 * contributors may be used to endorse or promote products derived from this software 
 * without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY 
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES 
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT 
 * SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, 
 * INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, 
 * STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF 
 * THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System.Text;
using System.Net.Sockets;
using System.Net.Security;
using System.IO;
using System.Collections.Generic;

namespace System.Net.Smtp
{
	public class SmtpClient
	{
		private readonly TcpClient _client;
		private bool _useSSL;
		private SslStream _sslStream;
		private NetworkStream _networkStream;
		private Stream _stream;
		private readonly List<int> _messageQueue;

		public SmtpClient()
		{
			_client = new TcpClient();
			_messageQueue = new List<int>();
		}

		/// <summary>
		/// Connect to SMTP server without SSL on port 25
		/// </summary>
		/// <param name="server">smtp server to connect to</param>
		public void Connect(String server)
		{
			Connect(server, 25, false);
		}

		/// <summary>
		/// Connect to SMTP server without SSL on specified port
		/// </summary>
		/// <param name="server">smtp server to connect to</param>
		/// <param name="port">port number (usually 25)</param>
		public void Connect(String server, int port)
		{
			Connect(server, port, false);
		}

		/// <summary>
		/// Connect to SMTP server
		/// </summary>
		/// <param name="server">smtp server to connect to</param>
		/// <param name="port">port number (usually 25)</param>
		/// <param name="ssl">Use SSL</param>
		public void Connect(String server, int port, bool ssl)
		{
			_useSSL = ssl;

			_client.Connect(server, port);

			_networkStream = _client.GetStream();

			if (_useSSL)
			{
				_sslStream = new SslStream(_networkStream, true);

				try
				{
					_sslStream.AuthenticateAsClient(server);
				}
				catch (Exception ex)
				{
					throw new SmtpException(ex.Message + ". " + "If your using gmail, make sure to use port 465");
				}

				_stream = _sslStream;
			}
			else
			{
				_stream = _networkStream;
			}

			string response = Response();

			if (response.Substring(0, 3) != "220")
			{
				throw new SmtpException(response);
			}
		}

		/// <summary>
		/// Send quit message
		/// </summary>
		public void Quit()
		{
			while (_messageQueue.Count > 0) Threading.Thread.SpinWait(100);

			Write("QUIT");

			string response = Response();

			if (response.Substring(0, 3) != "221")
			{
				throw new SmtpException(response);
			}

			_stream.Close();
			_client.Close();
		}

		/// <summary>
		/// greet server after connect
		/// </summary>
		/// <param name="ehlo">Send EHLO instead of HELO</param>
		public void SayHelo(bool ehlo)
		{
			String machineName = Environment.MachineName;

			if (ehlo)
			{
				Write("EHLO " + machineName);
			}
			else
			{
				Write("HELO " + machineName);
			}

			string response = Response();

			if (response.Substring(0, 3) != "250")
			{
				throw new SmtpException(response);
			}
		}


		public void SendAuthLogin(String username, String password)
		{
			String base64Username = Convert.ToBase64String(Encoding.ASCII.GetBytes(username));
			String base64Password = Convert.ToBase64String(Encoding.ASCII.GetBytes(password));

			Write("AUTH LOGIN");

			string response = Response();

			if (response.Substring(0, 3) != "334")
			{
				throw new SmtpException(response);
			}

			Write(base64Username);

			response = Response();

			if (response.Substring(0, 3) != "334")
			{
				throw new SmtpException(response);
			}

			Write(base64Password);

			response = Response();

			if (response.Substring(0, 3) != "235")
			{
				throw new SmtpException(response);
			}
		}

		public void SendMailFrom(String from)
		{
			Write("MAIL FROM: " + from);

			string response = Response();

			if (response.Substring(0, 3) != "250")
			{
				throw new SmtpException(response);
			}
		}

		public void SendRcptTo(String to)
		{
			Write("RCPT TO: " + to);

			string response = Response();

			if (response.Substring(0, 3) != "250")
			{
				throw new SmtpException(response);
			}
		}

		public void SendData(String data)
		{
			Write("DATA");

			string response = Response();

			if (response.Substring(0, 3) != "354")
			{
				throw new SmtpException(response);
			}

			Write(data);

			Write("\r\n.\r\n");

			response = Response();

			if (response.Substring(0, 3) != "250")
			{
				throw new SmtpException(response);
			}
		}

		public void SendMessage(SmtpMessage msg)
		{
			_messageQueue.Add(1);
			SendMailFrom(msg.From);
			SendRcptTo(msg.To);
			SendData(msg.GenerateMessage());
			_messageQueue.RemoveAt(0);
		}

		public void SendMessageAsync(SmtpMessage msg, AsyncCallback cb)
		{
			_messageQueue.Add(1);
			SmtpClientSendMessageAsyncResult messageAsync = new SmtpClientSendMessageAsyncResult { CB = cb, Message = msg };

			Threading.Thread thread = new Threading.Thread(SendMessageAsync_Thread);
			thread.Start(messageAsync);
		}

		private void SendMessageAsync_Thread(Object obj)
		{
			SmtpClientSendMessageAsyncResult messageAsync = (SmtpClientSendMessageAsyncResult)obj;

			SmtpMessage msg = messageAsync.Message;

			SendMailFrom(msg.From);
			SendRcptTo(msg.To);
			SendData(msg.GenerateMessage());

			if(messageAsync.CB != null) messageAsync.CB.Invoke(messageAsync);

			_messageQueue.RemoveAt(0);
		}

		#region Internals

		private void Write(String str)
		{
			ASCIIEncoding encoding = new ASCIIEncoding();

			if (!str.EndsWith("\r\n")) str += "\r\n";

			byte[] bufferBytes = encoding.GetBytes(str);

			_stream.Write(bufferBytes, 0, bufferBytes.Length);
		}

		private string Response()
		{
			ASCIIEncoding encoding = new ASCIIEncoding();
			byte[] bufferBytes = new byte[1024];
			int count = 0;

			while (true)
			{
				byte[] receiveBuffer = new byte[2];
				int byteCount = _stream.Read(receiveBuffer, 0, 1);
				if (byteCount == 1)
				{
					bufferBytes[count] = receiveBuffer[0];
					count++;

					if (receiveBuffer[0] == '\n')
					{
						break;
					}
				}
				else
				{
					break;
				}
			}

			return encoding.GetString(bufferBytes, 0, count);
		}

		#endregion
	}
}
