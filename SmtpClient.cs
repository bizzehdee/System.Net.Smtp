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
    internal TcpClient m_client;
    internal bool m_use_ssl = false;
    internal SslStream m_ssl_stream = null;
    internal NetworkStream m_network_stream = null;
    internal Stream m_stream = null;
    internal List<int> m_msg_queue;

    public SmtpClient()
    {
      m_client = new TcpClient();
      m_msg_queue = new List<int>();
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
      String m_response = "";
      m_use_ssl = ssl;

      m_client.Connect(server, port);

      m_network_stream = m_client.GetStream();

      if (m_use_ssl)
      {
        m_ssl_stream = new SslStream((Stream)m_network_stream, true);

        try
        {
          m_ssl_stream.AuthenticateAsClient(server);
        }
        catch (Exception ex)
        {
          throw new SmtpException(ex.Message + ". " + "If your using gmail, make sure to use port 465");
        }

        m_stream = (Stream)m_ssl_stream;
      }
      else
      {
        m_stream = m_network_stream;
      }

      m_response = this.Response();

      if (m_response.Substring(0, 3) != "220")
      {
        throw new SmtpException(m_response);
      }
    }

    /// <summary>
    /// Send quit message
    /// </summary>
    public void Quit()
    {
      String m_response = "";

      while (m_msg_queue.Count > 0) Threading.Thread.SpinWait(100);

      this.Write("QUIT");

      m_response = this.Response();

      if (m_response.Substring(0, 3) != "221")
      {
        throw new SmtpException(m_response);
      }

      m_stream.Close();
      m_client.Close();
    }

    /// <summary>
    /// greet server after connect
    /// </summary>
    /// <param name="ehlo">Send EHLO instead of HELO</param>
    public void SayHelo(bool ehlo)
    {
      String m_response = "";
      String m_me = System.Environment.MachineName;

      if (ehlo)
      {
        this.Write("EHLO " + m_me);
      }
      else
      {
        this.Write("HELO " + m_me);
      }

      m_response = this.Response();

      if (m_response.Substring(0, 3) != "250")
      {
        throw new SmtpException(m_response);
      }
    }


    public void SendAuthLogin(String username, String password)
    {
      String m_response = "";
      String m_base64_username = Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes(username));
      String m_base64_password = Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes(password));

      this.Write("AUTH LOGIN");

      m_response = this.Response();

      if (m_response.Substring(0, 3) != "334")
      {
        throw new SmtpException(m_response);
      }

      this.Write(m_base64_username);

      m_response = this.Response();

      if (m_response.Substring(0, 3) != "334")
      {
        throw new SmtpException(m_response);
      }

      this.Write(m_base64_password);

      m_response = this.Response();

      if (m_response.Substring(0, 3) != "235")
      {
        throw new SmtpException(m_response);
      }
    }

    public void SendMailFrom(String from)
    {
      String m_response = "";

      this.Write("MAIL FROM: " + from);

      m_response = this.Response();

      if (m_response.Substring(0, 3) != "250")
      {
        throw new SmtpException(m_response);
      }
    }

    public void SendRcptTo(String to)
    {
      String m_response = "";

      this.Write("RCPT TO: " + to);

      m_response = this.Response();

      if (m_response.Substring(0, 3) != "250")
      {
        throw new SmtpException(m_response);
      }
    }

    public void SendData(String data)
    {
      String m_response = "";

      this.Write("DATA");

      m_response = this.Response();

      if (m_response.Substring(0, 3) != "354")
      {
        throw new SmtpException(m_response);
      }

      this.Write(data);

      this.Write("\r\n.\r\n");

      m_response = this.Response();

      if (m_response.Substring(0, 3) != "250")
      {
        throw new SmtpException(m_response);
      }
    }

    public void SendMessage(SmtpMessage msg)
    {
      m_msg_queue.Add(1);
      this.SendMailFrom(msg.From);
      this.SendRcptTo(msg.To);
      this.SendData(msg.GenerateMessage());
      m_msg_queue.RemoveAt(0);
    }

    public void SendMessageAsync(SmtpMessage msg, AsyncCallback cb)
    {
      m_msg_queue.Add(1);
      SmtpClientSendMessageAsyncResult m_obj = new SmtpClientSendMessageAsyncResult();
      m_obj.cb = cb;
      m_obj.Message = msg;

      Threading.Thread m_thread = new Threading.Thread(SendMessageAsync_Thread);
      m_thread.Start(m_obj);
    }

    internal void SendMessageAsync_Thread(Object obj)
    {
      SmtpClientSendMessageAsyncResult m_obj = (SmtpClientSendMessageAsyncResult)obj;

      SmtpMessage msg = m_obj.Message;

      this.SendMailFrom(msg.From);
      this.SendRcptTo(msg.To);
      this.SendData(msg.GenerateMessage());

      if(m_obj.cb != null) m_obj.cb.Invoke(m_obj);

      m_msg_queue.RemoveAt(0);
    }

    #region Internals

    internal void Write(String str)
    {
      ASCIIEncoding m_enc = new ASCIIEncoding();
      byte[] m_buf = new byte[1024];

      if (!str.EndsWith("\r\n")) str += "\r\n";

      m_buf = m_enc.GetBytes(str);

      m_stream.Write(m_buf, 0, m_buf.Length);
    }

    internal string Response()
    {
      String m_ret = "";
      ASCIIEncoding m_enc = new ASCIIEncoding();
      byte[] m_buf = new byte[1024];
      int m_count = 0;

      while (true)
      {
        byte[] m_rbuf = new byte[2];
        int m_bytes = m_stream.Read(m_rbuf, 0, 1);
        if (m_bytes == 1)
        {
          m_buf[m_count] = m_rbuf[0];
          m_count++;

          if (m_rbuf[0] == '\n')
          {
            break;
          }
        }
        else
        {
          break;
        }
      }

      m_ret = m_enc.GetString(m_buf, 0, m_count);

      return m_ret;
    }

    #endregion
  }
}
