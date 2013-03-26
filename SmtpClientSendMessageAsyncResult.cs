using System;
using System.Collections.Generic;
using System.Text;

namespace System.Net.Smtp
{
  internal partial class SmtpClientSendMessageAsyncResult : IAsyncResult
  {
    public SmtpMessage Message { get; set; }
    internal AsyncCallback cb { get; set; }

    public object AsyncState
    {
      get { throw new NotImplementedException(); }
    }

    public Threading.WaitHandle AsyncWaitHandle
    {
      get { throw new NotImplementedException(); }
    }

    public bool CompletedSynchronously
    {
      get { throw new NotImplementedException(); }
    }

    public bool IsCompleted
    {
      get { throw new NotImplementedException(); }
    }
  }
}
