using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Nancy.Json;
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
using System.Threading;
using System.Threading.Tasks;
using static RTDWebAPI.Controllers.DeleteWorkinProcessController;
using static RTDWebAPI.Controllers.GetUIDataController;
using RTDSrv;
using RTDWebAPI.Commons.Method.WSClient;
using System.Xml;
using System.IO;

namespace RTDWebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    //[EnableCors("MyPolicy")]
    public class GetUIDataController : BasicController
    {
        private readonly IFunctionService _functionService;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly DBTool _dbTool;
        private readonly ConcurrentQueue<EventQueue> _eventQueue;
        private readonly Dictionary<string, object> _uiDataCatch;
        private readonly Dictionary<string, string> _alarmDetail;
        private readonly List<DBTool> _lstDBSession;

        public GetUIDataController(List<DBTool> lstDBSession, IConfiguration configuration, ILogger logger, IFunctionService functionService, ConcurrentQueue<EventQueue> eventQueue, Dictionary<string, object> uiDataCatch, Dictionary<string, string> alarmDetail)
        {
            string tmpMsg = "";
            _dbTool = (DBTool)lstDBSession[0]; 
            _logger = logger;
            _configuration = configuration;
            _functionService = functionService;
            _eventQueue = eventQueue;
            _uiDataCatch = uiDataCatch;
            _lstDBSession = lstDBSession;
            _alarmDetail = alarmDetail;

            for (int idb = _lstDBSession.Count-1; idb >= 0; idb--)
            {
                _dbTool = _lstDBSession[idb];
                if (_dbTool.IsConnected)
                {
                    break;
                }
            }

            tmpMsg = string.Format("{2} UI API, Function[{0}], Thread ID [{1}]", "GetUIDataController", Thread.CurrentThread.ManagedThreadId, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
            Console.WriteLine(tmpMsg);
        }

        [HttpGet("Display")]
        public string Show()
        {
            string tmpMsg = Thread.CurrentThread.ManagedThreadId.ToString();
            Console.WriteLine(tmpMsg);
            return "Hi 你好";
        }

        [HttpGet("GetPerson")]
        public ActionResult<SchemaLotInfo> GetPerson()
        {
            SchemaLotInfo tmp = new SchemaLotInfo();
            //var p = new Person
            //{
            //    Id = 1,
            //    Name = "Jason",
            //    Age = 27,
            //    Sex = "man"
            //};
            return tmp;
        }

        [HttpGet("GetLotInfoData")]
        public ActionResult<String> GetLotInfoData()
        {
            List<SchemaLotInfo> lsPersons = new List<SchemaLotInfo>()
            {
         
            };

            string funcName = "GetLotInfo";
            string tmpMsg = "";
            string strResult = "";
            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                //// 查詢資料
                dt = _dbTool.GetDataTable(_BaseDataService.SelectTableLotInfo());
                //dt = (DataTable) _uiDataCatch["lotInfo"];
                dr = dt.Select();
                if (dr.Length <= 0)
                {

                }
                else
                {
                    strResult = JsonConvert.SerializeObject(dr);
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
        [HttpPost("GetLotInfoData")]
        public ActionResult<String> GetLotInfoData([FromBody] classDepartment value)
        {
            List<SchemaLotInfo> lsPersons = new List<SchemaLotInfo>()
            {

            };

            string funcName = "GetLotInfo";
            string tmpMsg = "";
            string strResult = "";
            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                tmpMsg = Thread.CurrentThread.ManagedThreadId.ToString();
                Console.WriteLine(tmpMsg);

                //// 查詢資料
                dt = _dbTool.GetDataTable(_BaseDataService.SelectTableLotInfoByDept(value.Department));
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

        [HttpGet("GetCarrierTransfer")]
        public ActionResult<String> GetCarrierTransfer()
        {
            List<SchemaCarrierTransfer> lsPersons = new List<SchemaCarrierTransfer>()
            {

            };

            string funcName = "SchemaCarrierTransfer";
            string tmpMsg = "";
            string strResult = "";
            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                //// 查詢資料
                dt = _dbTool.GetDataTable(_BaseDataService.SelectTableCarrierTransfer());
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
                //_logger.LogInformation(string.Format("Info :{0}", value.CarrierID));
                if (dt is not null)
                {
                    dt.Clear(); dt.Dispose(); dt = null; dr = null;
                }
            }

            return strResult;
        }

        [HttpGet("GetWorkInProcessSch")]
        public ActionResult<String> GetWorkInProcessSch()
        {
            List<SchemaWorkInProcessSch> lsPersons = new List<SchemaWorkInProcessSch>()
            {

            };

            string funcName = "SchemaWorkInProcessSch";
            string tmpMsg = "";
            string strResult = "";
            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();

            string tableOrder = "";
            string _keyOfEnv = "";
            string _keyRTDEnv = "";

            try
            {
                _keyRTDEnv = _configuration["RTDEnvironment:type"];
                _keyOfEnv = string.Format("RTDEnvironment:commandsTable:{0}", _keyRTDEnv);
                //"RTDEnvironment:commandsTable:PROD"
                tableOrder = _configuration[_keyOfEnv] is null ? "workinprocess_sch" : _configuration[_keyOfEnv];

                //// 查詢資料
                dt = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSch(tableOrder));
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
                //_logger.LogInformation(string.Format("Info :{0}", value.CarrierID));
                if (dt is not null)
                {
                    dt.Clear(); dt.Dispose(); dt = null; dr = null;
                }
            }

            return strResult;
        }
        [HttpGet("GetEquipmentInformation")]
        public ActionResult<String> GetEquipmentInformation()
        {
            List<SchemaEqpStatus> lsPersons = new List<SchemaEqpStatus>()
            {

            };

            string funcName = "GetEquipmentInformation";
            string tmpMsg = "";
            string strResult = "";
            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                //// 查詢資料
                dt = _dbTool.GetDataTable(_BaseDataService.SelectTableEquipmentStatus());
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
                //_logger.LogInformation(string.Format("Info :{0}", value.CarrierID));
                if (dt is not null)
                {
                    dt.Clear(); dt.Dispose(); dt = null; dr = null;
                }
            }

            return strResult;
        }
        [HttpPost("GetEquipmentInformation")]
        public ActionResult<String> GetEquipmentInformation([FromBody] classDepartment value)
        {
            List<SchemaEqpStatus> lsPersons = new List<SchemaEqpStatus>()
            {

            };

            string funcName = "GetEquipmentInformation";
            string tmpMsg = "";
            string strResult = "";
            DataTable dt = null;
            DataTable dtTemp = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                //// 查詢資料
                dt = _dbTool.GetDataTable(_BaseDataService.SelectTableEquipmentStatus(value.Department));
                dr = dt.Select();
                if (dr.Length <= 0)
                {

                }
                else
                {
                    strResult = JsonConvert.SerializeObject(dt);
                    //dtTemp = JsonConvert.DeserializeObject<DataTable>(strResult);
                }
            }
            catch (Exception ex)
            {
                return strResult;
            }
            finally
            {
                //_logger.LogInformation(string.Format("Info :{0}", value.CarrierID));
                if (dt is not null)
                {
                    dt.Clear(); dt.Dispose(); dt = null; dr = null;
                }
            }

            return strResult;
        }
        public class classDepartment
        {
            public string Department { get; set; }
        }

        [HttpPost("GetEquipListByLotId")]
        public List<String> GetEquipListByLotId([FromBody] QueryByLot value)
        {
            List<String> lsEquip = new List<String>();
            string funcName = "GetEquipListByLotId";
            string tmpMsg = "";
            string strResult = "";
            DataTable dt = null;
            DataTable dt2 = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                //// 查詢資料
                dt = _dbTool.GetDataTable(_BaseDataService.QueryEquipmentByLot(value.LotId));
                dr = dt.Select();
                if (dr.Length <= 0)
                {
                    //Do Nothing
                }
                else
                {
                    if (value.Rework)
                    {
                        dt2 = _dbTool.GetDataTable(_BaseDataService.QueryReworkEquipment());
                        if (dt2.Rows.Count > 0)
                        {
                            foreach (DataRow drPortId in dt2.Rows)
                            {
                                if (!drPortId["Port_Id"].ToString().Equals(""))
                                    lsEquip.Add(drPortId["Port_Id"].ToString());
                            }
                        }
                    } else
                    {
                        if (dr[0]["equiplist"].ToString().Contains(","))
                        {
                            string[] lstEquip = dr[0]["equiplist"].ToString().Split(',');
                            foreach (string tmp in lstEquip)
                            {
                                dt2 = _dbTool.GetDataTable(_BaseDataService.QueryEquipmentPortIdByEquip(tmp));
                                if (dt2.Rows.Count > 0)
                                {
                                    foreach (DataRow drPortId in dt2.Rows)
                                    {
                                        if (!drPortId["Port_Id"].ToString().Equals(""))
                                            lsEquip.Add(drPortId["Port_Id"].ToString());
                                    }
                                }
                            }
                        }
                        else
                        {
                            dt2 = _dbTool.GetDataTable(_BaseDataService.QueryEquipmentPortIdByEquip(dr[0]["equiplist"].ToString()));
                            if (dt2.Rows.Count > 0)
                            {
                                foreach (DataRow drPortId in dt2.Rows)
                                {
                                    if (!drPortId["Port_Id"].ToString().Equals(""))
                                        lsEquip.Add(drPortId["Port_Id"].ToString());
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return lsEquip;
            }
            finally
            {
                //_logger.LogInformation(string.Format("Info :{0}", value.CarrierID));
                if (dt is not null)
                {
                    dt.Clear(); dt.Dispose(); dt = null; dr = null;
                }
            }

            return lsEquip;
        }

        [HttpPost("GetCarrierByLotId")]
        public List<String> GetCarrierByLotId([FromBody] QueryByLot value)
        {
            List<String> lsCarrier = new List<String>();
            string funcName = "GetCarrierByLotId";
            string tmpMsg = "";
            string strResult = "";
            DataTable dt = null;
            string sql = "";
            bool isManual = false;
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {

                if (value.Rework.Equals(true))
                    isManual = true;

                //// 查詢資料
                dt = _dbTool.GetDataTable(_BaseDataService.QueryCarrierByLot(value.LotId));
                if (dt.Rows.Count <= 0)
                {
                    //Do Nothing
                }
                else
                {
                    foreach(DataRow dr in dt.Rows)
                    {
                        lsCarrier.Add(dr["carrier_id"].ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                return lsCarrier;
            }
            finally
            {
                //_logger.LogInformation(string.Format("Info :{0}", value.CarrierID));
                if (dt is not null)
                {
                    dt.Clear(); dt.Dispose(); dt = null;
                }
            }

            return lsCarrier;
        }
        [HttpGet("GetExecuteMode")]
        public List<int> GetExecuteMode()
        {
            string funcName = "GetExecuteMode";
            string tmpMsg = "";
            int iExecuteMode = 1;
            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            List<int> lsExecuteMode = new List<int>();
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                //// 查詢資料
                dt = _dbTool.GetDataTable(_BaseDataService.SelectRTDDefaultSet("ExecuteMode"));

                if (dt.Rows.Count > 0)
                {
                    //iExecuteMode = int.Parse(dt.Rows[0]["PARAMVALUE"].ToString());
                    int iMode = 0;
                    try
                    {
                        iMode = int.Parse(dt.Rows[0]["PARAMVALUE"].ToString());
                    }
                    catch(Exception ex)
                    { iMode = 0; }
                    lsExecuteMode.Add(iMode);
                }
            }
            catch (Exception ex)
            {
                return lsExecuteMode;
            }
            finally
            {
                //_logger.LogInformation(string.Format("Info :{0}", value.CarrierID));
                if (dt is not null)
                {
                    dt.Clear(); dt.Dispose(); dt = null; dr = null;
                }
            }

            return lsExecuteMode;
        }
        [HttpPost("ChangeExecuteMode")]
        public APIResult ChangeExecuteMode([FromBody] RTDDefaultSet value)
        {
            APIResult foo;
            string funcName = "ChangeExecuteMode";
            string tmpMsg = "";
            int iExecuteMode = 1;
            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                //// 查詢資料
                _dbTool.SQLExec(_BaseDataService.UpdateTableRTDDefaultSet("ExecuteMode", value.ParamValue, value.ModifyBy), out tmpMsg, true);

                if (tmpMsg.Equals(""))
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
            }
            catch (Exception ex)
            {
                foo = new APIResult()
                {
                    Success = true,
                    State = "NG",
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

        [HttpGet("GetListLcation")]
        public ActionResult<String> GetListLcation()
        {
            string funcName = "GetListLcation";
            string tmpMsg = "";
            string strResult = "";
            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            List<EquipmentPortState> lstEquipPortStates = new List<EquipmentPortState>();
            EquipmentPortState equipmentPortState = new EquipmentPortState();
            EquipmentSlotInfo equipmentSlotInfo = new EquipmentSlotInfo();
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                //// 查詢資料
                dt = _dbTool.GetDataTable(_BaseDataService.SelectEquipmentPortInfo());
                if (dt.Rows.Count <= 0)
                {

                }
                else
                {
                    equipmentPortState = new EquipmentPortState();
                    equipmentSlotInfo = new EquipmentSlotInfo();
                    foreach (DataRow dr2 in dt.Rows)
                    {
                        if (equipmentPortState.EqID is null)
                            equipmentPortState.EqID = dr2["equipid"].ToString();
                        else if (!equipmentPortState.EqID.Equals(dr2["equipid"].ToString()))
                        {
                            lstEquipPortStates.Add(equipmentPortState);
                            equipmentPortState = new EquipmentPortState();
                            equipmentPortState.EqID = dr2["equipid"].ToString();
                        }
                        equipmentSlotInfo = new EquipmentSlotInfo();
                        equipmentSlotInfo.slotNo = int.Parse(dr2["port_seq"].ToString().Equals("") ? "0" : dr2["port_seq"].ToString());
                        if (equipmentPortState.PortInfoList is null)
                            equipmentPortState.PortInfoList = new List<EquipmentSlotInfo>();
                        equipmentPortState.PortInfoList.Add(equipmentSlotInfo);
                    }
                    
                }

                //// 查詢儲存貨架資料
                dt = _dbTool.GetDataTable(_BaseDataService.QueryERackInfo());
                if (dt.Rows.Count <= 0)
                {

                }
                else
                {
                    string rackid = "";
                    int iRows = 0;

                    foreach (DataRow dr2 in dt.Rows)
                    {
                        rackid = dr2["erackID"].ToString();
                        iRows = int.Parse(dr2["LEN"].ToString());

                        equipmentPortState = new EquipmentPortState();
                        equipmentSlotInfo = new EquipmentSlotInfo();
                        for (int i = 1; i <= iRows; i++)
                        {
                            if (equipmentPortState.EqID is null)
                                equipmentPortState.EqID = rackid;
                            else if (!equipmentPortState.EqID.Equals(rackid))
                            {
                                lstEquipPortStates.Add(equipmentPortState);
                                equipmentPortState = new EquipmentPortState();
                                equipmentPortState.EqID = rackid;
                            }
                            equipmentSlotInfo = new EquipmentSlotInfo();
                            equipmentSlotInfo.slotNo = i;
                            if (equipmentPortState.PortInfoList is null)
                                equipmentPortState.PortInfoList = new List<EquipmentSlotInfo>();
                            equipmentPortState.PortInfoList.Add(equipmentSlotInfo);
                        }
                        if(lstEquipPortStates is not null)
                            lstEquipPortStates.Add(equipmentPortState);
                        else
                        {

                        }
                    }
                }

                strResult = JsonConvert.SerializeObject(lstEquipPortStates);
            }
            catch (Exception ex)
            {
                return strResult;
            }
            finally
            {
                //_logger.LogInformation(string.Format("Info :{0}", value.CarrierID));
                if (dt is not null)
                {
                    dt.Clear(); dt.Dispose(); dt = null; dr = null;
                }
            }

            return strResult;
        }

        [HttpPost("GetEquipPortByEquipId")]
        public ActionResult<String> GetEquipPortByEquipId([FromBody] QueryByEquipID value)
        {
            string funcName = "GetEquipPortByEquipId";
            string tmpMsg = "";
            string strResult = "";
            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                //// 查詢資料
                dt = _dbTool.GetDataTable(_BaseDataService.QueryEquipmentPortInfoByEquip(value.EquipmentID));
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
        [HttpPost("ResetSchSeqByLotId")]
        public APIResult ResetSchSeqByLotId([FromBody] ResetSchSeq value)
        {
            APIResult foo = new APIResult { };
            string funcName = "ResetSchSeqByLotId";
            string tmpMsg = "";
            string strResult = "";
            DataTable dt = null;
            DataRow[] dr = null;
            string tmpCustomerName = "";
            int oriSeq = 0;
            string tmpStage = "";
            string tmpSql = "";

            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                var jsonStringName = new JavaScriptSerializer();
                var jsonStringResult = jsonStringName.Serialize(value);
                _logger.Info(string.Format("Function:{0}, Received:[{1}]", funcName, jsonStringResult));

                //// 查詢資料
                dt = _dbTool.GetDataTable(_BaseDataService.SelectTableLotInfoByLotid(value.LotId));

                if (dt.Rows.Count > 0)
                {
                    tmpSql = _BaseDataService.LockLotInfo(true);
                    _dbTool.SQLExec(tmpSql, out tmpMsg, true);
                    //Lock Table

                    tmpCustomerName = dt.Rows.Count > 0 ? dt.Rows[0]["CustomerName"].ToString() : "";
                    oriSeq = dt.Rows.Count > 0 ? int.Parse(dt.Rows[0]["Sch_Seq"].ToString()) : 0;
                    tmpStage = dt.Rows.Count > 0 ? dt.Rows[0]["Stage"].ToString().Trim() : "";

                    _dbTool.SQLExec(_BaseDataService.UpdateSchSeq(tmpCustomerName, tmpStage, value.SchSeq, oriSeq), out tmpMsg, true);
                    if (!tmpMsg.Equals(""))
                    {
                        tmpMsg = String.Format("Update fail [UpdateSchSeq]: {0}", tmpMsg);
                        foo = new APIResult()
                        {
                            Success = false,
                            State = "NG",
                            Message = tmpMsg
                        };
                        return foo;
                    }

                    _dbTool.SQLExec(_BaseDataService.UpdateSchSeqByLotId(value.LotId, tmpCustomerName, value.SchSeq), out tmpMsg, true);

                    if (tmpMsg.Equals(""))
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
                        tmpMsg = String.Format("Update fail [UpdateSchSeqByLotId]: {0}", tmpMsg);
                        foo = new APIResult()
                        {
                            Success = false,
                            State = "NG",
                            Message = tmpMsg
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                tmpMsg = String.Format("Exception: {0}", ex.Message);
                foo = new APIResult()
                {
                    Success = false,
                    State = "NG",
                    Message = tmpMsg
                };
            }

            tmpSql = _BaseDataService.LockLotInfo(false);
            _dbTool.SQLExec(tmpSql, out tmpMsg, true);

            return foo;
        }
        [HttpGet("GetDepartment")]
        public List<ClsDept> GetDepartment()
        {
            string funcName = "GetDepartment";
            string tmpMsg = "";
            int iExecuteMode = 1;
            DataTable dt = null;
            string sql = "";
            List<ClsDept> lsDepartment = new List<ClsDept>();
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                //// 查詢資料
                dt = _dbTool.GetDataTable(_BaseDataService.SelectRTDDefaultSet("Department"));

                if (dt.Rows.Count > 0)
                {
                    //iExecuteMode = int.Parse(dt.Rows[0]["PARAMVALUE"].ToString());
                    string tmpDepartment = "";
                    string tmpDescription = "";
                    ClsDept tmpDept = new ClsDept();
                    foreach (DataRow dr in dt.Rows)
                    {
                        tmpDepartment = "";
                        tmpDept = new ClsDept();
                        try
                        {
                            tmpDepartment = dr["PARAMVALUE"].ToString();
                            tmpDescription = dr["Description"].ToString();
                        }
                        catch (Exception ex)
                        { tmpDepartment = ""; }

                        if (!tmpDepartment.Equals(""))
                        {
                            tmpDept.Department = tmpDepartment;
                            tmpDept.Description = tmpDescription;
                            lsDepartment.Add(tmpDept);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return lsDepartment;
            }
            finally
            {
                //_logger.LogInformation(string.Format("Info :{0}", value.CarrierID));
                if (dt is not null)
                {
                    dt.Clear(); dt.Dispose(); dt = null;
                }
            }

            return lsDepartment;
        }
        public class ClsDept
        {
            public string Department { get; set; }
            public string Description { get; set; }
        }
        [HttpPost("StartupEquipment")]
        public APIResult StartupEquipment([FromBody] ClassEquip value)
        {
            APIResult foo;
            string funcName = "StartupEquipment";
            string tmpMsg = "";
            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                dt = _dbTool.GetDataTable(_BaseDataService.QueryEquipmentStatusByEquip(value.Equip));

                if (dt.Rows.Count > 0)
                { 
                    if(dt.Rows[0]["curr_status"].Equals("DOWN"))
                    {
                        _dbTool.SQLExec(_BaseDataService.UpdateEquipCurrentStatus("UP", value.Equip), out tmpMsg, true);
                    }
                }

                if (tmpMsg.Equals(""))
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
            }
            catch (Exception ex)
            {
                foo = new APIResult()
                {
                    Success = true,
                    State = "NG",
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
        public class ClassEquip
        {
            public string Equip { get; set; }
        }
        [HttpPost("ResetReserveByCarrier")]
        public APIResult ResetReserveByCarrier([FromBody] ClassCarrier value)
        {
            APIResult foo;
            string funcName = "ResetReserveByCarrier";
            string tmpMsg = "";
            int iExecuteMode = 1;
            DataTable dt = null;
            DataTable dtTemp = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();

            string tableOrder = "";
            string _keyOfEnv = "";
            string _keyRTDEnv = "";

            try
            {
                _keyRTDEnv = _configuration["RTDEnvironment:type"];
                _keyOfEnv = string.Format("RTDEnvironment:commandsTable:{0}", _configuration["RTDEnvironment:type"]);
                //"RTDEnvironment:commandsTable:PROD"
                tableOrder = _configuration[_keyOfEnv] is null ? "workinprocess_sch" : _configuration[_keyOfEnv];

                dt = _dbTool.GetDataTable(_BaseDataService.QueryCarrierInfoByCarrierId(value.CarrierId));

                if(dt.Rows.Count > 0)
                {

                    if (dt.Rows[0]["reserve"].ToString().Equals("1"))
                    {
                        dtTemp = null;
                        dtTemp = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSchByCarrier(value.CarrierId, tableOrder));

                        if (dtTemp.Rows.Count > 0)
                        {
                            tmpMsg = "There are commands in WorkInProcess_Sch. Please check and execute this function again.";
                        }
                        else
                        {
                            //// 解除預約
                            _dbTool.SQLExec(_BaseDataService.UpdateTableReserveCarrier(value.CarrierId, false), out tmpMsg, true);
                        }
                    }
                    else
                    {
                        tmpMsg = "The Carrier have not been reserve.";
                    }
                }
                else
                {
                    tmpMsg = "Can not find this carrier id.";
                }

                if (tmpMsg.Equals(""))
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
            }
            catch (Exception ex)
            {
                foo = new APIResult()
                {
                    Success = true,
                    State = "NG",
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
        public class ClassCarrier
        {
            public string CarrierId { get; set; }
        }
        [HttpPost("GetWorkinProcessSchHis")]
        public List<SchemaWorkInProcessSchHis> GetWorkinProcessSchHis([FromBody] ClassTimeInterval value)
        {
            List<SchemaWorkInProcessSchHis> foo;
            string funcName = "GetWorkinProcessSchHis";
            string tmpMsg = "";
            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();
            foo = new List<SchemaWorkInProcessSchHis>();

            try
            {
                dt = _dbTool.GetDataTable(_BaseDataService.QueryWorkinProcessSchHis(value.StartTime, value.EndTime));

                if (dt.Rows.Count > 0)
                {
                    SchemaWorkInProcessSchHis WorkinProcessHis;
                    foreach (DataRow row in dt.Rows)
                    {
                        WorkinProcessHis = new SchemaWorkInProcessSchHis();
                        WorkinProcessHis.UUID = row["UUID"].ToString();
                        WorkinProcessHis.Cmd_Id = row["Cmd_Id"].ToString();
                        WorkinProcessHis.Cmd_Type = row["Cmd_Type"].ToString();
                        WorkinProcessHis.EquipId = row["EquipId"].ToString();
                        WorkinProcessHis.Cmd_State = row["Cmd_State"].ToString();
                        WorkinProcessHis.CarrierId = row["CarrierId"].ToString();
                        WorkinProcessHis.CarrierType = row["carrierType"].Equals(null) ? " " : row["carrierType"].ToString();
                        WorkinProcessHis.Source = row["Source"].ToString();
                        WorkinProcessHis.Dest = row["Dest"].ToString();
                        WorkinProcessHis.Priority = row["Priority"].ToString().Equals("") ? 0 : int.Parse(row["Priority"].ToString());
                        WorkinProcessHis.Replace = row["Replace"].ToString().Equals("") ? 0 : int.Parse(row["Replace"].ToString());
                        WorkinProcessHis.Back = row["Back"].ToString();
                        WorkinProcessHis.LotID = row["LotID"].ToString();
                        WorkinProcessHis.Customer = row["Customer"].ToString();
                        if(row["Create_Dt"] is not null && !row["Create_Dt"].ToString().Equals(""))
                            WorkinProcessHis.Create_Dt = DateTime.Parse(row["Create_Dt"].ToString());
                        if (row["Modify_Dt"] is not null && !row["Modify_Dt"].ToString().Equals(""))
                            WorkinProcessHis.Modify_Dt = DateTime.Parse(row["Modify_Dt"].ToString());
                        if (row["LastModify_Dt"] is not null && !row["LastModify_Dt"].ToString().Equals(""))
                            WorkinProcessHis.LastModify_Dt = DateTime.Parse(row["LastModify_Dt"].ToString());

                        foo.Add(WorkinProcessHis);
                    }
                }

                if (tmpMsg.Equals(""))
                {
                    
                }
                else
                {
                    
                }
            }
            catch (Exception ex)
            {
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
        public class ClassTimeInterval
        {
            public string StartTime { get; set; }
            public string EndTime { get; set; }
        }
        [HttpPost("QueryStatisticalOfDispatch1")]
        public List<RTDStatistical> QueryStatisticalOfDispatch1([FromBody] ClassStatisticalCdt value)
        {
            List<RTDStatistical> foo;
            string funcName = "QueryStatisticalOfDispatch1";
            string tmpMsg = "";
            DataTable dt = null;
            DataTable dtTemp = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();
            foo = new List<RTDStatistical>();
            DateTime dtStart;
            DateTime dtEnd;
            double dHour = 0;
            double dunit = 1;
            DateTime dtCurrUTCTime;
            DateTime dtLocTime;
            bool isNightShift = false;

            try
            {
                if (value.Zone.Contains("-"))
                {
                    dunit = 1;
                    dHour = dunit * double.Parse(value.Zone.Replace("-", ""));
                }
                else if (value.Zone.Contains("+"))
                {
                    dunit = -1;
                    dHour = dunit * double.Parse(value.Zone.Replace("+", ""));
                }
                else
                {
                    dunit = -1;
                    dHour = dunit * double.Parse(value.Zone);
                }

                if (value.CurrentDateTime.Equals(""))
                {
                    dtLocTime = DateTime.Now;
                    dtCurrUTCTime = DateTime.Now;
                }
                else
                {
                    dtLocTime = DateTime.Parse(value.CurrentDateTime);
                    dtCurrUTCTime = DateTime.Parse(value.CurrentDateTime).AddHours(dHour);
                }

                isNightShift = _functionService.NightOrDay(value.CurrentDateTime);

                dtStart = DateTime.Parse(dtLocTime.ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour);
                dtEnd = DateTime.Parse(dtLocTime.AddDays(1).ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour);

                List<string> lstType = new List<string> { "Total", "Success", "Failed" };
                foreach (string type in lstType)
                {
                    sql = _BaseDataService.QueryStatisticalOfDispatch(dtCurrUTCTime, value.Unit, type);
                    dt = _dbTool.GetDataTable(sql);

                    if (dt.Rows.Count > 0)
                    {
                        try
                        {
                            RTDStatistical rtdStatistical;
                            foreach (DataRow row in dt.Rows)
                            {
                                int iTimes1 = 0;
                                int iTimes2 = 0;
                                rtdStatistical = new RTDStatistical();
                                if (row.Table.Columns.Contains("Years"))
                                {
                                    if (row["Years"] is not null && !row["Years"].ToString().Equals(""))
                                        rtdStatistical.Year = int.Parse(row["Years"].ToString());
                                }
                                if (row.Table.Columns.Contains("Months"))
                                {
                                    if (row["Months"] is not null && !row["Months"].ToString().Equals(""))
                                        rtdStatistical.Month = int.Parse(row["Months"].ToString());
                                }
                                if (row.Table.Columns.Contains("Days"))
                                {
                                    if (row["Days"] is not null && !row["Days"].ToString().Equals(""))
                                        rtdStatistical.Day = int.Parse(row["Days"].ToString());
                                }
                                if (row.Table.Columns.Contains("Hours"))
                                {
                                    if (row["Hours"] is not null && !row["Hours"].ToString().Equals(""))
                                        rtdStatistical.Hour = int.Parse(row["Hours"].ToString());
                                }
                                if (row.Table.Columns.Contains("Type"))
                                {
                                    if (row["Type"] is not null && !row["Type"].ToString().Equals(""))
                                        rtdStatistical.Type = row["Type"].ToString();
                                }

                                if (rtdStatistical.Type is null)
                                    rtdStatistical.Type = type;

                                switch (value.Unit.ToLower())
                                {
                                    case "years":
                                        dtStart = DateTime.Parse(dtLocTime.ToString("yyyy") + "-01-01 00:00").AddHours(dHour);
                                        dtEnd = DateTime.Parse(dtLocTime.AddYears(1).ToString("yyyy") + "-01-01 00:00").AddHours(dHour);
                                        break;
                                    case "months":
                                        dtStart = DateTime.Parse(dtLocTime.ToString("yyyy-MM") + "-01 00:00").AddHours(dHour);
                                        dtEnd = DateTime.Parse(dtLocTime.AddMonths(1).ToString("yyyy-MM") + "-01 00:00").AddHours(dHour);
                                        break;
                                    case "days":
                                        dtStart = DateTime.Parse(dtLocTime.ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour);
                                        dtEnd = DateTime.Parse(dtLocTime.AddDays(1).ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour);
                                        break;
                                    case "shift":
                                        //dHour = dHour - 4;
                                        if (isNightShift)
                                        {
                                            if (dtLocTime.Hour >= 20)
                                            {
                                                dtStart = DateTime.Parse(dtLocTime.AddDays(1).ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour - 4);
                                                dtEnd = DateTime.Parse(dtLocTime.AddDays(1).ToString("yyyy-MM-dd ") + " 12:00").AddHours(dHour - 4);
                                            }
                                            else
                                            {
                                                dtStart = DateTime.Parse(dtLocTime.ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour - 4);
                                                dtEnd = DateTime.Parse(dtLocTime.ToString("yyyy-MM-dd ") + " 12:00").AddHours(dHour - 4);
                                            }
                                        }
                                        else
                                        {
                                            dtStart = DateTime.Parse(dtLocTime.ToString("yyyy-MM-dd ") + " 12:00").AddHours(dHour - 4);
                                            dtEnd = DateTime.Parse(dtLocTime.AddDays(1).ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour - 4);
                                        }
                                        
                                        break;
                                    default:
                                        dtStart = DateTime.Parse(dtLocTime.ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour);
                                        dtEnd = DateTime.Parse(dtLocTime.AddDays(1).ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour);
                                        break;
                                }

                                sql = _BaseDataService.CalcStatisticalTimesFordiffZone(true, dtStart, dtEnd, value.Unit, type, dHour);
                                dtTemp = _dbTool.GetDataTable(sql);
                                iTimes1 = dtTemp.Rows.Count>0 ? int.Parse(dtTemp.Rows[0]["time"].ToString()) : 0;
                                sql = _BaseDataService.CalcStatisticalTimesFordiffZone(false, dtStart, dtEnd, value.Unit, type, dHour);
                                dtTemp = _dbTool.GetDataTable(sql);
                                iTimes2 = dtTemp.Rows.Count > 0 ? int.Parse(dtTemp.Rows[0]["time"].ToString()) : 0;

                                if(value.Unit.ToLower().Equals("shift"))
                                {
                                    rtdStatistical.Times = iTimes1 - iTimes2;
                                }
                                else
                                {
                                    rtdStatistical.Times = int.Parse(row["Time"].ToString()) + iTimes1 - iTimes2;
                                }
                                

                                foo.Add(rtdStatistical);
                            }
                        }
                        catch(Exception ex)
                        {
                            tmpMsg = ex.Message;
                        }
                    }
                }

                if (tmpMsg.Equals(""))
                {

                }
                else
                {

                }
            }
            catch (Exception ex)
            {
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
        [HttpPost("QueryStatisticalOfDispatch")]
        public List<RTDStatistical> QueryStatisticalOfDispatch([FromBody] ClassStatisticalCdt value)
        {
            List<RTDStatistical> foo;
            string funcName = "QueryStatisticalOfDispatch";
            string tmpMsg = "";
            DataTable dt = null;
            DataTable dtTemp = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();
            foo = new List<RTDStatistical>();
            DateTime dtStart;
            DateTime dtEnd;
            double dHour = 0;
            double dunit = 1;
            DateTime dtCurrUTCTime;
            DateTime dtLocTime;
            bool isNightShift = false;
            RTDStatistical rtdStatistical;

            try
            {
                if (value.Zone.Contains("-"))
                {
                    dunit = 1;
                    dHour = dunit * double.Parse(value.Zone.Replace("-", ""));
                }
                else if (value.Zone.Contains("+"))
                {
                    dunit = -1;
                    dHour = dunit * double.Parse(value.Zone.Replace("+", ""));
                }
                else
                {
                    dunit = -1;
                    dHour = dunit * double.Parse(value.Zone);
                }

                if (value.CurrentDateTime.Equals(""))
                {
                    dtLocTime = DateTime.Now;
                    dtCurrUTCTime = DateTime.Now;
                }
                else
                {
                    dtLocTime = DateTime.Parse(value.CurrentDateTime);
                    dtCurrUTCTime = DateTime.Parse(value.CurrentDateTime).AddHours(dHour);
                }

                isNightShift = _functionService.NightOrDay(value.CurrentDateTime);

                dtStart = DateTime.Parse(dtLocTime.ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour);
                dtEnd = DateTime.Parse(dtLocTime.AddDays(1).ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour);

                dHour = 0;

                switch (value.Unit.ToLower())
                {
                    case "years":
                        dtStart = DateTime.Parse(dtLocTime.ToString("yyyy") + "-01-01 00:00").AddHours(dHour);
                        dtEnd = DateTime.Parse(dtLocTime.AddYears(1).ToString("yyyy") + "-01-01 00:00").AddHours(dHour);
                        break;
                    case "months":
                        dtStart = DateTime.Parse(dtLocTime.ToString("yyyy-MM") + "-01 00:00").AddHours(dHour);
                        dtEnd = DateTime.Parse(dtLocTime.AddMonths(1).ToString("yyyy-MM") + "-01 00:00").AddHours(dHour);
                        break;
                    case "days":
                        dtStart = DateTime.Parse(dtLocTime.ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour);
                        dtEnd = DateTime.Parse(dtLocTime.AddDays(1).ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour);
                        break;
                    case "shift":
                        //dHour = dHour - 4;
                        if (isNightShift)
                        {
                            if (dtLocTime.Hour >= 20)
                            {
                                dtStart = DateTime.Parse(dtLocTime.AddDays(1).ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour - 4);
                                dtEnd = DateTime.Parse(dtLocTime.AddDays(1).ToString("yyyy-MM-dd ") + " 12:00").AddHours(dHour - 4);
                            }
                            else
                            {
                                dtStart = DateTime.Parse(dtLocTime.ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour - 4);
                                dtEnd = DateTime.Parse(dtLocTime.ToString("yyyy-MM-dd ") + " 12:00").AddHours(dHour - 4);
                            }
                        }
                        else
                        {
                            dtStart = DateTime.Parse(dtLocTime.ToString("yyyy-MM-dd ") + " 12:00").AddHours(dHour - 4);
                            dtEnd = DateTime.Parse(dtLocTime.AddDays(1).ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour - 4);
                        }

                        break;
                    default:
                        dtStart = DateTime.Parse(dtLocTime.ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour);
                        dtEnd = DateTime.Parse(dtLocTime.AddDays(1).ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour);
                        break;
                }

                sql = _BaseDataService.CalcStatisticalTimes(dtStart.ToString("yyyy/MM/dd HH:mm:ss"), dtEnd.ToString("yyyy/MM/dd HH:mm:ss"));
                dtTemp = _dbTool.GetDataTable(sql);

                List<string> lstType = new List<string> { "Total", "Success", "Failed" };
                string currType = "";
                int iTotal = 0;

                foreach (string type in lstType)
                {
                    iTotal = 0;

                    rtdStatistical = new RTDStatistical();
                    rtdStatistical.Type = type;

                    if (dtTemp.Rows.Count > 0)
                    {
                        foreach (DataRow dr1 in dtTemp.Rows)
                        {
                            switch(type)
                            {
                                case "Total":
                                    iTotal = iTotal + int.Parse(dr1["TIMES"].ToString());

                                    break;
                                default:
                                    if(dr1["STATUS"].ToString().Equals(type))
                                    {
                                        iTotal = iTotal + int.Parse(dr1["TIMES"].ToString());
                                    }
                                    break;
                            }
                        }

                        rtdStatistical.Times = iTotal;
                    }
                    else
                    {
                        rtdStatistical.Times = 0;
                    }

                    foo.Add(rtdStatistical);
                }

                if (tmpMsg.Equals(""))
                {

                }
                else
                {

                }
            }
            catch (Exception ex)
            {
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
        public class ClassStatisticalCdt
        {
            public string StartDateTime { get; set; }
            public string CurrentDateTime { get; set; }
            public string Unit { get; set; }
            public string Zone { get; set; }
        }
        [HttpGet("GetRtdAlarm")]
        public ActionResult<String> GetRtdAlarm()
        {

            string funcName = "GetRtdAlarm";
            string tmpMsg = "";
            string strResult = "";
            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                //// 查詢資料
                dt = _dbTool.GetDataTable(_BaseDataService.QueryRtdNewAlarm());
                
                if (dt.Rows.Count > 0)
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
        [HttpGet("GetRtdAlarmAll")]
        public ActionResult<String> GetRtdAlarmAll()
        {

            string funcName = "GetRtdAlarmAll";
            string tmpMsg = "";
            string strResult = "";
            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                //// 查詢資料
                dt = _dbTool.GetDataTable(_BaseDataService.QueryAllRtdAlarm());

                if (dt.Rows.Count > 0)
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
        [HttpPost("CleanRtdAlarm")]
        public APIResult CleanRtdAlarm([FromBody] ClassDateTime value)
        {
            APIResult foo;
            string funcName = "CleanRtdAlarm";
            string tmpMsg = "";
            DataTable dt = null;
            DataRow[] dr = null;
            string sql;
            DateTime dtLimit;
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                dtLimit = DateTime.Parse(value.DateTime.ToString());

                if (dtLimit <= DateTime.Now)
                {
                    _dbTool.SQLExec(_BaseDataService.UpdateRtdAlarm(value.DateTime), out tmpMsg, true);
                }

                if (tmpMsg.Equals(""))
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
            }
            catch (Exception ex)
            {
                foo = new APIResult()
                {
                    Success = true,
                    State = "NG",
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
        public class ClassDateTime
        {
            public string DateTime { get; set; }
        }

        [HttpPost("DeleteWorkinProcess")]
        public APIResult DeleteWorkinProcess([FromBody] KeyWorkInProcessSch value)
        {
            APIResult foo = new();
            IBaseDataService _BaseDataService = new BaseDataService();
            EventQueue _eventQ = new EventQueue();
            string funcName = "DeleteWorkinProcess";
            string tmpMsg = "";
            string tmp2Msg = "";

            _eventQ.EventName = funcName;
            string CommandId = "";
            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            string lotid = "";
            string tableOrder = "";
            string _keyOfEnv = "";
            string _keyRTDEnv = "";

            try
            {
                var jsonStringName = new JavaScriptSerializer();
                var jsonStringResult = jsonStringName.Serialize(value);
                _logger.Info(string.Format("Function:[{0}], WorkinProcess:{1}", funcName, jsonStringResult));

                _keyRTDEnv = _configuration["RTDEnvironment:type"];
                _keyOfEnv = string.Format("RTDEnvironment:commandsTable:{0}", _configuration["RTDEnvironment:type"]);
                //"RTDEnvironment:commandsTable:PROD"
                tableOrder = _configuration[_keyOfEnv] is null ? "workinprocess_sch" : _configuration[_keyOfEnv];

                CommandId = value.CommandID;
                if (CommandId.Equals(""))
                {
                    foo.Success = false;
                    foo.State = "NG";
                    foo.Message = "Command ID can not be empty.";
                    return foo;
                }

                // 查詢Lot資料
                if (!_dbTool.IsConnected)
                { 
                    
                }

                sql = string.Format(_BaseDataService.SelectTableWorkInProcessSchByCmdId(CommandId, tableOrder));
                dt = _dbTool.GetDataTable(sql);
                dr = dt.Select();

                if (dt.Rows.Count > 0)
                {
                    tmpMsg = "";
                    lotid = !dt.Rows[0]["lotid"].ToString().Equals("") ? dt.Rows[0]["lotid"].ToString().Trim() : "";

                    APIResult apiResult = new APIResult();
                    if (dt.Rows[0]["cmd_current_state"].Equals("Init"))
                    { //Cancel
                        apiResult = _functionService.SentAbortOrCancelCommandtoMCS(_configuration, _logger, 1, CommandId);
                        if (apiResult.Success)
                        {

                        }
                        else
                        {

                        }
                    }
                    else if (dt.Rows[0]["cmd_current_state"].Equals("Running"))
                    { //Abort
                        apiResult = _functionService.SentAbortOrCancelCommandtoMCS(_configuration, _logger, 2, CommandId);
                        if (apiResult.Success)
                        {

                        }
                        else
                        {

                        }
                    }
                    else if (dt.Rows[0]["cmd_current_state"].Equals("Failed"))
                    { //Reset lot_info RTD_STATE to READY
                        if (!lotid.Equals(""))
                        {
                            sql = _BaseDataService.UpdateTableLotInfoToReadyByLotid(lotid);
                            _dbTool.SQLExec(sql, out tmpMsg, true);
                        }
                        tmpMsg = "Command run failed.";
                        apiResult.Success = true;
                        apiResult.State = "NG";
                        apiResult.Message = tmpMsg;
                    }
                    else
                    { //Pass of Success/ Failed/ Others
                        if (!lotid.Equals(""))
                        {
                            sql = _BaseDataService.UpdateTableLotInfoToReadyByLotid(lotid);
                            _dbTool.SQLExec(sql, out tmpMsg, true);
                        }
                        tmpMsg = "";
                        apiResult.Success = true;
                        apiResult.State = "OK";
                        apiResult.Message = tmpMsg;
                    }

                    if (apiResult.Success)
                    {
                        // 更新狀態資料
                        string CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        if (_dbTool.SQLExec(_BaseDataService.UpdateTableWorkInProcessSchByCmdId("DELETE", CurrentTime, CommandId, tableOrder), out tmpMsg, true))
                        {
                            if (_dbTool.SQLExec(_BaseDataService.UpdateTableWorkInProcessSchHisByCmdId(CommandId), out tmpMsg, true))
                            {
                                foreach (DataRow row in dt.Rows)
                                {
                                    if (row["CARRIERID"].ToString().Equals("*") || row["CARRIERID"].ToString().Equals(""))
                                    { }
                                    else
                                    {
                                        if (_dbTool.SQLExec(_BaseDataService.UpdateTableReserveCarrier(row["CARRIERID"].ToString(), false), out tmpMsg, true))
                                        { }
                                    }
                                }

                                if (_dbTool.SQLExec(_BaseDataService.DeleteWorkInProcessSchByCmdId(CommandId, tableOrder), out tmpMsg, true))
                                {
                                    //Do Nothing
                                    foo.Success = true;
                                    foo.State = "OK";
                                    foo.Message = tmpMsg;
                                }
                                else
                                {
                                    //Do Nothing
                                    tmp2Msg = String.Format("WorkinProcess delete fail. [Exception] {0}", tmpMsg);
                                    foo.Success = false;
                                    foo.State = "NG";
                                    foo.Message = tmp2Msg;
                                }
                            }

                        }
                        else
                        {
                            //Do Nothing
                            tmp2Msg = String.Format("WorkinProcess update fail. [Exception] {0}", tmpMsg);
                            foo.Success = false;
                            foo.State = "NG";
                            foo.Message = tmp2Msg;

                            _logger.Debug(tmpMsg);

                            /*
                            if (tmpMsg.IndexOf("Object reference not set to an instance of an object") > 0)
                            {

                                if (_dbTool.dbPool.CheckConnet(out tmpMsg))
                                {
                                    _dbTool.DisConnectDB(out tmp2Msg);

                                    if (!tmp2Msg.Equals(""))
                                    {
                                        _logger.Debug(string.Format("DB disconnect failed [{0}]", tmp2Msg));
                                    }
                                    else
                                    {
                                        _logger.Debug(string.Format("Database disconect."));
                                    }

                                    if (!_dbTool.IsConnected)
                                    {
                                        _logger.Debug(string.Format("Database re-established."));
                                        _dbTool.ConnectDB(out tmp2Msg);
                                    }
                                }
                                else
                                {
                                    if (!_dbTool.IsConnected)
                                    {
                                        string[] _argvs = new string[] { "", "", "" };
                                        if (_functionService.CallRTDAlarm(_dbTool, 20100, _argvs))
                                        {
                                            _logger.Debug(string.Format("Database re-established."));
                                            _dbTool.ConnectDB(out tmp2Msg);
                                        }
                                    }
                                }

                                if (!tmp2Msg.Equals(""))
                                {
                                    _logger.Debug(string.Format("DB re-established failed [{0}]", tmp2Msg));
                                }
                                else
                                {
                                    string[] _argvs = new string[] { "", "", "" };
                                    if (_functionService.CallRTDAlarm(_dbTool, 20101, _argvs))
                                    {
                                        _logger.Debug(string.Format("DB re-connection sucess", tmp2Msg));
                                    }
                                }
                            }
                            */
                        }
                    }
                    else
                    {
                        foo = apiResult;

                        if (foo.Message.Contains("InternalServerError"))
                        {
                            if (_dbTool.SQLExec(_BaseDataService.DeleteWorkInProcessSchByCmdId(CommandId, tableOrder), out tmpMsg, true))
                            {
                                if(tmpMsg.Equals(""))
                                    tmpMsg = string.Format("Remove command failed {0}, Just delete RTD order.", CommandId);

                                _logger.Debug(tmpMsg);
                            }
                        }
                    }
                }
                else
                {
                    tmpMsg = "Data Not found.";
                    foo.Success = false;
                    foo.State = "NG";
                    foo.Message = tmpMsg;
                }

                if (foo.State == "OK")
                {
                    _eventQ.EventObject = value;
                    _eventQueue.Enqueue(_eventQ);
                }
                else
                {
                    _logger.Debug(foo.Message);
                }
            }
            catch (Exception ex)
            {
                foo.Success = false;
                foo.State = "NG";
                foo.Message = String.Format("Unknow issue. [{0}] Exception: {1}", funcName, ex.Message);
                _logger.Debug(foo.Message);
            }

            return foo;
        }

        [HttpPost("SwitchMachineReworkMode")]
        public APIResult SwitchMachineReworkMode([FromBody] EquipmentReworkMode value)
        {
            APIResult foo;
            string funcName = "SwitchMachineReworkMode";
            string tmpMsg = "";
            int iExecuteMode = 1;
            DataTable dt = null;
            DataTable dtTemp = null;
            DataRow[] dr = null;
            string sql = "";
            string tmpWorkGroup = "";
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                dt = _dbTool.GetDataTable(_BaseDataService.QueryEquipmentStatusByEquip(value.EquipID));

                if (dt.Rows.Count > 0)
                {
                    Boolean isManualMode = dt.Rows[0]["manualmode"].ToString().Equals("1") ? true : false;
                    Boolean _expired = dt.Rows[0]["expired"].ToString().Equals("1") ? true : false;
                    Boolean _effective = dt.Rows[0]["effective"].ToString().Equals("1") ? true : false;
                    string descTemp = isManualMode.Equals(true) ? "Manual" : "Auto";
                    string _args = "";
                    tmpWorkGroup = dt.Rows[0]["workgroup"].ToString();

                    if (_expired)
                    {
                        if (!value.ReworkMode.Equals(isManualMode))
                        {
                            tmpMsg = string.Format("Equipment [%s] been change to %s", value.EquipID, descTemp);

                            //// 更新狀態
                            _dbTool.SQLExec(_BaseDataService.ManualModeSwitch(value.EquipID, value.ReworkMode), out tmpMsg, true);
                            _logger.Debug(tmpMsg);
                        }
                    }
                    else 
                    {
                        if (!value.ReworkMode.Equals(isManualMode))
                        {
                            tmpMsg = string.Format("Equipment [%s] been change to %s", value.EquipID, descTemp);

                            //// 更新狀態
                            _dbTool.SQLExec(_BaseDataService.ManualModeSwitch(value.EquipID, value.ReworkMode), out tmpMsg, true);
                            _logger.Debug(tmpMsg);
                        }

                        if (isManualMode)
                        {
                            if(!_expired)
                            {
                                _args = string.Format("{0},{1},{2},{3}", "expired", value.EquipID, "UI", "1");
                                sql = _BaseDataService.UpdateEquipReserve(_args);
                                _logger.Debug(sql);
                                _dbTool.SQLExec(sql, out tmpMsg, true);
                                if (!tmpMsg.Equals(""))
                                    _logger.Debug(tmpMsg);
                            }
                        }
                    }
                }
                else
                {
                    tmpMsg = "Can not find this carrier id.";
                }

                if (tmpMsg.Equals(""))
                {
                    foo = new APIResult()
                    {
                        Success = true,
                        State = "OK",
                        Message = tmpMsg
                    };

                    if(value.ReworkMode.Equals(true))
                    {
                        //"Position": "202310131411001"
                        dtTemp = _dbTool.GetDataTable(_BaseDataService.QueryCarrierOnRack(tmpWorkGroup, value.EquipID));

                        if (dtTemp.Rows.Count > 0)
                        {
                            foreach (DataRow row in dtTemp.Rows)
                            {
                                sql = string.Format(_BaseDataService.UpdateTableLotInfoReset(row["lot_id"].ToString()));
                                _dbTool.SQLExec(sql, out tmpMsg, true);
                            }
                        }
                    }
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
            }
            catch (Exception ex)
            {
                foo = new APIResult()
                {
                    Success = true,
                    State = "NG",
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

        [HttpPost("SaveMachineReserveDatetimeSet")]
        public APIResult SaveMachineReserveDatetimeSet([FromBody] ClassMachineReserveDatetimeSet value)
        {
            APIResult foo;
            string funcName = "SaveMachineReserveDatetimeSet";
            string tmpMsg = "";
            int iExecuteMode = 1;
            DataTable dt = null;
            DataTable dtTemp = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();
            DateTime dtEffective;
            DateTime dtExpired;
            string reserveBy = "";
            string _args = "";
            DateTime dtCurrent = DateTime.Now;

            try
            {
                try
                {
                    if (value.DateTimeEffective.Equals(""))
                        dtEffective = DateTime.Now;
                    else
                        dtEffective = DateTime.Parse(value.DateTimeEffective);

                    if (value.DateTimeEffective.Equals(""))
                        dtExpired = DateTime.Now;
                    else
                        dtExpired = DateTime.Parse(value.DateTimeEffective);
                }
                catch(Exception ex)
                {
                    tmpMsg = "Time format not correct.";
                    goto resultStage;
                }


                if (value.ReserveBy.Equals(""))
                {
                    tmpMsg = "Coulmn ModfiyBy can not empry.";
                    goto resultStage;
                }
                else
                    reserveBy = value.ReserveBy;

                dt = _dbTool.GetDataTable(_BaseDataService.QueryEquipmentPortIdByEquip(value.EquipID));

                if (dt.Rows.Count > 0)
                {
                    string conditions = "";
                    sql = _BaseDataService.QueryReserveStateByEquipid(value.EquipID);
                    dt = _dbTool.GetDataTable(sql);
                    if (dt.Rows.Count <= 0)
                    {
                        conditions = string.Format("{0},{1},{2},{3}", value.EquipID, value.DateTimeEffective, value.DateTimeExpired, value.ReserveBy);
                        sql = _BaseDataService.InsertEquipReserve(conditions);
                        _dbTool.SQLExec(sql, out tmpMsg, true);
                    }
                    else
                    {
                        conditions = string.Format("{0},{1},{2},{3},{4}", "SETTIME", value.EquipID, value.ReserveBy, value.DateTimeEffective, value.DateTimeExpired);
                        sql = _BaseDataService.UpdateEquipReserve(conditions);
                        _dbTool.SQLExec(sql, out tmpMsg, true);
                    }

                    //CheckReserveTimeByEqpID
                    dtTemp = _dbTool.GetDataTable(_BaseDataService.CheckReserveTimeByEqpID(value.EquipID));
                    if (dtTemp.Rows.Count > 0)
                    {
                        dtCurrent = DateTime.Now;
                        _logger.Debug(string.Format("{0}--reservestart--{1}-------------", dtCurrent, value.EquipID));

                        sql = _BaseDataService.ManualModeSwitch(value.EquipID, true);
                        _logger.Debug(sql);
                        _dbTool.SQLExec(sql, out tmpMsg, true);
                        if (!tmpMsg.Equals(""))
                            _logger.Debug(tmpMsg);

                        _args = string.Format("{0},{1},{2},{3}", "effective", value.EquipID, "RTD", "1");
                        sql = _BaseDataService.UpdateEquipReserve(_args);
                        _logger.Debug(sql);
                        _dbTool.SQLExec(sql, out tmpMsg, true);
                        if (!tmpMsg.Equals(""))
                            _logger.Debug(tmpMsg);
                    }
                }
                else
                {
                    tmpMsg = "Equipment not found.";
                }

                resultStage:

                if (tmpMsg.Equals(""))
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
            }
            catch (Exception ex)
            {
                foo = new APIResult()
                {
                    Success = true,
                    State = "NG",
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
        public class ClassMachineReserveDatetimeSet
        {
            public string EquipID { get; set; }
            public string DateTimeEffective { get; set; }
            public string DateTimeExpired { get; set; }
            public string ReserveBy { get; set; }
        }

        [HttpGet("GetReserveTime")]
        public ActionResult<String> GetReserveTime(string _equipid)
        {

            string funcName = "GetReserveTime";
            string tmpMsg = "";
            string strResult = "";
            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                //// 查詢資料
                dt = _dbTool.GetDataTable(_BaseDataService.QueryReserveStateByEquipid( _equipid));

                if (dt.Rows.Count > 0)
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
        public class ClassCancelReserve
        {
            public string EquipID { get; set; }
            public string ReserveBy { get; set; }
        }

        [HttpPost("CancelReserveTime")]
        public APIResult CancelReserveTime([FromBody] ClassCancelReserve value)
        {
            //"Position": "202310131447002"
            APIResult foo;
            string funcName = "CancelReserveTime";
            string tmpMsg = "";
            string strResult = "";
            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();
            Boolean _effective = false;
            Boolean _expired = false;
            string conditions = "";

            try
            {
                //// 查詢資料
                dt = _dbTool.GetDataTable(_BaseDataService.QueryReserveStateByEquipid(value.EquipID));

                if (dt.Rows.Count > 0)
                {
                    _expired = dt.Rows[0]["expired"].ToString().Equals("0") ? false : true;

                    if(!_expired)
                    {
                        conditions = string.Format("{0},{1},{2},{3}", "EXPIRED", value.EquipID, value.ReserveBy,"1");
                        sql = _BaseDataService.UpdateEquipReserve(conditions);
                        _dbTool.SQLExec(sql, out tmpMsg, true);
                    }
                }

                if (tmpMsg.Equals(""))
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
            }
            catch (Exception ex)
            {
                foo = new APIResult()
                {
                    Success = false,
                    State = "NG",
                    Message = ex.Message
                };
            }
            finally
            {
                if (dt is not null)
                {
                    dt.Clear(); dt.Dispose(); dt = null; dr = null;
                }
            }

            return foo;
        }

        [HttpPost("ResetLotPriority")]
        public APIResult ResetLotPriority([FromBody] ClassLotPriority value)
        {
            APIResult foo;
            string funcName = "ResetLotPriority";
            string tmpMsg = "";
            int iExecuteMode = 1;
            DataTable dt = null;
            DataTable dtTemp = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                try
                {
                    if (value.LotID.Equals(""))
                    {
                        tmpMsg = "LotID can not be empty.";

                        foo = new APIResult()
                        {
                            Success = false,
                            State = "NG",
                            Message = tmpMsg
                        };

                        return foo;
                    }

                    sql = _BaseDataService.SelectTableLotInfoByLotid(value.LotID);
                    dt = _dbTool.GetDataTable(sql);

                    if (dt.Rows.Count <= 0)
                    {
                        tmpMsg = "LotID is not Exist.";

                        foo = new APIResult()
                        {
                            Success = false,
                            State = "NG",
                            Message = tmpMsg
                        };

                        return foo;
                    }

                    if(value.NewPriority <= 0 && value.NewPriority >= 100)
                    {
                        tmpMsg = "Priority only between 1 and 99.";

                        foo = new APIResult()
                        {
                            Success = false,
                            State = "NG",
                            Message = tmpMsg
                        };
                    }

                    sql = _BaseDataService.UpdatePriorityByLotid(value.LotID, value.NewPriority);
                    _dbTool.SQLExec(sql, out tmpMsg, true);
                }
                catch (Exception ex)
                {
                    
                }

                if (tmpMsg.Equals(""))
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
            }
            catch (Exception ex)
            {
                foo = new APIResult()
                {
                    Success = true,
                    State = "NG",
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
        public class ClassLotPriority
        {
            public string LotID { get; set; }
            public int NewPriority { get; set; }
        }

        [HttpPost("ManualCheckIn")]
        public APIResult ManualCheckIn([FromBody] ManualCheckIn value)
        {
            APIResult foo;
            APIResult tmpResult;
            string funcName = "ManualCheckIn";
            string tmpMsg = "";
            int iExecuteMode = 1;
            DataTable dt = null;
            DataTable dtTemp = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();
            List<string> tmpp = new List<string>();
            string webServiceMode = "soap11";
            string webUrl = "";
            string apiFormat = "";
            string apiFunc = "";

            JCETWebServicesClient jcetWebServiceClient = new JCETWebServicesClient();
            JCETWebServicesClient.ResultMsg resultMsg = new JCETWebServicesClient.ResultMsg();
            JObject oResp = null;

            string tmpCarrierID = "";
            string tmpLotID = "";
            WebServiceResponse webServiceResp;

            try
            {
                webServiceMode = _configuration["WebService:CIMAPIMode"] is null ? "soap11" : _configuration["WebService:CIMAPIMode"];
                webUrl = _configuration["WebService:CIMAPP"] is null ? "http://scscimapp006.jcim-sg.jsg.jcetglobal.com/C10_RTD_API" : _configuration["WebService:CIMAPP"];

                try
                {
                    try {
                        tmpCarrierID = value.CarrierID is not null ? value.CarrierID : "";
                        tmpLotID = value.LotID is not null ? value.LotID : "";

                        if (value.PortID.Equals(""))
                        {
                            tmpMsg = "Must provide Equipment PortID.";

                            foo = new APIResult()
                            {
                                Success = false,
                                State = "NG",
                                Message = tmpMsg
                            };

                            return foo;
                        }

                        if (!tmpCarrierID.Equals("") && tmpLotID.Equals(""))
                        {
                            try
                            {
                                dt = _dbTool.GetDataTable(_BaseDataService.QueryCarrierInfoByCarrierId(tmpCarrierID));
                                if (dt.Rows.Count > 0)
                                {
                                    //iPreTransMode = dt.Rows[0]["PRETRANSFER"].ToString().Equals("1") ? true : false;
                                    tmpLotID = dt.Rows[0]["lot_id"] is not null ? dt.Rows[0]["lot_id"].ToString() : "";
                                }
                            } catch(Exception ex) { }
                        }

                        if (tmpCarrierID.Equals(""))
                        {
                            if (tmpLotID.Equals(""))
                            {
                                tmpMsg = "Must Keyin CarrierID or LotID.";

                                foo = new APIResult()
                                {
                                    Success = false,
                                    State = "NG",
                                    Message = tmpMsg
                                };

                                return foo;
                            }
                        }
                        else
                        {
                            if (value.UserID.Equals(""))
                            {
                                tmpMsg = "Must Keyin UserID.";

                                foo = new APIResult()
                                {
                                    Success = false,
                                    State = "NG",
                                    Message = tmpMsg
                                };

                                return foo;
                            }
                            if (value.Pwd.Equals(""))
                            {
                                tmpMsg = "Must Keyin password.";

                                foo = new APIResult()
                                {
                                    Success = false,
                                    State = "NG",
                                    Message = tmpMsg
                                };

                                return foo;
                            }
                        }
                    } catch (Exception ex){
                        tmpMsg = string.Format("Exception: {0}", ex.Message);

                        foo = new APIResult()
                        {
                            Success = false,
                            State = "NG",
                            Message = tmpMsg
                        };

                        return foo;
                    }

                    try
                    {
                        jcetWebServiceClient._url = ""; //CIMAPP006 會依function自動生成url
                        jcetWebServiceClient.Logger = _logger;
                        resultMsg = new JCETWebServicesClient.ResultMsg();
                        apiFunc = "CheckEcert";
                        apiFormat = _configuration[string.Format("WebService:CIMAPIFormat:{0}", apiFunc.ToUpper())] is null ? "" : _configuration[string.Format("WebService:CIMAPIFormat:{0}", apiFunc.ToUpper())];
                        resultMsg = jcetWebServiceClient.CIMAPP006("CheckEcert", webUrl, apiFormat, webServiceMode, value.PortID, value.UserID, value.Pwd, tmpLotID);
                        string result3 = resultMsg.retMessage.Trim('\"');
                        string tmpCert = "";
                        string resp_Code = "";
                        string resp_Msg = "";
                        string result4 = "";

                        //result4 = "\"{\"Status\": \"Success\",\"Code\": 200,\"Msg\": \"SUCCESS\"}\"";
                        //result4 = "{{\"\"{\"Status\":\"Failure\",\"Code\":201,\"Msg\":\"No Course Code found\"}\"";
                        //result3 = result4.Trim('\"');

                        _logger.Info(string.Format("CIMAPP006, [{0}][{1}][{2}][{3}]", apiFunc, resultMsg.status, resultMsg.retMessage, result3));

                        webServiceResp = new WebServiceResponse();

                        if (resultMsg.status)
                        {
                            //JsonReader reader = new JsonTextReader(new StringReader(resultMsg.retMessage));

                            //JObject objResp = (JObject)JsonConvert.DeserializeObject(resultMsg.retMessage);
                            try {
                                result3 = "{\"Status\": \"Success\",\"Code\": 200,\"Msg\": \"SUCCESS\"}";
                                _logger.Info(string.Format("CIMAPP006, [{0}][{1}]", "JsonConvert", result3));
                                webServiceResp = JsonConvert.DeserializeObject<WebServiceResponse>(result3);

                                _logger.Info(string.Format("CIMAPP006, [{0}][{1}]", apiFunc, string.Format("Content:{0}{1}", webServiceResp.Code, webServiceResp.Msg)));
                                //oResp = JObject.Parse(resultMsg.retMessage);
                            }
                            catch (Exception ex) {
                                _logger.Info(string.Format("CIMAPP006 [{0}][{1}]", apiFunc, string.Format("[Exception:{0} / {1}]", result3, ex.Message)));
                                string tmp = "";

                                if (result3.IndexOf("SUCCESS") > 0)
                                {
                                    webServiceResp.Status = "SUCCESS";
                                    webServiceResp.Code = "200";
                                    tmp = "";
                                    tmp = result3.Split("Msg")[1].Split(':')[1].Split('}')[0];
                                    webServiceResp.Msg = tmp.Trim('"');
                                }
                                else
                                {
                                    tmp = result3.Split("Status")[1].Split(':')[1].Split(',')[0];
                                    webServiceResp.Status = tmp.Trim('"');
                                    tmp = "";
                                    tmp = result3.Split("Code")[1].Split(':')[1].Split(',')[0];
                                    webServiceResp.Code = tmp.Trim('"');
                                    tmp = "";
                                    tmp = result3.Split("Msg")[1].Split(':')[1].Split('}')[0];
                                    webServiceResp.Msg = tmp.Trim('"');
                                }
                            }
                            
                        }
                        else
                        {
                            tmpMsg = string.Format("An unknown exception occurred in the web service. Please call IT-CIM deportment. [Exception][{0}][{1}]", apiFunc, result3);

                            webServiceResp.Status = "FAILURE";
                            webServiceResp.Code = "000";
                            webServiceResp.Msg = tmpMsg;

                            foo = new APIResult()
                            {
                                Success = false,
                                State = "NG",
                                Message = tmpMsg
                            };

                            return foo;
                        }

                        if (webServiceResp is not null)
                        {
                            if (webServiceResp.Status.ToUpper().Equals("SUCCESS"))
                            {
                                resp_Code = webServiceResp.Code;
                                resp_Msg = webServiceResp.Msg;

                                if (_functionService.CheckMCSStatus(_dbTool, _logger))
                                {

                                    tmpp.Add(value.CarrierID);
                                    tmpp.Add(tmpLotID);
                                    tmpp.Add(value.PortID);
                                    tmpp.Add(value.Quantity.ToString());
                                    tmpp.Add(value.Total.ToString());
                                    tmpp.Add(value.UserID);
                                    tmpp.Add(value.Pwd);
                                    tmpResult = _functionService.SentCommandtoMCSByModel(_dbTool, _configuration, _logger, "ManualCheckIn", tmpp);
                                    tmpMsg = tmpResult.Message;
                                    //sql = _BaseDataService.UpdatePriorityByLotid(value.LotID, value.NewPriority);
                                    //_dbTool.SQLExec(sql, out tmpMsg, true);
                                }
                                else
                                {
                                    _logger.Info(string.Format("MCS Status incorrect, [{0}] not success. [{1}]", "ManualCheckIn", value.CarrierID));
                                }

                                foo = new APIResult()
                                {
                                    Success = true,
                                    State = "OK",
                                    Message = tmpMsg
                                };

                                return foo;
                            }
                            else if (webServiceResp.Status.ToUpper().Equals("FAILURE"))
                            {
                                resp_Code = webServiceResp.Code;
                                resp_Msg = webServiceResp.Msg;

                                tmpMsg = string.Format("This account has no permissions. [Code:{0}, Msg:{1}]", resp_Code, resp_Msg);

                                foo = new APIResult()
                                {
                                    Success = false,
                                    State = "NG",
                                    Message = tmpMsg
                                };

                                return foo;
                            }
                            else
                            {
                                resp_Code = webServiceResp.Code;
                                resp_Msg = webServiceResp.Msg;

                                tmpMsg = string.Format("Unknow Issue. [Code:{0}, Msg:{1}]", resp_Code, resp_Msg);

                                foo = new APIResult()
                                {
                                    Success = false,
                                    State = "NG",
                                    Message = tmpMsg
                                };

                                return foo;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        tmpMsg = ex.Message;
                        _logger.Info(string.Format("CIMAPP006, [{0}][{1}]", "Exception ex", tmpMsg));
                    }   
                }
                catch (Exception ex)
                {
                    tmpMsg = ex.Message;
                }

                if (tmpMsg.Equals(""))
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
            }
            catch (Exception ex)
            {
                foo = new APIResult()
                {
                    Success = true,
                    State = "NG",
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

        [HttpPost("QueryHistoryCmds")]
        public List<HisCommandStatus> QueryHistoryCmds([FromBody] ClassStatisticalCdt value)
        {
            List<HisCommandStatus> foo;
            string funcName = "QueryHistoryCmds";
            string tmpMsg = "";
            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();
            foo = new List<HisCommandStatus>();

            try
            {
                //dt = _dbTool.GetDataTable(_BaseDataService.GetHistoryCommands(value.CurrentDateTime, value.Unit, value.Zone));

                foo = _functionService.GetHistoryCommands(_dbTool, _alarmDetail, value.StartDateTime, value.CurrentDateTime, value.Unit, value.Zone);

            }
            catch (Exception ex)
            {
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
        [HttpPost("SetPreTransByWorkgroup")]
        public APIResult SetPreTransByWorkgroup([FromBody] ClassWorkgroup value)
        {
            APIResult foo;
            string funcName = "SetPreTransByWorkgroup";
            string tmpMsg = "";
            DataTable dt = null;
            DataTable dtTemp = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                dt = _dbTool.GetDataTable(_BaseDataService.QueryWorkgroupSet(value.Workgroup));

                if (dt.Rows.Count > 0)
                {
                    //iPreTransMode = dt.Rows[0]["PRETRANSFER"].ToString().Equals("1") ? true : false;
                    Boolean enablePreTrans = dt.Rows[0]["PRETRANSFER"].ToString().Equals("1") ? true : false;

                    if (enablePreTrans)
                    {
                        if (value.Set.Equals(0))
                        {
                            //// 更新狀態
                            _dbTool.SQLExec(_BaseDataService.SetResetPreTransfer(value.Workgroup,false), out tmpMsg, true);
                            _logger.Debug(tmpMsg);
                        }
                    }
                    else
                    {
                        if (value.Set.Equals(1))
                        {
                            //// 更新狀態
                            _dbTool.SQLExec(_BaseDataService.SetResetPreTransfer(value.Workgroup, true), out tmpMsg, true);
                            _logger.Debug(tmpMsg);
                        }
                    }
                }
                else
                {
                    tmpMsg = string.Format("Workgroup {0} not exist.", value.Workgroup);
                }

                if (tmpMsg.Equals(""))
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
            }
            catch (Exception ex)
            {
                foo = new APIResult()
                {
                    Success = true,
                    State = "NG",
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
        public class ClassWorkgroup
        {
            public string Workgroup { get; set; }
            public int Set { get; set; }
        }
        [HttpPost("EnableEquipmentPort")]
        public APIResult EnableEquipmentPort([FromBody] ClassEquipPort value)
        {
            APIResult foo;
            string funcName = "EnableEquipmentPort";
            string tmpMsg = "";
            string tmpMsg2 = "";
            DataTable dt = null;
            DataTable dtTemp = null;
            DataRow[] dr = null;
            string sql = "";
            string _userid = "";
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                dt = _dbTool.GetDataTable(_BaseDataService.SelectTableEQP_Port_SetByPortId(value.PortID));

                if (dt.Rows.Count > 0)
                {
                    //iPreTransMode = dt.Rows[0]["PRETRANSFER"].ToString().Equals("1") ? true : false;
                    Boolean enableEQPPort = dt.Rows[0]["DISABLE"].ToString().Equals("1") ? true : false;

                    //_userid = value.UserID.Equals("") ? "-----" : value.UserID;

                    if (enableEQPPort)
                    {
                        if (value.Set.Equals(0))
                        {
                            //// 更新狀態
                            _dbTool.SQLExec(_BaseDataService.EnableEqpipPort(value.PortID, false), out tmpMsg, true);
                            _logger.Debug(tmpMsg);

                            tmpMsg2 = string.Format("Equipment Port {0} been enabled.[{1}][{2}]", value.PortID, value.Set, _userid);
                        }
                    }
                    else
                    {
                        if (value.Set.Equals(1))
                        {
                            //// 更新狀態
                            _dbTool.SQLExec(_BaseDataService.EnableEqpipPort(value.PortID, true), out tmpMsg, true);
                            _logger.Debug(tmpMsg);

                            tmpMsg2 = string.Format("Equipment Port {0} been disable.[{1}][{2}]", value.PortID, value.Set, _userid);
                        }
                    }

                    if (!tmpMsg2.Equals(""))
                        _logger.Debug(tmpMsg2);
                }
                else
                {
                    tmpMsg = string.Format("Equipment Port {0} not exist.", value.PortID);
                }

                if (tmpMsg.Equals(""))
                {
                    foo = new APIResult()
                    {
                        Success = true,
                        State = "OK",
                        Message = tmpMsg2
                    };
                }
                else
                {
                    foo = new APIResult()
                    {
                        Success = false,
                        State = "NG",
                        Message = tmpMsg2
                    };
                }
            }
            catch (Exception ex)
            {
                foo = new APIResult()
                {
                    Success = true,
                    State = "NG",
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
        public class ClassEquipPort
        {
            public string PortID { get; set; }
            public int Set { get; set; }
        }
        public class ClassEquipPort2
        {
            public string PortID { get; set; }
            public int Set { get; set; }
            public string UserID { get; set; }
        }


        [HttpPost("ResetRTDStateByLot")]
        public APIResult ResetRTDStateByLot([FromBody] ClassResetRTDStateByLot value)
        {
            APIResult foo;
            string funcName = "ResetRTDStateByLot";
            string tmpMsg = "";
            int iExecuteMode = 1;
            DataTable dt = null;
            DataTable dtTemp = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                try
                {
                    if (value.LotID.Equals(""))
                    {
                        tmpMsg = "LotID can not be empty.";

                        foo = new APIResult()
                        {
                            Success = false,
                            State = "NG",
                            Message = tmpMsg
                        };

                        return foo;
                    }

                    if (value.Username.Equals(""))
                    {
                        tmpMsg = "User name can not be empty.";

                        foo = new APIResult()
                        {
                            Success = false,
                            State = "NG",
                            Message = tmpMsg
                        };

                        return foo;
                    }

                    sql = _BaseDataService.SelectTableLotInfoByLotid(value.LotID);
                    dt = _dbTool.GetDataTable(sql);

                    if (dt.Rows.Count <= 0)
                    {
                        tmpMsg = "LotID is not Exist.";

                        foo = new APIResult()
                        {
                            Success = false,
                            State = "NG",
                            Message = tmpMsg
                        };

                        return foo;
                    }

                    sql = _BaseDataService.UpdateTableLotInfoReset(value.LotID);
                    _dbTool.SQLExec(sql, out tmpMsg, true);

                    if (!tmpMsg.Equals(""))
                    {
                        tmpMsg = "Reset Lot [{0}] Info Failed.";

                        foo = new APIResult()
                        {
                            Success = false,
                            State = "NG",
                            Message = string.Format(tmpMsg, value.LotID)
                        };

                        return foo;
                    }
                }
                catch (Exception ex)
                {

                }

                if (tmpMsg.Equals(""))
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
            }
            catch (Exception ex)
            {
                foo = new APIResult()
                {
                    Success = true,
                    State = "NG",
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
        public class ClassResetRTDStateByLot
        {
            public string LotID { get; set; }
            public string Username { get; set; }
        }

        [HttpGet("QueryRtdAlarmHistory")]
        public ActionResult<String> QueryRtdAlarmHistory([FromBody] ClassQueryRTDAlarm value)
        {

            string funcName = "QueryRtdAlarmHistory";
            string tmpMsg = "";
            string strResult = "";
            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();

            try
            {
                //// 查詢資料
                dt = _dbTool.GetDataTable(_BaseDataService.QueryAllRtdAlarm());

                if (dt.Rows.Count > 0)
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
    }
    public class ClassTimeInterval
    {
        public string StartTime { get; set; }
        public string EndTime { get; set; }
    }
}
