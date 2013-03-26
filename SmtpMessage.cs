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
using System;
using System.Collections.Generic;
using System.Text;

namespace System.Net.Smtp
{
  public class SmtpMessage
  {
    public enum MessageType
    {
      NONE,
      TEXT_ONLY,
      HTML_ONLY,
      TEXT_AND_HTML
    }

    public SmtpHeaderList Headers { get; set; }
    public String BodyText { get; set; }
    public String BodyHtml { get; set; }
    public String Subject { get; set; }
    public String From { get; set; }
    public String To { get; set; }
    public String ReplyTo { get; set; }
    public String ReadReceiptTo { get; set; }
    public MessageType Type { get; set; }
    public SmtpAttachmentList Attachments { get; set; }
    
    public SmtpMessage()
    {
      Headers = new SmtpHeaderList();
      Attachments = new SmtpAttachmentList();
      BodyText = "";
      BodyHtml = "";
      Subject = "";
      From = "";
      To = "";
      ReplyTo = "";
      ReadReceiptTo = "";
      Type = MessageType.NONE;
    }

    public String GenerateMessage()
    {
      StringBuilder m_message = new StringBuilder();
      String m_bound = "";
      String m_bound_mixed = "";

      if (Type == MessageType.NONE)
      {
        throw new SmtpException("Type == MessageType.NONE");
      }

      m_message.AppendLine("To: " + To);
      m_message.AppendLine("From: " + From);
      m_message.AppendLine("Subject: " + Subject);
      m_message.AppendLine("Reply-To: " + (ReplyTo != "" ? ReplyTo : From));
      if (ReadReceiptTo != "") m_message.AppendLine("Disposition-Notification-To: " + ReadReceiptTo);

      foreach (SmtpHeader h in Headers)
      {
        m_message.AppendLine(h.Name + ": " + h.Value);
      }

      m_message.AppendLine("MIME-Version: 1.0");

      if (this.Attachments.Count > 0)
      {
        m_bound = GenerateBound();
        m_bound_mixed = GenerateBound();
        m_message.AppendLine("Content-type: multipart/mixed; boundary=\"" + m_bound_mixed + "\"");
      }
      else if (Type == MessageType.TEXT_ONLY)
      {
        m_message.AppendLine("Content-type: text/plain");
      }
      else if (Type == MessageType.HTML_ONLY)
      {
        m_message.AppendLine("Content-type: text/html");
      }
      else if (Type == MessageType.TEXT_AND_HTML)
      {
        m_bound = GenerateBound();
        m_message.AppendLine("Content-type: multipart/alternative; boundary=\"" + m_bound + "\"");
      }

      m_message.AppendLine();

      if (this.Attachments.Count > 0)
      {
        // open up bound mixed
        m_message.AppendLine("--" + m_bound_mixed);
        m_message.AppendLine("Content-type: multipart/alternative; boundary=\"" + m_bound + "\"");
        m_message.AppendLine();

        m_message.AppendLine("--" + m_bound);

        if (Type == MessageType.TEXT_ONLY)
        {
          m_message.AppendLine("Content-type: text/plain");
          m_message.AppendLine();
          m_message.AppendLine(BodyText);
        }
        else if (Type == MessageType.HTML_ONLY)
        {
          m_message.AppendLine("Content-type: text/html");
          m_message.AppendLine();
          m_message.AppendLine(BodyHtml);
        }
        else if (Type == MessageType.TEXT_AND_HTML)
        {
          m_message.AppendLine("Content-type: text/plain");
          m_message.AppendLine();
          m_message.AppendLine(BodyText);
          m_message.AppendLine();

          m_message.AppendLine("--" + m_bound);
          m_message.AppendLine("Content-type: text/html");
          m_message.AppendLine();
          m_message.AppendLine(BodyHtml);
        }

        m_message.AppendLine();
        m_message.AppendLine("--" + m_bound + "--");
        m_message.AppendLine();

        foreach (SmtpAttachment i in Attachments)
        {
          m_message.AppendLine("--" + m_bound_mixed);
          m_message.AppendLine("Content-Type: " + i.GetMimeType() + "; name=\"" + i.FileNameShort + "\"");
          m_message.AppendLine("Content-Transfer-Encoding: base64");
          m_message.AppendLine("Content-Disposition: attachment; filename=\"" + i.FileNameShort + "\"");
          m_message.AppendLine();
          m_message.AppendLine(i.GetBase64());
          m_message.AppendLine();
        }

        m_message.AppendLine("--" + m_bound_mixed + "--");
      }
      else
      {
        if (Type == MessageType.TEXT_ONLY)
        {
          m_message.AppendLine(BodyText);
        }
        else if (Type == MessageType.HTML_ONLY)
        {
          m_message.AppendLine(BodyHtml);
        }
        else if (Type == MessageType.TEXT_AND_HTML)
        {
          m_message.AppendLine("--" + m_bound);
          m_message.AppendLine("Content-type: text/plain");
          m_message.AppendLine();
          m_message.AppendLine(BodyText);

          m_message.AppendLine("--" + m_bound);
          m_message.AppendLine("Content-type: text/html");
          m_message.AppendLine();
          m_message.AppendLine(BodyHtml);

          m_message.AppendLine("--" + m_bound + "--");
        }
      }

      return m_message.ToString();
    }

    private String GenerateBound()
    {
      StringBuilder m_bld = new StringBuilder();
      StringBuilder m_bldhash = new StringBuilder();
      Random m_rand = new Random((int)DateTime.UtcNow.Ticks);
      for (int x = 0; x < 32; x++)
      {
        m_bld.Append(m_rand.Next());
      }
      byte[] bs = System.Text.Encoding.UTF8.GetBytes(m_bld.ToString());
      System.Security.Cryptography.MD5 m_md5 = System.Security.Cryptography.MD5.Create();
      m_md5.Initialize();
      byte[] hash = m_md5.ComputeHash(bs, 0, bs.Length);

      for (int i = 0; i < hash.Length; i++) m_bldhash.Append(hash[i].ToString("x2"));

      String m_hash = m_bldhash.ToString();

      return m_hash;
    }
  }
}
