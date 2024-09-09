﻿using Microsoft.AspNetCore.Mvc;
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
    public class ReleaseLotController : BasicController
    {
        private readonly IFunctionService _functionService;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly DBTool _dbTool;
        private readonly ConcurrentQueue<EventQueue> _eventQueue;

        public ReleaseLotController(DBTool dbTool, IConfiguration configuration, ILogger logger, IFunctionService functionService, ConcurrentQueue<EventQueue> eventQueue)
        {
            _logger = logger;
            _configuration = configuration;
            _functionService = functionService;
            _dbTool = dbTool;
            _eventQueue = eventQueue;
        }

        [HttpPost]
        public APIResult Post([FromBody] TransferList value)
        {
            APIResult foo = new();
            IBaseDataService _BaseDataService = new BaseDataService();
            EventQueue _eventQ = new EventQueue();
            string funcName = "ReleaseLot";
            string tmpMsg = "";

            _eventQ.EventName = funcName;
            string LotId = "";
            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";

            try
            {
                LotId = value.LotID;
                if (LotId.Equals(""))
                {
                    foo.Success = false;
                    foo.State = "NG";
                    foo.Message = "Lot ID can not be empty.";
                    return foo;
                }

                // 查詢Lot資料
                sql = string.Format(_BaseDataService.SelectTableLotInfoByLotid(LotId));
                dt = _dbTool.GetDataTable(sql);
                dr = dt.Select();

                if (dt.Rows.Count > 0)
                {
                    tmpMsg = "";

                    // 更新狀態資料
                    if (_dbTool.SQLExec(_BaseDataService.UpdateTableLotInfoReset(LotId), out tmpMsg, true))
                    {
                        //Do Nothing
                        foo.Success = true;
                        foo.State = "OK";
                        foo.Message = tmpMsg;
                    }
                    else
                    {
                        //Do Nothing
                        string tmp2Msg = String.Format("Update failed. [Exception] {0}", tmpMsg);
                        foo.Success = false;
                        foo.State = "NG";
                        foo.Message = tmp2Msg;
                    }
                }
                else
                {
                    //Do Nothing
                    tmpMsg = String.Format("Can not find the lotid [{0}]", LotId);
                    foo.Success = false;
                    foo.State = "NG";
                    foo.Message = tmpMsg;
                }

                if (foo.State == "OK")
                {
                    _eventQ.EventObject = value;
                    _eventQueue.Enqueue(_eventQ);
                }
            }
            catch(Exception ex)
            {
                foo.Success = false;
                foo.State = "NG";
                foo.Message = String.Format("Unknow issue. [{0}] Exception: {1}", funcName, ex.Message);
                _logger.Debug(foo.Message);
            }
            finally
            {
                dt.Clear(); dt.Dispose(); 
                dt = null; dr = null;
            }

            return foo;
        }
    }
}
