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
    public class JcetDataSerach : BasicController
    {
        private readonly IFunctionService _functionService;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly DBTool _dbTool;

        public JcetDataSerach(IConfiguration configuration, ILogger logger, IFunctionService functionService, DBTool dbTool)
        {
            _logger = logger;
            _configuration = configuration;
            _functionService = functionService;
            _dbTool = dbTool;
        }

        [HttpGet("GetAdsLotInfo")]
        public string GetAdsLotInfo()
        {
            string funcName = "GetAdsLotInfo";
            string tmpMsg = "";
            string strResult = "";
            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                //// 查詢資料
                sql = _BaseDataService.SelectTableADSData(_functionService.GetExtenalTables(_configuration, "SyncExtenalData", "AdsInfo"));
                //_logger.Info(string.Format("sql string: [{0}]", sql));
                dt = _dbTool.GetDataTable(sql);
                dr = dt.Select();
                if (dr.Length <= 0)
                {

                }
                else
                {
                    strResult = JsonConvert.SerializeObject(dt);
                }
            }
            catch (Exception ex)
            {
                return strResult;
            }
            finally
            {
                if (dt is not null)
                {
                    dt.Clear(); dt.Dispose(); dt = null; dr = null;
                }
            }

            return strResult;
        }

        [HttpGet("GetPromisStageInfo")]
        public string GetPromisStageInfo()
        {
            string funcName = "GetPromisStageInfo";
            string tmpMsg = "";
            string strResult = "";
            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                //// 查詢資料
                sql = _BaseDataService.ShowTableEQUIP_MATRIX();
                //_logger.Info(string.Format("sql string: [{0}]", sql));
                dt = _dbTool.GetDataTable(sql);
                dr = dt.Select();
                if (dr.Length <= 0)
                {

                }
                else
                {
                    strResult = JsonConvert.SerializeObject(dt);
                }
            }
            catch (Exception ex)
            {
                return strResult;
            }
            finally
            {
                if (dt is not null)
                {
                    dt.Clear(); dt.Dispose(); dt = null; dr = null;
                }
            }

            return strResult;
        }

        [HttpPost("GetWorkGroupSet")]
        public string GetWorkGroupSet([FromBody] classEquipId value)
        {
            string funcName = "GetWorkGroupSet";
            string tmpMsg = "";
            string strResult = "";
            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                //// 查詢資料
                sql = _BaseDataService.SelectWorkgroupSet(value.EquipID);
                //_logger.Info(string.Format("sql string: [{0}]", sql));
                dt = _dbTool.GetDataTable(sql);
                dr = dt.Select();
                if (dr.Length <= 0)
                {

                }
                else
                {
                    strResult = JsonConvert.SerializeObject(dt);
                }
            }
            catch (Exception ex)
            {
                return strResult;
            }
            finally
            {
                if (dt is not null)
                {
                    dt.Clear(); dt.Dispose(); dt = null; dr = null;
                }
            }

            return strResult;
        }
        public class classEquipId
        {
            public string EquipID { get; set; }
        }
    }
}
