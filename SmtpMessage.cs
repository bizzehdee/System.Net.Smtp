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
using System.Collections.Generic;
using System.Text;

namespace System.Net.Smtp
{
	public class SmtpMessage
	{
		private List<SmtpAttachment> _attachments;
		private List<SmtpHeader> _headers;

		public enum MessageType
		{
			None,
			TextOnly,
			HtmlOnly,
			TextAndHtml
		}

		public IEnumerable<SmtpHeader> Headers
		{
			get { return _headers; }
			set { _headers = value as List<SmtpHeader>; }
		}
		public String BodyText { get; set; }
		public String BodyHtml { get; set; }
		public String Subject { get; set; }
		public String From { get; set; }
		public String To { get; set; }
		public String ReplyTo { get; set; }
		public String ReadReceiptTo { get; set; }
		public MessageType Type { get; set; }
		public IEnumerable<SmtpAttachment> Attachments
		{
			get { return _attachments; }
			set { _attachments = value as List<SmtpAttachment>; }
		}
		
		public SmtpMessage()
		{
			_headers = new List<SmtpHeader>();
			_attachments = new List<SmtpAttachment>();
			BodyText = "";
			BodyHtml = "";
			Subject = "";
			From = "";
			To = "";
			ReplyTo = "";
			ReadReceiptTo = "";
			Type = MessageType.None;
		}

		public String GenerateMessage()
		{
			StringBuilder message = new StringBuilder();
			String bound = "";
			String boundMixed = "";

			if (Type == MessageType.None)
			{
				throw new SmtpException("Type == MessageType.NONE");
			}

			message.AppendLine("To: " + To);
			message.AppendLine("From: " + From);
			message.AppendLine("Subject: " + Subject);
			message.AppendLine("Reply-To: " + (ReplyTo != "" ? ReplyTo : From));
			if (ReadReceiptTo != "") message.AppendLine("Disposition-Notification-To: " + ReadReceiptTo);

			foreach (SmtpHeader h in Headers)
			{
				message.AppendLine(h.Name + ": " + h.Value);
			}

			message.AppendLine("MIME-Version: 1.0");

			if (_attachments.Count > 0)
			{
				bound = GenerateBound();
				boundMixed = GenerateBound();
				message.AppendLine("Content-type: multipart/mixed; boundary=\"" + boundMixed + "\"");
			}
			else switch (Type)
			{
				case MessageType.TextOnly:
					message.AppendLine("Content-type: text/plain");
					break;
				case MessageType.HtmlOnly:
					message.AppendLine("Content-type: text/html");
					break;
				case MessageType.TextAndHtml:
					bound = GenerateBound();
					message.AppendLine("Content-type: multipart/alternative; boundary=\"" + bound + "\"");
					break;
			}

			message.AppendLine();

			if (_attachments.Count > 0)
			{
				// open up bound mixed
				message.AppendLine("--" + boundMixed);
				message.AppendLine("Content-type: multipart/alternative; boundary=\"" + bound + "\"");
				message.AppendLine();

				message.AppendLine("--" + bound);

				if (Type == MessageType.TextOnly)
				{
					message.AppendLine("Content-type: text/plain");
					message.AppendLine();
					message.AppendLine(BodyText);
				}
				else if (Type == MessageType.HtmlOnly)
				{
					message.AppendLine("Content-type: text/html");
					message.AppendLine();
					message.AppendLine(BodyHtml);
				}
				else if (Type == MessageType.TextAndHtml)
				{
					message.AppendLine("Content-type: text/plain");
					message.AppendLine();
					message.AppendLine(BodyText);
					message.AppendLine();

					message.AppendLine("--" + bound);
					message.AppendLine("Content-type: text/html");
					message.AppendLine();
					message.AppendLine(BodyHtml);
				}

				message.AppendLine();
				message.AppendLine("--" + bound + "--");
				message.AppendLine();

				foreach (SmtpAttachment i in Attachments)
				{
					message.AppendLine("--" + boundMixed);
					message.AppendLine("Content-Type: " + i.GetMimeType() + "; name=\"" + i.FileNameShort + "\"");
					message.AppendLine("Content-Transfer-Encoding: base64");
					message.AppendLine("Content-Disposition: attachment; filename=\"" + i.FileNameShort + "\"");
					message.AppendLine();
					message.AppendLine(i.GetBase64());
					message.AppendLine();
				}

				message.AppendLine("--" + boundMixed + "--");
			}
			else
			{
				if (Type == MessageType.TextOnly)
				{
					message.AppendLine(BodyText);
				}
				else if (Type == MessageType.HtmlOnly)
				{
					message.AppendLine(BodyHtml);
				}
				else if (Type == MessageType.TextAndHtml)
				{
					message.AppendLine("--" + bound);
					message.AppendLine("Content-type: text/plain");
					message.AppendLine();
					message.AppendLine(BodyText);

					message.AppendLine("--" + bound);
					message.AppendLine("Content-type: text/html");
					message.AppendLine();
					message.AppendLine(BodyHtml);

					message.AppendLine("--" + bound + "--");
				}
			}

			return message.ToString();
		}

		private static String GenerateBound()
		{
			StringBuilder stringBuilder = new StringBuilder();
			StringBuilder stringHash = new StringBuilder();
			Random random = new Random((int)DateTime.UtcNow.Ticks);
			for (int x = 0; x < 32; x++)
			{
				stringBuilder.Append(random.Next());
			}
			byte[] bs = Encoding.UTF8.GetBytes(stringBuilder.ToString());
			System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
			md5.Initialize();
			byte[] hash = md5.ComputeHash(bs, 0, bs.Length);
			for (int i = 0; i < hash.Length; i++) stringHash.Append(hash[i].ToString("x2"));

			return stringHash.ToString();
		}
	}
}
