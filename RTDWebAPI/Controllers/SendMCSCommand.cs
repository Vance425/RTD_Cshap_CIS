using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using NLog;
using RTDWebAPI.Commons.Method.Database;
using RTDWebAPI.Interface;
using RTDWebAPI.Models;
using RTDWebAPI.Service;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace RTDWebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SendMCSCommand : BasicController
    {
        private readonly IFunctionService _functionService;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly List<DBTool> _lstDBSession;
        private readonly DBTool _dbTool;
        private readonly ConcurrentQueue<EventQueue> _eventQueue;

        public SendMCSCommand(List<DBTool> lstDBSession, IConfiguration configuration, ILogger logger, IFunctionService functionService, ConcurrentQueue<EventQueue> eventQueue)
        {
            _dbTool = (DBTool)lstDBSession[0];
            _logger = logger;
            _configuration = configuration;
            _functionService = functionService;
            _eventQueue = eventQueue;
        }

        [HttpPost]
        public APIResult Post([FromBody] FuncNo value)
        {
            APIResult foo = new APIResult();
            IBaseDataService _BaseDataService = new BaseDataService();

            string tmpMsg = "";
            bool bResult = false;
            List<string> args = new();
            DataTable dt = null;

            string funcName = "";
            EventQueue _eventQ = new EventQueue();

            try
            {
                args = new();

                switch (value.FunctionNo)
                {
                    case "1":
                        args.Add(value.KeyCode);//("EOTD");
                        foo = _functionService.SentCommandtoMCSByModel(_dbTool, _configuration, _logger, "GetDeviceInfo", args);

                        string _portID = "";
                        string _locate = "";
                        int _slotno = 0;
                        string _carrierID = "";
                        string _sql = "";
                        string _carrier = "";

                        CarrierLocationUpdate oclu = new CarrierLocationUpdate();

                        foreach (var key in foo.Data)
                        {
                            _portID = key.Key;
                            _locate = _portID.Split("_LP")[0].ToString();
                            _slotno = int.Parse(_portID.Split("_LP")[1].ToString());
                            _carrierID = key.Value.ToString().Trim();

                            _carrier = "";

                            if(_carrierID.Equals(""))
                            {
                                //carrierID is empty
                                _sql = _BaseDataService.GetCarrierByLocate(_locate, _slotno);
                                dt = _dbTool.GetDataTable(_sql);

                                if (dt.Rows.Count > 0)
                                {
                                    _carrier = dt.Rows[0]["carrier_id"].ToString();

                                    oclu = new CarrierLocationUpdate();
                                    oclu.CarrierID = _carrier;
                                    oclu.Location = "";
                                    oclu.LocationType = "";
                                    //do locate update date
                                    funcName = "CarrierLocationUpdate";
                                    _eventQ.EventName = funcName;

                                    _eventQ.EventObject = oclu;
                                    _eventQueue.Enqueue(_eventQ);
                                }
                            }
                            else
                            {
                                //carrierID is not empty
                                _sql = _BaseDataService.SelectTableCarrierTransferByCarrier(_carrierID);
                                dt = _dbTool.GetDataTable(_sql);

                                if (dt.Rows.Count > 0)
                                {
                                    _carrier = dt.Rows[0]["carrier_id"].ToString();

                                    oclu = new CarrierLocationUpdate();
                                    //do locate update date
                                    funcName = "CarrierLocationUpdate";
                                    _eventQ.EventName = funcName;

                                    _eventQ.EventObject = oclu;
                                    _eventQueue.Enqueue(_eventQ);
                                }
                            }
                        }

                        break;
                    case "2":
                        args.Add(value.KeyCode);//("EOTD");
                        foo = _functionService.SentCommandtoMCSByModel(_dbTool, _configuration, _logger, "GetCommandList", args);

                        string _commandList  = "";
                        string _workinprocessCmd = "";
                        string _Key = "";
                        string _CommandID = "";


                        CommandList ocmdList = new CommandList();

                        if (foo.Success)
                        {
                            foreach (var key in foo.Data)
                            {
                                _Key = key.Key;
                                _CommandID = key.Value.ToString().Trim();

                                if (!_CommandID.Equals(""))
                                {
                                    //commands
                                    _sql = _BaseDataService.SelectTableWorkInProcessSchByCmdId(_CommandID, "workinprocess_sch");
                                    dt = _dbTool.GetDataTable(_sql);

                                    if (dt.Rows.Count > 0)
                                    {
                                        //Do Nothing
                                    }
                                    else
                                    {

                                        if (_commandList.Equals(""))
                                            _commandList = dt.Rows[0]["cmd_id"].ToString();
                                        else
                                        {
                                            _workinprocessCmd = dt.Rows[0]["cmd_id"].ToString();
                                            _commandList = string.Format("{0},{1}", _commandList, _workinprocessCmd);
                                        }
                                        //do delete workinprocess_sch command
                                        funcName = "GetCommandList";
                                        //_eventQ.EventName = funcName;

                                        //_eventQ.EventObject = ocmdList;
                                        //_eventQueue.Enqueue(_eventQ);
                                    }
                                }
                            }
                        }

                        tmpMsg = _commandList;

                        break;
                    default:
                        break;
                }

                //bResult = _functionService.SentTransferCommandtoToMCS(_dbTool, _configuration, _logger, value, out tmpMsg);
            }
            catch(Exception ex)
            {
            }

            if(tmpMsg.Equals(""))
            {
                foo = new APIResult()
                {
                    Success = true,
                    State = "OK",
                    Message = tmpMsg
                };
            }
            else
            {
                foo = new APIResult()
                {
                    Success = false,
                    State = "NG",
                    Message = tmpMsg
                };
            }

            _logger.Info(string.Format("Info:{0}",tmpMsg));
            _logger.Warn(string.Format("Warning:{0}", tmpMsg));
            _logger.Error(string.Format("Error:{0}", tmpMsg));
            _logger.Debug(string.Format("Debug:{0}", tmpMsg));

            //string sql = "select * from gyro_lot_carrier_associate";
            //DataSet ds = dbPool.GetDataSet(sql);

            return foo;
        }
        public class FuncNo
        {
            public string FunctionNo { get; set; }
            public string KeyCode { get; set; }
        }
    }
}
