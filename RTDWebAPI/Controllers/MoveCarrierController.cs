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
    public class MoveCarrierController : BasicController
    {
        private readonly IFunctionService _functionService;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly DBTool _dbTool;
        private readonly ConcurrentQueue<EventQueue> _eventQueue;

        public MoveCarrierController(DBTool dbTool, IConfiguration configuration, ILogger logger, IFunctionService functionService, ConcurrentQueue<EventQueue> eventQueue)
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
            string funcName = "MoveCarrier";
            string tmpMsg = "";

            _eventQ.EventName = funcName;
            string LotId = "";
            string CarrierId = "";
            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            int iQty = 0;

            try
            {
                CarrierId = value.CarrierID;
                if (CarrierId.Equals(""))
                {
                    foo.Success = false;
                    foo.State = "NG";
                    foo.Message = "Transfer command must provide carrier id.";
                    return foo;
                }

                //系統取出lot id 及 Quantity
                sql = string.Format(_BaseDataService.SelectTableCarrierAssociateByCarrierID(CarrierId));
                dt = _dbTool.GetDataTable(sql);
                if (dt.Rows.Count > 0)
                {
                    LotId = dt.Rows[0]["Lot_ID"].ToString().Trim();
                    value.LotID = LotId;
                    iQty = dt.Rows[0]["Quantity"].ToString().Equals("") ? 0 : int.Parse(dt.Rows[0]["Quantity"].ToString().Trim()); 
                    value.Quantity = iQty;
                }

                // 查詢Carrier 當前位址
                sql = string.Format(_BaseDataService.SelectTableCarrierTransferByCarrier(CarrierId));
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    tmpMsg = "";
                    string tmpSource = _functionService.GetLocatePort(dt.Rows[0]["Locate"].ToString(), dt.Rows[0]["PortNo"].ToString().Equals("") ? 1 : int.Parse(dt.Rows[0]["PortNo"].ToString()), "");

                    value.Source = tmpSource.Equals(value.Source) ? value.Source : tmpSource;
                }
                else
                {
                    //Do Nothing
                    tmpMsg = "The Carrier has not on e-Rack!";
                    foo.Success = false;
                    foo.State = "NG";
                    foo.Message = tmpMsg;

                    return foo;
                }

                if(!value.Dest.Equals(""))
                {                     
                    //Do Nothing
                    foo.Success = true;
                    foo.State = "OK";
                    foo.Message = tmpMsg;
                }
                else
                {
                    //Do Nothing
                    tmpMsg = "Destination must be specified!";
                    foo.Success = false;
                    foo.State = "NG";
                    foo.Message = tmpMsg;

                    return foo;
                }

                if (foo.State == "OK")
                {
                    _eventQ.EventObject = value;
                    _eventQueue.Enqueue(_eventQ);
                }
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
                dt.Clear(); dt.Dispose();
                dt = null; dr = null;
            }

            return foo;
        }
    }
}
