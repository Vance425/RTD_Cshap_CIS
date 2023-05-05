using Microsoft.AspNetCore.Mvc;
using NLog;
using Microsoft.Extensions.Configuration;
using RTDWebAPI.Commons.DataRelated.SQLSentence;
using RTDWebAPI.Commons.Method.Database;
using RTDWebAPI.Commons.Method.WSClient;
using RTDWebAPI.Interface;
using RTDWebAPI.Models;
using RTDWebAPI.Service;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Xml;
using System.IO;
using RTDWebAPI.Commons.Method.Tools;

namespace RTDWebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LoginController : BasicController
    {

        private readonly ILogger<LoginController> _logger;
        private readonly IConfiguration _configuration;
        private readonly DBTool _dbTool;
        IFunctionService _functionService;

        public LoginController(ILogger<LoginController> logger, IConfiguration configuration, DBTool dbTool)
        {
            _logger = logger;
            _configuration = configuration;
            _dbTool = dbTool;
        }

        [HttpPost]
        public APIResult Post([FromBody] UserModel value)
        {
            APIResult foo;
            string tmpMsg = "";
            _functionService = new FunctionService();

            try
            {
                Console.WriteLine(value.Username);

                JCETWebServicesClient jcetWebServiceClient = new JCETWebServicesClient();
                //jcetWebServiceClient.hostname = "127.0.0.1";
                //jcetWebServiceClient.portno = 54350;
                jcetWebServiceClient._url = _configuration["WebService:url"];
                JCETWebServicesClient.ResultMsg resultMsg = new JCETWebServicesClient.ResultMsg();
                resultMsg = jcetWebServiceClient.UserLogin(value.Username, value.Password);
                string result3 = resultMsg.retMessage;
                //<?xml version="1.0" encoding="utf - 8"?><Beans><Status Value="FAILURE" /><ErrMsg Value="SECURITY. % UAF - W - LOGFAIL, user authorization failure, privileges removed." /></Beans>';
                //string test = "<body><head>test header</head></body>";

                if (resultMsg.status)
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(result3);
                    XmlNode xn = xmlDoc.SelectSingleNode("Beans");

                    XmlNodeList xnlA = xn.ChildNodes;
                    String member_valodation = "";
                    String member_validation_message = "";
                    foreach (XmlNode xnA in xnlA)
                    {
                        Console.WriteLine(xnA.Name);
                        if ((xnA.Name) == "Status")
                        {
                            XmlElement xeB = (XmlElement)xnA;
                            if ((xeB.GetAttribute("Value")) == "SUCCESS")
                            {
                                member_valodation = "OK";
                            }
                            else
                            {
                                member_valodation = "NG";
                            }

                        }
                        if ((xnA.Name) == "ErrMsg")
                        {
                            XmlElement xeB = (XmlElement)xnA;
                            member_validation_message = xeB.GetAttribute("Value");
                        }

                        Console.WriteLine(member_valodation);
                    }
                    if (member_valodation == "OK")
                    {
                        tmpMsg = string.Format("User Name: [{0}] Login success.", value.Username);
                        foo = new APIResult()
                        {
                            Success = true,
                            State = "OK",
                            Message = tmpMsg
                        };
                    }
                    else
                    {
                        tmpMsg = string.Format("user login failed. error: {0}", member_validation_message);
                        foo = new APIResult()
                        {
                            Success = false,
                            State = "NG",
                            Message = tmpMsg
                        };
                    }
                }
                else
                {
                    tmpMsg = resultMsg.retMessage;
                    foo = new APIResult()
                    {
                        Success = false,
                        State = "NG",
                        Message = tmpMsg
                    };
                }

            }
            catch (Exception ex)
            {
                tmpMsg = ex.Message;
                foo = new APIResult()
                {
                    Success = false,
                    State = "NG",
                    Message = tmpMsg
                };
            }

            _logger.LogInformation(string.Format("Info:{0}",tmpMsg));

            return foo;
        }
    }
}
