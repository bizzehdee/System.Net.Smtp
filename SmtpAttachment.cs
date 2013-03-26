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
using System.IO;
using System.Text.RegularExpressions;

namespace System.Net.Smtp
{
  public class SmtpAttachment
  {
    public String FileName { get; set; }
    public String FileNameShort { get; set; }

    public SmtpAttachment()
    {
      FileName = "";
    }

    public SmtpAttachment(String file)
    {
      LoadFile(file);
    }

    public void LoadFile(String file)
    {
      FileName = file;
      FileNameShort = FileName.Substring(FileName.LastIndexOf('\\')+1);
    }

    public Int32 GetFileSize()
    {
      if (File.Exists(FileName))
      {
        FileInfo m_info = new FileInfo(FileName);
        return (Int32)m_info.Length;
      }
      return 0;
    }

    public String GetBase64()
    {
      if (File.Exists(FileName))
      {
        byte[] m_bytes = File.ReadAllBytes(FileName);
        String m_file_base64 = Convert.ToBase64String(m_bytes);

        StringBuilder m_full = new StringBuilder();
        Int32 m_len = m_file_base64.Length;
        for (int i = 0; i < (m_len); i+=76)
        {
          m_full.AppendLine(m_file_base64.Substring(i, Math.Min(76, m_len - i)));
        }

        return m_full.ToString();
      }

      return "";
    }

    public String GetMimeType()
    {
      String m_mime = "application/unknown";
      String m_ext = System.IO.Path.GetExtension(FileName).ToLower();

      Microsoft.Win32.RegistryKey regKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(m_ext);
      if (regKey != null && regKey.GetValue("Content Type") != null)
      {
        m_mime = regKey.GetValue("Content Type").ToString();
      }
      return m_mime;
    }

  }
}
