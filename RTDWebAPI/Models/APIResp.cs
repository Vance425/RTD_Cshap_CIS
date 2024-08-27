using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace RTDWebAPI.Models
{
    public class APIResp
    {
        public APIResp(string json)
        {
            JObject jObject = JObject.Parse(json);

            Success = (bool)jObject["Success"];
            State = (string)jObject["State"];
            ErrorCode = (int)jObject["ErrorCode"];
            Message = (string)jObject["Message"];
            Data = jObject["Data"].ToString();
        }

        public bool Success { get; set; }
        public string State { get; set; }
        public int ErrorCode { get; set; }
        public string Message { get; set; }
        public string Data { get; set; }

    }
}