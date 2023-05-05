using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Nancy.Json;
using NLog;
using RTDWebAPI.Commons.DataRelated.SQLSentence;
using RTDWebAPI.Commons.Method.Database;
using RTDWebAPI.Interface;
using RTDWebAPI.Models;
using RTDWebAPI.Service;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;

namespace RTDWebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]

    public class EquipmentPortStatusUpdateController : BasicController
    {
        IBaseDataService BaseDataService;
        SqlSentences sqlSentences;
        //IConfiguration configuration;
        //public PortStateChangeController(IConfiguration _configuration)
        //{
        //    configuration = _configuration;
        //}

        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly DBTool _dbTool;
        private readonly ConcurrentQueue<EventQueue> _eventQueue;
        private readonly List<DBTool> _lstDBSession;

        public EquipmentPortStatusUpdateController(ILogger logger, IConfiguration configuration, List<DBTool> lstDBSession, ConcurrentQueue<EventQueue> eventQueue)
        {
            _logger = logger;
            _configuration = configuration;
            //_dbTool = (DBTool)lstDBSession[1];
            _eventQueue = eventQueue;
            _lstDBSession = lstDBSession;

            for (int idb = 0; idb < _lstDBSession.Count; idb++)
            {
                _dbTool = _lstDBSession[idb];
                if (_dbTool.IsConnected)
                {
                    break;
                }
            }
        }

        [HttpPost]
        public APIResult Post([FromBody] AEIPortInfo value)
        {
            APIResult foo = new();
            IBaseDataService _BaseDataService = new BaseDataService();
            string funcName = "EquipmentPortStatusUpdate";
            string tmpMsg = "";

            EventQueue _eventQ = new EventQueue();
            _eventQ.EventName = funcName;
            DataTable dt = null;
            DataTable dtTemp = null;
            DataRow[] dr = null;
            string sql = "";
            string _lastLot = "";

            try
            {
                var jsonStringName = new JavaScriptSerializer();
                var jsonStringResult = jsonStringName.Serialize(value);
                _logger.Info(string.Format("Function:{0}, Received:[{1}]", funcName, jsonStringResult));

                if (value.PortID.Equals(""))
                {
                    foo.Success = false;
                    foo.State = "NG";
                    foo.Message = "Port ID not correct.";
                    return foo;
                }

                /// 查詢資料
                sql = string.Format(_BaseDataService.SelectTableEQP_Port_SetByPortId(value.PortID)) ;
                dt = _dbTool.GetDataTable(sql);
                string tCondition = string.Format("Port_Seq = {0}", value.PortID);
                dr = dt.Select("");
                
                if (dr.Length <= 0)
                {
                    foo.Success = false;
                    foo.State = "NG";
                    foo.Message = "PortID is not exist.";
                    return foo;
                }
                else
                {
                    if (dr[0]["Port_State"].ToString().Equals(value.PortTransferState))
                    {
                        foo.Success = false;
                        foo.State = "NG";
                        foo.Message = "Port stae no change.";
                    }
                    else
                    {
                        string EquipID = dr[0]["EQUIPID"].ToString();
                        string PortSeq = dr[0]["Port_Seq"].ToString();
                        string PortState = value.PortTransferState.ToString();
                        _dbTool.SQLExec(_BaseDataService.UpdateTableEQP_Port_Set(EquipID, PortSeq, PortState), out tmpMsg, true);

                        //20230413V1.0 Added by Vance
                        if (PortState.Equals("1"))
                        {
                            sql = string.Format(_BaseDataService.QueryLastLotFromEqpPort(EquipID, PortSeq));
                            dtTemp = _dbTool.GetDataTable(sql);
                            if (dtTemp.Rows.Count > 0)
                            {
                                _lastLot = dtTemp.Rows[0]["lastLot"].ToString();

                                _dbTool.SQLExec(_BaseDataService.UpdateLastLotIDtoEQPPortSet(EquipID, PortSeq, _lastLot), out tmpMsg, true);
                            }
                        }

                        foo.Success = true;
                        foo.State = "OK";
                        foo.Message = "Port ID has be changed.";

                    }
                }

                if (foo.State == "OK")
                {
                    _eventQ.EventObject = value;
                    _eventQueue.Enqueue(_eventQ);
                }
                else
                {
                    //Do Nothing
                }
                //_eventQ.EventObject = value;
                //_eventQueue.Enqueue(_eventQ);

                foo.Success = true;
                foo.State = "OK";
                foo.Message = String.Format("");
                _logger.Debug(foo.Message);
            }
            catch (Exception ex)
            {
                foo.Success = false;
                foo.State = "NG";
                foo.Message = String.Format("Unknow issue. [{0}] Exception: {1}", funcName, ex.Message);
                _logger.Debug(foo.Message);
            }
            finally
            {
                /*
                if (dt != null)
                {
                    dt.Clear(); dt.Dispose(); dt = null;
                }
                dr = null;
                */
            }

            return foo;
        }
    }
}
