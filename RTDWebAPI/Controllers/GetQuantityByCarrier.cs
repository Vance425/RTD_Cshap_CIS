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
    public class GetQuantityByCarrier : BasicController
    {
        private readonly IFunctionService _functionService;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly DBTool _dbTool;
        private readonly List<DBTool> _lstDBSession;

        public GetQuantityByCarrier(IConfiguration configuration, ILogger logger, IFunctionService functionService, List<DBTool> lstDBSession)
        {
            _logger = logger;
            _configuration = configuration;
            _functionService = functionService;
            _dbTool = (DBTool)lstDBSession[1];
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
        public ApiResultQuantityInfo Get([FromBody] clsCarrier value)
        {
            ApiResultQuantityInfo foo;
            string funcName = "GetQuantityByCarrier";
            string tmpMsg = "";
            int iExecuteMode = 1;
            DataTable dt = null;
            DataTable dtTemp = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();
            int iSplitMode = 0;

            try
            {
                sql = string.Format(_BaseDataService.SelectRTDDefaultSet("SPLITMODE"));
                dtTemp = _dbTool.GetDataTable(sql);
                if (dtTemp.Rows.Count > 0)
                {
                    iSplitMode = dtTemp.Rows[0]["paramvalue"].ToString().Equals("0") ? 0 : int.Parse(dtTemp.Rows[0]["paramvalue"].ToString());
                }
                else
                    iSplitMode = 0;

                //// 查詢資料
                dt = _dbTool.GetDataTable(_BaseDataService.QueryQuantityByCarrier(value.CarrierId));

                if (dt.Rows.Count <= 0)
                {
                    foo = new ApiResultQuantityInfo()
                    {
                        State = "NG",
                        CarrierId = value.CarrierId,
                        LotId = "",
                        Total = 0,
                        Quantity = 0,
                        ErrorCode = "",
                        Message = "Carrier Id is not exist."
                    };
                }
                else
                {
                    int _total_qty = 0;
                    int _quantity = 0;
                    switch (iSplitMode)
                    {
                        case 1:
                            _total_qty = int.Parse(dt.Rows[0]["quantity"].ToString());
                            _quantity = int.Parse(dt.Rows[0]["quantity"].ToString());
                            if (!dt.Rows[0]["total_qty"].ToString().Equals(dt.Rows[0]["quantity"].ToString()))
                            {
                                sql = _BaseDataService.UpdateLotinfoTotalQty(dt.Rows[0]["lot_id"].ToString(), _quantity);
                                _dbTool.SQLExec(sql, out tmpMsg, true);
                            }
                            break;
                        case 0:
                        default:
                            _total_qty = int.Parse(dt.Rows[0]["total_qty"].ToString());
                            _quantity = int.Parse(dt.Rows[0]["quantity"].ToString());
                            break;
                    }

                    foo = new ApiResultQuantityInfo()
                    {
                        State = "OK",
                        CarrierId = value.CarrierId,
                        LotId = dt.Rows[0]["lot_id"].ToString(),
                        Total = _total_qty,
                        Quantity = _quantity
                    };
                }
            }
            catch (Exception ex)
            {
                foo = new ApiResultQuantityInfo()
                {
                    State = "NG",
                    CarrierId = value.CarrierId,
                    LotId = "",
                    Total = 0,
                    Quantity = 0,
                    ErrorCode = "",
                    Message = ex.Message
                };
            }
            finally
            {
                //_logger.LogInformation(string.Format("Info :{0}", value.CarrierID));
                if (dt is not null)
                {
                    dt.Clear(); dt.Dispose(); dt = null; dr = null;
                }
            }

            return foo;
        }
        public class clsCarrier
        {
            public string CarrierId { get; set; }
        }
        public class ApiResultQuantityInfo
        {
            public string State { get; set; }
            public string CarrierId { get; set; }
            public string LotId { get; set; }
            public int Total { get; set; }
            public int Quantity { get; set; }
            public string ErrorCode { get; set; }
            public string Message { get; set; }
        }
    }
}
