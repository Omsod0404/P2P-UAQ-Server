﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P2P_UAQ_Server.Models
{
    public enum MessageType
    {
        UserConnected = 1,
        UserDisconnected = 2,
        Message,
        ChatRequest,
        ChatCloseRequest,
        ChatMessage,
        File,
		UsernameInUse,
    }
}
