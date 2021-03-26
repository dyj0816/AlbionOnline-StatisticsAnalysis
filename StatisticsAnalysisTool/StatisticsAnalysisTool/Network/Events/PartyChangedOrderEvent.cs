﻿using Albion.Network;
using StatisticsAnalysisTool.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace StatisticsAnalysisTool.Network.Handler
{
    public class PartyChangedOrderEvent : BaseEvent
    {
        public PartyChangedOrderEvent(Dictionary<byte, object> parameters) : base(parameters)
        {
            try
            {
                if (parameters.ContainsKey(1))
                {
                    UserGuid = parameters[1].ObjectToGuid();
                }

                if (parameters.ContainsKey(2))
                {
                    Username = string.IsNullOrEmpty(parameters[2].ToString()) ? string.Empty : parameters[2].ToString();
                }
            }
            catch(Exception e)
            {
                Debug.Print(e.Message);
            }
        }

        public Guid? UserGuid;
        public string Username;
    }
}