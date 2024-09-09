using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
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
    public class FunctionTest : BasicController
    {
        private readonly IFunctionService _functionService;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly DBTool _dbTool;

        public FunctionTest(IConfiguration configuration, ILogger logger, IFunctionService functionService, DBTool dbTool)
        {
            _logger = logger;
            _configuration = configuration;
            _functionService = functionService;
            _dbTool = dbTool;
        }

        [HttpPost("CallRTDAlarmSet")]
        public Boolean CallRTDAlarmSet([FromBody] classAlarmCode value)
        {
            string funcName = "CallRTDAlarmSet";
            string tmpMsg = "";
            string strResult = "";
            bool bResult = false;
            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                string equipid = "EQP123";
                string conditions = "";
                sql = _BaseDataService.QueryReserveStateByEquipid(equipid);
                conditions = "EQP234,2023/03/30 09:00:00,2023/03/30 10:59:59,400973";
                sql = _BaseDataService.InsertEquipReserve(conditions);
                _dbTool.SQLExec(sql, out tmpMsg, true);
                conditions = "SETTIME,EQP234,400973,2023/03/30 09:00:00,2023/03/30 09:59:59";
                sql = _BaseDataService.UpdateEquipReserve(conditions);

                sql = _BaseDataService.SelectAvailableCarrierByCarrierType("Foup", false);

                dt = _dbTool.GetDataTable(_BaseDataService.QueryRackByGroupID("ERT01"));

                if(dt.Rows.Count > 0)
                {
                    bResult = false;
                }

                if(_functionService.AutoHoldForDispatchIssue(_dbTool, _configuration, _logger, out tmpMsg))
                {
                    bResult = true;
                    return bResult;
                }

                string[] tmpString = new string[] {value.CommandID, "", ""};
                if (_functionService.CallRTDAlarm(_dbTool, int.Parse(value.AlarmCode), tmpString))
                {
                    bResult = true;
                }
            }
            catch (Exception ex)
            {
                bResult = false;
            }
            finally
            {
                //Do Nothing
            }

            return bResult;
        }
        public class classAlarmCode
        {
            public string AlarmCode { get; set; }
            public string CommandID { get; set; }
        }
    }
}
