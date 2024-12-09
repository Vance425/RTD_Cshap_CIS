using Microsoft.Extensions.Configuration;
using Nancy.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using RTDWebAPI.APP;
using RTDWebAPI.Commons.Method.Database;
using RTDWebAPI.Commons.Method.Mail;
using RTDWebAPI.Commons.Method.Tools;
using RTDWebAPI.Commons.Method.WSClient;
using RTDWebAPI.Interface;
using RTDWebAPI.Models;
using ServiceStack;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.ServiceModel;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace RTDWebAPI.Service
{
    public class FunctionService : IFunctionService
    {
        public IBaseDataService _BaseDataService = new BaseDataService();

        public bool AutoCheckEquipmentStatus(DBTool _dbTool, ConcurrentQueue<EventQueue> _evtQueue)
        {

            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            string tmpMsg = "";
            //_evtQueue = new ConcurrentQueue<EventQueue>();
            EventQueue oEventQ = new EventQueue();
            oEventQ.EventName = "AutoCheckEquipmentStatus";

            bool bResult = false;
            try
            {
                NormalTransferModel normalTransfer = new NormalTransferModel();
                //20230413V1.1 Added by Vance
                sql = string.Format(_BaseDataService.SelectTableLotInfoOfReady());
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    DataTable dt2 = null;
                    List<string> lstEqp = new List<string>();
                    foreach (DataRow dr2 in dt.Rows)
                    {
                        normalTransfer = new NormalTransferModel();
                        sql = string.Format(_BaseDataService.SelectTableCarrierType(dr2["lotID"].ToString()));
                        dt2 = _dbTool.GetDataTable(sql);

                        if (dt2.Rows.Count <= 0)
                        {
                            //不在貨架上面, Lock 先排除
                            sql = _BaseDataService.LockLotInfoWhenReady(dr2["lotID"].ToString());
                            _dbTool.SQLExec(sql, out tmpMsg, true);
                            continue;
                        }
                        normalTransfer.LotID = dr2["lotID"].ToString();
                        normalTransfer.CarrierID = dt2.Rows[0]["Carrier_ID"].ToString();

                        if (!normalTransfer.CarrierID.Equals(""))
                        {
                            //己派送的, Lock
                            oEventQ.EventObject = normalTransfer;
                            _evtQueue.Enqueue(oEventQ);

                            sql = _BaseDataService.LockLotInfoWhenReady(dr2["lotID"].ToString());
                            _dbTool.SQLExec(sql, out tmpMsg, true);
                            return true;
                        }
                    }
                }
                else
                {
                    sql = _BaseDataService.UnLockAllLotInfoWhenReadyandLock();
                    _dbTool.SQLExec(sql, out tmpMsg, true);
                }
            }
            catch (Exception ex)
            {
                tmpMsg = String.Format("[Exception][{0}]: {1}", oEventQ.EventName, ex.Message);
            }
            finally
            {
                if(dt != null)
                    dt.Dispose();
            }
            dt = null;
            dr = null;

            return bResult;
        }
        public bool AbnormalyEquipmentStatus(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, bool DebugMode, ConcurrentQueue<EventQueue> _evtQueue, out List<NormalTransferModel> _lstNormalTransfer)
        {

            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            EventQueue oEventQ = new EventQueue();
            oEventQ.EventName = "AbnormalyEquipmentStatus";
            _lstNormalTransfer = new List<NormalTransferModel>();

            string tableOrder = "";
            string _keyOfEnv = "";
            string _keyRTDEnv = "";

            bool bResult = false;
            try
            {
                _keyRTDEnv = _configuration["RTDEnvironment:type"];
                _keyOfEnv = string.Format("RTDEnvironment:commandsTable:{0}", _keyRTDEnv);
                //"RTDEnvironment:commandsTable:PROD"
                tableOrder = _configuration[_keyOfEnv] is null ? "workinprocess_sch" : _configuration[_keyOfEnv];

                NormalTransferModel normalTransfer = new NormalTransferModel();
                sql = string.Format(_BaseDataService.SelectEqpStatusWaittoUnload());
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    DataTable dt2 = null;

                    foreach (DataRow dr2 in dt.Rows)
                    {
                        oEventQ = new EventQueue();
                        oEventQ.EventName = "AbnormalyEquipmentStatus";

                        normalTransfer = new NormalTransferModel();

                        sql = string.Format(_BaseDataService.SelectTableWorkInProcessSchByEquipPort(dr2["EQUIPID"].ToString(), dr2["PORT_ID"].ToString(), tableOrder));
                        dt2 = _dbTool.GetDataTable(sql);

                        if (dt2.Rows.Count > 0)
                        {
                            if (dt2.Rows[0]["SOURCE"].ToString().Equals("*"))
                                continue;
                            else
                            {
                                DataTable dt3;
                                sql = string.Format(_BaseDataService.SelectTableWorkInProcessSchByPortId(dr2["PORT_ID"].ToString(), tableOrder));
                                dt3 = _dbTool.GetDataTable(sql);
                                if (dt3.Rows.Count > 0)
                                    continue;
                                dt3 = null;
                            }
                        }


                        normalTransfer.EquipmentID = dr2["EQUIPID"].ToString();
                        normalTransfer.PortModel = dr2["PORT_MODEL"].ToString();

                        dt2 = null;
                        sql = string.Format(_BaseDataService.QueryCarrierByLocate(dr2["EQUIPID"].ToString(), "semi_int.actl_ciserack_vw@semi_int"));
                        dt2 = _dbTool.GetDataTable(sql);

                        if (dt2.Rows.Count > 0)
                        {
                            normalTransfer.CarrierID = dt2.Rows[0]["Carrier_ID"].ToString().Equals("") ? "*" : dt2.Rows[0]["Carrier_ID"].ToString();
                            normalTransfer.LotID = dt2.Rows[0]["lot_id"].ToString().Equals("") ? "*" : dt2.Rows[0]["lot_id"].ToString();
                        }
                        else
                        {
                            normalTransfer.CarrierID = "";
                            normalTransfer.LotID = "";
                        }

                        if (!normalTransfer.EquipmentID.Equals(""))
                        {
                            if (DebugMode)
                            {
                                _logger.Debug(string.Format("[EqpStatusWaittoUnload] {0} / {1} / {2}", normalTransfer.EquipmentID, normalTransfer.CarrierID, normalTransfer.LotID));
                            }

                            _lstNormalTransfer.Add(normalTransfer);
                            oEventQ.EventObject = normalTransfer;
                            _evtQueue.Enqueue(oEventQ);
                        }
                    }
                }

                sql = string.Format(_BaseDataService.SelectEqpStatusIsDownOutPortWaittoUnload());
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    DataTable dt2 = null;

                    foreach (DataRow dr2 in dt.Rows)
                    {
                        oEventQ = new EventQueue();
                        oEventQ.EventName = "AbnormalyEquipmentStatus";

                        normalTransfer = new NormalTransferModel();

                        //sql = string.Format(_BaseDataService.SelectTableWorkInProcessSchByEquip(dr2["EQUIPID"].ToString()));
                        sql = string.Format(_BaseDataService.SelectTableWorkInProcessSchByEquipPort(dr2["EQUIPID"].ToString(), dr2["PORT_ID"].ToString(), tableOrder));
                        dt2 = _dbTool.GetDataTable(sql);

                        if (dt2.Rows.Count > 0)
                        {
                            if (dt2.Rows[0]["SOURCE"].ToString().Equals("*"))
                                continue;
                            else
                            {
                                DataTable dt3;
                                sql = string.Format(_BaseDataService.SelectTableWorkInProcessSchByPortId(dr2["PORT_ID"].ToString(), tableOrder));
                                dt3 = _dbTool.GetDataTable(sql);
                                if (dt3.Rows.Count > 0)
                                    continue;
                                dt3 = null;
                            }
                        }


                        normalTransfer.EquipmentID = dr2["EQUIPID"].ToString();
                        normalTransfer.PortModel = dr2["PORT_MODEL"].ToString();

                        dt2 = null;
                        sql = string.Format(_BaseDataService.QueryCarrierByLocate(dr2["EQUIPID"].ToString(), "semi_int.actl_ciserack_vw@semi_int"));
                        dt2 = _dbTool.GetDataTable(sql);

                        if (dt2.Rows.Count > 0)
                        {
                            normalTransfer.CarrierID = dt2.Rows[0]["Carrier_ID"].ToString().Equals("") ? "*" : dt2.Rows[0]["Carrier_ID"].ToString();
                            normalTransfer.LotID = dt2.Rows[0]["lot_id"].ToString().Equals("") ? "*" : dt2.Rows[0]["lot_id"].ToString();
                        }
                        else
                        {
                            normalTransfer.CarrierID = "";
                            normalTransfer.LotID = "";
                        }

                        if (!normalTransfer.EquipmentID.Equals(""))
                        {
                            if (DebugMode)
                            {
                                _logger.Debug(string.Format("[EqpStatusIsDownOutPortWaittoUnload] {0} / {1} / {2}", normalTransfer.EquipmentID, normalTransfer.CarrierID, normalTransfer.LotID));
                            }
                            _lstNormalTransfer.Add(normalTransfer);
                            oEventQ.EventObject = normalTransfer;
                            _evtQueue.Enqueue(oEventQ);
                        }
                    }
                }

                //Ready to Load時, 除model 為IO機台外, 其它的 IN的狀態不可為 0 (out of service)
                sql = string.Format(_BaseDataService.SelectEqpStatusReadytoload());
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    DataTable dt2 = null;

                    foreach (DataRow dr2 in dt.Rows)
                    {

                        oEventQ = new EventQueue();
                        oEventQ.EventName = "AbnormalyEquipmentStatus";


                        normalTransfer = new NormalTransferModel();

                        sql = string.Format(_BaseDataService.SelectTableWorkInProcessSchByEquipPort(dr2["EQUIPID"].ToString(), dr2["PORT_ID"].ToString(), tableOrder));
                        dt2 = _dbTool.GetDataTable(sql);

                        if (dt2.Rows.Count > 0)
                        {
                            if (dt2.Rows[0]["DEST"].ToString().Equals(dr2["PORT_ID"].ToString()))
                                continue;
                            else
                            {
                                //DataTable dt3;
                                //sql = string.Format(_BaseDataService.SelectTableWorkInProcessSchByPortId(dr2["PORT_ID"].ToString()));
                                //dt3 = _dbTool.GetDataTable(sql);
                                //if (dt3.Rows.Count > 0)
                                //    continue;
                                //dt3 = null;
                            }
                        }

                        if (dr2["PORT_MODEL"].ToString().Equals(""))
                            continue;

                        normalTransfer.EquipmentID = dr2["EQUIPID"].ToString();
                        normalTransfer.PortModel = dr2["PORT_MODEL"].ToString();

                        dt2 = null;
                        sql = string.Format(_BaseDataService.QueryCarrierByLocateType("ERACK", dr2["EQUIPID"].ToString(), "semi_int.actl_ciserack_vw@semi_int"));
                        dt2 = _dbTool.GetDataTable(sql);

                        if (dt2.Rows.Count > 0)
                        {
                            normalTransfer.CarrierID = dt2.Rows[0]["Carrier_ID"].ToString().Equals("") ? "*" : dt2.Rows[0]["Carrier_ID"].ToString();
                            normalTransfer.LotID = dt2.Rows[0]["lot_id"].ToString().Equals("") ? "*" : dt2.Rows[0]["lot_id"].ToString();
                        }
                        else
                        {
                            normalTransfer.CarrierID = "";
                            normalTransfer.LotID = "";
                        }

                        if (!normalTransfer.EquipmentID.Equals(""))
                        {
                            if (DebugMode)
                            {
                                _logger.Debug(string.Format("[EqpStatusReadytoload] {0} / {1} / {2}", normalTransfer.EquipmentID, normalTransfer.CarrierID, normalTransfer.LotID));
                            }

                            _lstNormalTransfer.Add(normalTransfer);
                            //_lstNormalTransfer.AddRange(normalTransfer);
                            oEventQ.EventObject = normalTransfer;
                            _evtQueue.Enqueue(oEventQ);
                        }
                    }
                }
            }
            catch (Exception ex)
            { }
            finally
            {
                if (dt != null)
                    dt.Dispose(); 
            }
            dt = null;
            dr = null;

            return bResult;
        }
        public bool CheckLotInfo(DBTool _dbTool, IConfiguration _configuration, ILogger _logger)
        {
            DataTable dt = null;
            DataTable dt2 = null;
            DataTable dtTemp = null;
            DataTable dtTemp2 = null;
            DataRow[] dr = null;
            string sql = "";
            string tmpMsg = "";

            bool bResult = false;
            try
            {
                sql = string.Format(_BaseDataService.SelectTableCheckLotInfo(GetExtenalTables(_configuration, "SyncExtenalData", "AdsInfo")));
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    string sql2 = "";
                    string sqlMsg = "";
                    tmpMsg = "Sync Ads Data for LotInfo: LotID='{0}', RTD State from [{1}] to [{2}]";
                    string _tmpOriState = "";
                    string _tmpNewState = "";
                    string _lotID = "";

                    foreach (DataRow dr2 in dt.Rows)
                    {
                        sql2 = "";
                        sqlMsg = "";

                        try
                        {
                            _lotID = dr2["lotid"] is null ? "--lotID--" : dr2["lotid"].ToString();
                            _tmpOriState = dr2["OriState"] is null ? "--Ori--" : dr2["OriState"].ToString();
                            _tmpNewState = dr2["State"] is null ? "--New--" : dr2["State"].ToString();
                        }
                        catch (Exception ex) { }

                        if (dr2["State"].ToString().Equals("New"))
                        {
                            if (dr2["OriState"].ToString().Equals("INIT"))
                            { }
                            else if (dr2["OriState"].ToString().Equals("NONE"))
                            {
                                //增加
                                sql2 = string.Format(_BaseDataService.InsertTableLotInfo(GetExtenalTables(_configuration, "SyncExtenalData", "AdsInfo"), dr2["lotid"].ToString()));
                                _dbTool.SQLExec(sql2, out sqlMsg, true);

                                if (TriggerCarrierInfoUpdate(_dbTool, _configuration, _logger, dr2["lotid"].ToString()))
                                {
                                    //Send InfoUpdate
                                    _logger.Info(string.Format(tmpMsg, _lotID, _tmpOriState, _tmpNewState));
                                }
                            }
                            else if (!dr2["OriState"].ToString().Equals("INIT"))
                            {
                                //歸零
                                sql2 = string.Format(_BaseDataService.UpdateTableLotInfoReset(dr2["lotid"].ToString()));
                                _dbTool.SQLExec(sql2, out sqlMsg, true);

                                _logger.Info(string.Format(tmpMsg, _lotID, _tmpOriState, "Reset"));
                            }
                            else
                            {
                                //增加
                                sql2 = string.Format(_BaseDataService.InsertTableLotInfo(GetExtenalTables(_configuration, "SyncExtenalData", "AdsInfo"), dr2["lotid"].ToString()));
                                _dbTool.SQLExec(sql2, out sqlMsg, true);

                                if (TriggerCarrierInfoUpdate(_dbTool, _configuration, _logger, dr2["lotid"].ToString()))
                                {
                                    //Send InfoUpdate
                                    _logger.Info(string.Format(tmpMsg, _lotID, _tmpOriState, _tmpNewState));
                                }
                            }
                        }
                        else if (dr2["State"].ToString().Equals("Remove"))
                        {
                            //if (!dr2["OriState"].ToString().Equals("INIT"))
                            //{
                            //    //歸零
                            //    sql2 = string.Format(_BaseDataService.UpdateTableLotInfoReset(dr2["lotid"].ToString()));
                            //    _dbTool.SQLExec(sql2, out sqlMsg, true);
                            //}
                            //else 
                            if (!dr2["OriState"].ToString().Equals("COMPLETED"))
                            {
                                //Update State to DELETED
                                sql2 = string.Format(_BaseDataService.UpdateTableLotInfoState(dr2["lotid"].ToString(), "DELETED"));
                                _dbTool.SQLExec(sql2, out sqlMsg, true);

                                _logger.Info(string.Format(tmpMsg, _lotID, _tmpOriState, "Remove"));
                            }
                        }
                        else if (dr2["State"].ToString().Equals("DELETED"))
                        {
                            if (dr2["OriState"].ToString().Equals("INIT"))
                            {
                                //歸零
                                sql2 = string.Format(_BaseDataService.UpdateTableLotInfoReset(dr2["lotid"].ToString()));
                                _dbTool.SQLExec(sql2, out sqlMsg, true);

                                _logger.Info(string.Format(tmpMsg, _lotID, _tmpOriState, "Reset"));
                            }
                            else if (dr2["OriState"].ToString().Equals("DELETED"))
                            {
                                if (!dr2["lastmodify_dt"].ToString().Equals(""))
                                {
                                    _dbTool.SQLExec(_BaseDataService.SyncNextStageOfLot(GetExtenalTables(_configuration, "SyncExtenalData", "AdsInfo"), dr2["lotid"].ToString()), out sqlMsg, true);

                                    _logger.Info(string.Format(tmpMsg, _lotID, _tmpOriState, "Remove"));
                                }
                            }
                            else
                            { }
                        }
                        else if (dr2["State"].ToString().Equals("INIT"))
                        {
                            if (!dr2["lastmodify_dt"].ToString().Equals(""))
                            {
                                if (TimerTool("day", dr2["lastmodify_dt"].ToString()) > 1)
                                    _dbTool.SQLExec(_BaseDataService.UpdateTableLastModifyByLot(dr2["lotid"].ToString()), out sqlMsg, true);
                            }
                        }
                        else if (dr2["State"].ToString().Equals("NEXT"))
                        {
                            if (!dr2["lastmodify_dt"].ToString().Equals(""))
                            {
                                sql2 = string.Format(_BaseDataService.QueryDataByLotid(dr2["lotid"].ToString(), "lot_info"));
                                dtTemp = _dbTool.GetDataTable(sql2);

                                if (dtTemp.Rows.Count > 0)
                                {
                                    if (int.Parse(dtTemp.Rows[0]["priority"].ToString()) >= 70)
                                    {
                                        _dbTool.SQLExec(_BaseDataService.SyncNextStageOfLotNoPriority(GetExtenalTables(_configuration, "SyncExtenalData", "AdsInfo"), dr2["lotid"].ToString()), out sqlMsg, true);
                                        _logger.Info(string.Format("Special priority: [{0}][{1}][{2}][{3}]", _lotID, dtTemp.Rows[0]["priority"].ToString(), _tmpOriState, _tmpNewState));
                                    }
                                    else
                                    {
                                        _dbTool.SQLExec(_BaseDataService.SyncNextStageOfLot(GetExtenalTables(_configuration, "SyncExtenalData", "AdsInfo"), dr2["lotid"].ToString()), out sqlMsg, true);
                                    }

                                    if (TriggerCarrierInfoUpdate(_dbTool, _configuration, _logger, dr2["lotid"].ToString()))
                                    {
                                        //Send InfoUpdate
                                        _logger.Info(string.Format(tmpMsg, _lotID, _tmpOriState, _tmpNewState));
                                    }
                                }
                                //_dbTool.SQLExec(_BaseDataService.SyncNextStageOfLot(GetExtenalTables(_configuration, "SyncExtenalData", "AdsInfo"), dr2["lotid"].ToString()), out sqlMsg, true);

                                //if (TriggerCarrierInfoUpdate(_dbTool, _configuration, _logger, dr2["lotid"].ToString()))
                                //{
                                //    //Send InfoUpdate
                                //}
                            }
                        }
                        else if (dr2["State"].ToString().Equals("WAIT"))
                        {
                            if (!dr2["lotid"].ToString().Equals(""))
                            {
                                sql = _BaseDataService.SelectTableLotInfoByLotid(dr2["lotid"].ToString());
                                dtTemp = _dbTool.GetDataTable(sql);

                                if(dtTemp.Rows.Count > 0)
                                {
                                    foreach(DataRow drT in dtTemp.Rows)
                                    {
                                        if(drT["state"].ToString().Equals("HOLD"))
                                        {
                                            sql = _BaseDataService.QueryDataByLotid(dr2["lotid"].ToString(), GetExtenalTables(_configuration, "SyncExtenalData", "AdsInfo"));
                                            dtTemp2 = _dbTool.GetDataTable(sql);
                                            if(dtTemp2.Rows.Count > 0)
                                            {
                                                if(dtTemp2.Rows[0]["state"].ToString().Equals("WAIT"))
                                                {
                                                    sql2 = string.Format(_BaseDataService.UpdateLotinfoState(dr2["lotid"].ToString(), "WAIT"));
                                                    _dbTool.SQLExec(sql2, out sqlMsg, true);
                                                }
                                            }
                                        }
                                    }
                                }
                                //if (TriggerCarrierInfoUpdate(_dbTool, _configuration, _logger, dr2["lotid"].ToString()))
                                //{
                                //Send InfoUpdate
                                //}
                            }
                        }
                        else if (dr2["State"].ToString().Equals("Nothing"))
                        {
                            //Do Nothing
                        }
                    }

                    bResult = true;
                }

                dt = null;
                sql = string.Format(_BaseDataService.SelectTableCheckLotInfoNoData(GetExtenalTables(_configuration, "SyncExtenalData", "AdsInfo")));
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    foreach (DataRow dr2 in dt.Rows)
                    {
                        sql = string.Format(_BaseDataService.SelectTableLotInfoByLotid(dr2["lotid"].ToString()));
                        dtTemp = _dbTool.GetDataTable(sql);

                        if (dtTemp.Rows.Count > 0)
                        {
                            if (TimerTool("day", dtTemp.Rows[0]["lastmodify_dt"].ToString()) <= 1)
                                continue;
                        }

                        string sql2 = "";
                        string sqlMsg = "";
                        if (dr2["State"].ToString().Equals("Remove"))
                        {
                            if (!dr2["OriState"].ToString().Equals("COMPLETED"))
                            {
                                //Update State to DELETED
                                sql2 = string.Format(_BaseDataService.UpdateTableLotInfoState(dr2["lotid"].ToString(), "DELETED"));
                                _dbTool.SQLExec(sql2, out sqlMsg, true);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                bResult = false;
                throw;
            }
            finally
            {
                if (dt != null)
                    dt.Dispose();
                if (dt2 != null)
                    dt2.Dispose();
                if (dtTemp != null)
                    dtTemp.Dispose();
            }
            dt = null;
            dt2 = null;
            dtTemp = null;
            dr = null;

            return bResult;
        }
        public bool SyncEquipmentData(DBTool _dbTool)
        {
            DataTable dt = null;
            DataTable dt2 = null;
            DataRow[] dr = null;
            string sql = "";
            string tmpMsg = "";

            bool bResult = false;
            try
            {
                //有新增Equipment, 直接同步
                sql = string.Format(_BaseDataService.InsertTableEqpStatus());
                _dbTool.SQLExec(sql, out tmpMsg, true);

                //自動同步Equipment Model, Port Number (先建立對照表, 依對照表同步)


                //自動檢查最新的Equipment Status, 如太長時間未更新, 觸發Equipment status sync

                bResult = true;

            }
            catch (Exception ex)
            {
                bResult = false;
                throw;
            }

            return bResult;
        }
        public bool CheckLotCarrierAssociate(DBTool _dbTool, ILogger _logger)
        {

            DataTable dt = null;
            DataTable dt2 = null;
            DataRow[] dr = null;
            string sql = "";
            string tmpMsg = "";

            bool bResult = false;

            try
            {

                //Carrier Status is Init
                sql = string.Format(_BaseDataService.SelectTableLotInfo());
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    string sql2 = "";
                    string sqlMsg = "";

                    foreach (DataRow dr2 in dt.Rows)
                    {
                        sql2 = _BaseDataService.SelectTableCarrierAssociateByLotid(dr2["lotid"].ToString());
                        dt2 = _dbTool.GetDataTable(sql2);

                        if (dt2.Rows.Count > 0)
                        {
                            //表示此Lot有可用的Carrier 
                            string CarrierID = dt2.Rows[0]["carrier_id"].ToString();

                            //SelectTableCarrierAssociate 
                            //先有lot才會有關聯
                            //if (TimerTool("minutes", dr2["lastmodify_dt"].ToString()) > 3) { }
                            try
                            {
                                if (dr2["carrier_asso"].ToString().Equals("N"))
                                {
                                    if (dt2 is not null)
                                    {
                                        if (int.Parse(dt2.Rows[0]["ENABLE"].ToString()).Equals(1))
                                        {
                                            sql2 = string.Format(_BaseDataService.UpdateTableLotInfoSetCarrierAssociateByLotid(dr2["lotid"].ToString()));
                                            _dbTool.SQLExec(sql2, out sqlMsg, true);

                                            tmpMsg = string.Format("Bind Success. Lotid is [{0}]", dr2["lotid"].ToString());
                                            _logger.Debug(tmpMsg);
                                        }
                                    }
                                    else
                                    {
                                        tmpMsg = string.Format("carrier_asso is N, dt2 is null");
                                        _logger.Debug(tmpMsg);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                tmpMsg = string.Format("Bind Got Exception: {0}", ex.Message);
                                _logger.Debug(tmpMsg);
                            }
                        }
                        else
                        { //表示此Lot沒有可用的Carrier
                            if (dr2["carrier_asso"].ToString().Equals("Y"))
                            {
                                try
                                {
                                    if (dt2 is not null)
                                    {
                                        if (dt2.Rows.Count <= 0)
                                        {
                                            sql2 = string.Format(_BaseDataService.UpdateTableLotInfoSetCarrierAssociate2ByLotid(dr2["lotid"].ToString()));
                                            _dbTool.SQLExec(sql2, out sqlMsg, true);

                                            tmpMsg = string.Format("Unbind Success. Lotid is [{0}]", dr2["lotid"].ToString());
                                            _logger.Debug(tmpMsg);
                                        }
                                    }
                                    else
                                    {
                                        tmpMsg = string.Format("carrier_asso is Y, dt2 is null");
                                        _logger.Debug(tmpMsg);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    tmpMsg = string.Format("Unbind Got Exception: {0}", ex.Message);
                                    _logger.Debug(tmpMsg);
                                }
                            }
                        }
                    }
                }


                //當Carrier Transfer與Carrier Associate不同步時, 補齊Carrier Transfer！
                sql = string.Format(_BaseDataService.SelectCarrierAssociateIsTrue());
                dt = _dbTool.GetDataTable(sql);
                if (dt.Rows.Count > 0)
                {
                    tmpMsg = "";
                    foreach (DataRow dr3 in dt.Rows)
                    {
                        dt2 = _dbTool.GetDataTable(_BaseDataService.SelectTableCarrierAssociate2ByLotid(dr3["lotid"].ToString()));
                        dr = dt2.Select("Enable is not null");
                        if (dr.Length <= 0)
                        {
                            _dbTool.SQLExec(_BaseDataService.InsertInfoCarrierTransfer(dt2.Rows[0]["carrier_id"].ToString()), out tmpMsg, true);

                        }

                    }
                }

                bResult = true;
            }
            catch (Exception ex)
            {
                bResult = false;
            }
            finally
            {
                if (dt != null)
                    dt.Dispose();
                if (dt2 != null)
                    dt2.Dispose();
            }
            dt = null;
            dt2 = null;
            dr = null;

            return bResult;
        }
        public bool CheckLotEquipmentAssociate(DBTool _dbTool, ConcurrentQueue<EventQueue> _evtQueue)
        {

            DataTable dt = null;
            string sql = "";
            string tmpMsg = "";
            bool bResult = false;
            bool bReflush = false;
            string _adstable = "";
            //ConcurrentQueue<EventQueue>  evtQueue = new ConcurrentQueue<EventQueue>();

            try
            {
                sql = string.Format(_BaseDataService.SchSeqReflush());
                dt = _dbTool.GetDataTable(sql);
                /***當Customer , Stage 為群組, 當超過1組以上, 則需要重新整理Sch Seq */
                if (dt.Rows.Count > 1)
                {
                    bReflush = true;
                }
               else
                {
                    bReflush = false;
                }
                /*
                if (dt.Rows.Count > 0)
                {
                    if (dt.Rows[0]["allCount"] is not null)
                    {
                        if (int.Parse(dt.Rows[0]["allCount"].ToString()) >= 1)
                            bReflush = true;
                    }
                    else
                    {
                        bReflush = true;
                    }
                }
                */


                if (!bReflush)
                {
                    sql = _BaseDataService.ReflushWhenSeqZeroStateWait();
                    dt = _dbTool.GetDataTable(sql);
                    if (dt.Rows.Count > 0)
                    {
                        bReflush = true;
                    }
                }

#if DEBUG
                _adstable = "ads_info";
#else
                _adstable = "semi_int.actl_ciserack_vw@semi_int";
#endif
                sql = string.Format(_BaseDataService.ReflushProcessLotInfo(_adstable));
                dt = _dbTool.GetDataTable(sql);
                int iSchSeq = 0;
                string sCustomer = "";
                string sLastStage = "";
                int isLock = 0;

                if (dt.Rows.Count > 0)
                {
                    string sql2 = "";
                    string sqlMsg = "";
                    sCustomer = dt.Rows[0]["CustomerName"].ToString();
                    sLastStage = dt.Rows[0]["Stage"].ToString();

                    if (GetLockState(_dbTool))
                        return bResult;

                    foreach (DataRow dr2 in dt.Rows)
                    {
                        string CarrierID = GetCarrierByLotID(_dbTool, dr2["lotid"].ToString());

                        EventQueue _evtQ = new EventQueue();
                        _evtQ.EventName = "LotEquipmentAssociateUpdate";
                        NormalTransferModel _transferModel = new NormalTransferModel();

                        if (dr2["Rtd_State"].ToString().Equals("INIT"))
                        {
                            sql2 = string.Format(_BaseDataService.UpdateLotInfoSchSeqByLotid(dr2["lotid"].ToString(), 0));
                            _dbTool.SQLExec(sql2, out sqlMsg, true);
                            continue;
                        }

                        if (!dr2["CustomerName"].ToString().Equals(""))
                        {

                            if (bReflush)
                            {
                                if (dr2["CustomerName"].ToString().Equals(sCustomer))
                                {
                                    if (dr2["Stage"].ToString().Equals(sLastStage))
                                        iSchSeq += 1;
                                    else
                                    {
                                        //換站點 Stage
                                        iSchSeq = 1;
                                        sLastStage = dr2["Stage"].ToString();
                                    }
                                }
                                else
                                {
                                    //換客戶
                                    iSchSeq = 1;
                                    sCustomer = dr2["CustomerName"].ToString();
                                    sLastStage = dr2["Stage"].ToString();
                                }
                                //update lot_info sch_seq
                                sql2 = string.Format(_BaseDataService.UpdateLotInfoSchSeqByLotid(dr2["lotid"].ToString(), iSchSeq));
                                _dbTool.SQLExec(sql2, out sqlMsg, true);
                            }
                        }

                        bool bagain = false;
                        if (dr2["EQUIP_ASSO"].ToString().Equals("N"))
                        {
                            if (TimerTool("minutes", dr2["lastmodify_dt"].ToString()) <= 1)
                            { continue; }
                            else
                                bagain = true;

                            if (bagain)
                            {
                                //EQUIP_ASSO 為 No, 立即執行檢查
                                _transferModel.CarrierID = CarrierID;
                                _transferModel.LotID = dr2["lotid"].ToString();
                                _evtQ.EventObject = _transferModel;
                                _evtQueue.Enqueue(_evtQ);
                            }
                        }
                        else
                        {
                            //Equipment Assoicate 為 Yes, 每5分鐘再次檢查一次
                            if (!dr2["EQUIPLIST"].ToString().Equals(""))
                            {
                                if (TimerTool("minutes", dr2["lastmodify_dt"].ToString()) <= 5)
                                { continue; }
                                else
                                    bagain = true;
                            }
                            else
                            {
                                if (TimerTool("minutes", dr2["lastmodify_dt"].ToString()) <= 1)
                                { continue; }
                                else
                                    bagain = true;
                            }

                            if (bagain)
                            {
                                _transferModel.CarrierID = CarrierID;
                                _transferModel.LotID = dr2["lotid"].ToString();
                                _evtQ.EventObject = _transferModel;
                                _evtQueue.Enqueue(_evtQ);
                            }
                        }
                    }

                }

                bResult = true;
            }
            catch (Exception ex)
            {
                bResult = false;
            }
            finally
            {
                if (dt != null)
                    dt.Dispose();
            }
            dt = null;

            return bResult;
        }
        public bool UpdateEquipmentAssociateToReady(DBTool _dbTool, ConcurrentQueue<EventQueue> _evtQueue)
        {

            DataTable dt = null;
            string sql = "";

            bool bResult = false;
            //_evtQueue = new ConcurrentQueue<EventQueue>();

            try
            {
                sql = string.Format(_BaseDataService.SelectTableProcessLotInfo());
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    DataTable dt2 = null;
                    DataRow dr3 = null;
                    string sql2 = "";
                    string sqlMsg = "";

                    foreach (DataRow dr2 in dt.Rows)
                    {
                        string CarrierID = GetCarrierByLotID(_dbTool, dr2["lotid"].ToString());

                        EventQueue _evtQ = new EventQueue();
                        _evtQ.EventName = "UpdateEquipmentAssociateToReady";
                        NormalTransferModel _transferModel = new NormalTransferModel();

                        if (dr2["CARRIER_ASSO"].ToString().Equals("Y") && dr2["EQUIP_ASSO"].ToString().Equals("Y") && !dr2["STATE"].ToString().Equals("READY"))
                        {
                            sql2 = string.Format(_BaseDataService.UpdateTableLotInfoToReadyByLotid(dr2["lotid"].ToString()));
                            _dbTool.SQLExec(sql2, out sqlMsg, true);
                        }
                    }

                }

                bResult = true;
            }
            catch (Exception ex)
            {
                bResult = false;
            }
            finally
            {
                if (dt != null)
                    dt.Dispose();
            }
            dt = null;

            return bResult;
        }
        /// <summary>
        /// MCS-Lite : Sent Command to MCS-Lite
        /// </summary>
        /// <param name="_configuration"></param>
        /// <param name="_logger"></param>
        /// <returns></returns>
        /// 
        public string GetCarrierByLotID(DBTool _dbTool, string lotid)
        {
            DataTable dt = null;
            string sql = "";
            string strResult = "";
            try
            {
                sql = string.Format(_BaseDataService.SelectTableCarrierAssociateByLotid(lotid));
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    DataRow dr = dt.Rows[0];
                    strResult = dr["carrier_id"].ToString();
                }
            }
            catch (Exception ex)
            {
                strResult = ex.Message;
            }
            finally
            {
                if (dt != null)
                    dt.Dispose(); 
            }
            dt = null;

            return strResult;
        }
        public string GetCarrierByPortId(DBTool _dbTool, string _portId)
        {
            DataTable dt = null;
            string sql = "";
            string strResult = "";
            string tmpLocate = "";
            string tmpPortNo = "";
            int iPortNo = 0;
            try
            {
                if (_portId.Contains("_"))
                {
                    tmpLocate = _portId.Split('_')[0];
                    tmpPortNo = _portId.Split('_')[1];
                    iPortNo = !tmpPortNo.Trim().Equals("") ? int.Parse(tmpPortNo.Replace("LP", "")) : 0;
                }

                if (iPortNo > 0)
                {
                    sql = string.Format(_BaseDataService.GetCarrierByLocate(tmpLocate, iPortNo));
                    dt = _dbTool.GetDataTable(sql);

                    if (dt.Rows.Count > 0)
                    {
                        DataRow dr = dt.Rows[0];
                        strResult = dr["carrier_id"].ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                strResult = ex.Message;
            }
            finally
            {
                if (dt != null)
                    dt.Dispose();
            }
            dt = null;

            return strResult;
        }
        public HttpClient GetHttpClient(IConfiguration _configuration, string _tarMcsSvr)
        {
            HttpClient client = new HttpClient();
            try
            {
                string cfgPath_ip = "";
                string cfgPath_port = "";
                string cfgPath_timeSpan = "HttpClientSpan";

                if (_tarMcsSvr.Equals(""))
                {
                    cfgPath_ip = string.Format("MCS:ip");
                    cfgPath_port = string.Format("MCS:port");
                    cfgPath_timeSpan = string.Format("MCS:timeSpan");
                }
                else
                {
                    cfgPath_ip = string.Format("MCS:{0}:ip", _tarMcsSvr.Trim());
                    cfgPath_port = string.Format("MCS:{0}:port", _tarMcsSvr.Trim());
                    cfgPath_timeSpan = string.Format("MCS:{0}:timeSpan", _tarMcsSvr.Trim());
                }

                //client.BaseAddress = new Uri(string.Format("http://{0}:{1}/", _configuration["MCS:ip"], _configuration["MCS:port"]));
                //client.Timeout = TimeSpan.Parse(_configuration["MCS:timeSpan"]);
                client.BaseAddress = new Uri(string.Format("http://{0}:{1}/", _configuration[cfgPath_ip], _configuration[cfgPath_port]));
                client.Timeout = TimeSpan.FromSeconds(double.Parse(_configuration[cfgPath_timeSpan]));
                client.DefaultRequestHeaders.Accept.Clear();
            }
            catch (Exception ex)
            {
                throw;
            }
            return client;
        }
        public string SendDispatchCommand(IConfiguration _configuration, string postData)
        {
            string responseHttpMsg = "";
            //建立 HttpClient //No Execute
            HttpClient client = GetHttpClient(_configuration, "");
            // 指定 authorization header
            JObject oToken = JObject.Parse(GetAuthrizationTokenfromMCS(_configuration));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oToken["token"].ToString());

            // 將 data 轉為 json
            string json = JsonConvert.SerializeObject(postData);
            // 將轉為 string 的 json 依編碼並指定 content type 存為 httpcontent
            HttpContent contentPost = new StringContent(json, Encoding.UTF8, "application/json");
            // 發出 post 並取得結果
            HttpResponseMessage response = client.PostAsync("api/command", contentPost).GetAwaiter().GetResult();
            // 將回應結果內容取出並轉為 string 再透過 linqpad 輸出
            responseHttpMsg = response.Content.ReadAsStringAsync().GetAwaiter().GetResult().ToString();

            client.Dispose();
            response.Dispose();
            return responseHttpMsg;
        }
        public bool SentDispatchCommandtoMCS(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, List<string> ListCmds)
        {
            bool bResult = false;
            string tmpState = "";
            string tmpMsg = "";
            string exceptionCmdId = "";
            string issueLotid = "";

            HttpClient client;
            HttpResponseMessage response;

            string tableOrder = "";
            string _keyOfEnv = "";
            string _keyRTDEnv = "";
            string _startTime = "";
            string tmpCarrierId = "";

            try
            {
                _keyRTDEnv = _configuration["RTDEnvironment:type"];
                _keyOfEnv = string.Format("RTDEnvironment:commandsTable:{0}", _keyRTDEnv);
                //"RTDEnvironment:commandsTable:PROD"
                tableOrder = _configuration[_keyOfEnv] is null ? "workinprocess_sch" : _configuration[_keyOfEnv];

                if (_keyRTDEnv.Equals("UAT"))
                {
                    _logger.Info(string.Format("Pass. It's {0} Env.", _keyRTDEnv));
                    return true;
                }

                if (ListCmds.Count > 0)
                {
                    _logger.Trace(string.Format("[SentDispatchCommandtoMCS]:{0}", "IN"));

                    //Execute 3
                    client = GetHttpClient(_configuration, "");
                    // Add an Accept header for JSON format.
                    // 為JSON格式添加一個Accept表頭
                    client.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));
                    //Add Token
                    //JObject oToken = JObject.Parse(GetAuthrizationTokenfromMCS(_configuration));
                    //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oToken["token"].ToString());
                    response = new HttpResponseMessage();

                    DataTable dt = null;
                    DataTable dtTemp = null;
                    DataRow[] dr = null;
                    DataRow[] dr2 = null;
                    string sql = "";
                    bool bRetry = false;
                    string _cmdCurrentState = "";

                    foreach (string theCmdId in ListCmds)
                    {
                        string tmpCmdType = "";
                        exceptionCmdId = theCmdId;
                        _logger.Trace(string.Format("[SentDispatchCommandtoMCS]: Command ID [{0}]", theCmdId));
                        //// 查詢資料
                        ///
                        dt = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSchByCmdId(theCmdId, tableOrder));

                        if (dt.Rows.Count <= 0)
                            continue;
                        //已有其它線程正在處理

                        _startTime = dt.Rows[0]["start_dt"] is null ? "" : dt.Rows[0]["start_dt"].ToString().Trim().Equals("") ? "" : Convert.ToDateTime(dt.Rows[0]["start_dt"].ToString()).ToString("yyyy/MM/dd HH:mm:ss");

                        if (!_startTime.Trim().Equals(""))
                        {
                            if (Convert.ToDateTime(_startTime) > DateTime.Now)
                                continue;
                        }

                        tmpCmdType = dt.Rows[0]["CMD_TYPE"] is null ? "" : dt.Rows[0]["CMD_TYPE"].ToString();
                        _cmdCurrentState = dt.Rows[0]["CMD_CURRENT_STATE"] is null ? "" : dt.Rows[0]["CMD_CURRENT_STATE"].ToString();

                        if (dt.Rows[0]["REPLACE"].ToString().Equals("1"))
                        {
                            if (!_cmdCurrentState.Equals("NearComp"))
                            {
                                if (dt.Rows.Count <= 1)
                                    continue;
                            }
                        }

                        DateTime curDT = DateTime.Now;
                        if (int.Parse(dt.Rows[0]["ISLOCK"].ToString()).Equals(1))
                        {
                            try
                            {
                                string createDateTime = dt.Rows[0]["CREATE_DT"].ToString();
                                string lastDateTime = dt.Rows[0]["LASTMODIFY_DT"].ToString();
                                tmpCarrierId = dt.Rows[0]["CARRIERID"].ToString();
                                bool bHaveSent = dt.Rows[0]["CMD_CURRENT_STATE"].ToString().Equals("Init") ? true : false;

                                if (bHaveSent)
                                    continue;  //已送至TSC, 不主動刪除。等待TSC回傳結果！或人員自行操作刪除

                                if (!lastDateTime.Equals(""))
                                {
                                    bRetry = false;
                                    curDT = DateTime.Now;
                                    DateTime createDT = Convert.ToDateTime(createDateTime);
                                    DateTime tmpDT = Convert.ToDateTime(lastDateTime);
                                    TimeSpan minuteSpan = new TimeSpan(tmpDT.Ticks - curDT.Ticks);
                                    TimeSpan totalSpan = new TimeSpan(createDT.Ticks - curDT.Ticks);
                                    if (Math.Abs(minuteSpan.TotalMinutes) < 2)
                                    {
                                        continue;
                                    }
                                    else if (Math.Abs(minuteSpan.TotalMinutes) >= 2)
                                    {
                                        bRetry = true;
                                    }
                                    else if (Math.Abs(totalSpan.TotalMinutes) > 30)
                                    {
                                        dr = dt.Select("CMD_CURRENT_STATE not in ('Init', 'Running', 'Success','NearComp')");

                                        if (dr.Length > 0)
                                        {
                                            //嘗試發送, 大於30分鐘仍未送出, 刪除
                                            _dbTool.SQLExec(_BaseDataService.UpdateTableWorkInProcessSchHisByCmdId(theCmdId), out tmpMsg, true);
                                            _dbTool.SQLExec(_BaseDataService.DeleteWorkInProcessSchByCmdId(theCmdId, tableOrder), out tmpMsg, true);
                                            _logger.Trace(string.Format("[SentDispatchCommandtoMCS]: Delete Workinprocess_sch command[{0}] cause overtime 30 mints of retry", theCmdId));

                                            if (tmpCarrierId.Equals("*") || tmpCarrierId.Equals(""))
                                            { }
                                            else
                                            {
                                                if (_dbTool.SQLExec(_BaseDataService.UpdateTableReserveCarrier(tmpCarrierId, false), out tmpMsg, true))
                                                { }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        //Nothing
                                    }

                                    if (bRetry)
                                    {
                                        dr = dt.Select("CMD_CURRENT_STATE in ('Failed', ' ', '','Received')");
                                        if (dr.Length > 0)
                                        {
                                            _dbTool.SQLExec(_BaseDataService.UpdateUnlockWorkInProcessSchByCmdId(theCmdId, tableOrder), out tmpMsg, true);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            { }

                            continue;
                        }
                        else
                            _dbTool.SQLExec(_BaseDataService.UpdateLockWorkInProcessSchByCmdId(theCmdId, tableOrder), out tmpMsg, true);

                        dr = dt.Select("CMD_CURRENT_STATE in (' ','NearComp')");
                        if (dr.Length <= 0)
                        {
                            try 
                            {
                                _logger.Debug(string.Format("[SendTransferCommand][{0}][{1}]", theCmdId, "CMD_CURRENT_STATE=' '"));
                            }
                            catch(Exception ex) { }
                            

                            sql = _BaseDataService.UpdateTableWorkInProcessSchByCmdId("Received", curDT.ToString("yyyy-M-d hhmmss"), theCmdId, tableOrder);
                            try
                            {
                                curDT = DateTime.Now;
                                dr2 = dt.Select("CMD_CURRENT_STATE='Failed'");
                                if (dr2.Length <= 0)
                                    continue;
                            }
                            catch (Exception ex)
                            {
                                curDT = DateTime.Now;
                                _dbTool.SQLExec(sql, out tmpMsg, true);
                            }
                        }
                        else
                        {
                            try
                            {
                                _logger.Debug(string.Format("[SendTransferCommand][{0}][{1}][{2}]", theCmdId, dr[0]["CMD_CURRENT_STATE"], "CMD_CURRENT_STATE not ' '"));
                            }
                            catch (Exception ex) { }
                        }
                        //Pre-Transfer
                        string tmpPriority = dt.Rows[0]["PRIORITY"].ToString();
                        _logger.Debug(string.Format("[SendTransferCommand][{0}][{1}]", tmpCmdType, tmpPriority));

                        if (!tmpCarrierId.Equals(""))
                        {
                            int iPriority = 0;
                            sql = _BaseDataService.QueryLotInfoByCarrierID(tmpCarrierId);
                            dtTemp = _dbTool.GetDataTable(sql);

                            if (dtTemp.Rows.Count > 0)
                            {
                                iPriority = dtTemp.Rows[0]["PRIORITY"] is null ? 0 : int.Parse(dtTemp.Rows[0]["PRIORITY"].ToString());
                            }

                            if(iPriority >= 70)
                            {
                                tmpPriority = iPriority.ToString();
                            }
                            else
                            {
                                if (tmpCmdType.ToUpper().Equals("PRE-TRANSFER"))
                                {
                                    if (iPriority > 20)
                                    {
                                        tmpPriority = iPriority.ToString();
                                    }
                                    else
                                    {
                                        tmpPriority = "20";
                                    }
                                }
                                else
                                {
                                    tmpPriority = iPriority.ToString();
                                }
                            }
                        }
                        else 
                        {
                            if (tmpCmdType.ToUpper().Equals("PRE-TRANSFER"))
                                tmpPriority = "20";
                        }

                        _logger.Debug(string.Format("[SendTransferCommand][{0}][{1}]", tmpCmdType, tmpPriority));

                        string tmp00 = "{{0}, {1}, {2}, {3}}";
                        string tmp01 = string.Format("\"CommandID\": \"{0}\"", theCmdId);
                        string tmp02 = string.Format("\"Priority\": \"{0}\"", tmpCmdType.ToUpper().Equals("PRE-TRANSFER") ? "20" : dt.Rows[0]["PRIORITY"].ToString());
                        string tmp03 = string.Format("\"Replace\": \"{0}\"", dt.Rows[0]["REPLACE"].ToString());
                        string strTransferCmd = "\"CommandID\": \"" + theCmdId + "\",\"Priority\": " + tmpPriority + ",\"Replace\": " + dt.Rows[0]["REPLACE"].ToString() + "{0}";
                        string strTransCmd = "";
                        string tmpUid = "";
                        string tmpCarrierID = "";
                        //string tmpCmdType = "";
                        string tmpLoadCmdType = "";
                        string tmpLoadCarrierID = "";
                        string tmpDest = "";
                        foreach (DataRow tmDr in dt.Rows)
                        {
                            tmpDest = "";

                            tmpUid = tmDr["UUID"].ToString();
                            tmpCmdType = tmDr["CMD_TYPE"].ToString();
                            tmpDest = tmDr["DEST"].ToString();

                            if (tmpCmdType.ToUpper().Equals("LOAD"))
                                tmpLoadCmdType = tmpCmdType;

                            //if (!strTransCmd.Equals(""))
                            //{
                            //strTransCmd = strTransCmd + ",";
                            //}
                            tmpCarrierID = tmDr["CARRIERID"].ToString().Equals("*") ? "" : tmDr["CARRIERID"].ToString();

                            if (strTransCmd.Equals(""))
                            {
                                strTransCmd = strTransCmd + "{" +
                                       "\"CarrierID\": \"" + tmpCarrierID + "\", " +
                                       "\"Source\": \"" + tmDr["SOURCE"].ToString() + "\", " +
                                       "\"Dest\": \"" + tmDr["DEST"].ToString() + "\", " +
                                       "\"LotID\": \"" + tmDr["LotID"].ToString() + "\", " +
                                       "\"Quantity\":" + tmDr["Quantity"].ToString() + ", " +
                                       "\"Total\":" + tmDr["Total"].ToString() + ", " +
                                       "\"CarrierType\": \"" + tmDr["CARRIERTYPE"].ToString() + "\"} ";
                            }
                            else
                            {
                                strTransCmd = strTransCmd + ", {" +
                                            "\"CarrierID\": \"" + tmpCarrierID + "\", " +
                                            "\"Source\": \"" + tmDr["SOURCE"].ToString() + "\", " +
                                            "\"Dest\": \"" + tmDr["DEST"].ToString() + "\", " +
                                            "\"LotID\": \"" + tmDr["LotID"].ToString() + "\", " +
                                            "\"Quantity\":" + tmDr["Quantity"].ToString() + ", " +
                                            "\"Total\":" + tmDr["Total"].ToString() + ", " +
                                            "\"CarrierType\": \"" + tmDr["CARRIERTYPE"].ToString() + "\"} ";
                            }
                        }


                        string tmp2Cmd = string.Format(", \"Transfer\": [{0}]", strTransCmd);
                        Uri gizmoUri = null;
                        string strGizmo = "";

                        string tmp3Cmd = string.Format(strTransferCmd.ToString(), tmp2Cmd);
                        string tmp04 = "{" + tmp3Cmd + "}";

                        var gizmo = JObject.Parse(tmp04);

                        _logger.Trace(string.Format("[SendTransferCommand]:{0}", tmp04));
                        response = client.PostAsJsonAsync("api/SendTransferCommand", gizmo).Result;
                        //response = client.PostAsJsonAsync("api/SendTransferCommand", tmp04).Result;
                        var respContent = response.Content.ReadAsStringAsync();
                        string tmpResult = respContent.Result;

                        if (response != null)
                        {
                            //不為response, 即表示total加1 || 改成MCS回報後才統計總數
                            if (response.IsSuccessStatusCode)
                            {
                                //需等待回覆後再記錄
                                _logger.Info(string.Format("Info: SendCommand [{0}][{1}] is OK. {2}", theCmdId, tmpDest, response.StatusCode));
                                _dbTool.SQLExec(_BaseDataService.UpdateTableWorkInProcessSchHisByCmdId(theCmdId), out tmpMsg, true);
                                _dbTool.SQLExec(_BaseDataService.UpdateTableWorkInProcessSchByCmdId("Initial", DateTime.Now.ToString("yyyy-M-d hh:mm:ss"), theCmdId, tableOrder), out tmpMsg, true);
                                //新增一筆Total Record
                                //cmd_Type
                                if (tmpLoadCmdType.ToUpper().Equals("LOAD") && !tmpLoadCarrierID.Equals(""))
                                {
                                    _dbTool.SQLExec(_BaseDataService.InsertRTDStatisticalRecord(curDT.ToString("yyyy-MM-dd HH:mm:ss"), theCmdId, "T"), out tmpMsg, true);
                                }

                                foreach (DataRow tmDr in dt.Rows)
                                {

                                    string tmpSql = _BaseDataService.SelectTableCarrierAssociateByCarrierID(tmDr["CARRIERID"].ToString());
                                    DataTable dt2 = _dbTool.GetDataTable(tmpSql);
                                    if (dt2.Rows.Count > 0)
                                    {
                                        string tmpLotid = dt2.Rows[0]["lot_id"].ToString().Trim();
                                        _dbTool.SQLExec(_BaseDataService.UpdateTableLotInfoState(tmpLotid, "PROC"), out tmpMsg, true);
                                    }
                                }

                                bResult = true;
                                tmpState = "OK";
                                tmpMsg = "";
                            }
                            else
                            {
                                //_logger.Info(string.Format("Info: SendCommand [{0}] Failed. {1}", theCmdId, response.RequestMessage));
                                _logger.Info(string.Format("Info: SendCommand [{0}][{1}] Failed. {2}", theCmdId, tmpDest, response.RequestMessage));
                                //傳送失敗, 即計算為Failed
                                //新增一筆Total Record
                                //_dbTool.SQLExec(_BaseDataService.InsertRTDStatisticalRecord(curDT.ToString("yyyy-MM-dd HH:mm:ss"), theCmdId, "F"), out tmpMsg, true);

                                //_dbTool.SQLExec(_BaseDataService.UpdateTableWorkInProcessSchHisByUId(tmpUid), out tmpMsg, true);
                                _dbTool.SQLExec(_BaseDataService.UpdateTableWorkInProcessSchByCmdId("Failed", DateTime.Now.ToString("yyyy-M-d hh:mm:ss"), theCmdId, tableOrder), out tmpMsg, true);
                                _dbTool.SQLExec(_BaseDataService.UpdateUnlockWorkInProcessSchByCmdId(theCmdId, tableOrder), out tmpMsg, true);
                                //Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                                bResult = false;
                                tmpState = "NG";
                                tmpMsg = string.Format("State Code : {0}, Reason : {1}.", response.StatusCode, response.RequestMessage);
                                //_logger.Info(string.Format("Info: SendCommand Failed. {0}", tmpMsg));
                                _logger.Info(string.Format("SendCommand [{0}] Failed Message: {1}", theCmdId, tmpMsg));

                                string[] _argvs = new string[] { theCmdId, "", "" };
                                if (CallRTDAlarm(_dbTool, 10100, _argvs))
                                {
                                    _logger.Info(string.Format("Info: SendCommand Failed. {0}", tmpMsg));
                                }
                            }
                        }
                        else
                        {
                            //沒有立刻回應時, 多等待0.5秒補送1次後, 仍無response才判斷逾時
                            Thread.Sleep(500);
                            _logger.Trace(string.Format("[SendTransferCommand][Resend][{0}]", tmp04));
                            response = client.PostAsJsonAsync("api/SendTransferCommand", gizmo).Result;

                            if (response != null)
                            {

                                //不為response, 即表示total加1 || 改成MCS回報後才統計總數
                                if (response.IsSuccessStatusCode)
                                {
                                    //需等待回覆後再記錄
                                    _logger.Info(string.Format("Info: Retry SendCommand [{0}] is OK. {1}", theCmdId, response.StatusCode));
                                    _dbTool.SQLExec(_BaseDataService.UpdateTableWorkInProcessSchHisByCmdId(theCmdId), out tmpMsg, true);
                                    _dbTool.SQLExec(_BaseDataService.UpdateTableWorkInProcessSchByCmdId("Initial", DateTime.Now.ToString("yyyy-M-d hh:mm:ss"), theCmdId, tableOrder), out tmpMsg, true);
                                    //新增一筆Total Record
                                    //cmd_Type
                                    if (tmpLoadCmdType.ToUpper().Equals("LOAD") && !tmpLoadCarrierID.Equals(""))
                                    {
                                        _dbTool.SQLExec(_BaseDataService.InsertRTDStatisticalRecord(curDT.ToString("yyyy-MM-dd HH:mm:ss"), theCmdId, "T"), out tmpMsg, true);
                                    }

                                    foreach (DataRow tmDr in dt.Rows)
                                    {

                                        string tmpSql = _BaseDataService.SelectTableCarrierAssociateByCarrierID(tmDr["CARRIERID"].ToString());
                                        DataTable dt2 = _dbTool.GetDataTable(tmpSql);
                                        if (dt2.Rows.Count > 0)
                                        {
                                            string tmpLotid = dt2.Rows[0]["lot_id"].ToString().Trim();
                                            _dbTool.SQLExec(_BaseDataService.UpdateTableLotInfoState(tmpLotid, "PROC"), out tmpMsg, true);
                                        }
                                    }

                                    bResult = true;
                                    tmpState = "OK";
                                    tmpMsg = "";
                                }
                                else
                                {
                                    _logger.Info(string.Format("Info: Retry SendCommand [{0}] Failed. {1}", theCmdId, response.StatusCode));
                                    //傳送失敗, 即計算為Failed
                                    //新增一筆Total Record
                                    //_dbTool.SQLExec(_BaseDataService.InsertRTDStatisticalRecord(curDT.ToString("yyyy-MM-dd HH:mm:ss"), theCmdId, "F"), out tmpMsg, true);

                                    //_dbTool.SQLExec(_BaseDataService.UpdateTableWorkInProcessSchHisByUId(tmpUid), out tmpMsg, true);
                                    _dbTool.SQLExec(_BaseDataService.UpdateTableWorkInProcessSchByCmdId("Failed", DateTime.Now.ToString("yyyy-M-d hh:mm:ss"), theCmdId, tableOrder), out tmpMsg, true);
                                    _dbTool.SQLExec(_BaseDataService.UpdateUnlockWorkInProcessSchByCmdId(theCmdId, tableOrder), out tmpMsg, true);
                                    //Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                                    bResult = false;
                                    tmpState = "NG";
                                    tmpMsg = string.Format("State Code : {0}, Reason : {1}.", response.StatusCode, response.RequestMessage);
                                    //_logger.Info(string.Format("Info: SendCommand Failed. {0}", tmpMsg));
                                    _logger.Info(string.Format("SendCommand [{0}] Failed Message: {1}", theCmdId, tmpMsg));

                                    string[] _argvs = new string[] { theCmdId, "", "" };
                                    if (CallRTDAlarm(_dbTool, 10100, _argvs))
                                    {
                                        _logger.Info(string.Format("Info: SendCommand Failed. {0}", tmpMsg));
                                    }
                                }
                            }
                            else
                            {
                                bResult = false;
                                tmpState = "NG";
                                tmpMsg = string.Format("[SendTransferCommand][{0}][{1}]", "應用程式呼叫 API 發生異常", "MCS response timeout.");
                                _logger.Trace(string.Format("[SendTransferCommand][{0}][{1}]", tmpMsg, "MCS response timeout."));
                            }
                        }

                        //if(tmpState.Equals("NG"))
                        //_dbTool.SQLExec(_BaseDataService.UpdateUnlockWorkInProcessSchByCmdId(theCmdId), out tmpMsg, true);


                    }

                    //Release Resource
                    client.Dispose();
                    response.Dispose();
                }
            }
            catch (ArgumentOutOfRangeException ex)
            {
                tmpMsg = string.Format("ArgumentOutOfRangeException [{0}]: ex.Message [{1}]", "SentDispatchCommandtoMCS", ex.Message);
                _logger.Info(tmpMsg);
                tmpMsg = "";
                _dbTool.SQLExec(_BaseDataService.UpdateTableWorkInProcessSchByCmdId(" ", DateTime.Now.ToString("yyyy-M-d hh:mm:ss"), exceptionCmdId, tableOrder), out tmpMsg, true);
                if (!tmpMsg.Equals(""))
                    _logger.Info(tmpMsg);
                tmpMsg = "";
                _dbTool.SQLExec(_BaseDataService.UpdateUnlockWorkInProcessSchByCmdId(exceptionCmdId, tableOrder), out tmpMsg, true);
                if (!tmpMsg.Equals(""))
                    _logger.Info(tmpMsg);
                string[] _argvs = new string[] { exceptionCmdId, "", "" };
                if (CallRTDAlarm(_dbTool, 10101, _argvs))
                {
                    _logger.Info(string.Format("Info: SendCommand Failed. {0}", ex.Message));
                }
            }
            catch (Exception ex)
            {
                //_dbTool.SQLExec(_BaseDataService.UpdateUnlockWorkInProcessSchByCmdId(exceptionCmdId), out tmpMsg, true);
                //需要Alarm to Front UI
                tmpMsg = string.Format("Info: SendCommand Failed. Exception is {0}, ", ex.Message);
                _logger.Info(tmpMsg);
                tmpMsg = "";
                _dbTool.SQLExec(_BaseDataService.UpdateTableWorkInProcessSchByCmdId(" ", DateTime.Now.ToString("yyyy-M-d hh:mm:ss"), exceptionCmdId, tableOrder), out tmpMsg, true);
                if (!tmpMsg.Equals(""))
                    _logger.Info(tmpMsg);
                tmpMsg = "";
                _dbTool.SQLExec(_BaseDataService.UpdateUnlockWorkInProcessSchByCmdId(exceptionCmdId, tableOrder), out tmpMsg, true);
                if (!tmpMsg.Equals(""))
                    _logger.Info(tmpMsg);
                string[] _argvs = new string[] { exceptionCmdId, "", "" };
                if (CallRTDAlarm(_dbTool, 10101, _argvs))
                {
                    _logger.Info(string.Format("Info: SendCommand Failed. {0}", ex.Message));
                }
            }

            //_logger.Info(string.Format("Info: Result is {0}, Reason :", foo.State, foo.Message));

            return bResult;
        }
        public bool SentCommandtoToMCS(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, List<string> ListCmds)
        {
            bool bResult = false;
            string tmpState = "";
            string tmpMsg = "";
            NormalTransferModel transferCmds = new NormalTransferModel();
            TransferList theTransfer = new TransferList();
            //Http Object
            HttpClient client;
            HttpResponseMessage response;

            string tableOrder = "";
            string _keyOfEnv = "";
            string _keyRTDEnv = "";

            try
            {
                _keyRTDEnv = _configuration["RTDEnvironment:type"];
                _keyOfEnv = string.Format("RTDEnvironment:commandsTable:{0}", _keyRTDEnv);
                //"RTDEnvironment:commandsTable:PROD"
                tableOrder = _configuration[_keyOfEnv] is null ? "workinprocess_sch" : _configuration[_keyOfEnv];

                if (ListCmds.Count > 0)
                {
                    //No Execute
                    client = GetHttpClient(_configuration, "");
                    // Add an Accept header for JSON format.
                    // 為JSON格式添加一個Accept表頭
                    client.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));
                    //JObject oToken = JObject.Parse(GetAuthrizationTokenfromMCS(_configuration));
                    //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oToken["token"].ToString());
                    response = new HttpResponseMessage();

                    DataTable dt = null;
                    DataRow[] dr = null;
                    string sql = "";
                    int iRec = 0;
                    foreach (string theUuid in ListCmds)
                    {
                        //// 查詢資料
                        dt = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSchByCmdId(theUuid, tableOrder));
                        dr = dt.Select("CMD_CURRENT_STATE='Initial'");
                        if (dr.Length <= 0)
                        {
                            _logger.Info(string.Format("no find the uuid [{0}]", theUuid));
                            continue;
                        }

                        //組織派送指令
                        if (transferCmds.CommandID.Equals(""))
                        {
                            transferCmds.CommandID = "";
                            transferCmds.CarrierID = "";
                            transferCmds.Priority = 0;
                            transferCmds.Replace = 0;
                        }

                        theTransfer = new TransferList();
                        theTransfer.Source = dr[iRec]["Source"].ToString();
                        theTransfer.Dest = dr[iRec]["Dest"].ToString();
                        theTransfer.LotID = dr[iRec]["LotID"].ToString();
                        theTransfer.Quantity = 0;
                        theTransfer.CarrierType = dr[iRec]["CarrierType"].ToString();

                        transferCmds.Transfer.Add(theTransfer);
                        iRec++;

                        transferCmds.Replace = iRec;
                    }

                    Uri gizmoUri = null;

                    response = client.PostAsJsonAsync("api/command", transferCmds).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        gizmoUri = response.Headers.Location;
                        bResult = true;
                        tmpState = "OK";
                        tmpMsg = "";
                    }
                    else
                    {
                        //Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                        bResult = false;
                        tmpState = "NG";
                        tmpMsg = string.Format("State Code : {0}, Reason : {1}.", response.StatusCode, response.ReasonPhrase);
                    }

                    //Release Resource
                    client.Dispose();
                    response.Dispose();
                }
            }
            catch (Exception ex)
            {

            }

            //_logger.Info(string.Format("Info: Result is {0}, Reason :", foo.State, foo.Message));

            return bResult;
        }
        public bool SentTransferCommandtoToMCS(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, TransferList ListCmds, out string _tmpMsg)
        {
            bool bResult = false;
            string tmpState = "";
            string tmpMsg = "";
            NormalTransferModel transferCmds = new NormalTransferModel();
            TransferList theTransfer = new TransferList();
            //Http Object
            HttpClient client;
            HttpResponseMessage response;
            _tmpMsg = "";

            string tableOrder = "";
            string _keyOfEnv = "";
            string _keyRTDEnv = "";

            try
            {
                _keyRTDEnv = _configuration["RTDEnvironment:type"];
                _keyOfEnv = string.Format("RTDEnvironment:commandsTable:{0}", _keyRTDEnv);
                //"RTDEnvironment:commandsTable:PROD"
                tableOrder = _configuration[_keyOfEnv] is null ? "workinprocess_sch" : _configuration[_keyOfEnv];

                if (_keyRTDEnv.Equals("UAT"))
                {
                    _logger.Info(string.Format("Pass. It's {0} Env.", _keyRTDEnv));
                    return true;
                }

                if (!ListCmds.CarrierID.Equals(""))
                {
                    //No Execute
                    client = GetHttpClient(_configuration, "");
                    // Add an Accept header for JSON format.
                    // 為JSON格式添加一個Accept表頭
                    client.Timeout = TimeSpan.FromMinutes(1);
                    client.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));
                    //JObject oToken = JObject.Parse(GetAuthrizationTokenfromMCS(_configuration));
                    //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oToken["token"].ToString());
                    response = new HttpResponseMessage();

                    DataTable dt = null;
                    DataRow[] dr = null;
                    string sql = "";
                    int iRec = 0;

                    //foreach (string theUuid in ListCmds)
                    if(ListCmds is not null)
                    {
                        //// 查詢資料
                        /*
                        dt = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSchByCmdId(""));
                        dr = dt.Select("CMD_CURRENT_STATE='Initial'");
                        if (dr.Length <= 0)
                        {
                            _logger.Info(string.Format("no find the uuid [{0}]", theUuid));
                            continue;
                        }*/

                        //組織派送指令
                        if (transferCmds is null)
                        {
                            transferCmds = new NormalTransferModel();
                            transferCmds.CommandID = Tools.GetCommandID(_dbTool);
                            transferCmds.CarrierID = ListCmds.CarrierID;
                            transferCmds.Priority = 0;
                            transferCmds.Replace = 0;
                            transferCmds.Transfer = new List<TransferList>();
                        }
                        else
                        {
                            transferCmds.CommandID = Tools.GetCommandID(_dbTool);
                            transferCmds.CarrierID = ListCmds.CarrierID;
                            transferCmds.Priority = 0;
                            transferCmds.Replace = 0;
                            transferCmds.Transfer = new List<TransferList>();
                        }

                        theTransfer = new TransferList();
                        theTransfer.Source = ListCmds.Source;
                        theTransfer.Dest = ListCmds.Dest;
                        theTransfer.LotID = ListCmds.LotID;
                        theTransfer.Quantity = ListCmds.Quantity;
                        theTransfer.CarrierType = ListCmds.CarrierType;

                        transferCmds.Transfer.Add(theTransfer);
                        iRec++;

                        transferCmds.Replace = iRec;
                    }

                    Uri gizmoUri = null;

                    response = client.PostAsJsonAsync("api/command", transferCmds).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        gizmoUri = response.Headers.Location;
                        bResult = true;
                        tmpState = "OK";
                        tmpMsg = "";
                    }
                    else
                    {
                        //Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                        bResult = false;
                        tmpState = "NG";
                        tmpMsg = string.Format("State Code : {0}, Reason : {1}.", response.StatusCode, response.ReasonPhrase);
                    }

                    //Release Resource
                    client.Dispose();
                    response.Dispose();
                }
            }
            catch (ArgumentOutOfRangeException ex)
            {
                _tmpMsg = ex.Message;
            }
            catch (Exception ex)
            {
                _tmpMsg = ex.Message;
            }

            //_logger.Info(string.Format("Info: Result is {0}, Reason :", foo.State, foo.Message));

            return bResult;
        }
        public APIResult SentCommandtoMCS(IConfiguration _configuration, ILogger _logger, List<string> agrs)
        {
            APIResult foo;
            bool bResult = false;
            string tmpState = "";
            string tmpMsg = "";
            int iRemotecmd = int.Parse(agrs[0]);
            string remoteCmd = "";
            string extCmd = "";
            //agrs : remote cmd, command id, vehicleId
            HttpClient client;
            HttpResponseMessage response;

            string tableOrder = "";
            string _keyOfEnv = "";
            string _keyRTDEnv = "";

            try
            {
                _keyRTDEnv = _configuration["RTDEnvironment:type"];
                _keyOfEnv = string.Format("RTDEnvironment:commandsTable:{0}", _keyRTDEnv);
                //"RTDEnvironment:commandsTable:PROD"
                tableOrder = _configuration[_keyOfEnv] is null ? "workinprocess_sch" : _configuration[_keyOfEnv];

                //Execute 1 
                client = GetHttpClient(_configuration, "");
                // Add an Accept header for JSON format.
                // 為JSON格式添加一個Accept表頭
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
                JObject oToken = JObject.Parse(GetAuthrizationTokenfromMCS(_configuration));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oToken["token"].ToString());


                response = client.GetAsync("api/command").Result;  // Blocking call（阻塞调用）! 

                //iRemotecmd: system: [1.pause, 2.resume], transfer: [3.abort, 4.cancel],  vihicle: [5. release, 6.charge, 7.assert, 8.sweep]
                switch (iRemotecmd)
                {
                    case 1:
                        remoteCmd = "pause";
                        extCmd = "";
                        break;
                    case 2:
                        remoteCmd = "resume";
                        extCmd = " ";
                        break;
                    case 3:
                        remoteCmd = "abort";
                        extCmd = string.Format(", CommandID: '{0}'", agrs[1]);
                        break;
                    case 4:
                        remoteCmd = "cancel";
                        extCmd = string.Format(", CommandID: '{0}'", agrs[1]);
                        break;
                    case 5:
                        remoteCmd = "release";
                        extCmd = string.Format(", parameter: { VehicleID: '{0}'}", agrs[1]);
                        break;
                    case 6:
                        remoteCmd = "charge";
                        extCmd = string.Format(", parameter: { VehicleID: '{0}'}", agrs[1]);
                        break;
                    case 7:
                        remoteCmd = "assert";
                        extCmd = string.Format(", parameter: { VehicleID: '{0}'}", agrs[1]);
                        break;
                    case 8:
                        remoteCmd = "sweep";
                        extCmd = string.Format(", parameter: { VehicleID: '{0}'}", agrs[1]);
                        break;
                    default:
                        remoteCmd = "cancel";
                        extCmd = string.Format(", CommandID: '{0}'", agrs[1]);
                        break;
                }

                // Create a new product
                // 创建一个新产品
                //var gizmo = new Product() { Name = "Gizmo", Price = 100, Category = "Widget" };
                Uri gizmoUri = null;
                string strGizmo = "";
                if (extCmd.Equals(""))
                {
                    strGizmo = "{ \"remote_cmd\": \"{0}\"}";

                    try
                    {
                        strGizmo = string.Format(strGizmo, remoteCmd.ToString());
                    }
                    catch (Exception ex)
                    { tmpMsg = ex.Message; }
                }
                else
                {
                    strGizmo = "{ remote_cmd: \"{0}\",  {1} }";

                    strGizmo = string.Format(strGizmo, remoteCmd);
                }

                strGizmo = "{ \"remote_cmd\": \"pause\" }";
                var gizmo = JObject.Parse(strGizmo);
                response = client.PostAsJsonAsync("api/command", gizmo).Result;

                if (response.IsSuccessStatusCode)
                {
                    gizmoUri = response.Headers.Location;
                    bResult = true;
                    tmpState = "OK";
                    tmpMsg = "";
                }
                else
                {
                    //Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                    bResult = false;
                    tmpState = "NG";
                    tmpMsg = string.Format("State Code : {0}, Reason : {1}.", response.StatusCode, response.ReasonPhrase);
                    _logger.Debug(tmpMsg);
                }

                //PostAsJsonAsync is an extension method defined in System.Net.Http.HttpClientExtensions.It is equivalent to the following:
                //PostAsJsonAsync是在System.Net.Http.HttpClientExtensions中定义的一个扩展方法。上述代码与以下代码等效：

                //var product = new Product() { Name = "Gizmo", Price = 100, Category = "Widget" };

                //// Create the JSON formatter.
                //// 创建JSON格式化器。
                //MediaTypeFormatter jsonFormatter = new JsonMediaTypeFormatter();

                //// Use the JSON formatter to create the content of the request body.
                //// 使用JSON格式化器创建请求体内容。
                //HttpContent content = new ObjectContent<Product>(product, jsonFormatter);

                //// Send the request.
                //// 发送请求。
                //var resp = client.PostAsync("api/products", content).Result;

                foo = new APIResult()
                {
                    Success = bResult,
                    State = tmpState,
                    Message = tmpMsg
                };

                client.Dispose();
                response.Dispose();
            }
            catch (ArgumentOutOfRangeException ex)
            {
                foo = new APIResult()
                {
                    Success = false,
                    State = "NG",
                    Message = ex.Message
                };
                _logger.Debug(string.Format("Exception: {0}", foo.Message));
            }
            catch (Exception ex)
            {
                foo = new APIResult()
                {
                    Success = false,
                    State = "NG",
                    Message = ex.Message
                };
                _logger.Debug(string.Format("Exception: {0}", foo.Message));
            }

            _logger.Info(string.Format("Info: Result is {0}, Reason :", foo.State, foo.Message));

            return foo;
        }
        public APIResult SentCommandtoMCSByModel(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, string _model, List<string> args)
        {
            APIResult foo;
            bool bResult = false;
            string tmpFuncName = "SentCommandtoMCSByModel";
            string tmpState = "";
            string tmpMsg = "";
            string remoteCmd = "None";

            HttpClient client;
            HttpResponseMessage response;

            string tableOrder = "";
            string _keyOfEnv = "";
            string _keyRTDEnv = "";
            JObject jRespData = new JObject();
            string _tmpKey = "";

            try
            {
                _keyRTDEnv = _configuration["RTDEnvironment:type"];
                _keyOfEnv = string.Format("RTDEnvironment:commandsTable:{0}", _keyRTDEnv);
                //"RTDEnvironment:commandsTable:PROD"
                tableOrder = _configuration[_keyOfEnv] is null ? "workinprocess_sch" : _configuration[_keyOfEnv];

                if (_keyRTDEnv.Equals("UAT"))
                {
                    foo = new APIResult()
                    {
                        Success = true,
                        State = "OK",
                        Message = "Pass. It's UAT Env"
                    };

                    return foo;
                }

                //_logger.Info(string.Format("Run Function [{0}]: Model is {1}", tmpFuncName, _model));
                client = GetHttpClient(_configuration, "");
                // Add an Accept header for JSON format.
                // 為JSON格式添加一個Accept表頭
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                Uri gizmoUri = null;
                string strGizmo = "{ }";
                _tmpKey = _model;

                switch (_model.ToUpper())
                {
                    case "INFOUPDATE":
                        remoteCmd = string.Format("api/{0}", _model);
                        InfoUpdate tmpModel = new InfoUpdate();
                        
                        tmpModel.LotID = args[0];
                        tmpModel.Stage = args[1];
                        //tmpModel.machine = args[2];
                        //tmpModel.desc = args[3];
                        tmpModel.CarrierID = args[4];
                        tmpModel.Cust = args[5];
                        tmpModel.PartID = args[6];
                        tmpModel.LotType = args[7];
                        tmpModel.Automotive = args[8];
                        tmpModel.State = args[9];
                        tmpModel.HoldCode = args[10];
                        tmpModel.TurnRatio = float.Parse(args[11]);
                        tmpModel.EOTD = args[12];
                        tmpModel.HoldReas = args[13];
                        tmpModel.POTD = args[14];
                        tmpModel.WaferLot = args[15];
                        tmpModel.Quantity = int.Parse(args[16]);
                        tmpModel.Total = int.Parse(args[17]);
                        tmpModel.Force = args[18].Equals("") ? false : args[18].ToLower().Equals("true") ? true : false;

                        gizmoUri = null;
                        strGizmo = System.Text.Json.JsonSerializer.Serialize<InfoUpdate>(tmpModel);
                        break;
                    case "EQUIPMENTSTATUSSYNC":
                        remoteCmd = string.Format("api/{0}", _model);
                        EquipmentStatusSync tmpEqpSync = new EquipmentStatusSync();
                        tmpEqpSync.PortID = args[0];

                        gizmoUri = null;
                        strGizmo = System.Text.Json.JsonSerializer.Serialize<EquipmentStatusSync>(tmpEqpSync);
                        break;
                    case "MANUALCHECKIN":
                        remoteCmd = string.Format("api/{0}", _model);
                        ManualCheckIn tmpManualCheckin = new ManualCheckIn();

                        tmpManualCheckin.CarrierID = args[0];
                        tmpManualCheckin.LotID = args[1];
                        tmpManualCheckin.PortID = args[2];
                        tmpManualCheckin.Quantity = int.Parse(args[3]);
                        tmpManualCheckin.Total = int.Parse(args[4]);
                        tmpManualCheckin.UserID = args[5];
                        tmpManualCheckin.Pwd = args[6];

                        gizmoUri = null;
                        strGizmo = System.Text.Json.JsonSerializer.Serialize<ManualCheckIn>(tmpManualCheckin);

                        _logger.Info(strGizmo);

                        break;
                    case "GETDEVICEINFO":
                        remoteCmd = string.Format("api/{0}", _model);
                        DeviceInfo tmpDeviceInfo = new DeviceInfo();

                        tmpDeviceInfo.DeviceID = args[0];

                        gizmoUri = null;
                        strGizmo = System.Text.Json.JsonSerializer.Serialize<DeviceInfo>(tmpDeviceInfo);

                        break;
                    case "BATCH":
                        remoteCmd = string.Format("api/{0}", _model);
                        Batch tmpBatch = new Batch();

                        tmpBatch.EQPID = args[0];
                        tmpBatch.CarrierID = args[1];
                        tmpBatch.TotalFoup = int.Parse(args[2]);

                        gizmoUri = null;
                        strGizmo = System.Text.Json.JsonSerializer.Serialize<Batch>(tmpBatch);

                        break;
                    case "GETCOMMANDLIST":
                        remoteCmd = string.Format("api/{0}", _model);
                        CommandList tmpCommandList = new CommandList();

                        tmpCommandList.CommandID = args[0];

                        gizmoUri = null;
                        strGizmo = System.Text.Json.JsonSerializer.Serialize<CommandList>(tmpCommandList);
                        //strGizmo = "";

                        break;
                    default:
                        remoteCmd = string.Format("api/{0}", _model);
                        break;
                }

                response = new HttpResponseMessage();

                if (strGizmo.Equals(""))
                {
                    response = client.PostAsJsonAsync(remoteCmd, strGizmo).Result;
                }
                else
                {
                    bool _tmpConvert = false;
                    try {
                        _tmpConvert = true;
                        var gizmo = JObject.Parse(strGizmo);
                        _logger.Info(string.Format("Send Command by JObject.Parse.[{0}]", gizmo.ToString()));

#if DEBUG
//Do Nothing
                        response = client.PostAsJsonAsync(remoteCmd, gizmo).Result;
#else
                        response = client.PostAsJsonAsync(remoteCmd, gizmo).Result;
#endif

                        tmpMsg = string.Format("MCS response is [{0}] [{1}]", response.IsSuccessStatusCode.ToString(), response.Content.ToString());
                        _logger.Info(tmpMsg);

                    }
                    catch(Exception ex) { _tmpConvert = false; }

                    try
                    {
                        if (!_tmpConvert)
                        {
                            _tmpConvert = true;
                            string sGizmo = JsonConvert.SerializeObject(strGizmo);

#if DEBUG
//Do Nothing
#else
                            response = client.PostAsJsonAsync(remoteCmd, sGizmo).Result;
                            _logger.Info(string.Format("Send Command by JsonConvert.SerializeObject.[{0}]", sGizmo));
#endif
                        }
                    }
                    catch (Exception ex) { _tmpConvert = false; }

                    if (!_tmpConvert)
                    {
                        _tmpConvert = true;
                        _logger.Info(string.Format("Send Command by JsonSerializer.Serialize<ManualCheckIn>.[{0}]", strGizmo));

#if DEBUG
//Do Nothing
#else
                        response = client.PostAsJsonAsync(remoteCmd, strGizmo).Result;
#endif
                    }
                }

                var respContent = response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    gizmoUri = response.Headers.Location;
                    bResult = true;
                    tmpState = "OK";
                    //tmpMsg = string.Format("State Code : {0}, Reason : {1}.", response.StatusCode, response.ReasonPhrase);

                    if (_model.ToUpper().Equals("GETDEVICEINFO"))
                    {
                        string tmpResult = respContent.Result;
                        APIResponse tmpResponse = new APIResponse(tmpResult);
                        jRespData = tmpResponse.Data;

                        _logger.Info(string.Format("APIResponse. [{0}][{1}][{2}][{3}]", tmpResponse.Success, tmpResponse.State, tmpResponse.ErrorCode, tmpResponse.Message));
                    }
                    else if (_model.ToUpper().Equals("GETCOMMANDLIST"))
                    {
                        string tmpResult = respContent.Result;
                        APIResp tmpResponse = new APIResp(tmpResult);
                        //jRespData = tmpResponse.Data;

                        _logger.Info(string.Format("APIResp. [{0}][{1}][{2}][{3}]", tmpResponse.Success, tmpResponse.State, tmpResponse.ErrorCode, tmpResponse.Message));
                    }
                    else
                    {
                        string tmpResult = respContent.Result;
                        APIResponse tmpResponse = new APIResponse(tmpResult);
                        jRespData = tmpResponse.Data;
                        tmpMsg = string.Format("State Code : {0}, Reason : {1}.", response.StatusCode, response.ReasonPhrase);

                        _logger.Info(string.Format("APIResponse. [{0}][{1}][{2}][{3}]", tmpResponse.Success, tmpResponse.State, tmpResponse.ErrorCode, tmpResponse.Message));
                    }
                }
                else
                {
                    //Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                    string tmpResult = respContent.Result;
                    APIResponse tmpResponse = new APIResponse(tmpResult);
                    jRespData = tmpResponse.Data;

                    bResult = false;
                    tmpState = "NG";
                    tmpMsg = string.Format("State Code : {0}[{1}], Reason : {2}.", tmpState, response.StatusCode, response.ReasonPhrase);

                    _logger.Info(tmpMsg);
                }

                if (!_model.ToUpper().Equals("INFOUPDATE"))
                    _logger.Info(tmpMsg);

                foo = new APIResult()
                {
                    Success = bResult,
                    State = tmpState,
                    Message = tmpMsg,
                    Data = jRespData
                };

                client.Dispose();
                response.Dispose();
                respContent = null;
            }
            catch (ArgumentOutOfRangeException ex)
            {
                _logger.Info(string.Format("ArgumentOutOfRangeException [{0}]: ex.Message [{1}]", tmpFuncName, ex.Message));

                foo = new APIResult()
                {
                    Success = false,
                    State = "NG",
                    Message = ex.Message
                };
            }
            catch (Exception ex)
            {
                _logger.Info(string.Format("Exception [{0}]: ex.Message [{1}]", tmpFuncName, ex.Message));

                string[] _argvs = new string[] { _tmpKey, "", "", _configuration[string.Format("RTDAlarm:ByAlarmNumber:{0}", "30100")] };
                if (CallRTDAlarm(_dbTool, 30100, _argvs))
                { }

                foo = new APIResult()
                {
                    Success = false,
                    State = "NG",
                    Message = ex.Message
                };
            }

            //_logger.Info(string.Format("[{0}]: Result is {1}, Reason : [{2}]", tmpFuncName, foo.State, foo.Message));

            return foo;
        }
        public APIResult SentAbortOrCancelCommandtoMCS(IConfiguration _configuration, ILogger _logger, int iRemotecmd, string _commandId)
        {
            APIResult foo;
            bool bResult = false;
            string tmpState = "";
            string tmpMsg = "";
            string remoteCmd = "cancel";
            HttpClient client;
            HttpResponseMessage response;

            string tableOrder = "";
            string _keyOfEnv = "";
            string _keyRTDEnv = "";

            try
            {
                _keyRTDEnv = _configuration["RTDEnvironment:type"];
                _keyOfEnv = string.Format("RTDEnvironment:commandsTable:{0}", _keyRTDEnv);
                //"RTDEnvironment:commandsTable:PROD"
                tableOrder = _configuration[_keyOfEnv] is null ? "workinprocess_sch" : _configuration[_keyOfEnv];

                if (_keyRTDEnv.Equals("UAT"))
                {
                    foo = new APIResult()
                    {
                        Success = true,
                        State = "OK",
                        Message = "Pass. It's UAT Env"
                    };

                    return foo;
                }

                client = GetHttpClient(_configuration, "");
                client.Timeout = TimeSpan.FromSeconds(60);
                // Add an Accept header for JSON format.
                // 為JSON格式添加一個Accept表頭
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                switch (iRemotecmd)
                {
                    case 1:
                        remoteCmd = "api/CancelTransferCommand";
                        break;
                    case 2:
                        remoteCmd = "api/AbortTransferCommand";
                        break;
                    default:
                        remoteCmd = "api/CancelTransferCommand";
                        break;
                }

                response = new HttpResponseMessage();

                Uri gizmoUri = null;
                string strGizmo = "{ \"CommandID\": \"" + _commandId + "\" }";
                //strGizmo = string.Format("{ \"CommandID\": \"{0}\" }", _commandId);

                var gizmo = JObject.Parse(strGizmo);
                response = client.PostAsJsonAsync(remoteCmd, gizmo).Result;

                if (response.IsSuccessStatusCode)
                {
                    gizmoUri = response.Headers.Location;
                    bResult = true;
                    tmpState = "OK";
                    tmpMsg = "";
                }
                else
                {
                    //Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                    bResult = false;
                    tmpState = "NG";
                    tmpMsg = string.Format("State Code : {0}, Reason : {1}.", response.StatusCode, response.ReasonPhrase);

                    _logger.Info(string.Format("Exception [{0}]: State is NG.  message: {1}", remoteCmd, tmpMsg));

                }

                foo = new APIResult()
                {
                    Success = bResult,
                    State = tmpState,
                    Message = tmpMsg
                };

                client.Dispose();
                response.Dispose();
            }
            catch (ArgumentOutOfRangeException ex)
            {
                _logger.Info(string.Format("ArgumentOutOfRangeException [{0}]: ex.Message [{1}]", "SentAbortOrCancelCommandtoMCS", ex.Message));
                foo = new APIResult()
                {
                    Success = false,
                    State = "NG",
                    Message = ex.Message
                };
            }
            catch (Exception ex)
            {
                foo = new APIResult()
                {
                    Success = false,
                    State = "NG",
                    Message = ex.Message
                };

                _logger.Info(string.Format("Exception [{0}]: exception message: {1}", remoteCmd, ex.Message));
            }

            _logger.Info(string.Format("Info: Result is {0}, Reason :", foo.State, foo.Message));

            return foo;
        }
        public string GetAuthrizationTokenfromMCS(IConfiguration _configuration)
        {
            string tokenS = "";
            bool bKey = false;

            try
            {
                string path = System.IO.Directory.GetCurrentDirectory() + "Authorization";
                FileInfo fi;
                TimeSpan ts;

                if (File.Exists(path))
                {
                    fi = new FileInfo(path);
                    TimeSpan ts1 = new TimeSpan(fi.LastAccessTimeUtc.Ticks);
                    TimeSpan ts2 = new TimeSpan(DateTime.Now.Ticks);
                    ts = ts1.Subtract(ts2).Duration();
                    if (ts.Days >= 7)
                    {
                        bKey = true;
                    }
                }
                else
                {
                    bKey = true;
                }

                if (bKey)
                {
                    HttpClient client = null;
                    // Add an Accept header for JSON format.
                    // 為JSON格式添加一個Accept表頭
                    Dictionary<string, string> dicParams = new Dictionary<string, string>();
                    string _url = string.Format("http://{0}:{1}/api/login", _configuration["MCS:ip"], _configuration["MCS:port"]);
                    dicParams.Add("username", "gyro");
                    dicParams.Add("password", "gsi5613686");

                    using (client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                        HttpResponseMessage response = client.PostAsync(_url, new FormUrlEncodedContent(dicParams)).Result;
                        var token = response.Content.ReadAsStringAsync().Result;
                        tokenS = token.ToString();
                    }

                    File.WriteAllText(path, tokenS);
                }
                else
                {
                    tokenS = File.ReadAllText(path);
                }
            }
            catch (Exception ex)
            { }
            return tokenS;
        }
        public string GetLotIdbyCarrier(DBTool _dbTool, string _carrierId, out string errMsg)
        {
            DataTable dt = null;
            DataRow[] dr = null;
            string tmpLotId = "";
            errMsg = "";

            try
            {
                dt = _dbTool.GetDataTable(_BaseDataService.SelectTableCarrierAssociateByCarrierID(_carrierId));
                dr = dt.Select();
                if (dr.Length <= 0)
                {
                    errMsg = "Can not find this Carrier Id.";
                }
                else
                {
                    if (dr[0]["LOT_ID"].ToString().Equals(""))
                    {
                        errMsg = "The carrier have not create association with lot.";
                    }
                    else
                    { tmpLotId = dr[0]["LOT_ID"].ToString(); }
                }
            }
            catch (Exception ex)
            { }
            finally
            {
                if (dt != null)
                    dt.Dispose(); 
            }
            dt = null;
            dr = null;

            return tmpLotId;
        }
        public List<string> CheckAvailableQualifiedTesterMachine(DBTool _dbTool, IConfiguration _configuration, bool DebugMode, ILogger _logger, string _lotId)
        {
            string tmpMsg = "";
            List<string> TesterMachineList = new List<string>();

            string url = _configuration["WebService:url"];
            string username = _configuration["WebService:username"];
            string password = _configuration["WebService:password"];
            string webServiceMode = _configuration["WebService:Mode"];

            DataTable dt = null;
            string tmpSql = "";

            try
            {
                JCETWebServicesClient jcetWebServiceClient = new JCETWebServicesClient();
                //jcetWebServiceClient.hostname = "127.0.0.1";
                //jcetWebServiceClient.portno = 54350;
                jcetWebServiceClient._url = url;
                JCETWebServicesClient.ResultMsg resultMsg = new JCETWebServicesClient.ResultMsg();
                resultMsg = jcetWebServiceClient.GetAvailableQualifiedTesterMachine(DebugMode, webServiceMode, username, password, _lotId);

#if DEBUG
                //_logger.Info(string.Format("Info:{0}", tmpMsg));
#else
#endif

                if (resultMsg.status)
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(resultMsg.retMessage);

                    XmlNodeList xnlA = xmlDoc.GetElementsByTagName("string");

                    if (xnlA.Count > 0)
                    {
                        //先取得Current Stage
                        dt = null;
                        string curStage = "";
                        string curEqpAsso = "";
                        tmpSql = _BaseDataService.SelectTableLotInfoByLotid(_lotId);
                        dt = _dbTool.GetDataTable(tmpSql);
                        if (dt.Rows.Count > 0)
                        {
                            curStage = dt.Rows[0]["STAGE"].ToString().Trim();
                            curEqpAsso = dt.Rows[0]["EQUIP_ASSO"].ToString().Trim();
                        }

                        foreach (XmlNode xnA in xnlA)
                        {
                            if (!xnA.InnerText.Equals(" "))
                            {
                                dt = null;
                                tmpSql = "";
                                tmpSql = _BaseDataService.SelectTableEQUIP_MATRIX(xnA.InnerText, curStage);
                                dt = _dbTool.GetDataTable(tmpSql);
                                if (dt.Rows.Count > 0)
                                {
                                    DataTable dtLot = _dbTool.GetDataTable(_BaseDataService.SelectTableLotInfoByLotid(_lotId));
                                    string tmpCustomer = "";
                                    if (dtLot.Rows.Count > 0)
                                        tmpCustomer = dtLot.Rows[0]["CustomerName"].ToString();

                                    DataTable dtMap = _dbTool.GetDataTable(_BaseDataService.SelectPrefmap(xnA.InnerText));
                                    if (dtMap.Rows.Count > 0)
                                    {
                                        if (dtMap.Rows[0]["CUSTOMER_PREF_1"].ToString().Contains(tmpCustomer))
                                        {
                                            TesterMachineList.Insert(0, xnA.InnerText);
                                        }
                                        else if (dtMap.Rows[0]["CUSTOMER_PREF_2"].ToString().Contains(tmpCustomer))
                                        {
                                            TesterMachineList.Add(xnA.InnerText);
                                        }
                                        else if (dtMap.Rows[0]["CUSTOMER_PREF_3"].ToString().Contains(tmpCustomer))
                                        {
                                            TesterMachineList.Add(xnA.InnerText);
                                        }
                                        else
                                            TesterMachineList.Add(xnA.InnerText);
                                    }
                                    else
                                        TesterMachineList.Add(dt.Rows[0]["EQPID"].ToString());
                                }
                                else
                                    continue;
                            }
                        }

                        if (curEqpAsso.Equals("Y"))
                        {
                            if (TesterMachineList.Count <= 0)
                            {
                                tmpSql = _BaseDataService.UpdateTableLotInfoReset(_lotId);
                                _dbTool.SQLExec(tmpSql, out tmpMsg, true);
                            }
                        }
                    }
                    else
                    {
                        tmpMsg = "No Available Tester Machine.";
                    }
                }
                else
                {
                    tmpMsg = "Get Available Qualified Tester Machine failed.";
                }
            }
            catch (Exception ex)
            {
                tmpMsg = string.Format("Unknow error. [Exception] {0}", ex.Message);
                _logger.Debug(ex.Message);
            }

            return TesterMachineList;
        }
        public List<string> CheckAvailableQualifiedTesterMachine(IConfiguration _configuration, ILogger _logger, string _username, string _password, string _lotId)
        {
            List<string> TesterMachine = new List<string>();
            string webserurl = _configuration["WebService:url"];
            Console.WriteLine("hahaha:----" + webserurl);
            //var binding = new BasicHttpBinding();
            ////根據WebService 的URL 構建終端點對象，參數是提供的WebService地址
            //var endpoint = new EndpointAddress(String.Format(@" {0} ", webserurl));
            ////創建調用接口的工廠，注意這裡泛型只能傳入接口泛型接口裡面的參數是WebService裡面定義的類名+Soap 
            //var factory = new ChannelFactory<WebServiceSoap>(binding, endpoint);
            ////從工廠獲取具體的調用實例
            //var callClient = factory.CreateChannel();
            //調用具體的方法，這裡是HelloWorldAsync 方法


            ////調用TestMethod方法，並傳遞參數
            //CheckAvailableQualifiedTesterMachine body = new CheckAvailableQualifiedTesterMachine(_username, _password);
            //Task<CheckAvailableQualifiedTesterMachine> testResponsePara = callClient.CheckAvailableQualifiedTesterMachine(new CheckAvailableQualifiedTesterMachine(body));
            ////獲取
            //string result3 = testResponsePara.Result.Body._TPQuery_CheckPROMISLoginResult;
            ////<?xml version="1.0" encoding="utf - 8"?><Beans><Status Value="FAILURE" /><ErrMsg Value="SECURITY. % UAF - W - LOGFAIL, user authorization failure, privileges removed." /></Beans>';
            ////string test = "<body><head>test header</head></body>";
            ////XmlDocument xmlDoc = new XmlDocument();
            ////xmlDoc.LoadXml(result3);
            ////XmlNode xn = xmlDoc.SelectSingleNode("Beans");


            //XmlNodeList xnlA = xn.ChildNodes;
            //String member_valodation = "";
            //String member_validation_message = "";
            //foreach (XmlNode xnA in xnlA)
            //{
            //    Console.WriteLine(xnA.Name);
            //    if ((xnA.Name) == "Status")
            //    {
            //        XmlElement xeB = (XmlElement)xnA;
            //        if ((xeB.GetAttribute("Value")) == "SUCCESS")
            //        {
            //            member_valodation = "OK";
            //        }
            //        else
            //        {
            //            member_valodation = "NG";
            //        }

            //    }
            //    if ((xnA.Name) == "ErrMsg")
            //    {
            //        XmlElement xeB = (XmlElement)xnA;
            //        member_validation_message = xeB.GetAttribute("Value");
            //    }

            //    Console.WriteLine(member_valodation);
            //}
            //if (member_valodation == "OK")
            //{
            //    //A claim is a statement about a subject by an issuer and
            //    //represent attributes of the subject that are useful in the context of authentication and authorization operations.
            //    if (objLoginModel.UserName == "admin")
            //    {
            //        objLoginModel.Role = "Admin";
            //    }
            //    else
            //    {
            //        objLoginModel.Role = "User";
            //    }
            //    var claims = new List<Claim>() {
            //            //new Claim(ClaimTypes.NameIdentifier,Convert.ToString(user.UserId)),
            //                new Claim("user_name",objLoginModel.UserName),
            //                new Claim(ClaimTypes.Role,objLoginModel.Role),
            //            //new Claim("FavoriteDrink","Tea")
            //            };
            //    //Initialize a new instance of the ClaimsIdentity with the claims and authentication scheme
            //    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            //    //Initialize a new instance of the ClaimsPrincipal with ClaimsIdentity
            //    var principal = new ClaimsPrincipal(identity);
            //    //SignInAsync is a Extension method for Sign in a principal for the specified scheme.
            //    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            //        principal, new AuthenticationProperties() { IsPersistent = objLoginModel.RememberLogin });

            //    return LocalRedirect(objLoginModel.ReturnUrl);
            //}
            //else
            //{
            //    ModelState.AddModelError("UserName", "Username Error");
            //    ModelState.AddModelError("Password", "Password error");
            //    return View(objLoginModel);
            //}

            //TesterMachine.Add("CTWT-01");
            //TesterMachine.Add("CTDS-06");
            return TesterMachine;
        }
        public bool BuildTransferCommands(DBTool _dbTool, IConfiguration configuration, ILogger _logger, bool DebugMode, EventQueue _oEventQ, Dictionary<string, string> _threadControll, List<string> _lstEquipment, out List<string> _arrayOfCmds)
        {
            bool bResult = false;
            string tmpMsg = "";
            string tmpSmsMsg = "";
            string strEquip = "";
            string strPortModel = "";
            ArrayList tmpCmds = new();
            _arrayOfCmds = new List<string>();
            string lotid = "";
            string tmpSql = "";
            string tmpCustomerName = "";
            string tmpStage = "";
            string tmpPartId = "";
            List<string> lstPortIDs = new List<string>();
            string _args = "";

            string tableOrder = "";
            string _keyOfEnv = "";
            string _keyRTDEnv = "";

            try
            {
                DataTable dt = null;
                DataTable dtTemp = null;
                DataRow[] dr = null;// strPortModel

                _keyRTDEnv = configuration["RTDEnvironment:type"];
                _keyOfEnv = string.Format("RTDEnvironment:commandsTable:{0}", _keyRTDEnv);
                //"RTDEnvironment:commandsTable:PROD"
                tableOrder = configuration[_keyOfEnv] is null ? "workinprocess_sch" : configuration[_keyOfEnv];

                if (_oEventQ.EventName.Equals("CommandStatusUpdate") || _oEventQ.EventName.Equals("CarrierLocationUpdate"))
                {
                    //CarrierLocationUpdate
                    return bResult;
                }
                else
                    lotid = ((NormalTransferModel)_oEventQ.EventObject).LotID;

                tmpSql = _BaseDataService.SelectTableLotInfoByLotid(lotid);
                dt = _dbTool.GetDataTable(tmpSql);
                if (dt.Rows.Count > 0)
                {
                    tmpCustomerName = dt.Rows[0]["CustomerName"].ToString().Trim();
                    tmpStage = dt.Rows[0]["Stage"].ToString().Trim();
                    tmpPartId = dt.Rows[0]["PartID"].ToString().Trim();
                }

                //由符合的設備中, 找出一台可用的機台
                string sql = "";
                bool bHaveEQP = false;
                bool bKey = false;
                string rtsMachineState = "";
                string rtsCurrentState = "";
                string rtsDownState = "";

                foreach (string Equip in _lstEquipment)
                {
                    bHaveEQP = false;
                    try
                    {
                        //再次檢查, 是否有限定特定Stage 可於部份機台上執行！
                        sql = _BaseDataService.SelectTableEQUIP_MATRIX(Equip, tmpStage);
                        dt = _dbTool.GetDataTable(tmpSql);
                        if (dt.Rows.Count <= 0)
                        {
                            tmpMsg = String.Format("Equipment[{0}] not in EQUIP MATRIX", Equip);
                            _logger.Debug(tmpMsg);

                            continue;
                        }

                        sql = string.Format(_BaseDataService.SelectTableEQPStatusInfoByEquipID(Equip));
                        dt = _dbTool.GetDataTable(sql);

                        if (dt.Rows.Count > 0)
                        {
                            lstPortIDs = new List<string>();
                            strEquip = "";
                            if (!strEquip.Equals(""))
                            { bHaveEQP = true; }

                            if (dt.Rows[0]["ISLOCK"].ToString().Equals("1"))
                            {
                                //其它Command已指定, 直接跳過, 防止永遠派不出指令
                                continue;
                            }

                            //取RTS Equipment Status
                            dtTemp = _dbTool.GetDataTable(_BaseDataService.GetRTSEquipStatus(GetExtenalTables(configuration, "SyncExtenalData", "RTSEQSTATE"), Equip));
                            if (dtTemp.Rows.Count > 0)
                            {
                                //machine_state, curr_status, down_state
                                //rtsMachineState, rtsCurrentState, rtsDownState
                                rtsMachineState = dtTemp.Rows[0]["machine_state"].ToString();
                                rtsCurrentState = dtTemp.Rows[0]["curr_status"].ToString();
                                rtsDownState = dtTemp.Rows[0]["down_state"].ToString();
                            }

                            //檢查狀態 Current Status 需要為UP
                            if (rtsCurrentState.Equals("UP"))
                            {
                                //可用
                                strEquip = Equip;
                            }
                            else if (rtsCurrentState.Equals("PM"))
                            {

                            }
                            else if (rtsCurrentState.Equals("DOWN"))
                            {
                                if (rtsDownState.Equals("IDLE"))
                                {
                                    strEquip = Equip;
                                }
                                if (rtsDownState.Equals("DOWN"))
                                {
                                    tmpMsg = String.Format("[BuildTransferCommands][{0}][RTS Status Issue: machine state is {1}, current state is {2}, down state is {3}]", "GetRTSEquipStatus", rtsMachineState, rtsCurrentState, rtsDownState);
                                    _logger.Debug(tmpMsg);

                                    continue;
                                }
                            }
                            else
                            {
                                //無法用, 直接確認下一台Equipment
                                continue;
                            }
                            //檢查Port Status是否可用
                            bKey = false;
                            int iState = 0;

                            foreach (DataRow drCurr in dt.Rows)
                            {
                                iState = int.Parse(drCurr["PORT_STATE"].ToString());

                                if (iState == 0 || iState == 1 || iState == 6 || iState == 9)
                                {
                                    bKey = false; break;
                                }
                                else
                                {
                                    bKey = true;
                                    string tmpCurrPort = drCurr["Port_Id"].ToString().Trim();

                                    //20230413V1.2 Modify by Vance
                                    sql = string.Format(_BaseDataService.SelectTableWorkInProcessSchByEquipPort(Equip, tmpCurrPort, tableOrder));
                                    dtTemp = _dbTool.GetDataTable(sql);

                                    if (dtTemp.Rows.Count > 0)
                                        bHaveEQP = true;

                                    lstPortIDs.Add(tmpCurrPort);

                                    break;
                                }
                            }
                        }

                        if (bHaveEQP)
                        {
                            //己經有指令的直接加入嘗試再次發送
                            if (dt is not null)
                            {
                                _arrayOfCmds.Add(dtTemp.Rows[0]["CMD_ID"].ToString());

                                dtTemp.Clear(); dtTemp.Dispose(); dtTemp = null;
                            }
                            continue;
                        }

                        if (bHaveEQP)
                        {
                            if (dtTemp is not null)
                            { dtTemp.Clear(); dtTemp.Dispose(); dtTemp = null; }
                            break;
                        }
                    }
                    catch(Exception ex)
                    {
                        tmpMsg = String.Format("BuildTransferCommands. [Exception 1]: {0}", ex.Message);
                        _logger.Debug(tmpMsg);
                    }

                    if (!bKey)
                    { continue; }
                    else
                    { break; }
                }
                //取出EquipModel5
                if (strEquip.Equals(""))
                { return bResult; }

                sql = string.Format(_BaseDataService.SelectTableEQP_STATUSByEquipId(strEquip));
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    strPortModel = dt.Rows[0]["Port_Model"].ToString();
                }
                else
                {
                    _arrayOfCmds = null;
                    return bResult;
                }

                lock (_threadControll)
                {
                    if (_threadControll.ContainsKey(strEquip))
                    {
                        if (ThreadLimitTraffice(_threadControll, strEquip, 3, "ss", ">"))
                        {
                            _threadControll[strEquip] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        }
                        else
                        {
                            return bResult;
                        }
                    }
                    else
                    {
                        if (!_threadControll.ContainsKey(strEquip))
                            _threadControll.Add(strEquip, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        else
                            return bResult;
                    }
                }

                try
                {
                    /*
                    * 3. If next lot is different CUSTDEVICE
                    a. RTD to trigger pre-alert by Email / SMS when distributed for the last lot with same device.
                    b. RTD to call SP to change RTS status to CONV and trigger Email / SMS alert to inform
                    equipment already change to CONV.
                    c. RTS to bring down equipment and change equipment status from Promis.
                    d. RTD to insert SMS data into table RTD_SMS_TRIGGER_DATA@AMHS. SCS will trigger SMS
                    based on the table.  //RTD 将 SMS 数据插入表 RTD_SMS_TRIGGER_DATA@AMHS。 SCS 将触发 SMS
                    e. RTD to stop distribute for the next lot until user to clear alarm from RTD.
                    */
                    string resultCode = "";
                    if (VerifyCustomerDevice(_dbTool, _logger, strEquip, tmpCustomerName, lotid, out resultCode))
                    {
                        bool bAlarm = false;

                        //同一设备分发最后一批时，RTD 通过电子邮件/短信触发预警。
                        //呼叫 SP 将 RTS 状态更改为 CONV 并触发 Email/SMS 警报通知
                        //将 SMS 数据插入表 RTD_SMS_TRIGGER_DATA@AMHS。 SCS 将触发 SMS
                        tmpSmsMsg = "";
                        //NEXT LOT 83633110.2 NEED TO SETUP AFTER CURRENT LOT END.  //換客戶的最後一批Lot發送此訊息
                        //--"NEXT LOT {0} NEED TO SETUP AFTER CURRENT LOT END.", LotId
                        //LOT 83633110.2 NEED TO SETUP NOW. EQUIP DOWN FROM RTS.    //換客戶後第一批Lot發此訊息
                        //--"LOT {0} NEED TO SETUP NOW. EQUIP DOWN FROM RTS.", LotId
                        MailMessage tmpMailMsg = new MailMessage();
                        tmpMailMsg.To.Add(configuration["MailSetting:AlarmMail"]);
                        //msg.To.Add("b@b.com");可以發送給多人
                        //msg.CC.Add("c@c.com");
                        //msg.CC.Add("c@c.com");可以抄送副本給多人 
                        //這裡可以隨便填，不是很重要
                        tmpMailMsg.From = new MailAddress(configuration["MailSetting:username"], configuration["MailSetting:EntryBy"], Encoding.UTF8);

                        RTDAlarms rtdAlarms = new RTDAlarms();
                        rtdAlarms.UnitType = "";
                        rtdAlarms.UnitID = "";
                        rtdAlarms.Level = "";
                        rtdAlarms.Code = 0;
                        rtdAlarms.Cause = "";
                        rtdAlarms.SubCode = "";
                        rtdAlarms.Detail = "";//SET , RESET
                        rtdAlarms.CommandID = "";//(KEY: Command ID, Machine, eRack
                        rtdAlarms.Params = "";
                        rtdAlarms.Description = "";
                        //IDX, unitType, unitID, level, code, cause, subCode, detail, commandID, params, description, new, createdAt, last_updated
                        //3509	3871	System	RTD	Issue	30001	Dispatch overtime	2	Auto Hold Lot	83757482.1			0	28/7/2023 6:52:47 AM	28/7/2023 7:32:13 AM
                        //3510    3787    Rack ERS01   Error   20051   Erack off line  0   SET ERS01           0   21 / 7 / 2023 9:15:59 PM    24 / 7 / 2023 1:44:08 AM
                        switch (resultCode)
                        {
                            case "1001":
                                //不同客戶後的第一批
                                //tmpSmsMsg = string.Format("LOT {0} NEED TO SETUP NOW. EQUIP DOWN FROM RTS.", lotid);

                                /* 上面3個參數分別是發件人地址（可以隨便寫），發件人姓名，編碼*/
                                /*
                                tmpMailMsg.Subject = "Device Setup Alert";//郵件標題
                                tmpMailMsg.SubjectEncoding = Encoding.UTF8;//郵件標題編碼
                                tmpMailMsg.Body = string.Format("LOT {0} ({1}) NEED TO SETUP NOW. EQUIP DOWN FROM RTS.", lotid, tmpPartId); //郵件內容
                                tmpMailMsg.BodyEncoding = Encoding.UTF8;//郵件內容編碼 
                                tmpMailMsg.IsBodyHtml = true;//是否是HTML郵件 
                                bAlarm = true;*/

                                tmpSmsMsg = "lotid:{0}$equipid:{1}$partid:{2}$customername:{3}$stage:{4}";

                                try
                                {
                                    List<string> lstParams = new List<string>();
                                    lstParams.Add(string.Format("\"lotid\":\"{0}\"", lotid));
                                    lstParams.Add(string.Format("\"equipid\":\"{0}\"", strEquip));
                                    lstParams.Add(string.Format("\"partid\":\"{0}\"", tmpPartId));
                                    lstParams.Add(string.Format("\"customername\":\"{0}\"", tmpCustomerName));
                                    lstParams.Add(string.Format("\"stage\":\"{0}\"", tmpStage));

                                    rtdAlarms.UnitType = "System";
                                    rtdAlarms.UnitID = "RTD";
                                    rtdAlarms.Level = "ALARM";
                                    rtdAlarms.Code = int.Parse(resultCode);
                                    rtdAlarms.Cause = "Device Setup Alert";
                                    rtdAlarms.SubCode = "";
                                    rtdAlarms.Detail = "SET";//SET , RESET
                                    rtdAlarms.CommandID = strEquip;//(KEY: Command ID, Machine, eRack
                                    //rtdAlarms.Params = JsonConvert.SerializeObject(lstParams);
                                    string tmpAAA = JsonConvert.SerializeObject(lstParams);
                                    rtdAlarms.Params = tmpAAA;
                                    string tmpRTDAlarm = configuration["RTDAlarm:Condition"] is null ? "eMail:false$SMS:false$repeat:false$hours:0$mints:10" : configuration[string.Format("RTDAlarm:ByAlarmNumber:{0}", resultCode)] is null ? configuration["RTDAlarm:Condition"] : configuration[string.Format("RTDAlarm:ByAlarmNumber:{0}", resultCode)];
                                    rtdAlarms.EventTrigger = tmpRTDAlarm;
                                    //rtdAlarms.EventTrigger = configuration["RTDAlarm:Condition"] is null ? "eMail:true$SMS:false$repeat:false$hours:0$mints:10" : configuration["RTDAlarm:Condition"];
                                } catch { }

                                break;
                            case "1002":
                                string tmpNextLot = GetLotIdbyCarrier(_dbTool, GetCarrierByPortId(_dbTool, tmpPartId), out tmpMsg);
                                string tmpNextPartId = "";
                                if (!tmpNextLot.Equals(""))
                                {
                                    tmpSql = _BaseDataService.SelectTableLotInfoByLotid(lotid);
                                    dt = _dbTool.GetDataTable(tmpSql);
                                    if (dt.Rows.Count > 0)
                                        tmpNextPartId = dt.Rows[0]["PartId"].ToString().Trim();
                                }
                                //同客戶的最後一批
                                //tmpSmsMsg = string.Format("NEXT LOT {0} NEED TO SETUP AFTER CURRENT LOT {1}({2}) END.", tmpNextLot, lotid, tmpPartId);

                                /* 上面3個參數分別是發件人地址（可以隨便寫），發件人姓名，編碼*/
                                /*
                                tmpMailMsg.Subject = "Setup Pre-Alert";//郵件標題
                                tmpMailMsg.SubjectEncoding = Encoding.UTF8;//郵件標題編碼
                                tmpMailMsg.Body = string.Format(@"{0} 
Tool: {5}
Next Lot: ({1})
Current Lot {2}
Mfg Device: ({4})
Customer Device: {3}", tmpSmsMsg, tmpNextLot, lotid, tmpNextPartId, tmpPartId, strEquip); //郵件內容
                                tmpMailMsg.BodyEncoding = Encoding.UTF8;//郵件內容編碼 
                                tmpMailMsg.IsBodyHtml = true;//是否是HTML郵件 

                                bAlarm = true;
                                */

                                tmpSmsMsg = "lotid:{0}$equipid:{1}$partid:{2}$customername:{3}$stage:{4}$nextlot:{5}$nextpart:{6}";

                                try {
                                    List<string> lstParams = new List<string>();
                                    lstParams.Add(string.Format("\"lotid\":\"{0}\"", lotid));
                                    lstParams.Add(string.Format("\"equipid\":\"{0}\"", strEquip));
                                    lstParams.Add(string.Format("\"partid\":\"{0}\"", tmpPartId));
                                    lstParams.Add(string.Format("\"customername\":\"{0}\"", tmpCustomerName));
                                    lstParams.Add(string.Format("\"stage\":\"{0}\"", tmpStage));
                                    lstParams.Add(string.Format("\"nextlot\":\"{0}\"", tmpNextLot));
                                    lstParams.Add(string.Format("\"nextpart\":\"{0}\"", tmpNextPartId));

                                    rtdAlarms.UnitType = "System";
                                    rtdAlarms.UnitID = "RTD";
                                    rtdAlarms.Level = "Warning";
                                    rtdAlarms.Code = int.Parse(resultCode);
                                    rtdAlarms.Cause = "Setup Pre-Alert";
                                    rtdAlarms.SubCode = "";
                                    rtdAlarms.Detail = "SET";//SET , RESET
                                    rtdAlarms.CommandID = strEquip;//(KEY: Command ID, Machine, eRack
                                    //rtdAlarms.Params = String.Format(tmpSmsMsg, lotid, strEquip, tmpPartId, tmpCustomerName, tmpStage, tmpNextLot, tmpNextPartId);
                                    rtdAlarms.Params = JsonConvert.SerializeObject(lstParams);
                                    string tmpRTDAlarm = configuration["RTDAlarm:Condition"] is null ? "eMail:false$SMS:false$repeat:false$hours:0$mints:10" : configuration[string.Format("RTDAlarm:ByAlarmNumber:{0}", resultCode)] is null ? configuration["RTDAlarm:Condition"] : configuration[string.Format("RTDAlarm:ByAlarmNumber:{0}", resultCode)];
                                    rtdAlarms.EventTrigger = tmpRTDAlarm;
                                    //rtdAlarms.EventTrigger = configuration["RTDAlarm:Condition"] is null ? "eMail:true$SMS:true$repeat:false$hours:0$mints:10" : configuration["RTDAlarm:Condition"];

                                } catch { }

                                break;
                            default:
                                bAlarm = false;
                                break;
                        }


                        if (!rtdAlarms.UnitType.Equals(""))
                        {
                            _args = string.Format("{0},{1},{2},{3}", rtdAlarms.UnitID, rtdAlarms.Code, rtdAlarms.SubCode, rtdAlarms.CommandID);
                            sql = string.Format(_BaseDataService.QueryExistRTDAlarms(_args));
                            dt = _dbTool.GetDataTable(sql);

                            if (dt.Rows.Count > 0)
                            {
                                tmpMsg = string.Format("[QueryExistRTDAlarms][{0}]", _args);
                                _logger.Info(tmpMsg);
                            }
                            else
                            {
                                if (!rtdAlarms.UnitID.Equals(""))
                                {
                                    tmpSql = _BaseDataService.InsertRTDAlarm(rtdAlarms);
                                    _dbTool.SQLExec(tmpSql, out tmpMsg, true);
                                }
                            }
                        }

                        /*
                        if (bAlarm)
                        {
                            ///發送SMS 
                            try
                            {
                                tmpMsg = "";
                                sql = string.Format(_BaseDataService.InsertSMSTriggerData(strEquip, tmpStage, tmpSmsMsg, "N", configuration["MailSetting:EntryBy"]));
                                tmpMsg = string.Format("Send SMS: SQLExec[{0}]", sql);
                                _logger.Info(tmpMsg);
                                _dbTool.SQLExec(sql, out tmpMsg, true);
                            }
                            catch (Exception ex)
                            {
                                tmpMsg = String.Format("Insert SMS trigger data failed. [Exception]: {0}", ex.Message);
                                _logger.Debug(tmpMsg);
                            }

                            ///寄送Mail
                            try
                            {
                                MailController MailCtrl = new MailController();
                                MailCtrl.Config = configuration;
                                MailCtrl.Logger = _logger;
                                MailCtrl.DB = _dbTool;
                                MailCtrl.MailMsg = tmpMailMsg;

                                MailCtrl.SendMail();

                                tmpMsg = string.Format("SendMail: {0}, [{1}]", tmpMailMsg.Subject, tmpMailMsg.Body);
                                _logger.Info(tmpMsg);
                            }
                            catch (Exception ex)
                            {
                                tmpMsg = String.Format("SendMail failed. [Exception]: {0}", ex.Message);
                                _logger.Debug(tmpMsg);
                            }
                        }
                        */
                    }
                    else
                    { }
                }
                catch (Exception ex)
                {
                    tmpMsg = String.Format("BuildTransferCommands. [Exception 2]: {0}", ex.Message);
                    _logger.Debug(tmpMsg);
                }

                //依Equip port Model來產生搬運指令
                //bool CreateTransferCommandByPortModel(_dbTool, _Equip, PortModel, out _arrayOfCmds)
                if (CreateTransferCommandByPortModel(_dbTool, configuration, _logger, DebugMode, strEquip, strPortModel, _oEventQ, out _arrayOfCmds))
                { }
                else
                { }
            }
            catch (Exception ex)
            {
                tmpMsg = String.Format("BuildTransferCommands. [Exception]: {0}", ex.Message);
                _logger.Debug(tmpMsg);
            }

            return bResult;
        }
        public bool CreateTransferCommandByPortModel(DBTool _dbTool, IConfiguration configuration, ILogger _logger, bool DebugMode, string _Equip, string _portModel, EventQueue _oEventQ, out List<string> _arrayOfCmds)
        {
            bool result = false;
            string tmpMsg = "";
            _arrayOfCmds = new List<string>();
            ILogger logger = LogManager.GetCurrentClassLogger();
            bool _DebugMode = true;
            bool bStateChange = false;
            bool bStageIssue = false;
            string tableOrder = "";
            string _keyOfEnv = "";
            string _keyRTDEnv = "";

            try
            {
                DataTable dtTemp = null;
                DataTable dtTemp2 = null;
                DataTable dtPortsInfo = null;
                DataTable dtCarrierInfo = null;
                DataTable dtAvaileCarrier = null;
                DataTable dtWorkgroupSet = null;
                DataTable dtLoadPortCarrier = null;
                DataTable dtWorkInProcessSch;
                DataTable dtPrepareNextWorkgroup = null;
                DataTable dtCurrentWorkgroupProcess = null;
                DataRow[] drIn = null;
                DataRow[] drOut = null;
                DataRow[] drCarrierData = null;
                DataRow drCarrier = null;
                DataRow[] drPortState;

                string sql = "";
                string lotID = "";
                string CarrierID = "";
                string UnloadCarrierID = "";
                string MetalRingCarrier = "";
                int Quantity = 0;
                string vStage = "";
                string vStage2 = "";
                string rtsMachineState = "";
                string rtsCurrentState = "";
                string rtsDownState = "";
                string rtdEqpCustDevice = "";
                bool isManualMode = false;
                bool _expired = false;
                bool _effective = false;

                bool useFaileRack = false;
                string faileRack = "";
                string outeRack = "";
                string ineRack = "";
                string tmpRack = "";
                string _carrierID = "";
                int _Workgroup_priority = 30;
                int _Priority = 30;

                bool bNearComplete = false;
                string _commandState = "";

                bool bCustDevice = true;
                bool bCheckRecipe = true;

                bool bBindWorkgroup = false;
                bool bPrepareNextWorkgroup = false;
                string _NextWorkgroup = "";
                string _Workgroup = "";
                int _iPrepareQty = 0;


                _keyRTDEnv = configuration["RTDEnvironment:type"];
                _keyOfEnv = string.Format("RTDEnvironment:commandsTable:{0}", _keyRTDEnv);
                //"RTDEnvironment:commandsTable:PROD"
                tableOrder = configuration[_keyOfEnv] is null ? "workinprocess_sch" : configuration[_keyOfEnv];

                _DebugMode = DebugMode;

                try
                {
                    if (configuration["NearCompleted:Enable"].ToLower().Equals("true"))
                        bNearComplete = true;
                }
                catch (Exception ex) { }

                //防止同一機台不同線程執行
                dtTemp = _dbTool.GetDataTable(_BaseDataService.QueryEquipLockState(_Equip));
                if (dtTemp.Rows.Count <= 0)
                {
                    if (_DebugMode)
                    {
                        _logger.Debug(string.Format("[CreateTransferCommandByPortModel][{0}] QueryEquipLockState[{1}]", _oEventQ.EventName, _Equip));
                    }

                    _dbTool.SQLExec(_BaseDataService.LockEquip(_Equip, true), out tmpMsg, true);
                }

                //取RTS Equipment Status
                dtTemp = _dbTool.GetDataTable(_BaseDataService.GetRTSEquipStatus(GetExtenalTables(configuration, "SyncExtenalData", "RTSEQSTATE"), _Equip));
                if (dtTemp.Rows.Count > 0)
                {
                    //machine_state, curr_status, down_state
                    //rtsMachineState, rtsCurrentState, rtsDownState
                    rtsMachineState = dtTemp.Rows[0]["machine_state"].ToString();
                    rtsCurrentState = dtTemp.Rows[0]["curr_status"].ToString();
                    rtsDownState = dtTemp.Rows[0]["down_state"].ToString();
                }
                else
                {
                    if (_DebugMode)
                    {
                        _logger.Debug(string.Format("[CreateTransferCommandByPortModel][{0}] GetRTSEquipStatus[{1}] _Out", _oEventQ.EventName, _Equip));
                    }
                    return false; //RTS找不到, 即不繼續產生指令
                }

                //先取得Equipment 的Ports Status
                sql = string.Format(_BaseDataService.SelectTableEQP_STATUSByEquipId(_Equip));
                dtPortsInfo = _dbTool.GetDataTable(sql);
                NormalTransferModel evtObject2 = null;

                switch (_oEventQ.EventName)
                {
                    case "CarrierLocationUpdate":
                        //Do Event CarrierLocationUpdate
                        CarrierLocationUpdate evtObject = (CarrierLocationUpdate)_oEventQ.EventObject;
                        CarrierID = evtObject.CarrierID;
                        sql = _BaseDataService.QueryLotInfoByCarrierID(CarrierID);
                        dtTemp = _dbTool.GetDataTable(sql);
                        if (dtTemp.Rows.Count > 0)
                        {
                            lotID = dtTemp.Rows[0]["lot_id"].ToString();
                        }
                        break;
                    case "LotEquipmentAssociateUpdate":
                        //STATE=WAIT , EQUIP_ASSO=N >> Do this process.
                        evtObject2 = (NormalTransferModel)_oEventQ.EventObject;
                        CarrierID = evtObject2.CarrierID;
                        lotID = evtObject2.LotID;
                        break;
                    case "AutoCheckEquipmentStatus":
                        //STATE=WAIT , EQUIP_ASSO=N >> Do this process.
                        evtObject2 = (NormalTransferModel)_oEventQ.EventObject;
                        lotID = evtObject2.LotID;
                        CarrierID = evtObject2.CarrierID;
                        bStateChange = true;
                        break;
                    case "AbnormalyEquipmentStatus":
                        evtObject2 = (NormalTransferModel)_oEventQ.EventObject;
                        lotID = evtObject2.LotID;
                        if (_DebugMode)
                        {
                            _logger.Debug(string.Format("[CreateTransferCommandByPortModel][{0}] {1} / {2}", _oEventQ.EventName, _Equip, lotID));
                        }
                        bStateChange = true;
                        break;
                    case "EquipmentPortStatusUpdate":
                        evtObject2 = (NormalTransferModel)_oEventQ.EventObject;
                        lotID = evtObject2.LotID;

                        if (evtObject2.CarrierID.Equals(""))
                        {
                            sql = string.Format(_BaseDataService.SelectTableCarrierAssociate3ByLotid(lotID));
                            dtTemp = _dbTool.GetDataTable(sql);

                            if (dtTemp.Rows.Count > 0)
                            {
                                UnloadCarrierID = dtTemp.Rows[0]["carrier_id"].ToString();
                            }
                        }
                        else
                        {
                            UnloadCarrierID = evtObject2.CarrierID;
                        }

                        if (_DebugMode)
                        {
                            _logger.Debug(string.Format("[CreateTransferCommandByPortModel][{0}] {1} / {2}", _oEventQ.EventName, _Equip, lotID));
                        }
                        bStateChange = true;
                        break;
                    default:
                        break;
                }
                
                //站點不當前站點不同, 不產生指令
                if (!lotID.Equals("") && !lotID.Equals("*"))
                {
                    vStage = "";
                    sql = _BaseDataService.CheckLotStage(configuration["CheckLotStage:Table"], lotID);
                    dtTemp = _dbTool.GetDataTable(sql);

                    if (dtTemp.Rows.Count > 0)
                    {
                        vStage = dtTemp.Rows[0]["stage1"].ToString();
                        vStage2 = dtTemp.Rows[0]["stage1"].ToString().Equals("") ? dtTemp.Rows[0]["stage2"].ToString() : dtTemp.Rows[0]["stage1"].ToString();

                        if (!dtTemp.Rows[0]["stage1"].ToString().Equals(dtTemp.Rows[0]["stage2"].ToString()))
                        {
                            _logger.Debug(string.Format("Base Lot Info: LotID= {0}, RTD_Stage={1}, MES_Stage={2}, RTD_State={3}, MES_State={4} _", lotID, dtTemp.Rows[0]["stage1"].ToString(), dtTemp.Rows[0]["stage2"].ToString(), dtTemp.Rows[0]["state1"].ToString(), dtTemp.Rows[0]["state2"].ToString()));
                            if (configuration["AppSettings:Work"].ToUpper().Equals("EWLB"))
                            {
                                if (bStateChange)
                                {
                                    bStageIssue = true;
                                }
                                else
                                {
                                    //_logger.Debug(string.Format("[CreateTransferCommandByPortModel][{0}] lot [{1}] stage not correct. can not build command.", _oEventQ.EventName, lotID));
                                    _logger.Debug(string.Format("[CreateTransferCommandByPortModel][{0}] lot [{1}] Check Stage Failed. _Out", _oEventQ.EventName, lotID));
                                    //if (configuration["AppSettings:Work"].ToUpper().Equals("EWLB"))
                                    return result;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (configuration["AppSettings:Work"].ToUpper().Equals("EWLB"))
                        {
                            //Unload lot. not need control.
                            _logger.Debug(string.Format("[CreateTransferCommandByPortModel][{0}] The lot [{1}] have stage issue, does not belong to current stage.", _oEventQ.EventName, lotID));
                            //return result;
                            bStageIssue = true;
                        }
                    }
                }

                if (dtPortsInfo.Rows.Count <= 0)
                { return result; }
                else
                {
                    try
                    {
                        try
                        {
                            _logger.Debug(string.Format("[GetEquipInfo 1]: EQUIPID[{0}], {1}/{2}/{3}/{4}", _Equip, dtPortsInfo.Rows[0]["CustDevice"].ToString(), dtPortsInfo.Rows[0]["MANUALMODE"].ToString(), dtPortsInfo.Rows[0]["Expired"].ToString(), dtPortsInfo.Rows[0]["Effective"].ToString()));
                        }
                        catch (Exception ex) { }

                        rtdEqpCustDevice = dtPortsInfo.Rows[0]["CustDevice"] is null ? "" : dtPortsInfo.Rows[0]["CustDevice"].ToString().Trim();
                        isManualMode = dtPortsInfo.Rows[0]["MANUALMODE"] is null ? false : dtPortsInfo.Rows[0]["MANUALMODE"].ToString().Equals("1") ? true : false;
                        _expired = dtPortsInfo.Rows[0]["Expired"] is null ? false : dtPortsInfo.Rows[0]["Expired"].ToString().Equals("1") ? true : false;
                        _effective = dtPortsInfo.Rows[0]["Effective"] is null ? false : dtPortsInfo.Rows[0]["Effective"].ToString().Equals("1") ? true : false;
                        _Priority = dtPortsInfo.Rows[0]["prio"] is null ? 20 : int.Parse(dtPortsInfo.Rows[0]["prio"].ToString());

                        _logger.Debug(string.Format("[GetEquipInfo 2]: EQUIPID[{0}], {1}/{2}/{3}/{4}", _Equip, rtdEqpCustDevice, isManualMode, _expired, _effective));
                    }
                    catch (Exception ex)
                    { }
                }

                //上機失敗皆回(in erack)
                NormalTransferModel normalTransfer = new NormalTransferModel();
                TransferList lstTransfer = new TransferList();
                normalTransfer.EquipmentID = _Equip;
                normalTransfer.PortModel = _portModel;
                normalTransfer.Transfer = new List<TransferList>();
                normalTransfer.LotID = lotID;
                int iCheckState = 0;
                bool bIsMatch = true;
                string EquipCustDevice = "";
                bool bPortDisabled = false;
                int iPrepareCustDeviceNo = 0;
                int iProcessCustDeviceNo = 0;
                int iProcessNextWorkgroup = 0;

                //input: 空的 normalTransfer, dtPortsInfo, 

                //====================================
                //Port Type:
                //0. Out of Service
                //1. Transfer Blocked
                //2. Near Completion
                //3. Ready to Unload
                //4. Empty (Ready to load)
                //5. Reject and Ready to unload
                //6. Port Alarm
                //9. Unknow
                //====================================

                int iReplace = 0;
                string sPortState = "";
                int iPortState = 0;
                bool _OnlyUnload = false;
                foreach (DataRow drRecord in dtPortsInfo.Rows)
                {
                    //dtWorkInProcessSch = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSchByPortId(drRecord["PORT_ID"].ToString()));
                    //if (dtWorkInProcessSch.Rows.Count > 0)
                    //    continue;

                    lstTransfer = new TransferList();
                    dtAvaileCarrier = null;
                    dtLoadPortCarrier = null;
                    string _sql = "";
                    DataTable dtCustDevice = null;
                    string sPortID = "";
                    string _CarrierTypebyPort = "";

                    try
                    {
                        bPortDisabled = drRecord["DISABLE"] is not null ? drRecord["DISABLE"].ToString().Equals("1") ? true : false : false;

                        _logger.Debug(string.Format("[GetEquipCustDevice]: Flag In"));
                        _logger.Debug(string.Format("[GetEquipCustDevice]: EQUIPID: {0}", drRecord["EQUIPID"].ToString()));

                        if (bPortDisabled)
                            continue;

                        _sql = _BaseDataService.GetEquipCustDevice(drRecord["EQUIPID"].ToString());
                        _logger.Debug(string.Format("[GetEquipCustDevice]: SQL: {0}", _sql));
                        dtCustDevice = _dbTool.GetDataTable(_sql);
                        _logger.Debug(string.Format("[GetEquipCustDevice]: Got DtTemp"));

                        iPortState = GetPortStatus(dtPortsInfo, drRecord["port_id"].ToString(), out sPortState);
                        sPortID = drRecord["port_id"].ToString();

                        if (!iPortState.ToString().Equals("3") || !iPortState.ToString().Equals("2"))
                        {
                            if (dtCustDevice.Rows.Count > 0)
                            {
                                _logger.Debug(string.Format("[GetEquipCustDevice]: {0}, GetEquipCustDevice.Count: {1} ", drRecord["EQUIPID"].ToString(), dtCustDevice.Rows.Count));

                                foreach (DataRow drTemp in dtCustDevice.Rows)
                                {
                                    EquipCustDevice = drTemp["device"].ToString().Trim();
                                    _logger.Debug(string.Format("[GetEquipCustDevice]: {0}, [CustDevice]: {1} ", drRecord["EQUIPID"].ToString(), EquipCustDevice));
                                    if (!EquipCustDevice.Equals(""))
                                        break;
                                }

                                if (!rtdEqpCustDevice.Equals(EquipCustDevice))
                                {
                                    sql = _BaseDataService.UpdateCustDeviceByEquipID(drRecord["EQUIPID"].ToString(), EquipCustDevice);
                                    _dbTool.SQLExec(sql, out tmpMsg, true);
                                }
                                //EquipCustDevice = dtTemp.Rows[0]["device"].ToString();
                                //_logger.Debug(string.Format("[GetEquipCustDevice]: {0}, [CustDevice]: {1} ", drRecord["EQUIPID"].ToString(), EquipCustDevice));
                            }
                        }

                        dtTemp = _dbTool.GetDataTable(_BaseDataService.GetCarrierTypeByPort(sPortID));
                        if (dtTemp.Rows.Count > 0)
                        {
                            _CarrierTypebyPort = dtTemp.Rows[0]["command_type"].ToString();
                        }
                    }
                    catch(Exception ex)
                    {
                        _logger.Debug(string.Format("[GetEquipCustDevice][{0}] {1}, {2} , Exception: {3} _Out", drRecord["EQUIPID"].ToString(), EquipCustDevice, _sql, ex.Message));

                        //若無法正確取得EquipCustDevice, 則直接跳出邏輯, 屬於重大異常！不可繼續執行
                        continue;
                    }
                    //Select Workgroup Set
                    dtWorkgroupSet = _dbTool.GetDataTable(_BaseDataService.SelectWorkgroupSet(drRecord["EQUIPID"].ToString()));

                    if(dtWorkgroupSet.Rows.Count > 0)
                    {
                        
                        try
                        {
                            
                            string _eqpworkgroup = "";
                            //_maximumqty, _effectiveslot
                            string _equiprecipe = "";
                            string _lotrecipe = "";
                            string _equiprecipeGroup = "";
                            string _lotrecipeGroup = "";

                            _eqpworkgroup = dtWorkgroupSet.Rows[0]["workgroup"] is null ? "" : dtWorkgroupSet.Rows[0]["workgroup"].ToString();

                            string cdtTemp = string.Format("STAGE='{0}'", vStage);
                            DataRow[] drWorkgroup = dtWorkgroupSet.Select(cdtTemp);

                            if (drWorkgroup.Length > 0)
                            {
                                useFaileRack = drWorkgroup[0]["USEFAILERACK"] is null ? false : drWorkgroup[0]["USEFAILERACK"].ToString().Equals("1") ? true : false;
                                faileRack = drWorkgroup[0]["F_ERACK"] is null ? "" : drWorkgroup[0]["F_ERACK"].ToString();
                                outeRack = drWorkgroup[0]["OUT_ERACK"] is null ? "" : drWorkgroup[0]["OUT_ERACK"].ToString();
                                ineRack = drWorkgroup[0]["IN_ERACK"] is null ? "" : drWorkgroup[0]["IN_ERACK"].ToString();
                                bCustDevice = dtWorkgroupSet.Rows[0]["checkCustDevice"] is not null ? dtWorkgroupSet.Rows[0]["checkCustDevice"].ToString().Equals("1") ? true : false : true;
                                //bCheckEquipLookupTable = drWorkgroup[0]["CHECKEQPLOOKUPTABLE"] is null ? false : drWorkgroup[0]["CHECKEQPLOOKUPTABLE"].ToString().Equals("1") ? true : false;
                                //_priority = drWorkgroup[0]["prio"] is null ? 20 : int.Parse(drWorkgroup[0]["prio"].ToString());
                                //effectiveslot, maximumqty
                                bCheckRecipe = drWorkgroup[0]["CHECKCUSTDEVICE"] is null ? false : drWorkgroup[0]["CHECKCUSTDEVICE"].ToString().Equals("1") ? true : false;
                                _Workgroup_priority = drWorkgroup[0]["prio"] is null ? 20 : int.Parse(drWorkgroup[0]["prio"].ToString());
                                bBindWorkgroup = drWorkgroup[0]["BINDWORKGROUP"] is not null ? drWorkgroup[0]["BINDWORKGROUP"].ToString().Equals("1") ? true : false : true;
                                bPrepareNextWorkgroup = drWorkgroup[0]["PREPARENEXTWORKGROUP"] is not null ? drWorkgroup[0]["PREPARENEXTWORKGROUP"].ToString().Equals("1") ? true : false : true;
                                _NextWorkgroup = drWorkgroup[0]["NEXTWORKGROUP"] is null ? "" : drWorkgroup[0]["NEXTWORKGROUP"].ToString();
                                _iPrepareQty = drWorkgroup[0]["PREPAREQTY"] is not null ? int.Parse(drWorkgroup[0]["PREPAREQTY"].ToString()) : 0;
                                _Workgroup = drWorkgroup[0]["WORKGROUP"] is null ? "" : drWorkgroup[0]["WORKGROUP"].ToString();
                            }
                            else
                            {
                                if (!vStage.Equals(vStage2))
                                {
                                    //Stage and Stage2 not same 
                                    cdtTemp = string.Format("STAGE='{0}'", vStage2);
                                    drWorkgroup = dtWorkgroupSet.Select(cdtTemp);

                                    if (drWorkgroup.Length > 0)
                                    {
                                        useFaileRack = drWorkgroup[0]["USEFAILERACK"] is null ? false : drWorkgroup[0]["USEFAILERACK"].ToString().Equals("1") ? true : false;
                                        faileRack = drWorkgroup[0]["F_ERACK"] is null ? "" : drWorkgroup[0]["F_ERACK"].ToString();
                                        outeRack = drWorkgroup[0]["OUT_ERACK"] is null ? "" : drWorkgroup[0]["OUT_ERACK"].ToString();
                                        ineRack = drWorkgroup[0]["IN_ERACK"] is null ? "" : drWorkgroup[0]["IN_ERACK"].ToString();
                                        //bCheckEquipLookupTable = drWorkgroup[0]["CHECKEQPLOOKUPTABLE"] is null ? false : drWorkgroup[0]["CHECKEQPLOOKUPTABLE"].ToString().Equals("1") ? true : false;
                                        //_priority = drWorkgroup[0]["prio"] is null ? 20 : int.Parse(drWorkgroup[0]["prio"].ToString());
                                        bCheckRecipe = drWorkgroup[0]["CHECKCUSTDEVICE"] is null ? false : drWorkgroup[0]["CHECKCUSTDEVICE"].ToString().Equals("1") ? true : false;
                                        bCustDevice = drWorkgroup[0]["CHECKCUSTDEVICE"] is null ? false : drWorkgroup[0]["CHECKCUSTDEVICE"].ToString().Equals("1") ? true : false;
                                        _Workgroup_priority = drWorkgroup[0]["prio"] is null ? 20 : int.Parse(drWorkgroup[0]["prio"].ToString());
                                        bBindWorkgroup = drWorkgroup[0]["BINDWORKGROUP"] is not null ? drWorkgroup[0]["BINDWORKGROUP"].ToString().Equals("1") ? true : false : true;
                                        bPrepareNextWorkgroup = drWorkgroup[0]["PREPARENEXTWORKGROUP"] is not null ? drWorkgroup[0]["PREPARENEXTWORKGROUP"].ToString().Equals("1") ? true : false : true;
                                        _NextWorkgroup = drWorkgroup[0]["NEXTWORKGROUP"] is null ? "" : drWorkgroup[0]["NEXTWORKGROUP"].ToString();
                                        _iPrepareQty = drWorkgroup[0]["PREPAREQTY"] is not null ? int.Parse(drWorkgroup[0]["PREPAREQTY"].ToString()) : 0;
                                        _Workgroup = drWorkgroup[0]["WORKGROUP"] is null ? "" : drWorkgroup[0]["WORKGROUP"].ToString();
                                    }
                                    else
                                    {
                                        cdtTemp = string.Format("STAGE='{0}'", "DEFAULT");
                                        drWorkgroup = dtWorkgroupSet.Select(cdtTemp);

                                        if (drWorkgroup.Length > 0)
                                        {
                                            useFaileRack = drWorkgroup[0]["USEFAILERACK"] is null ? false : drWorkgroup[0]["USEFAILERACK"].ToString().Equals("1") ? true : false;
                                            faileRack = drWorkgroup[0]["F_ERACK"] is null ? "" : drWorkgroup[0]["F_ERACK"].ToString();
                                            outeRack = drWorkgroup[0]["OUT_ERACK"] is null ? "" : drWorkgroup[0]["OUT_ERACK"].ToString();
                                            ineRack = drWorkgroup[0]["IN_ERACK"] is null ? "" : drWorkgroup[0]["IN_ERACK"].ToString();
                                            //bCheckEquipLookupTable = drWorkgroup[0]["CHECKEQPLOOKUPTABLE"] is null ? false : drWorkgroup[0]["CHECKEQPLOOKUPTABLE"].ToString().Equals("1") ? true : false;
                                            //_priority = drWorkgroup[0]["prio"] is null ? 20 : int.Parse(drWorkgroup[0]["prio"].ToString());
                                            bCheckRecipe = drWorkgroup[0]["CHECKCUSTDEVICE"] is null ? false : drWorkgroup[0]["CHECKCUSTDEVICE"].ToString().Equals("1") ? true : false;
                                            bCustDevice = drWorkgroup[0]["CHECKCUSTDEVICE"] is null ? false : drWorkgroup[0]["CHECKCUSTDEVICE"].ToString().Equals("1") ? true : false;
                                            _Workgroup_priority = drWorkgroup[0]["prio"] is null ? 20 : int.Parse(drWorkgroup[0]["prio"].ToString());
                                            bBindWorkgroup = drWorkgroup[0]["BINDWORKGROUP"] is not null ? drWorkgroup[0]["BINDWORKGROUP"].ToString().Equals("1") ? true : false : true;
                                            bPrepareNextWorkgroup = drWorkgroup[0]["PREPARENEXTWORKGROUP"] is not null ? drWorkgroup[0]["PREPARENEXTWORKGROUP"].ToString().Equals("1") ? true : false : true;
                                            _NextWorkgroup = drWorkgroup[0]["NEXTWORKGROUP"] is null ? "" : drWorkgroup[0]["NEXTWORKGROUP"].ToString();
                                            _iPrepareQty = drWorkgroup[0]["PREPAREQTY"] is not null ? int.Parse(drWorkgroup[0]["PREPAREQTY"].ToString()) : 0;
                                            _Workgroup = drWorkgroup[0]["WORKGROUP"] is null ? "" : drWorkgroup[0]["WORKGROUP"].ToString();
                                        }
                                        else
                                        {
                                            useFaileRack = dtWorkgroupSet.Rows[0]["USEFAILERACK"] is null ? false : dtWorkgroupSet.Rows[0]["USEFAILERACK"].ToString().Equals("1") ? true : false;
                                            faileRack = dtWorkgroupSet.Rows[0]["F_ERACK"] is null ? "" : dtWorkgroupSet.Rows[0]["F_ERACK"].ToString();
                                            outeRack = dtWorkgroupSet.Rows[0]["OUT_ERACK"] is null ? "" : dtWorkgroupSet.Rows[0]["OUT_ERACK"].ToString();
                                            ineRack = dtWorkgroupSet.Rows[0]["IN_ERACK"] is null ? "" : dtWorkgroupSet.Rows[0]["IN_ERACK"].ToString();
                                            //bCheckEquipLookupTable = dtWorkgroupSet.Rows[0]["CHECKEQPLOOKUPTABLE"] is null ? false : dtWorkgroupSet.Rows[0]["CHECKEQPLOOKUPTABLE"].ToString().Equals("1") ? true : false;
                                            bCheckRecipe = drWorkgroup[0]["CHECKCUSTDEVICE"] is null ? false : drWorkgroup[0]["CHECKCUSTDEVICE"].ToString().Equals("1") ? true : false;
                                            bCustDevice = drWorkgroup[0]["CHECKCUSTDEVICE"] is null ? false : drWorkgroup[0]["CHECKCUSTDEVICE"].ToString().Equals("1") ? true : false;
                                            //_priority = 30;
                                            _Workgroup_priority = drWorkgroup[0]["prio"] is null ? 20 : int.Parse(drWorkgroup[0]["prio"].ToString());
                                            bBindWorkgroup = drWorkgroup[0]["BINDWORKGROUP"] is not null ? drWorkgroup[0]["BINDWORKGROUP"].ToString().Equals("1") ? true : false : true;
                                            bPrepareNextWorkgroup = drWorkgroup[0]["PREPARENEXTWORKGROUP"] is not null ? drWorkgroup[0]["PREPARENEXTWORKGROUP"].ToString().Equals("1") ? true : false : true;
                                            _NextWorkgroup = drWorkgroup[0]["NEXTWORKGROUP"] is null ? "" : drWorkgroup[0]["NEXTWORKGROUP"].ToString();
                                            _iPrepareQty = drWorkgroup[0]["PREPAREQTY"] is not null ? int.Parse(drWorkgroup[0]["PREPAREQTY"].ToString()) : 0;
                                            _Workgroup = drWorkgroup[0]["WORKGROUP"] is null ? "" : drWorkgroup[0]["WORKGROUP"].ToString();
                                        }
                                    }
                                }
                                else
                                {
                                    cdtTemp = string.Format("STAGE='{0}'", "DEFAULT");
                                    drWorkgroup = dtWorkgroupSet.Select(cdtTemp);

                                    if (drWorkgroup.Length > 0)
                                    {
                                        useFaileRack = drWorkgroup[0]["USEFAILERACK"] is null ? false : drWorkgroup[0]["USEFAILERACK"].ToString().Equals("1") ? true : false;
                                        faileRack = drWorkgroup[0]["F_ERACK"] is null ? "" : drWorkgroup[0]["F_ERACK"].ToString();
                                        outeRack = drWorkgroup[0]["OUT_ERACK"] is null ? "" : drWorkgroup[0]["OUT_ERACK"].ToString();
                                        ineRack = drWorkgroup[0]["IN_ERACK"] is null ? "" : drWorkgroup[0]["IN_ERACK"].ToString();
                                        //bCheckEquipLookupTable = drWorkgroup[0]["CHECKEQPLOOKUPTABLE"] is null ? false : drWorkgroup[0]["CHECKEQPLOOKUPTABLE"].ToString().Equals("1") ? true : false;
                                        //_priority = drWorkgroup[0]["prio"] is null ? 20 : int.Parse(drWorkgroup[0]["prio"].ToString());
                                        bCheckRecipe = drWorkgroup[0]["CHECKCUSTDEVICE"] is null ? false : drWorkgroup[0]["CHECKCUSTDEVICE"].ToString().Equals("1") ? true : false;
                                        bCustDevice = drWorkgroup[0]["CHECKCUSTDEVICE"] is null ? false : drWorkgroup[0]["CHECKCUSTDEVICE"].ToString().Equals("1") ? true : false;
                                        _Workgroup_priority = drWorkgroup[0]["prio"] is null ? 20 : int.Parse(drWorkgroup[0]["prio"].ToString());
                                        bBindWorkgroup = drWorkgroup[0]["BINDWORKGROUP"] is not null ? drWorkgroup[0]["BINDWORKGROUP"].ToString().Equals("1") ? true : false : true;
                                        bPrepareNextWorkgroup = drWorkgroup[0]["PREPARENEXTWORKGROUP"] is not null ? drWorkgroup[0]["PREPARENEXTWORKGROUP"].ToString().Equals("1") ? true : false : true;
                                        _NextWorkgroup = drWorkgroup[0]["NEXTWORKGROUP"] is null ? "" : drWorkgroup[0]["NEXTWORKGROUP"].ToString();
                                        _iPrepareQty = drWorkgroup[0]["PREPAREQTY"] is not null ? int.Parse(drWorkgroup[0]["PREPAREQTY"].ToString()) : 0;
                                        _Workgroup = drWorkgroup[0]["WORKGROUP"] is null ? "" : drWorkgroup[0]["WORKGROUP"].ToString();
                                    }
                                    else
                                    {
                                        useFaileRack = dtWorkgroupSet.Rows[0]["USEFAILERACK"] is null ? false : dtWorkgroupSet.Rows[0]["USEFAILERACK"].ToString().Equals("1") ? true : false;
                                        faileRack = dtWorkgroupSet.Rows[0]["F_ERACK"] is null ? "" : dtWorkgroupSet.Rows[0]["F_ERACK"].ToString();
                                        outeRack = dtWorkgroupSet.Rows[0]["OUT_ERACK"] is null ? "" : dtWorkgroupSet.Rows[0]["OUT_ERACK"].ToString();
                                        ineRack = dtWorkgroupSet.Rows[0]["IN_ERACK"] is null ? "" : dtWorkgroupSet.Rows[0]["IN_ERACK"].ToString();
                                        //bCheckEquipLookupTable = dtWorkgroupSet.Rows[0]["CHECKEQPLOOKUPTABLE"] is null ? false : dtWorkgroupSet.Rows[0]["CHECKEQPLOOKUPTABLE"].ToString().Equals("1") ? true : false;
                                        bCheckRecipe = dtWorkgroupSet.Rows[0]["CHECKCUSTDEVICE"] is null ? false : dtWorkgroupSet.Rows[0]["CHECKCUSTDEVICE"].ToString().Equals("1") ? true : false;
                                        bCustDevice = dtWorkgroupSet.Rows[0]["CHECKCUSTDEVICE"] is null ? false : dtWorkgroupSet.Rows[0]["CHECKCUSTDEVICE"].ToString().Equals("1") ? true : false;
                                        _Workgroup_priority = dtWorkgroupSet.Rows[0]["prio"] is null ? 20 : int.Parse(dtWorkgroupSet.Rows[0]["prio"].ToString());
                                        bBindWorkgroup = dtWorkgroupSet.Rows[0]["BINDWORKGROUP"] is not null ? dtWorkgroupSet.Rows[0]["BINDWORKGROUP"].ToString().Equals("1") ? true : false : true;
                                        bPrepareNextWorkgroup = dtWorkgroupSet.Rows[0]["PREPARENEXTWORKGROUP"] is not null ? dtWorkgroupSet.Rows[0]["PREPARENEXTWORKGROUP"].ToString().Equals("1") ? true : false : true;
                                        _NextWorkgroup = dtWorkgroupSet.Rows[0]["NEXTWORKGROUP"] is null ? "" : dtWorkgroupSet.Rows[0]["NEXTWORKGROUP"].ToString();
                                        _iPrepareQty = dtWorkgroupSet.Rows[0]["PREPAREQTY"] is not null ? int.Parse(dtWorkgroupSet.Rows[0]["PREPAREQTY"].ToString()) : 0;
                                        _Workgroup = dtWorkgroupSet.Rows[0]["WORKGROUP"] is null ? "" : dtWorkgroupSet.Rows[0]["WORKGROUP"].ToString();
                                    }
                                }
                            }

                            _logger.Debug(string.Format("[WorkgroupSet][{0}][{1}]{2}[{3}][{4}][{5}][{6}][{7}]", drRecord["EQUIPID"].ToString(), vStage, useFaileRack, faileRack, outeRack, ineRack, bCheckRecipe, _Workgroup_priority));
                            
                        }
                        catch (Exception ex)
                        {
                            _logger.Debug(string.Format("[Exception][{0}] {1}", "WorkgroupSet",ex.Message));
                            continue;
                        }

                        /*
                        try 
                        {
                            bCustDevice = dtWorkgroupSet.Rows[0]["checkCustDevice"] is not null ? dtWorkgroupSet.Rows[0]["checkCustDevice"].ToString().Equals("1") ? true : false : true;
                            useFaileRack = dtWorkgroupSet.Rows[0]["USEFAILERACK"] is null ? false : dtWorkgroupSet.Rows[0]["USEFAILERACK"].ToString().Equals("1") ? true : false;
                            faileRack = dtWorkgroupSet.Rows[0]["F_ERACK"] is null ? "" : dtWorkgroupSet.Rows[0]["F_ERACK"].ToString();
                            outeRack = dtWorkgroupSet.Rows[0]["OUT_ERACK"] is null ? "" : dtWorkgroupSet.Rows[0]["OUT_ERACK"].ToString();
                            ineRack = dtWorkgroupSet.Rows[0]["IN_ERACK"] is null ? "" : dtWorkgroupSet.Rows[0]["IN_ERACK"].ToString();
                            _Workgroup_priority = dtWorkgroupSet.Rows[0]["prio"] is null ? 20 : int.Parse(dtWorkgroupSet.Rows[0]["prio"].ToString());
                            bBindWorkgroup = dtWorkgroupSet.Rows[0]["BINDWORKGROUP"] is not null ? dtWorkgroupSet.Rows[0]["BINDWORKGROUP"].ToString().Equals("1") ? true : false : true;
                            bPrepareNextWorkgroup = dtWorkgroupSet.Rows[0]["PREPARENEXTWORKGROUP"] is not null ? dtWorkgroupSet.Rows[0]["PREPARENEXTWORKGROUP"].ToString().Equals("1") ? true : false : true;
                            _NextWorkgroup = dtWorkgroupSet.Rows[0]["NEXTWORKGROUP"] is null ? "" : dtWorkgroupSet.Rows[0]["NEXTWORKGROUP"].ToString();
                            _iPrepareQty = dtWorkgroupSet.Rows[0]["PREPAREQTY"] is not null ? int.Parse(dtWorkgroupSet.Rows[0]["PREPAREQTY"].ToString()) : 0;
                            _Workgroup = dtWorkgroupSet.Rows[0]["WORKGROUP"] is null ? "" : dtWorkgroupSet.Rows[0]["WORKGROUP"].ToString();
                        }
                        catch(Exception ex) {
                            _logger.Debug(string.Format("[Exception] {0}", ex.Message));
                            continue;
                        }
                        */

                        try {
                            if (bBindWorkgroup)
                            {
                                if (ineRack.Trim().Equals(""))
                                {
                                    //back stage


                                }
                                if (outeRack.Trim().Equals(""))
                                {
                                    //front stage
                                    outeRack = "";
                                }
                            }
                        } 
                        catch(Exception ex) { }
                    }

                    if (_DebugMode)
                    {
                        _logger.Debug(string.Format("[Port_Type] {0} / {1} / {2}", drRecord["EQUIPID"].ToString(), drRecord["Port_Type"].ToString(), drRecord["port_id"].ToString()));
                    }

                    //Port_Type: in 找一個載具Carrier, Out 找目的地Dest 
                    switch (drRecord["Port_Type"])
                    {
                        case "IN":
                            //0. wait to load 找到適合的Carrier, 併產生Load指令
                            //1. wait to unload 且沒有其它符合的Carrier 直接產生Unload指令
                            //2. wait to unload 而且有其它適用的Carrier (full), 產生Swap指令(Load + Unload)
                            //2.1. Unload, 如果out port的 carrier type 相同, 產生transfer 指令至out port
                            string _tmpDest = "";
                            _commandState = "";
                            iPortState = GetPortStatus(dtPortsInfo, drRecord["port_id"].ToString(), out sPortState);
                            if (_DebugMode)
                            {
                                _logger.Debug(string.Format("[Port_Type] {0} / {1}", drRecord["EQUIPID"].ToString(), iPortState, drRecord["PORT_ID"].ToString()));
                            }

                            switch (iPortState)
                            {
                                case 1:
                                    //1. Transfer Blocked
                                    continue;
    
                                case 2:
                                case 3:
                                case 5:
                                    //2. Near Completion
                                    //3.Ready to Unload
                                    //5. Reject and Ready to unload
                                    if (iPortState == 2)
                                    {
                                        if (bNearComplete)
                                        {
                                            _commandState = "NearComp";
                                        }
                                        else
                                        {
                                            //lstTransfer.CommandState = "NearComp";
                                            _commandState = "";
                                            break;
                                        }
                                    }
                                    else if (iPortState == 3)
                                    {

                                        sql = _BaseDataService.QueryNearCompletedByPortID(drRecord["PORT_ID"].ToString());
                                        dtTemp = _dbTool.GetDataTable(sql);
                                        if (dtTemp.Rows.Count > 0)
                                        {
                                            sql = _BaseDataService.ResetStartDtByCmdID(dtTemp.Rows[0]["cmd_id"].ToString());
                                            _dbTool.SQLExec(sql, out tmpMsg, true);

                                            if (tmpMsg.Equals(""))
                                                break;
                                            else
                                                _logger.Debug(tmpMsg);
                                        }
                                    }
                                    else
                                    {
                                        _commandState = "";
                                    }

                                    lstTransfer.CommandState = _commandState;

                                    try
                                    {
                                        //rtsMachineState, rtsCurrentState, rtsDownState
                                        //Unload 不管機台狀態20230710 Mark by Vance ...YY
                                        //if(rtsCurrentState.Equals("UP") || (rtsCurrentState.Equals("DOWN") && rtsDownState.Equals("IDLE")))
                                        //{ 
                                        //    //Do Nothing
                                        //}
                                        //else
                                        //{
                                        //    _logger.Debug(string.Format("[CreateTransferCommandByPortModel][RTS Status no match.] {0} / {1} / {2} / RTS Current Status: {3}, Down State: {4}", drRecord["EQUIPID"].ToString(), drRecord["Port_Type"].ToString(), drRecord["port_id"].ToString(), rtsCurrentState, rtsDownState));
                                        //    continue;
                                        //}
                                        if(_expired)
                                        {
                                            if (isManualMode)
                                                continue;
                                        }
                                        else
                                        {
                                            //Not expired. Manual more still do Unload
                                        }

                                        bIsMatch = false;
                                        dtWorkInProcessSch = _dbTool.GetDataTable(_BaseDataService.QueryWorkInProcessSchByPortIdForUnload(drRecord["PORT_ID"].ToString(), tableOrder));
                                        if (dtWorkInProcessSch.Rows.Count > 0)
                                            continue;

                                        //取得當前Load Port上的Carrier
                                        dtLoadPortCarrier = _dbTool.GetDataTable(_BaseDataService.SelectLoadPortCarrierByEquipId(drRecord["EQUIPID"].ToString()));
                                        if (dtLoadPortCarrier.Rows.Count > 0)
                                        {
                                            drIn = dtLoadPortCarrier.Select("PORTNO = '" + drRecord["PORT_SEQ"].ToString() + "'");
                                            UnloadCarrierID = drIn is null ? "" : drIn.Length > 0 ? drIn[0]["CARRIER_ID"].ToString() : "";
                                        }

                                        if (iPortState.Equals(5))
                                        {
                                            ///還原原本的lot status
                                            if (drIn.Length > 0)
                                            {
                                                string tmpCarrier = ""; string tmplastLot = "";
                                                tmpCarrier = drIn is null ? "" : drIn.Length > 0 ? drIn[0]["CARRIER_ID"].ToString() : "";
                                                sql = _BaseDataService.SelectTableCarrierAssociateByCarrierID(tmpCarrier);
                                                tmplastLot = "";

                                            }
                                        }

                                        try
                                        {
                                            if (bBindWorkgroup)
                                            {
                                                if (ineRack.Trim().Equals(""))
                                                {
                                                    //back stage(AOI)
                                                    //Not need to load carrier
                                                    tmpRack = "";
                                                }
                                                if (outeRack.Trim().Equals(""))
                                                {
                                                    //front stage(WAFER CLEAN)

                                                }
                                            }
                                        }
                                        catch (Exception ex) { }

                                        dtAvaileCarrier = GetAvailableCarrier(_dbTool, drRecord["CARRIER_TYPE"].ToString(), true, _keyRTDEnv);
                                        //AvaileCarrier is true

                                        int iQty = 0; int iQuantity = 0; int iTotalQty = 0;
                                        int iCountOfCarr = 0;
                                        bool isLastLot = false;
                                        bool bNoFind = true;

                                        if (dtAvaileCarrier.Rows.Count > 0)
                                        {
                                            bIsMatch = false;
                                            string tmpMessage = "";

                                            foreach (DataRow draCarrier in dtAvaileCarrier.Rows)
                                            {
                                                if (_DebugMode)
                                                {
                                                    _logger.Debug(string.Format("---locate is {0}", draCarrier["locate"].ToString()));
                                                    _logger.Debug(string.Format("---out erack is {0}", dtWorkgroupSet.Rows[0]["out_erack"].ToString()));
                                                }
                                                CarrierID = "";

                                                if (_oEventQ.EventName.Equals("AbnormalyEquipmentStatus"))
                                                {
                                                    //SelectTableEquipmentPortsInfoByEquipId    
                                                    sql = string.Format(_BaseDataService.SelectTableLotInfoByLotid(draCarrier["lot_id"].ToString()));
                                                    dtTemp2 = _dbTool.GetDataTable(sql);

                                                    //由機台來找lot時, Equipment 需為主要機台(第一台)
                                                    string[] arrEqp = dtTemp2.Rows[0]["equiplist"].ToString().Split(',');
                                                    bool isFirst = false;

                                                    foreach (string tmpEqp in arrEqp)
                                                    {
                                                        sql = string.Format(_BaseDataService.SelectTableEquipmentPortsInfoByEquipId(tmpEqp));
                                                        dtTemp = _dbTool.GetDataTable(sql);

                                                        if (dtTemp.Rows.Count > 0)
                                                        {
                                                            if (dtTemp.Rows[0]["manualmode"].ToString().Equals("1"))
                                                            {
                                                                continue;
                                                            }
                                                            else
                                                            {
                                                                if (drRecord["EQUIPID"].ToString().Equals(tmpEqp))
                                                                {
                                                                    isFirst = true;
                                                                    break;
                                                                }
                                                            }

                                                            if (isFirst)
                                                            {
                                                                continue;
                                                            }
                                                            else
                                                            {

                                                            }
                                                        }
                                                    }

                                                    if (!isFirst)
                                                        continue;
                                                    //sql = string.Format(_BaseDataService.QueryEquipListFirst(draCarrier["lot_id"].ToString(), drRecord["EQUIPID"].ToString()));
                                                    //dtTemp = _dbTool.GetDataTable(sql);

                                                    //if (dtTemp.Rows.Count <= 0)
                                                    //continue;
                                                }

                                                //站點不當前站點不同, 不取這批lot
                                                if (!draCarrier["lot_id"].ToString().Equals(""))
                                                {
                                                    sql = _BaseDataService.CheckLotStage(configuration["CheckLotStage:Table"], draCarrier["lot_id"].ToString());
                                                    dtTemp = _dbTool.GetDataTable(sql);

                                                    if (dtTemp.Rows.Count <= 0)
                                                        continue;
                                                    else
                                                    {
                                                        if (!dtTemp.Rows[0]["stage1"].ToString().Equals(dtTemp.Rows[0]["stage2"].ToString()))
                                                        {
                                                            _logger.Debug(string.Format("---LotID= {0}, RTD_Stage={1}, MES_Stage={2}, RTD_State={3}, MES_State={4}", draCarrier["lot_id"].ToString(), dtTemp.Rows[0]["stage1"].ToString(), dtTemp.Rows[0]["stage2"].ToString(), dtTemp.Rows[0]["state1"].ToString(), dtTemp.Rows[0]["state2"].ToString()));
                                                        }
                                                    }
                                                }

                                                if (_portModel.Equals("1I1OT2"))
                                                {
                                                    //1I1OT2 特定機台邏輯
                                                    //IN: Lotid 會對應一個放有Matal Ring 的Cassette, 需存在才進行派送
                                                    MetalRingCarrier = "";

                                                    sql = string.Format(_BaseDataService.CheckMetalRingCarrier(draCarrier["carrier_id"].ToString()));
                                                    dtTemp = _dbTool.GetDataTable(sql);
                                                    if (dtTemp.Rows.Count > 0)
                                                    {
                                                        MetalRingCarrier = dtTemp.Rows[0]["carrier_id"].ToString();
                                                        _logger.Debug(string.Format("---MetalRingCarrier is {0}", MetalRingCarrier));
                                                        //break;
                                                        CarrierID = draCarrier["carrier_id"].ToString();
                                                    }
                                                    else
                                                    {
                                                        if (_DebugMode)
                                                            _logger.Debug(string.Format("---Can Not Find Have MetalRing Cassette."));
                                                        //Can Not Find Have MetalRing Cassette.
                                                        continue;
                                                    }
                                                }

                                                //Check Equipment CustDevice / Lot CustDevice is same.
                                                if (!EquipCustDevice.Trim().Equals(""))
                                                {
                                                    string device = "";
                                                    string rtdLotDevice = "";
                                                    sql = _BaseDataService.QueryLotInfoByCarrierID(draCarrier["carrier_id"].ToString());
                                                    dtTemp = _dbTool.GetDataTable(sql);
                                                    if (dtTemp.Rows.Count > 0)
                                                    {
                                                        rtdLotDevice = dtTemp.Rows[0]["custdevice"] is null ? "" : dtTemp.Rows[0]["custdevice"].ToString();

                                                        //DataTable dtTemp2;
                                                        sql = _BaseDataService.QueryDataByLotid(dtTemp.Rows[0]["lot_id"].ToString(), GetExtenalTables(configuration, "SyncExtenalData", "AdsInfo"));
                                                        dtTemp2 = _dbTool.GetDataTable(sql);
                                                        //select lot_age, custDevice from semi_int.rtd_cis_ads_vw @SEMI_INT where lotid = '83727834.1';
                                                        if (dtTemp2.Rows.Count > 0)
                                                        {

                                                            device = dtTemp2.Rows[0]["custdevice"].ToString();
                                                            if (!device.Trim().Equals(""))
                                                            {
                                                                if (!device.Equals(rtdLotDevice))
                                                                {
                                                                    sql = _BaseDataService.UpdateCustDeviceByLotID(dtTemp.Rows[0]["lot_id"].ToString(), device);
                                                                    _dbTool.SQLExec(sql, out tmpMsg, true);
                                                                }

                                                                if (bCustDevice)
                                                                {
                                                                    if (!device.Equals(EquipCustDevice))
                                                                    {
                                                                        _logger.Debug(string.Format("[CreateTransferCommandByPortModel][{0}][{1}] {2} / {3}", _oEventQ.EventName, draCarrier["carrier_id"].ToString(), device, EquipCustDevice));

                                                                        MetalRingCarrier = "";
                                                                        continue;
                                                                    }
                                                                }
                                                            }
                                                            else
                                                            {
                                                                MetalRingCarrier = "";

                                                                ///the lot custDevice is empty, the carrier can not dispatching to tools.
                                                                _logger.Debug(string.Format("[CreateTransferCommandByPortModel][{0}][{1}] {2} / {3}, lot custDevice is null.", _oEventQ.EventName, draCarrier["carrier_id"].ToString(), device, EquipCustDevice));
                                                                continue;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            ///can not get lot from ads system.
                                                            _logger.Debug(string.Format("[CreateTransferCommandByPortModel][{0}][{1}] Can not get lot from ads.", _oEventQ.EventName, draCarrier["carrier_id"].ToString(), device, EquipCustDevice));
                                                            continue;
                                                        }
                                                    }
                                                    else
                                                        continue;
                                                }
                                                

                                                iQty = 0; iTotalQty = 0; iQuantity = 0; iCountOfCarr = 0;

                                                //Check Workgroup Set 
                                                bNoFind = true;
                                                sql = _BaseDataService.QueryRackByGroupID(dtWorkgroupSet.Rows[0]["in_erack"].ToString());
                                                dtTemp = _dbTool.GetDataTable(sql);
                                                bool blocatecheck = false;
                                                foreach (DataRow drRack in dtTemp.Rows)
                                                {
                                                    if (drRack["MAC"].ToString().Equals("STOCK"))
                                                        blocatecheck = true;

                                                    if (blocatecheck)
                                                    {
                                                        if (draCarrier["locate"].ToString().StartsWith(drRack["erackID"].ToString()))
                                                        {
                                                            bNoFind = false;
                                                            break;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        if (draCarrier["locate"].ToString().StartsWith(drRack["erackID"].ToString()))
                                                        {
                                                            bNoFind = false;
                                                            break;
                                                        }
                                                    }
                                                }

                                                if (bNoFind)
                                                {
                                                    continue;
                                                }

                                                //if (!draCarrier["locate"].ToString().Equals(dtWorkgroupSet.Rows[0]["in_erack"]))
                                                //    continue;

                                                iQuantity = draCarrier.Table.Columns.Contains("quantity") ? int.Parse(draCarrier["quantity"].ToString()) : 0;
                                                //檢查Cassette Qty總和, 是否與Lot Info Qty相同, 相同才可派送 (滿足相同lot要放在相同機台上的需求)
                                                string sqlSentence = _BaseDataService.CheckQtyforSameLotId(draCarrier["lot_id"].ToString(), drRecord["CARRIER_TYPE"].ToString());
                                                DataTable dtSameLot = new DataTable();
                                                dtSameLot = _dbTool.GetDataTable(sqlSentence);
                                                if (dtSameLot.Rows.Count > 0)
                                                {
                                                    ///iQty is sum same lot casette.
                                                    iQty = int.Parse(dtSameLot.Rows[0]["qty"].ToString());
                                                    ///iTotalQty is lot total quantity.
                                                    iTotalQty = int.Parse(dtSameLot.Rows[0]["total_qty"].ToString());
                                                    iCountOfCarr = int.Parse(dtSameLot.Rows[0]["NumOfCarr"].ToString());

                                                    if (iCountOfCarr > 1)
                                                    {
                                                        if (iQty == iTotalQty)
                                                        { //To Do...
                                                            isLastLot = true;
                                                            sql = string.Format(_BaseDataService.LockMachineByLot(draCarrier["lot_id"].ToString(), iQuantity, 0));
                                                            _dbTool.SQLExec(sql, out tmpMsg, true);
                                                        }
                                                        else
                                                        {
                                                            if (iQty <= iQuantity)
                                                            {
                                                                isLastLot = true;
                                                                sql = string.Format(_BaseDataService.LockMachineByLot(draCarrier["lot_id"].ToString(), iQuantity, 0));
                                                                _dbTool.SQLExec(sql, out tmpMsg, true);
                                                            }
                                                            else
                                                            {
                                                                isLastLot = false;
                                                                sql = string.Format(_BaseDataService.LockMachineByLot(draCarrier["lot_id"].ToString(), iQuantity, 1));
                                                                _dbTool.SQLExec(sql, out tmpMsg, true);
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        if (iQty < iTotalQty)
                                                        {
                                                            int _lockMachine = 0;
                                                            int _compQty = 0;
                                                            //draCarrier["lot_id"].ToString()
                                                            sql = string.Format(_BaseDataService.SelectTableLotInfoByLotid(draCarrier["lot_id"].ToString()));
                                                            dtTemp = _dbTool.GetDataTable(sql);
                                                            _lockMachine = dtTemp.Rows[0]["lockmachine"].ToString().Equals("1") ? 1 : 0;
                                                            _compQty = dtTemp.Rows[0]["comp_qty"].ToString().Equals("0") ? 0 : int.Parse(dtTemp.Rows[0]["comp_qty"].ToString());

                                                            if (_lockMachine.Equals(0) && _compQty == 0)
                                                            {
                                                                isLastLot = false;
                                                                sql = string.Format(_BaseDataService.LockMachineByLot(draCarrier["lot_id"].ToString(), iQuantity, 1));
                                                                _dbTool.SQLExec(sql, out tmpMsg, true);
                                                            }
                                                            else
                                                            {
                                                                if (_compQty + iQuantity >= iTotalQty)
                                                                {
                                                                    isLastLot = true;
                                                                    sql = string.Format(_BaseDataService.LockMachineByLot(draCarrier["lot_id"].ToString(), _compQty + iQuantity, 0));
                                                                    _dbTool.SQLExec(sql, out tmpMsg, true);
                                                                }
                                                                else
                                                                    isLastLot = false;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            isLastLot = true;
                                                            sql = string.Format(_BaseDataService.LockMachineByLot(draCarrier["lot_id"].ToString(), iQuantity, 0));
                                                            _dbTool.SQLExec(sql, out tmpMsg, true);
                                                        }
                                                    }
                                                }

                                                if (CheckIsAvailableLot(_dbTool, draCarrier["lot_id"].ToString(), drRecord["EquipID"].ToString()))
                                                {
                                                    CarrierID = draCarrier["Carrier_ID"].ToString();
                                                    bIsMatch = true;
                                                }
                                                else
                                                {
                                                    bIsMatch = false;
                                                    continue;
                                                }

                                                if (bIsMatch)
                                                {
                                                    _dbTool.SQLExec(_BaseDataService.UpdateTableReserveCarrier(CarrierID, true), out tmpMessage, true);
                                                    break;
                                                }
                                            }

                                            if (!tmpMessage.Equals(""))
                                            {
                                                _dbTool.SQLExec(_BaseDataService.UpdateTableReserveCarrier(CarrierID, false), out tmpMessage, true);
                                                continue;
                                            }
                                        }

                                        if (dtAvaileCarrier.Rows.Count <= 0 || !bIsMatch || bNoFind || isManualMode)
                                        {
                                            if(useFaileRack)
                                            {
                                                _tmpDest = faileRack;
                                            }
                                            else
                                            {
                                                _tmpDest = drRecord["IN_ERACK"].ToString();
                                            }

                                            CarrierID = drIn is null ? "" : drIn.Length > 0 ? drIn[0]["CARRIER_ID"].ToString() : "";

                                            lstTransfer.CommandType = "UNLOAD";
                                            lstTransfer.Source = drRecord["PORT_ID"].ToString();
                                            //lstTransfer.Dest = drRecord["IN_ERACK"].ToString();
                                            if (_portModel.Equals("1I1OT2"))
                                                lstTransfer.Dest = drRecord["OUT_ERACK"].ToString();
                                            else
                                                lstTransfer.Dest = _tmpDest;

                                            lstTransfer.CarrierID = UnloadCarrierID.Equals("") ? "*" : UnloadCarrierID;
                                            lstTransfer.Quantity = 0;
                                            //lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : "";
                                            lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString().Equals("") ? _CarrierTypebyPort : dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : _CarrierTypebyPort;
                                            break;
                                        }
                                        //AvaileCarrier is true
                                        if (bIsMatch)
                                        {
                                            drCarrierData = dtAvaileCarrier.Select("carrier_id = '" + CarrierID + "'");
                                            if (_DebugMode)
                                                _logger.Debug(string.Format("[drCarrierData][{0}] {1} / {2}", drRecord["PORT_ID"].ToString(), drCarrierData[0]["COMMAND_TYPE"].ToString(), CarrierID));

                                        }

                                        if(rtsCurrentState.Equals("UP") || (rtsCurrentState.Equals("DOWN") && rtsDownState.Equals("IDLE")))
                                        {
                                            if (!isManualMode)
                                            {
                                                //如果Reject時, 設備不處於Manual Mode才產生Load 指令

                                                lstTransfer.CommandType = "LOAD";
                                                lstTransfer.Source = "*";
                                                lstTransfer.Dest = drRecord["PORT_ID"].ToString();
                                                lstTransfer.CarrierID = CarrierID;
                                                lstTransfer.Quantity = int.Parse(drCarrierData.Length > 0 ? drCarrierData[0]["QUANTITY"].ToString() : "0");
                                                lstTransfer.CarrierType = drCarrierData.Length > 0 ? drCarrierData[0]["COMMAND_TYPE"].ToString() : "";
                                                lstTransfer.Total = iTotalQty;
                                                lstTransfer.IsLastLot = isLastLot ? 1 : 0;
                                                lstTransfer.CommandState = _commandState;
                                                //normalTransfer.Transfer.Add(lstTransfer);
                                                //iReplace++;
                                            }
                                        }
                                        else
                                        {
                                            sql = string.Format(_BaseDataService.UpdateTableEQP_STATUS(drRecord["EQUIPID"].ToString(), _BaseDataService.GetEquipState(rtsCurrentState), rtsMachineState, rtsDownState));
                                            _dbTool.SQLExec(sql, out tmpMsg, true);
                                            _logger.Debug(string.Format("[CreateTransferCommandByPortModel][RTS Status no match.] {0} / {1} / {2} / RTS Current Status: {3}, Down State: {4}", drRecord["EQUIPID"].ToString(), drRecord["Port_Type"].ToString(), drRecord["port_id"].ToString(), rtsCurrentState, rtsDownState));
                                            continue;
                                        }

                                        CarrierID = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["CARRIER_ID"].ToString() : "";

                                        if (_portModel.Equals("1I1OT2"))
                                        {
                                            CarrierID = drIn is null ? "" : drIn.Length > 0 ? drIn[0]["CARRIER_ID"].ToString() : "";

                                            normalTransfer.Transfer.Add(lstTransfer);
                                            iReplace++;

                                            lstTransfer = new TransferList();

                                            lstTransfer.CommandType = "UNLOAD";
                                            lstTransfer.Source = drRecord["PORT_ID"].ToString();
                                            lstTransfer.Dest = drRecord["OUT_ERACK"].ToString();
                                            lstTransfer.CarrierID = UnloadCarrierID.Equals("") ? "*" : UnloadCarrierID;
                                            lstTransfer.Quantity = 0;
                                            //lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : "";
                                            lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString().Equals("") ? _CarrierTypebyPort : dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : _CarrierTypebyPort;
                                            lstTransfer.CommandState = _commandState;
                                            break;
                                        }
                                        //檢查Out port 與 In port 的carrier type是否相同
                                        drOut = dtPortsInfo.Select("Port_Type='OUT'");
                                        if (drOut[0]["CARRIER_TYPE"].ToString().Equals(drRecord["CARRIER_TYPE"].ToString()))
                                        {
                                            string tmpPortState = "";
                                            CarrierID = drIn is null ? "" : drIn.Length > 0 ? drIn[0]["CARRIER_ID"].ToString() : "";
                                            //確認Out port是否處於Wait to Unload
                                            switch (GetPortStatus(dtPortsInfo, drOut[0]["port_id"].ToString(), out tmpPortState))
                                            {
                                                case 0:
                                                case 1:
                                                    break;
                                                case 2:
                                                case 3:
                                                case 4:
                                                    normalTransfer.Transfer.Add(lstTransfer);
                                                    iReplace++;

                                                    lstTransfer = new TransferList();
                                                    lstTransfer.CommandType = "TRANS";
                                                    lstTransfer.Source = drRecord["PORT_ID"].ToString();
                                                    lstTransfer.Dest = drOut[0]["PORT_ID"].ToString();
                                                    lstTransfer.CarrierID = CarrierID;
                                                    lstTransfer.Quantity = int.Parse(dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["QUANTITY"].ToString() : "0");
                                                    //lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : "";
                                                    lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString().Equals("") ? drRecord["CARRIER_TYPE"].ToString() : dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : "";
                                                    lstTransfer.CommandState = _commandState;
                                                    //normalTransfer.Transfer.Add(lstTransfer);
                                                    break;
                                                default:
                                                    CarrierID = drIn is null ? "" : drIn.Length > 0 ? drIn[0]["CARRIER_ID"].ToString() : "";

                                                    normalTransfer.Transfer.Add(lstTransfer);
                                                    iReplace++;

                                                    lstTransfer = new TransferList();

                                                    lstTransfer.CommandType = "UNLOAD";
                                                    lstTransfer.Source = drRecord["PORT_ID"].ToString();
                                                    lstTransfer.Dest = drRecord["IN_ERACK"].ToString();
                                                    lstTransfer.CarrierID = UnloadCarrierID.Equals("") ? "*" : UnloadCarrierID;
                                                    lstTransfer.Quantity = 0;
                                                    //lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : "";
                                                    lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString().Equals("") ? _CarrierTypebyPort : dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : _CarrierTypebyPort;
                                                    lstTransfer.CommandState = _commandState;
                                                    break;
                                            }

                                        }
                                        else
                                        {
                                            //Input vs Output Carrier Type is diffrence. do unload
                                            if (useFaileRack)
                                            {
                                                _tmpDest = faileRack;
                                            }
                                            else
                                            {
                                                _tmpDest = drRecord["IN_ERACK"].ToString();
                                            }

                                            CarrierID = drIn is null ? "" : drIn.Length > 0 ? drIn[0]["CARRIER_ID"].ToString() : "";

                                            normalTransfer.Transfer.Add(lstTransfer);
                                            iReplace++;

                                            lstTransfer = new TransferList();

                                            lstTransfer.CommandType = "UNLOAD";
                                            lstTransfer.Source = drRecord["PORT_ID"].ToString();
                                            lstTransfer.Dest = _tmpDest;
                                            lstTransfer.CarrierID = UnloadCarrierID.Equals("") ? "*" : UnloadCarrierID;
                                            lstTransfer.Quantity = 0;
                                            //lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : "";
                                            lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString().Equals("") ? _CarrierTypebyPort : dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : _CarrierTypebyPort;
                                            lstTransfer.CommandState = _commandState;
                                            break;
                                        }
                                    }
                                    catch (Exception ex)
                                    { }

                                    break;
                                case 4:
                                    //4. Empty (Ready to load)
                                    try
                                    {
                                        //rtsMachineState, rtsCurrentState, rtsDownState
                                        if (rtsCurrentState.Equals("UP") || (rtsCurrentState.Equals("DOWN") && rtsDownState.Equals("IDLE")))
                                        {
                                            //Do Nothing
                                            if (isManualMode)
                                                continue;
                                        }
                                        else
                                        {
                                            _logger.Debug(string.Format("[CreateTransferCommandByPortModel][RTS Status no match.] {0} / {1} / {2} / RTS Current Status: {3}, Down State: {4}", drRecord["EQUIPID"].ToString(), drRecord["Port_Type"].ToString(), drRecord["port_id"].ToString(), rtsCurrentState, rtsDownState));
                                            continue;
                                        }

                                        if (bStageIssue)
                                        {
                                            sql = _BaseDataService.LockLotInfoWhenReady(lotID);
                                            _dbTool.SQLExec(sql, out tmpMsg, true);
                                            if (_DebugMode)
                                            {
                                                _logger.Debug(string.Format("[StageIssue] {0} / {1} Load command create faild.", drRecord["EQUIPID"].ToString(), lotID));
                                            }

                                            continue;
                                        }

                                        dtWorkInProcessSch = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSchByPortId(drRecord["PORT_ID"].ToString(), tableOrder));
                                        if (dtWorkInProcessSch.Rows.Count > 0)
                                            continue;

                                        try
                                        {
                                            if (bBindWorkgroup)
                                            {
                                                if (ineRack.Trim().Equals(""))
                                                {
                                                    //back stage(AOI)
                                                    //Not need to load carrier
                                                    tmpRack = "";
                                                }
                                                if (outeRack.Trim().Equals(""))
                                                {
                                                    //front stage(WAFER CLEAN)

                                                }
                                            }
                                        }
                                        catch (Exception ex) { }

                                        bIsMatch = false;
                                        dtAvaileCarrier = GetAvailableCarrier(_dbTool, drRecord["CARRIER_TYPE"].ToString(), true, _keyRTDEnv);
                                        if (dtAvaileCarrier is null)
                                            continue;
                                        if (dtAvaileCarrier.Rows.Count <= 0)
                                            continue;

                                        int iQty = 0; int iQuantity = 0; int iTotalQty = 0;
                                        int iCountOfCarr = 0;
                                        bool isLastLot = false;

                                        if (dtAvaileCarrier.Rows.Count > 0)
                                        {
                                            bIsMatch = false;
                                            string tmpMessage = "";
                                            MetalRingCarrier = "";

                                            foreach (DataRow draCarrier in dtAvaileCarrier.Rows)
                                            {
                                                if (_DebugMode)
                                                {
                                                    _logger.Debug(string.Format("---locate is {0}", draCarrier["locate"].ToString()));
                                                    _logger.Debug(string.Format("---in erack is {0}", dtWorkgroupSet.Rows[0]["in_erack"].ToString()));
                                                }
                                                CarrierID = "";

                                                if (_oEventQ.EventName.Equals("AbnormalyEquipmentStatus"))
                                                {
                                                    //SelectTableEquipmentPortsInfoByEquipId    
                                                    sql = string.Format(_BaseDataService.SelectTableLotInfoByLotid(draCarrier["lot_id"].ToString()));
                                                    dtTemp2 = _dbTool.GetDataTable(sql);

                                                    //由機台來找lot時, Equipment 需為主要機台(第一台)
                                                    string[] arrEqp = dtTemp2.Rows[0]["equiplist"].ToString().Split(',');
                                                    bool isFirst = false;

                                                    foreach (string tmpEqp in arrEqp)
                                                    {
                                                        sql = string.Format(_BaseDataService.SelectTableEquipmentPortsInfoByEquipId(tmpEqp));
                                                        dtTemp = _dbTool.GetDataTable(sql);

                                                        if (dtTemp.Rows.Count > 0)
                                                        {
                                                            if (dtTemp.Rows[0]["manualmode"].ToString().Equals("1"))
                                                            {
                                                                continue;
                                                            }
                                                            else
                                                            {
                                                                if (drRecord["EQUIPID"].ToString().Equals(tmpEqp))
                                                                {
                                                                    isFirst = true;
                                                                    break;
                                                                }
                                                            }

                                                            if (isFirst)
                                                            {
                                                                continue;
                                                            }
                                                            else
                                                            { 
                                                                
                                                            }
                                                        }
                                                    }

                                                    if(!isFirst)
                                                        continue;
                                                    //sql = string.Format(_BaseDataService.QueryEquipListFirst(draCarrier["lot_id"].ToString(), drRecord["EQUIPID"].ToString()));
                                                    //dtTemp = _dbTool.GetDataTable(sql);

                                                    //if (dtTemp.Rows.Count <= 0)
                                                        //continue;
                                                }

                                                if (_portModel.Equals("1I1OT2"))
                                                {
                                                    _logger.Debug(string.Format("---_portModel is {0} / {1}", _portModel, draCarrier["carrier_id"].ToString()));
                                                    //1I1OT2 特定機台邏輯
                                                    //IN: Lotid 會對應一個放有Matal Ring 的Cassette, 需存在才進行派送
                                                    if (MetalRingCarrier.Equals(""))
                                                    {
                                                        sql = string.Format(_BaseDataService.CheckMetalRingCarrier(draCarrier["carrier_id"].ToString()));
                                                        dtTemp = _dbTool.GetDataTable(sql);
                                                        if (dtTemp.Rows.Count > 0)
                                                        {
                                                            MetalRingCarrier = dtTemp.Rows[0]["carrier_id"].ToString();
                                                            _logger.Debug(string.Format("---MetalRingCarrier is {0}", MetalRingCarrier));
                                                            //break;
                                                            CarrierID = draCarrier["carrier_id"].ToString();
                                                        }
                                                        else
                                                        {
                                                            if (_DebugMode)
                                                                _logger.Debug(string.Format("---Can Not Find Have MetalRing Cassette."));
                                                            //Can Not Find Have MetalRing Cassette.
                                                            continue;
                                                        }
                                                    }
                                                    else
                                                        continue;
                                                }

                                                if (!EquipCustDevice.Trim().Equals(""))
                                                {
                                                    string device = "";
                                                    sql = _BaseDataService.QueryLotInfoByCarrierID(draCarrier["carrier_id"].ToString());
                                                    dtTemp = _dbTool.GetDataTable(sql);
                                                    if (dtTemp.Rows.Count > 0)
                                                    {
                                                        //DataTable dtTemp2;
                                                        sql = _BaseDataService.QueryDataByLotid(dtTemp.Rows[0]["lot_id"].ToString(), GetExtenalTables(configuration, "SyncExtenalData", "AdsInfo"));
                                                        dtTemp2 = _dbTool.GetDataTable(sql);
                                                        //select lot_age, custDevice from semi_int.rtd_cis_ads_vw @SEMI_INT where lotid = '83727834.1';
                                                        if (dtTemp2.Rows.Count > 0)
                                                        {
                                                            device = dtTemp2.Rows[0]["custdevice"].ToString();
                                                            if (!device.Trim().Equals(""))
                                                            {
                                                                //_logger.Debug(string.Format("[CreateTransferCommandByPortModel][{0}] device[{1}] EquipCustDevice[{2}]", _oEventQ.EventName, device, EquipCustDevice));

                                                                if (!device.Equals(EquipCustDevice))
                                                                {
                                                                    _logger.Debug(string.Format("[CreateTransferCommandByPortModel][{0}][{1}] {2} / {3}", _oEventQ.EventName, draCarrier["carrier_id"].ToString(), device, EquipCustDevice));

                                                                    MetalRingCarrier = "";
                                                                    continue;
                                                                }
                                                            }
                                                            else
                                                            {
                                                                MetalRingCarrier = "";
                                                                ///the lot custDevice is empty, the carrier can not dispatching to tools.
                                                                _logger.Debug(string.Format("[CreateTransferCommandByPortModel][{0}][{1}] {2} / {3}, lot custDevice is null.", _oEventQ.EventName, draCarrier["carrier_id"].ToString(), device, EquipCustDevice));
                                                                continue;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            ///can not get lot from ads system.
                                                            _logger.Debug(string.Format("[CreateTransferCommandByPortModel][{0}][{1}] Can not get lot from ads.", _oEventQ.EventName, draCarrier["carrier_id"].ToString(), device, EquipCustDevice));
                                                            continue;
                                                        }
                                                    }
                                                    else
                                                        continue;
                                                }

                                                iQty = 0; iTotalQty = 0; iQuantity = 0; iCountOfCarr = 0;

                                                //Check Workgroup Set 
                                                bool bNoFind = true;
                                                sql = _BaseDataService.QueryRackByGroupID(dtWorkgroupSet.Rows[0]["in_erack"].ToString());
                                                dtTemp = _dbTool.GetDataTable(sql);
                                                bool blocatecheck = false;
                                                foreach (DataRow drRack in dtTemp.Rows)
                                                {
                                                    if(drRack["MAC"].ToString().Equals("STOCK"))
                                                        blocatecheck = true;

                                                    if (blocatecheck)
                                                    {
                                                        if (draCarrier["locate"].ToString().StartsWith(drRack["erackID"].ToString()))
                                                        {
                                                            bNoFind = false;
                                                            break;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        if (draCarrier["locate"].ToString().StartsWith(drRack["erackID"].ToString()))
                                                        {
                                                            bNoFind = false;
                                                            break;
                                                        }
                                                    }
                                                }

                                                if (bNoFind)
                                                {
                                                    if (_DebugMode)
                                                        _logger.Debug(string.Format("---Can Not Find eRack By GroupID."));

                                                    continue;
                                                }

                                                //if (!draCarrier["locate"].ToString().Equals(dtWorkgroupSet.Rows[0]["in_erack"]))
                                                //    continue;

                                                iQuantity = draCarrier.Table.Columns.Contains("quantity") ? int.Parse(draCarrier["quantity"].ToString()) : 0;
                                                //檢查Cassette Qty總和, 是否與Lot Info Qty相同, 相同才可派送 (滿足相同lot要放在相同機台上的需求)
                                                string sqlSentence = _BaseDataService.CheckQtyforSameLotId(draCarrier["lot_id"].ToString(), drRecord["CARRIER_TYPE"].ToString());
                                                DataTable dtSameLot = new DataTable();
                                                dtSameLot = _dbTool.GetDataTable(sqlSentence);
                                                if (dtSameLot.Rows.Count > 0)
                                                {
                                                    iQty = int.Parse(dtSameLot.Rows[0]["qty"].ToString());
                                                    iTotalQty = int.Parse(dtSameLot.Rows[0]["total_qty"].ToString());
                                                    iCountOfCarr = int.Parse(dtSameLot.Rows[0]["NumOfCarr"].ToString());

                                                    if (iCountOfCarr > 1)
                                                    {
                                                        if (iQty == iTotalQty)
                                                        { //To Do...
                                                            isLastLot = true;
                                                            sql = string.Format(_BaseDataService.LockMachineByLot(draCarrier["lot_id"].ToString(), iQuantity, 0));
                                                            _dbTool.SQLExec(sql, out tmpMsg, true);
                                                            _logger.Debug(tmpMsg);
                                                            tmpMsg = String.Format("=======IO==2==Load Carrier, Total Qty is {0}. Qty is {1}", iTotalQty, iQty);
                                                            _logger.Debug(tmpMsg);
                                                        }
                                                        else
                                                        {
                                                            tmpMsg = String.Format("=======IO==1==Load Carrier, Total Qty is {0}. Qty is {1}", iTotalQty, iQty);
                                                            _logger.Debug(tmpMsg);

                                                            if (iQty <= iQuantity)
                                                            {
                                                                isLastLot = true;
                                                                sql = string.Format(_BaseDataService.LockMachineByLot(draCarrier["lot_id"].ToString(), iQuantity, 0));
                                                                _dbTool.SQLExec(sql, out tmpMsg, true);
                                                            }
                                                            else
                                                            {
                                                                isLastLot = false;
                                                                sql = string.Format(_BaseDataService.LockMachineByLot(draCarrier["lot_id"].ToString(), iQuantity, 1));
                                                                _dbTool.SQLExec(sql, out tmpMsg, true);
                                                            }

                                                            if (iQty < iTotalQty)
                                                                continue;   //不搬運, 由unload 去發送(相同lot 需要由同一個port 執行)
                                                        }
                                                    }
                                                    else
                                                    {
                                                        if (iQty < iTotalQty)
                                                        {
                                                            int _lockMachine = 0;
                                                            int _compQty = 0;
                                                            //draCarrier["lot_id"].ToString()
                                                            sql = string.Format(_BaseDataService.SelectTableLotInfoByLotid(draCarrier["lot_id"].ToString()));
                                                            dtTemp = _dbTool.GetDataTable(sql);
                                                            _lockMachine = dtTemp.Rows[0]["lockmachine"].ToString().Equals("1") ? 1 : 0;
                                                            _compQty = dtTemp.Rows[0]["comp_qty"].ToString().Equals("0") ? 0 : int.Parse(dtTemp.Rows[0]["comp_qty"].ToString());

                                                            if (_lockMachine.Equals(0) && _compQty == 0)
                                                            {
                                                                isLastLot = false;
                                                                sql = string.Format(_BaseDataService.LockMachineByLot(draCarrier["lot_id"].ToString(), iQuantity, 1));
                                                                _dbTool.SQLExec(sql, out tmpMsg, true);
                                                            }
                                                            else
                                                            {
                                                                if (_compQty + iQuantity >= iTotalQty)
                                                                {
                                                                    isLastLot = true;
                                                                    sql = string.Format(_BaseDataService.LockMachineByLot(draCarrier["lot_id"].ToString(), _compQty + iQuantity, 0));
                                                                    _dbTool.SQLExec(sql, out tmpMsg, true);
                                                                }
                                                                else
                                                                    isLastLot = false;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            isLastLot = true;
                                                            sql = string.Format(_BaseDataService.LockMachineByLot(draCarrier["lot_id"].ToString(), iQuantity, 0));
                                                            _dbTool.SQLExec(sql, out tmpMsg, true);
                                                        }
                                                    }
                                                }

                                                if (CarrierID.Equals(""))
                                                {
                                                    if (CheckIsAvailableLot(_dbTool, draCarrier["lot_id"].ToString(), drRecord["EquipID"].ToString()))
                                                    {
                                                        CarrierID = draCarrier["Carrier_ID"].ToString();
                                                        bIsMatch = true;
                                                    }
                                                    else
                                                    {
                                                        MetalRingCarrier = "";
                                                        bIsMatch = false;
                                                        if (_DebugMode)
                                                            _logger.Debug(string.Format("[IsMatch][{0}] {1} / {2} / {3}", drRecord["PORT_ID"].ToString(), bIsMatch, draCarrier["lot_id"].ToString(), drRecord["EquipID"].ToString()));
                                                        continue;
                                                    }
                                                }
                                                else
                                                    bIsMatch = true;

                                                if (bIsMatch)
                                                {
                                                    _dbTool.SQLExec(_BaseDataService.UpdateTableReserveCarrier(CarrierID, true), out tmpMessage, true);
                                                    drCarrierData = dtAvaileCarrier.Select("carrier_id = '" + CarrierID + "'");
                                                    if (_DebugMode)
                                                        _logger.Debug(string.Format("[drCarrierData][{0}] {1} / {2} / {3}", drRecord["PORT_ID"].ToString(), bIsMatch, drCarrierData[0]["COMMAND_TYPE"].ToString(), CarrierID));

                                                    break;
                                                }
                                            }

                                            if (!bIsMatch)
                                                break;

                                            if (!tmpMessage.Equals(""))
                                            {
                                                _logger.Debug(string.Format("[tmpMessage] {0} / {1}", drRecord["EQUIPID"].ToString(), tmpMessage));
                                                _dbTool.SQLExec(_BaseDataService.UpdateTableReserveCarrier(CarrierID, false), out tmpMessage, true);
                                                continue;
                                            }
                                            drCarrierData = dtAvaileCarrier.Select("carrier_id = '" + CarrierID + "'");
                                            if (_DebugMode)
                                                _logger.Debug(string.Format("[drCarrierData][{0}] {1} / {2}", drRecord["PORT_ID"].ToString(), drCarrierData[0]["COMMAND_TYPE"].ToString(), CarrierID));

                                        }

                                        if (!isManualMode)
                                        {
                                            lstTransfer.CommandType = "LOAD";
                                            lstTransfer.Source = "*";
                                            lstTransfer.Dest = drRecord["PORT_ID"].ToString();
                                            lstTransfer.CarrierID = CarrierID;
                                            lstTransfer.Quantity = int.Parse(drCarrierData.Length > 0 ? drCarrierData[0]["QUANTITY"].ToString() : "0");
                                            lstTransfer.CarrierType = drCarrierData.Length > 0 ? drCarrierData[0]["COMMAND_TYPE"].ToString() : "";
                                            lstTransfer.Total = iTotalQty;
                                            lstTransfer.IsLastLot = isLastLot ? 1 : 0;
                                            lstTransfer.CommandState = _commandState;
                                        }
                                    }
                                    catch (Exception ex)
                                    { }
                                    break;
                                case 0:
                                default:
                                    continue;
                                    break;
                            }

                            break;
                        case "OUT":
                            //0. wait to load 找到適合的Carrier, 並產生Load指令
                            //1. wait to unload 且沒有其它符合的Carrier 直接產生Unload指令
                            //2. wait to unload 而且有其它適用的Carrier(empty), 產生Load + Unload指令
                            //2.1. Load, 如果與in port的 carrier type 相同, 就不產生Load

                            //In port 與 Out port 的carrier type是否相同
                            bIsMatch = false;
                            bool bPortTypeSame = false;
                            drIn = dtPortsInfo.Select("Port_Type='IN'");
                            if (_portModel.Equals("2I2OT1"))
                            {
                                bPortTypeSame = false;
                            }
                            else
                            {
                                if (drIn[0]["CARRIER_TYPE"].ToString().Equals(drRecord["CARRIER_TYPE"].ToString()))
                                    bPortTypeSame = true;
                            }
                            dtLoadPortCarrier = _dbTool.GetDataTable(_BaseDataService.SelectLoadPortCarrierByEquipId(drRecord["EQUIPID"].ToString()));
                            if (dtLoadPortCarrier is not null)
                                drOut = dtLoadPortCarrier.Select("PORTNO = '" + drRecord["PORT_SEQ"].ToString() + "'");

                            iPortState = GetPortStatus(dtPortsInfo, drRecord["port_id"].ToString(), out sPortState);
                            if (_DebugMode)
                            {
                                _logger.Debug(string.Format("[Port_Type] {0} / {1}", drRecord["EQUIPID"].ToString(), iPortState));
                            }
                            switch (iPortState)
                            {
                                case 1:
                                    //1. Transfer Blocked
                                    continue;
                                case 2:
                                case 3:
                                case 5:
                                    //2. Near Completion
                                    //3.Ready to Unload
                                    //5. Reject and Ready to unload
                                    if (iPortState == 2)
                                    {
                                        if (bNearComplete)
                                        {
                                            _commandState = "NearComp";
                                        }
                                        else
                                        {
                                            //lstTransfer.CommandState = "NearComp";
                                            _commandState = "";
                                            break;
                                        }
                                    }
                                    else if (iPortState == 3)
                                    {

                                        sql = _BaseDataService.QueryNearCompletedByPortID(drRecord["PORT_ID"].ToString());
                                        dtTemp = _dbTool.GetDataTable(sql);
                                        if (dtTemp.Rows.Count > 0)
                                        {
                                            sql = _BaseDataService.ResetStartDtByCmdID(dtTemp.Rows[0]["cmd_id"].ToString());
                                            _dbTool.SQLExec(sql, out tmpMsg, true);

                                            if (tmpMsg.Equals(""))
                                                break;
                                            else
                                                _logger.Debug(tmpMsg);
                                        }
                                    }
                                    else
                                    {
                                        _commandState = "";
                                    }
                                    lstTransfer.CommandState = _commandState;

                                    try
                                    {
                                        //rtsMachineState, rtsCurrentState, rtsDownState
                                        //unload not need check rts state, Mark by Vance 20230710 YY req
                                        //if (rtsCurrentState.Equals("UP") || (rtsCurrentState.Equals("DOWN") && rtsDownState.Equals("IDLE")))
                                        //{
                                        //    //Do Nothing
                                        //}
                                        //else
                                        //{
                                        //    _logger.Debug(string.Format("[CreateTransferCommandByPortModel][RTS Status no match.] {0} / {1} / {2} / RTS Current Status: {3}, Down State: {4}", drRecord["EQUIPID"].ToString(), drRecord["Port_Type"].ToString(), drRecord["port_id"].ToString(), rtsCurrentState, rtsDownState));
                                        //    continue;
                                        //}
                                        //仍然要下料
                                        //if (isManualMode)
                                            //continue;

                                        dtWorkInProcessSch = _dbTool.GetDataTable(_BaseDataService.QueryWorkInProcessSchByPortIdForUnload(drRecord["PORT_ID"].ToString(), tableOrder));
                                        if (dtWorkInProcessSch.Rows.Count > 0)
                                            continue;

                                        try
                                        {
                                            if (bBindWorkgroup)
                                            {
                                                if (ineRack.Trim().Equals(""))
                                                {
                                                    //back stage(AOI)
                                                    //Not need to load carrier
                                                    tmpRack = "";
                                                }
                                                if (outeRack.Trim().Equals(""))
                                                {
                                                    //front stage(WAFER CLEAN)

                                                }
                                            }
                                        }
                                        catch (Exception ex) { }

                                        //1I1OT2 己經取得可用的MetalRing Carrier x.R
                                        if (MetalRingCarrier.Equals(""))
                                        {
                                            dtAvaileCarrier = GetAvailableCarrier(_dbTool, drRecord["CARRIER_TYPE"].ToString(), false, _keyRTDEnv);
                                            //AvaileCarrier is true
                                            if (dtAvaileCarrier.Rows.Count > 0)
                                            {
                                                bIsMatch = false;
                                                string tmpMessage = "";
                                                foreach (DataRow draCarrier in dtAvaileCarrier.Rows)
                                                {
                                                    if (_DebugMode)
                                                    {
                                                        _logger.Debug(string.Format("---locate is {0}", draCarrier["locate"].ToString()));
                                                        _logger.Debug(string.Format("---out erack is {0}", dtWorkgroupSet.Rows[0]["out_erack"].ToString()));
                                                    }

                                                    //Check Workgroup Set 
                                                    bool bNoFind = true;
                                                    sql = _BaseDataService.QueryRackByGroupID(dtWorkgroupSet.Rows[0]["out_erack"].ToString());
                                                    dtTemp = _dbTool.GetDataTable(sql);
                                                    bool blocatecheck = false;
                                                    foreach (DataRow drRack in dtTemp.Rows)
                                                    {
                                                        if (drRack["MAC"].ToString().Equals("STOCK"))
                                                            blocatecheck = true;

                                                        if (blocatecheck)
                                                        {
                                                            if (draCarrier["locate"].ToString().StartsWith(drRack["erackID"].ToString()))
                                                            {
                                                                bNoFind = false;
                                                                break;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            if (draCarrier["locate"].ToString().StartsWith(drRack["erackID"].ToString()))
                                                            {
                                                                bNoFind = false;
                                                                break;
                                                            }
                                                        }
                                                    }

                                                    if (bNoFind)
                                                    {
                                                        continue;
                                                    }
                                                    //if (!draCarrier["locate"].ToString().Equals(dtWorkgroupSet.Rows[0]["out_erack"]))
                                                    //    continue;

                                                    CarrierID = "";

                                                    if (_DebugMode)
                                                    {
                                                        _logger.Debug(string.Format("---1.1-PortModel is {0}", _portModel));
                                                    }

                                                    if (_portModel.Equals("2I2OT1"))
                                                    {
                                                        CarrierID = draCarrier["Carrier_ID"].ToString();
                                                        bIsMatch = true;
                                                    }
                                                    else
                                                    {
                                                        if (CheckIsAvailableLot(_dbTool, draCarrier["lot_id"].ToString(), drRecord["EquipID"].ToString()))
                                                        {
                                                            CarrierID = draCarrier["Carrier_ID"].ToString();
                                                            bIsMatch = true;
                                                        }
                                                        else
                                                        {
                                                            bIsMatch = false;
                                                            continue;
                                                        }
                                                    }

                                                    if (bIsMatch)
                                                    {
                                                        _dbTool.SQLExec(_BaseDataService.UpdateTableReserveCarrier(CarrierID, true), out tmpMessage, true);
                                                        break;
                                                    }
                                                    break;
                                                }

                                                if (!tmpMessage.Equals(""))
                                                {
                                                    _dbTool.SQLExec(_BaseDataService.UpdateTableReserveCarrier(CarrierID, false), out tmpMessage, true);
                                                    continue;
                                                }
                                            }
                                            
                                            if (_DebugMode)
                                            {
                                                _logger.Debug(string.Format("---dtAvaileCarrier.Rows.Count is {0}", dtAvaileCarrier.Rows.Count));
                                                _logger.Debug(string.Format("---bIsMatch is {0}", bIsMatch));
                                            }

                                            if (dtAvaileCarrier.Rows.Count <= 0 || !bIsMatch || isManualMode)
                                            {
                                                CarrierID = drOut.Length > 0 ? drOut[0]["CARRIER_ID"].ToString() : "";

                                                if (_portModel.Equals("2I2OT1"))
                                                {
                                                    //"Position": "202310121423001"
                                                    //BG Loadport 3 ready to unload, when not availe carrier can use will not do unload commands
                                                    return result;   //return result; will stop logic for build commmands.
                                                    //if (CarrierID.Equals(""))
                                                    //    continue;  //continue just pass. but still do unload.
                                                }

                                                string tmpMessage = "";
                                                _dbTool.SQLExec(_BaseDataService.UpdateTableReserveCarrier(CarrierID, true), out tmpMessage, true);

                                                if (!tmpMessage.Equals(""))
                                                {
                                                    _dbTool.SQLExec(_BaseDataService.UpdateTableReserveCarrier(CarrierID, false), out tmpMessage, true);
                                                    continue;
                                                }

                                                lstTransfer.CommandType = "UNLOAD";
                                                lstTransfer.Source = drRecord["PORT_ID"].ToString();
                                                lstTransfer.Dest = drRecord["OUT_ERACK"].ToString();
                                                lstTransfer.CarrierID = UnloadCarrierID.Equals("") ? "*" : UnloadCarrierID;
                                                lstTransfer.Quantity = 0;
                                                //lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : "";
                                                lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString().Equals("") ? _CarrierTypebyPort : dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : _CarrierTypebyPort;
                                                lstTransfer.CommandState = _commandState;
                                                break;
                                            }

                                        }
                                        else
                                        {
                                            CarrierID = MetalRingCarrier;

                                            sql = string.Format(_BaseDataService.SelectTableCarrierTransferByCarrier(MetalRingCarrier));
                                            dtTemp = _dbTool.GetDataTable(sql);

                                            if (dtTemp.Rows.Count > 0)
                                            {
                                                dtAvaileCarrier = dtTemp;
                                            }
                                        }

                                        if (_DebugMode)
                                        {
                                            _logger.Debug(string.Format("----PortModel is {0}", _portModel));
                                        }

                                        if (_portModel.Equals("2I2OT1"))
                                        {
                                            CarrierID = CarrierID.Equals("") ? dtAvaileCarrier.Rows[0]["Carrier_ID"].ToString() : CarrierID;

                                            if (_DebugMode)
                                            {
                                                _logger.Debug(string.Format("----CarrierID is {0}", CarrierID));
                                            }

                                            lstTransfer.CommandType = "LOAD";
                                            lstTransfer.Source = "*";
                                            lstTransfer.Dest = drRecord["PORT_ID"].ToString();
                                            lstTransfer.CarrierID = CarrierID;
                                            lstTransfer.Quantity = dtLoadPortCarrier.Rows.Count > 0 ? int.Parse(dtLoadPortCarrier.Rows[0]["QUANTITY"].ToString()) : 0;
                                            lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : "";
                                            lstTransfer.CommandState = _commandState;

                                            normalTransfer.Transfer.Add(lstTransfer);
                                            iReplace++;

                                            if (_DebugMode)
                                            {
                                                _logger.Debug(string.Format("----Logic true "));
                                            }
                                        }
                                        else if (_portModel.Equals("1I1OT2"))
                                        {
                                            if (_DebugMode)
                                            {
                                                _logger.Debug(string.Format("----MetalRing Carrier is {0}", MetalRingCarrier));
                                            }

                                            if (rtsCurrentState.Equals("UP") || (rtsCurrentState.Equals("DOWN") && rtsDownState.Equals("IDLE")))
                                            {
                                                lstTransfer.CommandType = "LOAD";
                                                lstTransfer.Source = "*";
                                                lstTransfer.Dest = drRecord["PORT_ID"].ToString();
                                                lstTransfer.CarrierID = CarrierID;
                                                lstTransfer.Quantity = dtAvaileCarrier.Rows.Count > 0 ? int.Parse(dtAvaileCarrier.Rows[0]["QUANTITY"].ToString()) : 0;
                                                lstTransfer.CarrierType = dtAvaileCarrier.Rows.Count > 0 ? dtAvaileCarrier.Rows[0]["COMMAND_TYPE"].ToString() : "";
                                                lstTransfer.CommandState = _commandState;

                                                normalTransfer.Transfer.Add(lstTransfer);
                                                iReplace++;

                                                if (_DebugMode)
                                                {
                                                    _logger.Debug(string.Format("----Logic true "));
                                                }
                                            }
                                            else
                                            {
                                                _logger.Debug(string.Format("[CreateTransferCommandByPortModel][RTS Status no match.] {0} / {1} / {2} / RTS Current Status: {3}, Down State: {4}", drRecord["EQUIPID"].ToString(), drRecord["Port_Type"].ToString(), drRecord["port_id"].ToString(), rtsCurrentState, rtsDownState));
                                                continue;
                                            }
                                        }
                                        else
                                        {
                                            if (!bPortTypeSame)
                                            {
                                                CarrierID = CarrierID.Equals("") ? dtAvaileCarrier.Rows[0]["Carrier_ID"].ToString() : CarrierID;

                                                if (rtsCurrentState.Equals("UP") || (rtsCurrentState.Equals("DOWN") && rtsDownState.Equals("IDLE")))
                                                {
                                                    lstTransfer.CommandType = "LOAD";
                                                    lstTransfer.Source = "*";
                                                    lstTransfer.Dest = drRecord["PORT_ID"].ToString();
                                                    lstTransfer.CarrierID = CarrierID;
                                                    lstTransfer.Quantity = dtLoadPortCarrier.Rows.Count > 0 ? int.Parse(dtLoadPortCarrier.Rows[0]["QUANTITY"].ToString()) : 0;
                                                    lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : "";
                                                    lstTransfer.CommandState = _commandState;

                                                    normalTransfer.Transfer.Add(lstTransfer);
                                                    iReplace++;
                                                }
                                                else
                                                {
                                                    _logger.Debug(string.Format("[CreateTransferCommandByPortModel][RTS Status no match.] {0} / {1} / {2} / RTS Current Status: {3}, Down State: {4}", drRecord["EQUIPID"].ToString(), drRecord["Port_Type"].ToString(), drRecord["port_id"].ToString(), rtsCurrentState, rtsDownState));
                                                    continue;
                                                }
                                            }
                                        }

                                        if (_DebugMode)
                                        {
                                            _logger.Debug(string.Format("----unload "));
                                        }

                                        CarrierID = drOut.Length > 0 ? drOut[0]["CARRIER_ID"].ToString() : "";

                                        if (_DebugMode)
                                        {
                                            _logger.Debug(string.Format("----unload CarrierID is {0}", CarrierID));
                                        }

                                        lstTransfer = new TransferList();

                                        lstTransfer.CommandType = "UNLOAD";
                                        lstTransfer.Source = drRecord["PORT_ID"].ToString();
                                        lstTransfer.Dest = drRecord["OUT_ERACK"].ToString();
                                        lstTransfer.CarrierID = UnloadCarrierID.Equals("") ? "*" : UnloadCarrierID;
                                        lstTransfer.Quantity = 0;
                                        //lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : "";
                                        lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString().Equals("") ? _CarrierTypebyPort : dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : _CarrierTypebyPort;
                                        lstTransfer.CommandState = _commandState;
                                        //lstTransfer.CommandType = "UNLOAD";
                                        //lstTransfer.Source = drRecord["PORT_ID"].ToString();
                                        //lstTransfer.Dest = drRecord["OUT_ERACK"].ToString();
                                        //lstTransfer.CarrierID = CarrierID.Equals("") ? "*" : CarrierID;
                                        //lstTransfer.Quantity = 0;
                                        //lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : "";

                                        if (_DebugMode)
                                        {
                                            _logger.Debug(string.Format("----Done "));
                                        }
                                    }
                                    catch (Exception ex)
                                    { }
                                    break;
                                case 4:
                                    //4. Empty (Ready to load)
                                    try
                                    {
                                        //rtsMachineState, rtsCurrentState, rtsDownState
                                        if (rtsCurrentState.Equals("UP") || (rtsCurrentState.Equals("DOWN") && rtsDownState.Equals("IDLE")))
                                        {
                                            //Do Nothing
                                            if (isManualMode)
                                                continue;
                                        }
                                        else
                                        {
                                            _logger.Debug(string.Format("[CreateTransferCommandByPortModel][RTS Status no match.] {0} / {1} / {2} / RTS Current Status: {3}, Down State: {4}", drRecord["EQUIPID"].ToString(), drRecord["Port_Type"].ToString(), drRecord["port_id"].ToString(), rtsCurrentState, rtsDownState));
                                            continue;
                                        }

                                        dtWorkInProcessSch = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSchByPortId(drRecord["PORT_ID"].ToString(), tableOrder));
                                        if (dtWorkInProcessSch.Rows.Count > 0)
                                            continue;

                                        //drPortState = dtPortsInfo.Select("Port_State in (1, 4)");
                                        //if (drPortState.Length <= 0)
                                        //    continue;

                                        try
                                        {
                                            if (bBindWorkgroup)
                                            {
                                                if (ineRack.Trim().Equals(""))
                                                {
                                                    //back stage(AOI)
                                                    //Not need to load carrier
                                                    tmpRack = "";
                                                }
                                                if (outeRack.Trim().Equals(""))
                                                {
                                                    //front stage(WAFER CLEAN)

                                                }
                                            }
                                        }
                                        catch (Exception ex) { }

                                        if (MetalRingCarrier.Equals(""))
                                        {
                                            dtAvaileCarrier = GetAvailableCarrier(_dbTool, drRecord["CARRIER_TYPE"].ToString(), false, _keyRTDEnv);
                                            if (dtAvaileCarrier is null)
                                                continue;
                                            if (dtAvaileCarrier.Rows.Count <= 0)
                                                continue;

                                            if (dtAvaileCarrier.Rows.Count > 0)
                                            {
                                                bIsMatch = false;
                                                string tmpMessage = "";
                                                foreach (DataRow draCarrier in dtAvaileCarrier.Rows)
                                                {

                                                    //Check Workgroup Set 
                                                    bool bNoFind = true;
                                                    sql = _BaseDataService.QueryRackByGroupID(dtWorkgroupSet.Rows[0]["in_erack"].ToString());
                                                    dtTemp = _dbTool.GetDataTable(sql);
                                                    bool blocatecheck = false;
                                                    foreach (DataRow drRack in dtTemp.Rows)
                                                    {
                                                        if (drRack["MAC"].ToString().Equals("STOCK"))
                                                            blocatecheck = true;

                                                        if (blocatecheck)
                                                        {
                                                            if (draCarrier["locate"].ToString().StartsWith(drRack["erackID"].ToString()))
                                                            {
                                                                bNoFind = false;
                                                                break;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            if (draCarrier["locate"].ToString().StartsWith(drRack["erackID"].ToString()))
                                                            {
                                                                bNoFind = false;
                                                                break;
                                                            }
                                                        }
                                                    }

                                                    if (bNoFind)
                                                    {
                                                        continue;
                                                    }
                                                    //if (!draCarrier["locate"].ToString().Equals(dtWorkgroupSet.Rows[0]["out_erack"]))
                                                    //    continue;

                                                    CarrierID = "";
                                                    //if (CheckIsAvailableLot(_dbTool, draCarrier["lot_id"].ToString(), drRecord["EquipID"].ToString()))
                                                    if (true)
                                                    {   //output port 不用檢查lot id
                                                        CarrierID = draCarrier["Carrier_ID"].ToString();
                                                        bIsMatch = true;
                                                    }
                                                    else
                                                    {
                                                        bIsMatch = false;
                                                        continue;
                                                    }

                                                    if (bIsMatch)
                                                    {
                                                        _dbTool.SQLExec(_BaseDataService.UpdateTableReserveCarrier(CarrierID, true), out tmpMessage, true);
                                                        break;
                                                    }
                                                }

                                                if (!bIsMatch)
                                                    break;

                                                if (!tmpMessage.Equals(""))
                                                {
                                                    _dbTool.SQLExec(_BaseDataService.UpdateTableReserveCarrier(CarrierID, false), out tmpMessage, true);
                                                    continue;
                                                }
                                                drCarrierData = dtAvaileCarrier.Select("carrier_id = '" + CarrierID + "'");
                                            }
                                        }
                                        else
                                        {
                                            sql = string.Format(_BaseDataService.SelectTableCarrierTransferByCarrier(MetalRingCarrier));
                                            dtTemp = _dbTool.GetDataTable(sql);

                                            if(dtTemp.Rows.Count > 0)
                                            {
                                                drCarrierData = dtTemp.Select("carrier_id = '" + MetalRingCarrier + "'");
                                                CarrierID = MetalRingCarrier;
                                                if (_DebugMode)
                                                {
                                                    _logger.Debug(string.Format("[Port_Type] {0} / {1} / {2}", drRecord["EQUIPID"].ToString(), CarrierID, drRecord["PORT_ID"].ToString()));
                                                }
                                            }
                                        }

                                        lstTransfer.CommandType = "LOAD";
                                        lstTransfer.Source = "*";
                                        lstTransfer.Dest = drRecord["PORT_ID"].ToString();
                                        lstTransfer.CarrierID = CarrierID;
                                        lstTransfer.Quantity = drCarrierData[0]["QUANTITY"].ToString().Equals("") ? 0 : int.Parse(drCarrierData[0]["QUANTITY"].ToString());
                                        lstTransfer.CarrierType = drCarrierData[0]["COMMAND_TYPE"].ToString();
                                        lstTransfer.CommandState = _commandState;
                                    }
                                    catch (Exception ex)
                                    { }
                                    break;
                                case 0:
                                default:
                                    break;
                            }
                            break;
                        case "IO":
                        default:
                            //0. wait to load 找到適合的Carrier, 併產生Load指令
                            //1. wait to unload 且沒有其它符合的Carrier 直接產生Unload指令
                            //2. wait to unload 而且有其它適用的Carrier, 產生Swap指令(Load + Unload)
                            try
                            {
                                string _outErack = "";

                                iPortState = GetPortStatus(dtPortsInfo, drRecord["port_id"].ToString(), out sPortState);
                                if (_DebugMode)
                                {
                                    _logger.Debug(string.Format("[PortState] {0} / {1}", drRecord["EQUIPID"].ToString(), iPortState));
                                }
                                switch (iPortState)
                                {
                                    case 1:
                                        //1. Transfer Blocked
                                        continue;
                                    case 2:
                                    case 3:
                                    case 5:
                                        //2. Near Completion
                                        //3.Ready to Unload
                                        //5. Reject and Ready to unload
                                        if (iPortState == 2)
                                        {
                                            if (bNearComplete)
                                            {
                                                _commandState = "NearComp";
                                            }
                                            else
                                            {
                                                //lstTransfer.CommandState = "NearComp";
                                                _commandState = "";
                                                break;
                                            }
                                        } else if (iPortState == 3)
                                        {
                                            _commandState = "";
                                            if (isManualMode)
                                                _OnlyUnload = true;

                                            sql = _BaseDataService.QueryNearCompletedByPortID(drRecord["PORT_ID"].ToString());
                                            dtTemp = _dbTool.GetDataTable(sql);
                                            if(dtTemp.Rows.Count > 0)
                                            {
                                                sql = _BaseDataService.ResetStartDtByCmdID(dtTemp.Rows[0]["cmd_id"].ToString());
                                                _dbTool.SQLExec(sql, out tmpMsg, true);

                                                if (tmpMsg.Equals(""))
                                                    break;
                                                else
                                                    _logger.Debug(tmpMsg);
                                            }
                                        }
                                        else
                                        {
                                            _commandState = "";
                                        }

                                        lstTransfer.CommandState = _commandState;

                                        try
                                        {
                                            string _carrierLocate = "";
                                            //rtsMachineState, rtsCurrentState, rtsDownState
                                            //unload not need check rts state. Marked by Vance 20230710 YY req
                                            //if (rtsCurrentState.Equals("UP") || (rtsCurrentState.Equals("DOWN") && rtsDownState.Equals("IDLE")))
                                            //{
                                            //    //Do Nothing
                                            //}
                                            //else
                                            //{
                                            //    _logger.Debug(string.Format("[CreateTransferCommandByPortModel][RTS Status no match.] {0} / {1} / {2} / RTS Current Status: {3}, Down State: {4}", drRecord["EQUIPID"].ToString(), drRecord["Port_Type"].ToString(), drRecord["port_id"].ToString(), rtsCurrentState, rtsDownState));
                                            //    continue;
                                            //}
                                            if (_expired)
                                            {
                                                if (isManualMode)
                                                    continue;
                                            }
                                            else
                                            {
                                                //Not expired. Manual more still do Unload
                                            }

                                            dtWorkInProcessSch = _dbTool.GetDataTable(_BaseDataService.QueryWorkInProcessSchByPortIdForUnload(drRecord["PORT_ID"].ToString(), tableOrder));
                                            if (dtWorkInProcessSch.Rows.Count > 0)
                                                continue;

                                            try
                                            {
                                                if (bBindWorkgroup)
                                                {
                                                    if (ineRack.Trim().Equals(""))
                                                    {
                                                        //back stage
                                                        //need get front stage loadport id

                                                    }
                                                    if (outeRack.Trim().Equals(""))
                                                    {
                                                        //front stage
                                                        //EQ to EQ, do not run unload logic
                                                        continue;
                                                    }
                                                }
                                            }
                                            catch (Exception ex) { }

                                            //取得當前Load Port上的Carrier
                                            dtLoadPortCarrier = _dbTool.GetDataTable(_BaseDataService.QueryWorkInProcessSchByPortIdForUnload(drRecord["EQUIPID"].ToString(), tableOrder));
                                            if (dtLoadPortCarrier is not null)
                                            {
                                                if (dtLoadPortCarrier.Rows.Count > 0)
                                                {
                                                    drIn = dtLoadPortCarrier.Select("PORTNO = '" + drRecord["PORT_SEQ"].ToString() + "'");
                                                    UnloadCarrierID = drIn is null ? "" : drIn.Length > 0 ? drIn[0]["CARRIER_ID"].ToString() : "";
                                                }
                                            }

                                            if (_OnlyUnload)
                                                goto UnloadLogic;

                                            if (bPrepareNextWorkgroup)
                                            {
                                                //PrepareNextWorkgroup If it have set, run this logic
                                                //Get available equipment number
                                                //SelectAvailableCarrierIncludeEQPByCarrierType
                                                dtPrepareNextWorkgroup = null;
                                                sql = _BaseDataService.QureyPrepareNextWorkgroupNumber(_Workgroup);
                                                dtPrepareNextWorkgroup = _dbTool.GetDataTable(sql);

                                                if (dtPrepareNextWorkgroup.Rows.Count > 0)
                                                {
                                                    dtAvaileCarrier = GetAvailableCarrier2(_dbTool, drRecord["CARRIER_TYPE"].ToString(), true, _keyRTDEnv);
                                                }
                                                else
                                                    continue;
                                            }
                                            else
                                            {
                                                try
                                                {
                                                    if (bBindWorkgroup)
                                                    {
                                                        if (ineRack.Trim().Equals(""))
                                                        {
                                                            //back stage(AOI)
                                                            //Not need to load carrier
                                                            tmpRack = "";
                                                            dtAvaileCarrier = GetAvailableCarrierFromEQP(_dbTool, drRecord["port_id"].ToString(), drRecord["CARRIER_TYPE"].ToString(), true, _keyRTDEnv);
                                                        }
                                                        else if (outeRack.Trim().Equals(""))
                                                        {
                                                            //front stage(WAFER CLEAN)

                                                        }
                                                        else
                                                        {
                                                            dtAvaileCarrier = GetAvailableCarrier(_dbTool, drRecord["CARRIER_TYPE"].ToString(), true, _keyRTDEnv);
                                                        }
                                                    }
                                                    else
                                                        dtAvaileCarrier = GetAvailableCarrier(_dbTool, drRecord["CARRIER_TYPE"].ToString(), true, _keyRTDEnv);
                                                }
                                                catch (Exception ex) { }
                                            }

                                            int iQty = 0; int iQuantity = 0; int iTotalQty = 0;
                                            int iCountOfCarr = 0;
                                            bool isLastLot = false;
                                            bool bNoFind = true;
                                            

                                            //AvaileCarrier is true
                                            if (dtAvaileCarrier.Rows.Count > 0)
                                            {
                                                bIsMatch = false;
                                                string tmpMessage = "";
                                                foreach (DataRow draCarrier in dtAvaileCarrier.Rows)
                                                {
                                                    string _custdevice2 = "";
                                                    DataRow[] drTemp;

                                                    _carrierLocate = "";
                                                    try
                                                    {
                                                        iPrepareCustDeviceNo = 0;
                                                        iProcessCustDeviceNo = 0;
                                                        iProcessNextWorkgroup = 0;

                                                        try
                                                        {
                                                            _carrierLocate = draCarrier["locate"].ToString();
                                                        }
                                                        catch (Exception ex) { }

                                                        if (bPrepareNextWorkgroup)
                                                        {
                                                            _custdevice2 = draCarrier["custdevice"].ToString();

                                                            drTemp = dtPrepareNextWorkgroup.Select(string.Format("custdevice='{0}'", _custdevice2));

                                                            if (drTemp.Length <= 0)
                                                            {
                                                                //_logger.Info(string.Format("custDevice incorrect. [{0}]/[{1}]", draCarrier["lot_id"].ToString(), _custdevice2));
                                                                continue;
                                                            }

                                                            iPrepareCustDeviceNo = int.Parse(drTemp[0]["deviceQty"].ToString()) * _iPrepareQty;
                                                            //iProcessCustDeviceNo = 0;

                                                            dtCurrentWorkgroupProcess = null;
                                                            sql = _BaseDataService.QureyCurrentWorkgroupProcessNumber(_Workgroup);
                                                            dtCurrentWorkgroupProcess = _dbTool.GetDataTable(sql);
                                                            drTemp = dtCurrentWorkgroupProcess.Select(string.Format("custdevice='{0}'", _custdevice2));
                                                            iProcessCustDeviceNo = int.Parse(drTemp[0]["deviceQty"].ToString());

                                                            dtCurrentWorkgroupProcess = null;
                                                            sql = _BaseDataService.QureyProcessNextWorkgroupNumber(_Workgroup);
                                                            dtCurrentWorkgroupProcess = _dbTool.GetDataTable(sql);
                                                            drTemp = dtCurrentWorkgroupProcess.Select(string.Format("custdevice='{0}'", _custdevice2));
                                                            iProcessNextWorkgroup = int.Parse(drTemp[0]["deviceQty"].ToString());
                                                            //增加貨架上屬於當站的數量
                                                            int iQtyonerack = 0;
                                                            try
                                                            {
                                                                sql = _BaseDataService.QureyLotQtyOnerackForNextWorkgroup(_Workgroup);
                                                                dtTemp = _dbTool.GetDataTable(sql);

                                                                if (dtTemp.Rows.Count > 0)
                                                                {
                                                                    drTemp = dtTemp.Select(string.Format("custdevice='{0}'", _custdevice2));
                                                                    iQtyonerack = int.Parse(drTemp[0]["onerackQty"].ToString());
                                                                    //iQtyonerack = int.Parse(dtTemp.Rows[0]["onerackQty"].ToString());
                                                                }
                                                            }
                                                            catch (Exception ex) { }

                                                            if (iQtyonerack > 0)
                                                            {
                                                                //_logger.Info(string.Format("on erack available carrier have [{0}]", iQtyonerack));
                                                                iProcessCustDeviceNo = iProcessCustDeviceNo + iQtyonerack + iProcessNextWorkgroup;
                                                            }

                                                            //SWAP allow parepare one more carrier
                                                            _logger.Info(string.Format("Prepare Qty: PortState [{0}], Prepare Qty [{1}], Process Qty [{2}]", iPortState, iPrepareCustDeviceNo, iProcessCustDeviceNo));
                                                            if (iProcessCustDeviceNo >= iPrepareCustDeviceNo)
                                                            {
                                                                _logger.Info(string.Format("on erack available carrier have [{0}]/[{1}][{2}]", iQtyonerack, iProcessCustDeviceNo, iPrepareCustDeviceNo));
                                                                continue; 
                                                            }
                                                        }
                                                    }
                                                    catch (Exception ex) {
                                                        _logger.Info(string.Format("Prepare Exception:[{0}]", ex.Message));
                                                    }

                                                    iQty = 0; iTotalQty = 0; iQuantity = 0;

                                                    //站點不當前站點不同, 不取這批lot
                                                    if (!draCarrier["lot_id"].ToString().Equals(""))
                                                    {
                                                        sql = _BaseDataService.CheckLotStage(configuration["CheckLotStage:Table"], draCarrier["lot_id"].ToString());
                                                        dtTemp = _dbTool.GetDataTable(sql);

                                                        if (configuration["AppSettings:Work"].ToUpper().Equals("EWLB"))
                                                        {
                                                            if (dtTemp.Rows.Count <= 0)
                                                                continue;
                                                            else
                                                            {
                                                                if (!dtTemp.Rows[0]["stage1"].ToString().Equals(dtTemp.Rows[0]["stage2"].ToString()))
                                                                {
                                                                    _logger.Debug(string.Format("---LotID= {0}, RTD_Stage={1}, MES_Stage={2}, RTD_State={3}, MES_State={4}", draCarrier["lot_id"].ToString(), dtTemp.Rows[0]["stage1"].ToString(), dtTemp.Rows[0]["stage2"].ToString(), dtTemp.Rows[0]["state1"].ToString(), dtTemp.Rows[0]["state2"].ToString()));
                                                                }
                                                            }
                                                        }
                                                        else
                                                        {
                                                            if (dtTemp.Rows.Count > 0)
                                                            {
                                                                if (!dtTemp.Rows[0]["stage1"].ToString().Equals(dtTemp.Rows[0]["stage2"].ToString()))
                                                                {
                                                                    _logger.Debug(string.Format("---LotID= {0}, RTD_Stage={1}, MES_Stage={2}, RTD_State={3}, MES_State={4}", draCarrier["lot_id"].ToString(), dtTemp.Rows[0]["stage1"].ToString(), dtTemp.Rows[0]["stage2"].ToString(), dtTemp.Rows[0]["state1"].ToString(), dtTemp.Rows[0]["state2"].ToString()));
                                                                }
                                                            }
                                                        }
                                                    }

                                                    //Check Equipment CustDevice / Lot CustDevice is same.
                                                    if (!EquipCustDevice.Trim().Equals(""))
                                                    {
                                                        string device = "";
                                                        sql = _BaseDataService.QueryLotInfoByCarrierID(draCarrier["carrier_id"].ToString());
                                                        dtTemp = _dbTool.GetDataTable(sql);
                                                        if (dtTemp.Rows.Count > 0)
                                                        {
                                                            //DataTable dtTemp2;
                                                            sql = _BaseDataService.QueryDataByLotid(dtTemp.Rows[0]["lot_id"].ToString(), GetExtenalTables(configuration, "SyncExtenalData", "AdsInfo"));
                                                            dtTemp2 = _dbTool.GetDataTable(sql);
                                                            //select lot_age, custDevice from semi_int.rtd_cis_ads_vw @SEMI_INT where lotid = '83727834.1';
                                                            if (dtTemp2.Rows.Count > 0)
                                                            {

                                                                device = dtTemp2.Rows[0]["custdevice"].ToString();
                                                                if (!device.Trim().Equals(""))
                                                                {
                                                                    _logger.Debug(string.Format("[CreateTransferCommandByPortModel][{0}] device[{1}] EquipCustDevice[{2}]", _oEventQ.EventName, device, EquipCustDevice));

                                                                    if (bCustDevice)
                                                                    {
                                                                        if (!device.Equals(EquipCustDevice))
                                                                        {
                                                                            _logger.Debug(string.Format("[CreateTransferCommandByPortModel][{0}][{1}] {2} / {3}", _oEventQ.EventName, draCarrier["carrier_id"].ToString(), device, EquipCustDevice));

                                                                            continue;
                                                                        }
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    ///the lot custDevice is empty, the carrier can not dispatching to tools.
                                                                    _logger.Debug(string.Format("[CreateTransferCommandByPortModel][{0}][{1}] {2} / {3}, lot custDevice is null.", _oEventQ.EventName, draCarrier["carrier_id"].ToString(), device, EquipCustDevice));
                                                                    continue;
                                                                }
                                                            }
                                                            else
                                                            {
                                                                ///can not get lot from ads system.
                                                                _logger.Debug(string.Format("[CreateTransferCommandByPortModel][{0}][{1}] Can not get lot from ads.", _oEventQ.EventName, draCarrier["carrier_id"].ToString(), device, EquipCustDevice));
                                                                continue;
                                                            }
                                                        }
                                                        else
                                                            continue;
                                                    }

                                                    //Check Workgroup Set 
                                                    bNoFind = true;
                                                    sql = _BaseDataService.QueryRackByGroupID(dtWorkgroupSet.Rows[0]["in_erack"].ToString());
                                                    dtTemp = _dbTool.GetDataTable(sql);
                                                    bool blocatecheck = false;
                                                    foreach (DataRow drRack in dtTemp.Rows)
                                                    {
                                                        if (drRack["MAC"].ToString().Equals("STOCK"))
                                                            blocatecheck = true;

                                                        if (blocatecheck)
                                                        {
                                                            if (draCarrier["locate"].ToString().StartsWith(drRack["erackID"].ToString()))
                                                            {
                                                                bNoFind = false;
                                                                break;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            if (draCarrier["locate"].ToString().StartsWith(drRack["erackID"].ToString()))
                                                            {
                                                                bNoFind = false;
                                                                break;
                                                            }
                                                        }
                                                    }

                                                    if (bNoFind)
                                                    {
                                                        continue;
                                                    }

                                                    if (_DebugMode)
                                                    {
                                                        _logger.Debug(string.Format("[QueryRackByGroupID] {0} / {1} / {2}", drRecord["EQUIPID"].ToString(), draCarrier["locate"].ToString(), draCarrier["lot_id"].ToString()));
                                                    }
                                                    //if (!draCarrier["locate"].ToString().Equals(dtWorkgroupSet.Rows[0]["in_erack"]))
                                                    //    continue;

                                                    iQuantity = draCarrier.Table.Columns.Contains("quantity") ? int.Parse(draCarrier["quantity"].ToString()) : 0;
                                                    //檢查Cassette Qty總和, 是否與Lot Info Qty相同, 相同才可派送 (滿足相同lot要放在相同機台上的需求)
                                                    string sqlSentence = _BaseDataService.CheckQtyforSameLotId(draCarrier["lot_id"].ToString(), drRecord["CARRIER_TYPE"].ToString());
                                                    DataTable dtSameLot = new DataTable();
                                                    dtSameLot = _dbTool.GetDataTable(sqlSentence);
                                                    if (dtSameLot.Rows.Count > 0)
                                                    {
                                                        iQty = int.Parse(dtSameLot.Rows[0]["qty"].ToString());
                                                        iTotalQty = int.Parse(dtSameLot.Rows[0]["total_qty"].ToString());
                                                        iCountOfCarr = int.Parse(dtSameLot.Rows[0]["NumOfCarr"].ToString());

                                                        if (_DebugMode)
                                                        {
                                                            _logger.Debug(string.Format("[CheckQtyforSameLotId] {0} / {1} / {2} / {3}", drRecord["EQUIPID"].ToString(), iCountOfCarr, iQty, iTotalQty));
                                                        }

                                                        if (iCountOfCarr > 1)
                                                        {
                                                            if (iQty == iTotalQty)
                                                            { //To Do...
                                                                isLastLot = true;
                                                                sql = string.Format(_BaseDataService.LockMachineByLot(draCarrier["lot_id"].ToString(), iQuantity, 1));
                                                                _dbTool.SQLExec(sql, out tmpMsg, true);
                                                            }
                                                            else
                                                            {
                                                                if (iQty <= iQuantity)
                                                                {
                                                                    isLastLot = true;
                                                                    sql = string.Format(_BaseDataService.LockMachineByLot(draCarrier["lot_id"].ToString(), iQuantity, 0));
                                                                    _dbTool.SQLExec(sql, out tmpMsg, true);
                                                                }
                                                                else
                                                                {
                                                                    isLastLot = false;
                                                                    sql = string.Format(_BaseDataService.LockMachineByLot(draCarrier["lot_id"].ToString(), iQuantity, 1));
                                                                    _dbTool.SQLExec(sql, out tmpMsg, true);
                                                                }
                                                            }
                                                        }
                                                        else
                                                        {
                                                            if (iQty < iTotalQty)
                                                            {
                                                                int _lockMachine = 0;
                                                                int _compQty = 0;
                                                                //draCarrier["lot_id"].ToString()
                                                                sql = string.Format(_BaseDataService.SelectTableLotInfoByLotid(draCarrier["lot_id"].ToString()));
                                                                dtTemp = _dbTool.GetDataTable(sql);
                                                                _lockMachine = dtTemp.Rows[0]["lockmachine"].ToString().Equals("1") ? 1 : 0;
                                                                _compQty = dtTemp.Rows[0]["comp_qty"].ToString().Equals("0") ? 0 : int.Parse(dtTemp.Rows[0]["comp_qty"].ToString());

                                                                if (_lockMachine.Equals(0) && _compQty == 0)
                                                                {
                                                                    isLastLot = false;
                                                                    sql = string.Format(_BaseDataService.LockMachineByLot(draCarrier["lot_id"].ToString(), iQuantity, 1));
                                                                    _dbTool.SQLExec(sql, out tmpMsg, true);
                                                                }
                                                                else
                                                                {
                                                                    if (_compQty + iQuantity >= iTotalQty)
                                                                    {
                                                                        isLastLot = true;
                                                                        sql = string.Format(_BaseDataService.LockMachineByLot(draCarrier["lot_id"].ToString(), _compQty + iQuantity, 0));
                                                                        _dbTool.SQLExec(sql, out tmpMsg, true);
                                                                    }
                                                                    else
                                                                        isLastLot = false;
                                                                }
                                                            }
                                                            else
                                                            {
                                                                isLastLot = true;
                                                                sql = string.Format(_BaseDataService.LockMachineByLot(draCarrier["lot_id"].ToString(), iQuantity, 0));
                                                                _dbTool.SQLExec(sql, out tmpMsg, true);
                                                            }
                                                        }
                                                    }

                                                    CarrierID = "";
                                                    if (CheckIsAvailableLot(_dbTool, draCarrier["lot_id"].ToString(), drRecord["EquipID"].ToString()))
                                                    {
                                                        CarrierID = draCarrier["Carrier_ID"].ToString();
                                                        bIsMatch = true;
                                                    }
                                                    else
                                                    {
                                                        bIsMatch = false;
                                                        continue;
                                                    }

                                                    if (_DebugMode)
                                                    {
                                                        _logger.Debug(string.Format("[CheckIsAvailableLot] {0} / {1} / {2}", drRecord["EQUIPID"].ToString(), bIsMatch, draCarrier["lot_id"].ToString()));
                                                    }

                                                    if (bIsMatch)
                                                    {
                                                        _dbTool.SQLExec(_BaseDataService.UpdateTableReserveCarrier(CarrierID, true), out tmpMessage, true);
                                                        break;
                                                    }
                                                }

                                                if (!tmpMessage.Equals(""))
                                                {
                                                    if (_DebugMode)
                                                    {
                                                        _logger.Debug(string.Format("[tmpMessage] {0} / {1} ", drRecord["EQUIPID"].ToString(), tmpMessage));
                                                    }
                                                    _dbTool.SQLExec(_BaseDataService.UpdateTableReserveCarrier(CarrierID, false), out tmpMessage, true);
                                                    continue;
                                                }
                                            }

                                            if (_DebugMode)
                                            {
                                                _logger.Debug(string.Format("[Build Unload Command] {0} / {1} / {2}", drRecord["EQUIPID"].ToString(), bIsMatch, dtAvaileCarrier.Rows.Count));
                                            }

                                            if (dtAvaileCarrier.Rows.Count <= 0 || bIsMatch || CarrierID.Equals("") || bNoFind || isManualMode)
                                            {
                                                if (_DebugMode)
                                                {
                                                    _logger.Debug(string.Format("[dtAvaileCarrier] Have Availe Carrier {0}", dtAvaileCarrier.Rows.Count));
                                                }

                                                String tmp11 = "";
                                                CarrierID = drIn is not null ? drIn.Length > 0 ? drIn[0]["CARRIER_ID"].ToString() : "" : CarrierID.Equals("") ? "*" : CarrierID;

                                                if (_DebugMode)
                                                {
                                                    _logger.Debug(string.Format("[dtAvaileCarrier] Availe Carrier ID [{0}]", CarrierID));
                                                }

                                                string tempDest = "";
                                                string tmpReject = configuration["Reject:ERACK"] is not null ? configuration["Reject:ERACK"] : "IN_ERACK";
                                                if (iPortState.Equals(5))
                                                {
                                                    if (useFaileRack)
                                                    {
                                                        tempDest = faileRack;
                                                    }
                                                    else
                                                    {
                                                        tempDest = drRecord[tmpReject].ToString();
                                                    }
                                                }
                                                else
                                                    tempDest = drRecord["OUT_ERACK"].ToString();

                                                try
                                                {
                                                    //out eRack STK1
                                                    sql = _BaseDataService.QueryRackByGroupID(tempDest);
                                                    dtTemp2 = _dbTool.GetDataTable(sql);
                                                    //A/B
                                                    if (dtTemp2.Rows.Count > 0)
                                                    {
                                                        if (dtTemp2.Rows[0]["MAC"].ToString().Equals("STOCK"))
                                                        {
                                                            _outErack = dtTemp2.Rows[0]["erackID"].ToString();
                                                        }
                                                        else
                                                        {
                                                            _outErack = tempDest;
                                                        }
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    _logger.Debug(string.Format("[Exception STK: {0}]", _outErack));
                                                }

                                                lstTransfer.CommandType = "UNLOAD";
                                                lstTransfer.Source = drRecord["PORT_ID"].ToString();
                                                lstTransfer.Dest = _outErack;
                                                lstTransfer.CarrierID = UnloadCarrierID.Equals("") ? "*" : UnloadCarrierID;
                                                if (!lstTransfer.CarrierID.Equals(""))
                                                {
                                                    lstTransfer.LotID = "";
                                                }
                                                lstTransfer.Quantity = 0;
                                                lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString().Equals("") ? _CarrierTypebyPort : dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : _CarrierTypebyPort;
                                                lstTransfer.CommandState = _commandState;

                                                if (_DebugMode)
                                                {
                                                    _logger.Debug(string.Format("[dtAvaileCarrier] Availe Carrier ID [{0}] / {1}", lstTransfer.CarrierID, normalTransfer.Transfer.Count));
                                                }

                                                if (dtAvaileCarrier.Rows.Count <= 0)
                                                    break;
                                                else
                                                {
                                                    normalTransfer.Transfer.Add(lstTransfer);
                                                    iReplace++;
                                                }
                                                //break;
                                            }

                                            ///Equip is Manual Mode
                                            if (isManualMode)
                                            {
                                                _logger.Debug(string.Format("[Current Equipment Manual Mode] {0} / {1}", drRecord["PORT_ID"].ToString(), isManualMode));
                                                break;
                                            }

                                            //AvaileCarrier is true
                                            if (bIsMatch)
                                            {
                                                drCarrierData = dtAvaileCarrier.Select("carrier_id = '" + CarrierID + "'");
                                            }

                                            if (!isManualMode)
                                            {
                                                if (drCarrierData is null)
                                                    continue;

                                                if (drCarrierData.Length > 0)
                                                {
                                                    if (rtsCurrentState.Equals("UP") || (rtsCurrentState.Equals("DOWN") && rtsDownState.Equals("IDLE")))
                                                    {
                                                        if (!isManualMode)
                                                        {
                                                            tmpRack = "";
                                                            if (bBindWorkgroup)
                                                            {
                                                                if (ineRack.Trim().Equals(""))
                                                                {
                                                                    tmpRack = _carrierLocate;
                                                                }
                                                                else if (outeRack.Trim().Equals(""))
                                                                {
                                                                    //front stage(WAFER CLEAN)

                                                                }
                                                                else
                                                                {
                                                                    tmpRack = "*";
                                                                }
                                                            }
                                                            else
                                                                tmpRack = "*";

                                                            //Reject 時, 設備不是Manual mode 才產生LOAD指令
                                                            lstTransfer = new TransferList();

                                                            lstTransfer.CommandType = "LOAD";
                                                            lstTransfer.Source = tmpRack;
                                                            lstTransfer.Dest = drRecord["PORT_ID"].ToString();
                                                            lstTransfer.CarrierID = CarrierID;
                                                            iQty = drCarrierData == null ? 0 : int.Parse(drCarrierData[0]["QUANTITY"].ToString());
                                                            lstTransfer.Quantity = iQty;
                                                            lstTransfer.CarrierType = drCarrierData == null ? "" : drCarrierData[0]["COMMAND_TYPE"].ToString();
                                                            lstTransfer.Total = iTotalQty;
                                                            lstTransfer.IsLastLot = isLastLot ? 1 : 0;
                                                            lstTransfer.CommandState = _commandState;


                                                            if (_DebugMode)
                                                            {
                                                                _logger.Debug(string.Format("[dtAvaileCarrier] LOAD Command [{0}] / {1}", lstTransfer.CarrierID, normalTransfer.Transfer.Count));
                                                            }

                                                            if (iReplace < 1)
                                                            {
                                                                normalTransfer.Transfer.Add(lstTransfer);
                                                                iReplace++;
                                                            }
                                                            else
                                                                break;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        _logger.Debug(string.Format("[CreateTransferCommandByPortModel][RTS Status no match.] {0} / {1} / {2} / RTS Current Status: {3}, Down State: {4}", drRecord["EQUIPID"].ToString(), drRecord["Port_Type"].ToString(), drRecord["port_id"].ToString(), rtsCurrentState, rtsDownState));
                                                        continue;
                                                    }
                                                }
                                            }

                                            if (_DebugMode)
                                            {
                                                _logger.Debug(string.Format("[dtAvaileCarrier] LOAD iReplace [{0}] / {1}", lstTransfer.CarrierID, iReplace));
                                            }

                                            if (CarrierID.Equals(""))
                                                CarrierID = dtLoadPortCarrier.Rows[0]["CARRIER_ID"].ToString();


                                            UnloadLogic:
                                            //檢查Out port 與 In port 的carrier type是否相同
                                            drOut = dtPortsInfo.Select("Port_Type='IO'");
                                            if (drOut.Length > 0)
                                            {
                                                if (drOut[0]["CARRIER_TYPE"].ToString().Equals(drRecord["CARRIER_TYPE"].ToString()))
                                                {
                                                    string tmpPortState = "";
                                                    lstTransfer = new TransferList();
                                                    //確認Out port是否處於Wait to Unload
                                                    switch (GetPortStatus(dtPortsInfo, drOut[0]["port_id"].ToString(), out tmpPortState))
                                                    {
                                                        case 0:
                                                        case 1:
                                                        case 4:
                                                        case 2:
                                                            break;
                                                        case 3:
                                                        case 5:
                                                        default:
                                                            CarrierID = drIn is not null ? drIn.Length > 0 ? drIn[0]["CARRIER_ID"].ToString() : "" : "";

                                                            lstTransfer.CommandType = "UNLOAD";
                                                            lstTransfer.Source = drRecord["PORT_ID"].ToString();
                                                            lstTransfer.Dest = drRecord["OUT_ERACK"].ToString();
                                                            lstTransfer.CarrierID = UnloadCarrierID.Equals("") ? "*" : UnloadCarrierID;
                                                            lstTransfer.Quantity = 0;
                                                            //lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : "";
                                                            lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString().Equals("") ? _CarrierTypebyPort : dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : _CarrierTypebyPort;
                                                            lstTransfer.CommandState = _commandState;
                                                            break;
                                                    }

                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        { }

                                        break;
                                    case 4:
                                        //4. Empty (Ready to load)
                                        try
                                        {
                                            //rtsMachineState, rtsCurrentState, rtsDownState
                                            if (rtsCurrentState.Equals("UP") || (rtsCurrentState.Equals("DOWN") && rtsDownState.Equals("IDLE")))
                                            {
                                                //Do Nothing
                                                if (isManualMode)
                                                    continue;
                                            }
                                            else
                                            {
                                                _logger.Debug(string.Format("[CreateTransferCommandByPortModel][RTS Status no match.] {0} / {1} / {2} / RTS Current Status: {3}, Down State: {4}", drRecord["EQUIPID"].ToString(), drRecord["Port_Type"].ToString(), drRecord["port_id"].ToString(), rtsCurrentState, rtsDownState));
                                                continue;
                                            }

                                            if (bStageIssue)
                                            {
                                                sql = _BaseDataService.LockLotInfoWhenReady(lotID);
                                                _dbTool.SQLExec(sql, out tmpMsg, true);
                                                if (_DebugMode)
                                                {
                                                    _logger.Debug(string.Format("[StageIssue] {0} / {1} Load command create faild.", drRecord["EQUIPID"].ToString(), lotID));
                                                }

                                                continue;
                                            }

                                            dtWorkInProcessSch = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSchByPortId(drRecord["PORT_ID"].ToString(), tableOrder));
                                            if (dtWorkInProcessSch.Rows.Count > 0)
                                            {
                                                if (_DebugMode)
                                                {
                                                    _logger.Debug(string.Format("[WorkInProcessSch] {0} / {1} The lotid has already exist WorkInProcessSch.", drRecord["EQUIPID"].ToString(), lotID));
                                                }

                                                continue;
                                            }

                                            try
                                            {
                                                if (bBindWorkgroup)
                                                {
                                                    if (ineRack.Trim().Equals(""))
                                                    {
                                                        //back stage(AOI)
                                                        //Not need to load carrier
                                                        tmpRack = "";
                                                    }
                                                    if (outeRack.Trim().Equals(""))
                                                    {
                                                        //front stage(WAFER CLEAN)
                                                        
                                                    }
                                                }
                                            }
                                            catch (Exception ex) { }

                                            if (bPrepareNextWorkgroup)
                                            {
                                                //PrepareNextWorkgroup If it have set, run this logic
                                                //Get available equipment number
                                                //SelectAvailableCarrierIncludeEQPByCarrierType
                                                dtPrepareNextWorkgroup = null;
                                                sql = _BaseDataService.QureyPrepareNextWorkgroupNumber(_Workgroup);
                                                dtPrepareNextWorkgroup = _dbTool.GetDataTable(sql);

                                                if (dtPrepareNextWorkgroup.Rows.Count > 0)
                                                {
                                                    dtAvaileCarrier = GetAvailableCarrier2(_dbTool, drRecord["CARRIER_TYPE"].ToString(), true, _keyRTDEnv);
                                                }
                                                else
                                                    continue;
                                            }
                                            else
                                            {
                                                try
                                                {
                                                    if (bBindWorkgroup)
                                                    {
                                                        if (ineRack.Trim().Equals(""))
                                                        {
                                                            //back stage(AOI)
                                                            //Not need to load carrier
                                                            tmpRack = "";
                                                            dtAvaileCarrier = GetAvailableCarrierFromEQP(_dbTool, drRecord["port_id"].ToString(), drRecord["CARRIER_TYPE"].ToString(), true, _keyRTDEnv);
                                                        }
                                                        else if (outeRack.Trim().Equals(""))
                                                        {
                                                            //front stage(WAFER CLEAN)

                                                        }
                                                        else
                                                        {
                                                            dtAvaileCarrier = GetAvailableCarrier(_dbTool, drRecord["CARRIER_TYPE"].ToString(), true, _keyRTDEnv);
                                                        }
                                                    }
                                                    else
                                                        dtAvaileCarrier = GetAvailableCarrier(_dbTool, drRecord["CARRIER_TYPE"].ToString(), true, _keyRTDEnv);
                                                }
                                                catch (Exception ex) { }
                                            }

                                            //dtAvaileCarrier = GetAvailableCarrier(_dbTool, drRecord["CARRIER_TYPE"].ToString(), true, _keyRTDEnv);
                                            if (dtAvaileCarrier is null)
                                                continue;
                                            if (dtAvaileCarrier.Rows.Count <= 0)
                                            {
                                                if (_DebugMode)
                                                {
                                                    _logger.Debug(string.Format("[Availe Carrier] {0} / {1} Have not Available Carrier.", drRecord["EQUIPID"].ToString(), lotID));
                                                }

                                                continue;
                                            }

                                            if (_DebugMode)
                                            {
                                                _logger.Debug(string.Format("[GetAvailableCarrier] {0} / {1}", drRecord["EQUIPID"].ToString(), dtAvaileCarrier.Rows.Count));
                                            }

                                            int iQty = 0; int iQuantity = 0; int iTotalQty = 0;
                                            int iCountOfCarr = 0;
                                            bool isLastLot = false;
                                            string _carrierLocate = "";

                                            if (dtAvaileCarrier.Rows.Count > 0)
                                            {
                                                if (_DebugMode)
                                                {
                                                    _logger.Debug(string.Format("[GetAvailableCarrier IN] {0} / {1}", drRecord["EQUIPID"].ToString(), dtAvaileCarrier.Rows.Count));
                                                }

                                                bIsMatch = false;
                                                string tmpMessage = "";
                                                try
                                                {
                                                    string _custdevice2 = "";
                                                    DataRow[] drTemp;

                                                    foreach (DataRow draCarrier in dtAvaileCarrier.Rows)
                                                    {
                                                        _carrierLocate = "";
                                                        try {
                                                            iPrepareCustDeviceNo = 0;
                                                            iProcessCustDeviceNo = 0;
                                                            iProcessNextWorkgroup = 0;

                                                            try
                                                            {
                                                                _carrierLocate = draCarrier["locate"].ToString();
                                                            }catch(Exception ex) { }

                                                            if (bPrepareNextWorkgroup)
                                                            {
                                                                _custdevice2 = draCarrier["custdevice"].ToString();

                                                                drTemp = dtPrepareNextWorkgroup.Select(string.Format("custdevice='{0}'", _custdevice2));

                                                                if (drTemp.Length <= 0)
                                                                {
                                                                    //_logger.Info(string.Format("custDevice incorrect. [{0}]/[{1}]", draCarrier["lot_id"].ToString(), _custdevice2));
                                                                    continue;
                                                                }

                                                                iPrepareCustDeviceNo = int.Parse(drTemp[0]["deviceQty"].ToString()) * _iPrepareQty;
                                                                //iProcessCustDeviceNo = 0;

                                                                dtCurrentWorkgroupProcess = null;
                                                                sql = _BaseDataService.QureyCurrentWorkgroupProcessNumber(_Workgroup);
                                                                dtCurrentWorkgroupProcess = _dbTool.GetDataTable(sql);
                                                                drTemp = dtCurrentWorkgroupProcess.Select(string.Format("custdevice='{0}'", _custdevice2));
                                                                iProcessCustDeviceNo = int.Parse(drTemp[0]["deviceQty"].ToString());

                                                                dtCurrentWorkgroupProcess = null;
                                                                sql = _BaseDataService.QureyProcessNextWorkgroupNumber(_Workgroup);
                                                                dtCurrentWorkgroupProcess = _dbTool.GetDataTable(sql);
                                                                drTemp = dtCurrentWorkgroupProcess.Select(string.Format("custdevice='{0}'", _custdevice2));
                                                                iProcessNextWorkgroup = int.Parse(drTemp[0]["deviceQty"].ToString());

                                                                //增加貨架上屬於當站的數量
                                                                int iQtyonerack = 0;
                                                                try {
                                                                    sql = _BaseDataService.QureyLotQtyOnerackForNextWorkgroup(_Workgroup);
                                                                    dtTemp = _dbTool.GetDataTable(sql);

                                                                    if(dtTemp.Rows.Count > 0)
                                                                    {
                                                                        drTemp = dtTemp.Select(string.Format("custdevice='{0}'", _custdevice2));
                                                                        iQtyonerack = int.Parse(drTemp[0]["onerackQty"].ToString());
                                                                        //iQtyonerack = int.Parse(dtTemp.Rows[0]["onerackQty"].ToString());
                                                                    }
                                                                }
                                                                catch(Exception ex) { }

                                                                if(iQtyonerack > 0)
                                                                {
                                                                    //_logger.Info(string.Format("on erack available carrier have [{0}]", iQtyonerack));
                                                                    iProcessCustDeviceNo = iProcessCustDeviceNo + iQtyonerack + iProcessNextWorkgroup;
                                                                }

                                                                _logger.Info(string.Format("PortState [{0}], Prepare Qty [{1}], Process Qty [{2}]", iPortState, iPrepareCustDeviceNo, iProcessCustDeviceNo));
                                                                if (iProcessCustDeviceNo >= iPrepareCustDeviceNo)
                                                                {
                                                                    _logger.Info(string.Format("on erack available carrier have [{0}]", iQtyonerack));
                                                                    continue; 
                                                                }
                                                            }
                                                        }
                                                        catch(Exception ex) { }

                                                        iQty = 0; iTotalQty = 0; iQuantity = 0;

                                                        //Check Workgroup Set 
                                                        bool bNoFind = true;
                                                        try
                                                        {
                                                            sql = _BaseDataService.QueryRackByGroupID(dtWorkgroupSet.Rows[0]["in_erack"].ToString());

                                                            dtTemp = _dbTool.GetDataTable(sql);
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            if (_DebugMode)
                                                            {
                                                                _logger.Debug(string.Format("[drRack Exception: {0} / {1}]", drRecord["EQUIPID"].ToString(), ex.Message));
                                                            }
                                                        }

                                                        if (dtTemp.Rows.Count > 0)
                                                        {
                                                            try
                                                            {
                                                                foreach (DataRow drRack in dtTemp.Rows)
                                                                {
                                                                    try
                                                                    {
                                                                        if (_DebugMode)
                                                                        {
                                                                            _logger.Debug(string.Format("[drRack] {0} / {1} / {2} / {3} / {4}", drRecord["EQUIPID"].ToString(), draCarrier["carrier_id"].ToString(), draCarrier["locate"].ToString(), drRack["erackID"].ToString(), draCarrier["lot_id"].ToString()));
                                                                        }

                                                                        if (draCarrier["locate"].ToString().StartsWith(drRack["erackID"].ToString()))
                                                                        {
                                                                            bNoFind = false;

                                                                            if (_DebugMode)
                                                                            {
                                                                                _logger.Debug(string.Format("[AvailableCarrier ErackID] {0} / {1} / {2}", drRecord["EQUIPID"].ToString(), drRack["erackID"].ToString(), draCarrier["carrier_id"].ToString()));
                                                                            }
                                                                            break;
                                                                        }

                                                                        if (_DebugMode)
                                                                        {
                                                                            _logger.Debug(string.Format("[No Find] {0}/ {1}/ {2}/ {3}/ {4}", drRecord["EQUIPID"].ToString(), draCarrier["carrier_id"].ToString(), draCarrier["locate"].ToString(), drRack["erackID"].ToString(), draCarrier["lot_id"].ToString()));
                                                                        }
                                                                    }
                                                                    catch (Exception ex)
                                                                    {
                                                                        if (_DebugMode)
                                                                        {
                                                                            _logger.Debug(string.Format("[No Find] [Exception:{0}]", ex.Message));
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                            catch (Exception ex)
                                                            {
                                                                if (_DebugMode)
                                                                {
                                                                    _logger.Debug(string.Format("[No Find] [Exception:{0}]", ex.Message));
                                                                }
                                                            }
                                                        }

                                                        if (_DebugMode)
                                                        {
                                                            //_logger.Debug(string.Format("[Check Workgroup Set ] {0} / {1}", drRecord["EQUIPID"].ToString(), dtTemp.Rows.Count.ToString()));
                                                        }

                                                        //Check Equipment CustDevice / Lot CustDevice is same.
                                                        if (!EquipCustDevice.Trim().Equals(""))
                                                        {
                                                            string device = "";
                                                            sql = _BaseDataService.QueryLotInfoByCarrierID(draCarrier["carrier_id"].ToString());
                                                            dtTemp = _dbTool.GetDataTable(sql);
                                                            if (dtTemp.Rows.Count > 0)
                                                            {
                                                                //DataTable dtTemp2;
                                                                sql = _BaseDataService.QueryDataByLotid(dtTemp.Rows[0]["lot_id"].ToString(), GetExtenalTables(configuration, "SyncExtenalData", "AdsInfo"));
                                                                dtTemp2 = _dbTool.GetDataTable(sql);
                                                                //select lot_age, custDevice from semi_int.rtd_cis_ads_vw @SEMI_INT where lotid = '83727834.1';
                                                                if (dtTemp2.Rows.Count > 0)
                                                                {

                                                                    device = dtTemp2.Rows[0]["custdevice"].ToString();
                                                                    if (!device.Trim().Equals(""))
                                                                    {
                                                                        if (bCustDevice)
                                                                        {
                                                                            if (_DebugMode)
                                                                                _logger.Debug(string.Format("[CreateTransferCommandByPortModel][{0}] device[{1}] EquipCustDevice[{2}]", _oEventQ.EventName, device, EquipCustDevice));

                                                                            if (!device.Equals(EquipCustDevice))
                                                                            {
                                                                                _logger.Debug(string.Format("[CreateTransferCommandByPortModel][{0}][{1}] {2} / {3}", _oEventQ.EventName, draCarrier["carrier_id"].ToString(), device, EquipCustDevice));

                                                                                continue;
                                                                            }
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        ///the lot custDevice is empty, the carrier can not dispatching to tools.
                                                                        _logger.Debug(string.Format("[CreateTransferCommandByPortModel][{0}][{1}] {2} / {3}, lot custDevice is null.", _oEventQ.EventName, draCarrier["carrier_id"].ToString(), device, EquipCustDevice));
                                                                        continue;
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    ///can not get lot from ads system.
                                                                    _logger.Debug(string.Format("[CreateTransferCommandByPortModel][{0}][{1}] Can not get lot from ads.", _oEventQ.EventName, draCarrier["carrier_id"].ToString(), device, EquipCustDevice));
                                                                    continue;
                                                                }
                                                            }
                                                            else
                                                                continue;
                                                        }

                                                        if (bNoFind)
                                                        {
                                                            continue;
                                                        }
                                                        //if (!draCarrier["locate"].ToString().Equals(dtWorkgroupSet.Rows[0]["in_erack"]))
                                                        //    continue;

                                                        iQuantity = draCarrier.Table.Columns.Contains("quantity") ? int.Parse(draCarrier["quantity"].ToString()) : 0;
                                                        //檢查Cassette Qty總和, 是否與Lot Info Qty相同, 相同才可派送 (滿足相同lot要放在相同機台上的需求)
                                                        string sqlSentence = _BaseDataService.CheckQtyforSameLotId(draCarrier["lot_id"].ToString(), drRecord["CARRIER_TYPE"].ToString());
                                                        DataTable dtSameLot = new DataTable();
                                                        dtSameLot = _dbTool.GetDataTable(sqlSentence);

                                                        if (_DebugMode)
                                                        {
                                                            _logger.Debug(string.Format("[CheckQtyforSameLotId] {0} / {1} / {2}", drRecord["EQUIPID"].ToString(), draCarrier["lot_id"].ToString(), drRecord["CARRIER_TYPE"].ToString()));
                                                        }

                                                        if (dtSameLot.Rows.Count > 0)
                                                        {
                                                            iQty = int.Parse(dtSameLot.Rows[0]["qty"].ToString());
                                                            iTotalQty = int.Parse(dtSameLot.Rows[0]["total_qty"].ToString());
                                                            iCountOfCarr = int.Parse(dtSameLot.Rows[0]["NumOfCarr"].ToString());

                                                            if (iCountOfCarr > 1)
                                                            {
                                                                if (iQty == iTotalQty)
                                                                { //To Do...
                                                                    isLastLot = false;
                                                                    sql = string.Format(_BaseDataService.LockMachineByLot(draCarrier["lot_id"].ToString(), iQuantity, 1));
                                                                    _dbTool.SQLExec(sql, out tmpMsg, true);
                                                                    _logger.Debug(tmpMsg);
                                                                    tmpMsg = String.Format("=======IO==2==Load Carrier [{0}], Total Qty is {1}. Qty is {2}", draCarrier["lot_id"].ToString(), iTotalQty, iQty);
                                                                    _logger.Debug(tmpMsg);
                                                                }
                                                                else
                                                                {
                                                                    tmpMsg = String.Format("=======IO==1==Load Carrier, Total Qty is {0}. Qty is {1}", iTotalQty, iQty);
                                                                    _logger.Debug(tmpMsg);

                                                                    if (iQty <= iQuantity)
                                                                    {
                                                                        isLastLot = true;
                                                                        sql = string.Format(_BaseDataService.LockMachineByLot(draCarrier["lot_id"].ToString(), iQuantity, 0));
                                                                        _dbTool.SQLExec(sql, out tmpMsg, true);
                                                                    }
                                                                    else
                                                                        isLastLot = false;

                                                                    if (iQty < iTotalQty)
                                                                        continue;   //不搬運, 由unload 去發送(相同lot 需要由同一個port 執行)
                                                                }
                                                            }
                                                            else
                                                            {
                                                                if (iQty < iTotalQty)
                                                                {
                                                                    int _lockMachine = 0;
                                                                    int _compQty = 0;
                                                                    //draCarrier["lot_id"].ToString()
                                                                    sql = string.Format(_BaseDataService.SelectTableLotInfoByLotid(draCarrier["lot_id"].ToString()));
                                                                    dtTemp = _dbTool.GetDataTable(sql);
                                                                    _lockMachine = dtTemp.Rows[0]["lockmachine"].ToString().Equals("1") ? 1 : 0;
                                                                    _compQty = dtTemp.Rows[0]["comp_qty"].ToString().Equals("0") ? 0 : int.Parse(dtTemp.Rows[0]["comp_qty"].ToString());

                                                                    if (_lockMachine.Equals(0) && _compQty == 0)
                                                                    {
                                                                        isLastLot = false;
                                                                        sql = string.Format(_BaseDataService.LockMachineByLot(draCarrier["lot_id"].ToString(), iQuantity, 1));
                                                                        _dbTool.SQLExec(sql, out tmpMsg, true);
                                                                    }
                                                                    else
                                                                    {
                                                                        if (_compQty + iQuantity >= iTotalQty)
                                                                        {
                                                                            isLastLot = true;
                                                                            sql = string.Format(_BaseDataService.LockMachineByLot(draCarrier["lot_id"].ToString(), _compQty + iQuantity, 0));
                                                                            _dbTool.SQLExec(sql, out tmpMsg, true);
                                                                        }
                                                                        else
                                                                            isLastLot = false;
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    isLastLot = true;
                                                                    sql = string.Format(_BaseDataService.LockMachineByLot(draCarrier["lot_id"].ToString(), iQuantity, 0));
                                                                    _dbTool.SQLExec(sql, out tmpMsg, true);
                                                                }
                                                            }
                                                        }

                                                        CarrierID = "";
                                                        if (CheckIsAvailableLot(_dbTool, draCarrier["lot_id"].ToString(), drRecord["EquipID"].ToString()))
                                                        {
                                                            CarrierID = draCarrier["Carrier_ID"].ToString();

                                                            if (_DebugMode)
                                                            {
                                                                _logger.Debug(string.Format("[CheckIsAvailableLot] {0} / {1} / {2}", drRecord["EQUIPID"].ToString(), CarrierID, draCarrier["lot_id"].ToString()));
                                                            }

                                                            bIsMatch = true;
                                                        }
                                                        else
                                                        {
                                                            bIsMatch = false;
                                                            continue;
                                                        }

                                                        if (bIsMatch)
                                                        {
                                                            _dbTool.SQLExec(_BaseDataService.UpdateTableReserveCarrier(CarrierID, true), out tmpMessage, true);
                                                            break;
                                                        }
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    if (_DebugMode)
                                                    {
                                                        _logger.Debug(string.Format("[Exception:] {0} / {1}", drRecord["EQUIPID"].ToString(), ex.Message));
                                                    }
                                                }



                                                if (!bIsMatch)
                                                    break;

                                                if (!tmpMessage.Equals(""))
                                                {
                                                    if (_DebugMode)
                                                    {
                                                        _logger.Debug(string.Format("[tmpMessage] {0} / {1}", drRecord["EQUIPID"].ToString(), tmpMessage));
                                                    }

                                                    _dbTool.SQLExec(_BaseDataService.UpdateTableReserveCarrier(CarrierID, false), out tmpMessage, true);
                                                    continue;
                                                }
                                                drCarrierData = dtAvaileCarrier.Select("carrier_id = '" + CarrierID + "'");
                                            }

                                            if (!isManualMode)
                                            {
                                                tmpRack = "";
                                                if (bBindWorkgroup)
                                                {
                                                    if (ineRack.Trim().Equals(""))
                                                    {
                                                        tmpRack = _carrierLocate;
                                                    }
                                                    else if (outeRack.Trim().Equals(""))
                                                    {
                                                        //front stage(WAFER CLEAN)

                                                    }
                                                    else
                                                    {
                                                        tmpRack = "*";
                                                    }
                                                }
                                                else
                                                    tmpRack = "*";

                                                lstTransfer.CommandType = "LOAD";
                                                lstTransfer.Source = tmpRack;
                                                lstTransfer.Dest = drRecord["PORT_ID"].ToString();
                                                lstTransfer.CarrierID = CarrierID;
                                                lstTransfer.Quantity = drCarrierData[0]["QUANTITY"].ToString().Equals("") ? 0 : int.Parse(drCarrierData[0]["QUANTITY"].ToString());
                                                lstTransfer.CarrierType = drCarrierData[0]["COMMAND_TYPE"].ToString();
                                                lstTransfer.Total = iTotalQty;
                                                lstTransfer.IsLastLot = isLastLot ? 1 : 0;
                                                lstTransfer.CommandState = _commandState;
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            if (_DebugMode)
                                            {
                                                _logger.Debug(string.Format("[Exception:  {0} / {1} ]", drRecord["EQUIPID"].ToString(), iPortState));
                                            }
                                        }
                                        break;
                                    case 0:
                                    default:
                                        continue;
                                        break;
                                }
                                break;
                            }
                            catch (Exception ex)
                            { }
                            break;
                    }

                    if (lstTransfer is not null)
                    {

                        //將搬送指令加入Transfer LIst
                        if (lstTransfer.CarrierID is not null)
                        {

                            normalTransfer.Transfer.Add(lstTransfer);
                            iReplace++;
                        }
                    }

                    UnloadCarrierID = "";

                    if (normalTransfer.Transfer.Count > 0)
                    {
                        if (!_portModel.Equals("1I1OT2"))
                            break;
                    }
                }
                normalTransfer.Replace = iReplace > 0 ? iReplace - 1 : 0;

                ////////////////output normalTransfer

                //由Carrier Type找出符合的Carrier 


                //產生派送指令識別碼
                //U + 2022081502 + 00001
                //將所產生的指令加入WorkInProcess_Sch
                bool single = true;
                DataTable dtExist = new DataTable();
                EQPLastWaferTime _oLastWaferTime;

                tmpMsg = "";
                if (normalTransfer.Transfer is not null)
                {
                    if (_DebugMode)
                    {
                        _logger.Debug(string.Format("[{0}]----normalTransfer.Transfer.Count is {1}", normalTransfer.EquipmentID, normalTransfer.Transfer.Count));
                    }

                    if (normalTransfer.Transfer.Count > 0)
                    {
                        string tmpCarrierid = "";
                        int eqp_priority = 20;
                        List<string> lstEquips = new List<string>();

                        if (normalTransfer.Transfer.Count > 1)
                        { single = false; }

                        //if (!single)
                            //normalTransfer.CommandID = Tools.GetCommandID(_dbTool);

                        SchemaWorkInProcessSch workinProcessSch = new SchemaWorkInProcessSch();
                        //workinProcessSch.UUID = Tools.GetUnitID(_dbTool);
                        workinProcessSch.UUID = "";
                        //workinProcessSch.Cmd_Id = normalTransfer.CommandID;
                        workinProcessSch.Cmd_Id = "";
                        workinProcessSch.Cmd_State = "";    //派送前為NULL
                        workinProcessSch.EquipId = normalTransfer.EquipmentID;
                        workinProcessSch.Cmd_Current_State = "";    //派送前為NULL

                        try 
                        {
                            //"Position": "202310171805001"
                            dtTemp = _dbTool.GetDataTable(_BaseDataService.SelectWorkgroupSet(normalTransfer.EquipmentID));

                            if (dtTemp.Rows.Count > 0)
                            {
                                eqp_priority = dtTemp.Rows[0]["prio"] is null ? 20 : dtTemp.Rows[0]["prio"].ToString().Equals("0") ? 20 : int.Parse(dtTemp.Rows[0]["prio"].ToString());
                            }
                            else
                            {
                                eqp_priority = 30;
                            }
                            
                            _logger.Debug(string.Format("Get Equip[{0}] Workgroup priority is [{1}].", normalTransfer.EquipmentID, eqp_priority));
                        } 
                        catch(Exception ex) 
                        {
                            eqp_priority = 30;
                            _logger.Debug(string.Format("Exception: Equip[{0}], Message:[{1}].", normalTransfer.EquipmentID, ex.Message));
                        }

                        workinProcessSch.Priority = _Workgroup_priority.Equals(0) ? eqp_priority : _Workgroup_priority;             //預設優先權為10

                        if (single)
                            workinProcessSch.Replace = 0;
                        else
                        {
                            if (_portModel.Equals("2I2OT1"))
                            {
                                string tmpLoad = "";
                                string tmpUnload = "";
                                //workinProcessSch.Replace = normalTransfer.Transfer.Count > 1 ? normalTransfer.Transfer.Count - 1 : 1;
                                foreach (TransferList trans in normalTransfer.Transfer)
                                {
                                    if (trans.CommandType.Equals("LOAD"))
                                    {
                                        tmpLoad = trans.Dest;
                                    }
                                    else if (trans.CommandType.Equals("UNLOAD"))
                                    {
                                        tmpUnload = trans.Source;
                                    }

                                    if(tmpLoad.Equals(tmpUnload))
                                    {
                                        lstEquips.Add(tmpLoad);
                                        tmpLoad = "";
                                        tmpUnload = "";
                                    }

                                }

                                //workinProcessSch.Replace = 0;
                                //single = true;
                            }
                            else
                            {
                                string tmpLoad = "";
                                string tmpUnload = "";
                                foreach (TransferList trans in normalTransfer.Transfer)
                                {
                                    if (trans.CommandType.Equals("LOAD"))
                                    {
                                        tmpLoad = trans.Dest;
                                    }
                                    else if (trans.CommandType.Equals("UNLOAD"))
                                    {
                                        tmpUnload = trans.Source;
                                    }

                                    if (tmpLoad.Equals(tmpUnload))
                                    {
                                        lstEquips.Add(tmpLoad);
                                        tmpLoad = "";
                                        tmpUnload = "";
                                    }
                                }
                            }
                        }

                        bool isLoad = false;
                        bool isSwap = false;
                        string _lastDest = "";
                        int idxTrans = 0;
                        string _tmplotid = "";
                        DateTime dtStart = DateTime.Now;
                        bool _holdcarrier = false;
                        Dictionary<string, object> _nearCompleted = new Dictionary<string, object>();
                        foreach (TransferList trans in normalTransfer.Transfer)
                        {
                            //workinProcessSch.UUID = "";
                            //檢查Dest是否已存在於WorkinprocessSch裡, 存在不能再送, 
                            dtExist = new DataTable();
                            tmpMsg = "";
                            _oLastWaferTime = new EQPLastWaferTime();


                            if (trans.CommandType.Equals("LOAD"))
                            {
                                isLoad = true;

                                if (lstEquips.Exists(e => e.EndsWith(trans.Dest)))
                                {
                                    workinProcessSch.Replace = 1;
                                    isSwap = true;
                                }

                                _lastDest = trans.Dest;

                                dtExist = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSchByPortId(trans.Dest, tableOrder));
                                if (dtExist.Rows.Count > 0)
                                {
                                    logger.Debug(string.Format("Duplicate Command Type:[{0}], Command Source: {1}, Dest: {2}", trans.CommandType, trans.Source, trans.Dest));
                                    continue;   //目的地已有Carrier, 跳過
                                }
                                tmpCarrierid = trans.CarrierID.Equals("") ? "*" : trans.CarrierID;

                                //check carrier
                                dtExist = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSchByCarrier(tmpCarrierid, tableOrder));
                                if (dtExist.Rows.Count > 0)
                                {
                                    logger.Debug(string.Format("Duplicate Carrier [{0}], command [{1}] been auto cancel.", tmpCarrierid, normalTransfer.CommandID));
                                    if (dtExist.Rows[0]["cmd_state"].ToString().Equals("Init"))
                                    {
                                        //UpdateTableReserveCarrier
                                        _dbTool.SQLExec(_BaseDataService.UpdateTableReserveCarrier(tmpCarrierid, true), out tmpMsg, true);
                                        continue;   //目的地已有Carrier, 跳過
                                    }
                                    if (dtExist.Rows[0]["cmd_state"].ToString().Equals("Initial"))
                                    {
                                        //DeleteWorkInProcessSchByCmdId
                                        _dbTool.SQLExec(_BaseDataService.DeleteWorkInProcessSchByCmdId(dtExist.Rows[0]["cmd_id"].ToString(), tableOrder), out tmpMsg, true);
                                        logger.Debug(string.Format("Delete old command[{0}] cause have a new command for device[{2}].", dtExist.Rows[0]["cmd_id"].ToString(), trans.Dest));
                                    }
                                    if (dtExist.Rows[0]["cmd_state"].ToString().Equals("Running"))
                                    {
                                        //The Carrier state is running. cann't create new commands
                                        continue;   //目的地已有Carrier, 跳過
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }
                            }
                            if (trans.CommandType.Equals("UNLOAD"))
                            {
                                _nearCompleted = new Dictionary<string, object>();

                                isLoad = false;

                                tmpCarrierid = trans.CarrierID.Equals("") ? "*" : trans.CarrierID;

                                if (lstEquips.Exists(e => e.EndsWith(trans.Source)))
                                {
                                    workinProcessSch.Replace = 1;
                                    isSwap = true;
                                }

                                /** 如果Command Type is Unload, Carrier ID is *, 暫停3秒, 防止重複產生Unload 指令*/
                                if (tmpCarrierid.Equals("*"))
                                    System.Threading.Thread.Sleep(3000);

                                dtExist = _dbTool.GetDataTable(_BaseDataService.QueryWorkInProcessSchByPortIdForUnload(trans.Source, tableOrder));
                                if (dtExist.Rows.Count > 0)
                                {
                                    logger.Debug(string.Format("Duplicate Command Type:[{0}], Command Source: {1}, Dest: {2}", trans.CommandType, trans.Source, trans.Dest));
                                    continue;   //目的地已有Carrier, 跳過
                                }

                                dtTemp2 = _dbTool.GetDataTable(_BaseDataService.QueryCarrierOnPort(trans.Source));
                                if (dtTemp2.Rows.Count > 0)
                                {
                                    tmpCarrierid = dtTemp2.Rows[0]["carrier_id"] is null ? "*" : dtTemp2.Rows[0]["carrier_id"].ToString();
                                }

                                if (iPortState.Equals(5))
                                {
                                    //如果是Reject, 且在設備異常表裡不存在, 加入一筆記錄
                                    //
                                }
                            }
                            //Pre-Transfer
                            if (trans.CommandType.ToUpper().Equals("PRE-TRANSFER"))
                            {
                                workinProcessSch.Priority = 20;
                            }
                            else
                            {
                                int iPriority = 0;
                                sql = _BaseDataService.QueryLotInfoByCarrierID(tmpCarrierid);
                                dtTemp = _dbTool.GetDataTable(sql);

                                if (_DebugMode)
                                {
                                    _logger.Debug(string.Format("----Not pre-transfer {0}", tmpCarrierid));
                                }

                                if (dtTemp.Rows.Count > 0)
                                {
                                    iPriority = dtTemp.Rows[0]["PRIORITY"] is null ? 0 : int.Parse(dtTemp.Rows[0]["PRIORITY"].ToString());
                                    tmpMsg = string.Format("[Lot Priority] The carrier id [{0}] current priority is [{1}]", tmpCarrierid, iPriority);
                                    _logger.Debug(tmpMsg);
                                }

                                if (iPriority >= 70)
                                {
                                    workinProcessSch.Priority = iPriority;
                                }
                                else
                                {
                                    if (trans.CommandType.ToUpper().Equals("PRE-TRANSFER"))
                                    {
                                        if (iPriority > 20)
                                        {
                                            workinProcessSch.Priority = iPriority;
                                        }
                                        else
                                        {
                                            workinProcessSch.Priority = 20;
                                        }
                                    }
                                    else
                                    {
                                        workinProcessSch.Priority = eqp_priority;
                                    }
                                }
                            }

                            workinProcessSch.Cmd_Type = trans.CommandType.Equals("") ? "TRANS" : trans.CommandType;


                            if (_portModel.Equals("2I2OT1"))
                            {
                                if (isLoad)
                                {
                                    if (isSwap)
                                        workinProcessSch.Cmd_Id = "";
                                }
                                else
                                {
                                    if (isSwap)
                                    {
                                        //workinProcessSch.Cmd_Id = "";
                                        workinProcessSch.UUID = Tools.GetUnitID(_dbTool);
                                    }
                                    //workinProcessSch.Cmd_Id = Tools.GetCommandID(_dbTool);
                                }
                            }
                            else
                            {
                                if (_DebugMode)
                                {
                                    _logger.Debug(string.Format("----check commands type {0} ", trans.CommandType));
                                }
                                if (isSwap)
                                {

                                    //20240606 DS swap command loadport 1/loadport 2 command id diff
                                    if (trans.CommandType.Equals("LOAD"))
                                    {
                                        if (idxTrans <= 0)
                                        {
                                            workinProcessSch.Cmd_Id = "";
                                        }
                                        if (idxTrans > 1)
                                        {
                                            workinProcessSch.Cmd_Id = "";
                                        }
                                        idxTrans++;
                                    }
                                    workinProcessSch.UUID = "";
                                    if (trans.CommandType.Equals("UNLOAD"))
                                        idxTrans++;
                                }
                                else
                                {
                                    workinProcessSch.Cmd_Id = "";
                                    workinProcessSch.UUID = "";
                                }
                            }
                            //workinProcessSch.Cmd_Id = string.Format("{0}-{1}", Tools.GetCommandID(_dbTool), trans.CommandType);

                            tmpMsg = "";
                            //workinProcessSch.UUID = Tools.GetUnitID(_dbTool);  //變更為GID使用, 可查詢同一批派出的指令
                            
                            workinProcessSch.CarrierId = tmpCarrierid.Equals("") ? "*" : tmpCarrierid;
                            workinProcessSch.CarrierType = trans.CarrierType.Equals("") ? "" : trans.CarrierType;
                            workinProcessSch.Source = trans.Source.Equals("*") ? "*" : trans.Source;
                            workinProcessSch.Dest = trans.Dest.Equals("") ? "*" : trans.Dest;
                            workinProcessSch.Quantity = trans.Quantity;
                            workinProcessSch.Total = trans.Total;
                            workinProcessSch.IsLastLot = trans.IsLastLot;
                            workinProcessSch.Back = "*";

                            if (workinProcessSch.Cmd_Id.Equals(""))
                                dtStart = DateTime.Now;

                            DataTable dtInfo = new DataTable { };
                            if (trans.CarrierID is not null)
                            {
                                if (!trans.CarrierID.Equals("*") || trans.CarrierID.Trim().Equals(""))
                                    dtInfo = _dbTool.GetDataTable(_BaseDataService.QueryLotInfoByCarrierID(trans.CarrierID));
                            }

                            if (dtInfo is not null)
                            {
                                if (dtInfo.Rows.Count > 0)
                                {
                                    if (_DebugMode)
                                    {
                                        _logger.Debug(string.Format("----do check lot priority "));
                                    }

                                    workinProcessSch.LotID = dtInfo.Rows.Count <= 0 ? " " : dtInfo.Rows[0]["lotid"].ToString();
                                    workinProcessSch.Customer = dtInfo.Rows.Count <= 0 ? " " : dtInfo.Rows[0]["customername"].ToString();

                                    if (int.Parse(dtInfo.Rows[0]["priority"].ToString()) > 70)
                                    {
                                        workinProcessSch.Priority = int.Parse(dtInfo.Rows[0]["priority"].ToString());
                                    }
                                }
                                else
                                {
                                    workinProcessSch.LotID = " ";
                                    workinProcessSch.Customer = " ";
                                }
                            }
                            else
                            {
                                workinProcessSch.LotID = " ";
                                workinProcessSch.Customer = " ";
                            }

                            if (trans.CommandState.Equals("NearComp"))
                            {
                                if (_DebugMode)
                                {
                                    _logger.Debug(string.Format("----do Near Completed "));
                                }

                                _oLastWaferTime = new EQPLastWaferTime();
                                workinProcessSch.Cmd_Current_State = trans.CommandState is null ? "" : trans.CommandState;


                                if (trans.CommandType.Equals("UNLOAD"))
                                {

                                    //取得機台的時間
                                    int iHours = 0;
                                    int iMinutes = 0;
                                    string tmpLot = "";

                                    try
                                    {
                                        ////Get Last Lotid
                                        if (workinProcessSch.Source.IndexOf(workinProcessSch.EquipId) >= 0)
                                        {
                                            sql = _BaseDataService.GetEQPortLastLot(workinProcessSch.Source);
                                            dtTemp = _dbTool.GetDataTable(sql);

                                            if (dtTemp.Rows.Count > 0)
                                            {
                                                tmpLot = dtTemp.Rows[0]["LotID"].ToString();
                                            }
                                        }
                                    }
                                    catch (Exception ex) { }

                                    //iHours = 1;
                                    //iMinutes = 32;
                                    //EQPLastWaferTime
                                    //_oLastWaferTime
                                    _oLastWaferTime = GetLastWaferTimeByEQP(_dbTool, configuration, _logger, normalTransfer.EquipmentID, tmpLot);

                                    _nearCompleted.Add(workinProcessSch.Source, _oLastWaferTime);
                                }
                                else
                                {
                                    if(_nearCompleted.Count > 0)
                                        _oLastWaferTime = (EQPLastWaferTime)_nearCompleted[workinProcessSch.Dest];
                                }

                                //if (_oLastWaferTime.Hours.Equals(0) && _oLastWaferTime.Minutes.Equals(0))
                                //    continue;

                                //workinProcessSch.Start_Dt = dtStart.AddHours(_oLastWaferTime.Hours).AddMinutes(_oLastWaferTime.Minutes);

                                if (_oLastWaferTime.Minutes.Equals(0) || _oLastWaferTime.Minutes < 0)
                                    continue;

                                workinProcessSch.Start_Dt = dtStart.AddMinutes(_oLastWaferTime.Minutes);
                            }
                            else
                            {
                                workinProcessSch.Cmd_Current_State = "";
                                workinProcessSch.Start_Dt = dtStart;
                            }

                            if (_DebugMode)
                            {
                                _logger.Debug(string.Format("----do insert "));
                            }

                            if (workinProcessSch.Cmd_Id.Equals(""))
                                workinProcessSch.Cmd_Id = Tools.GetCommandID(_dbTool);
                            //else
                            //{
                                //if (trans.CommandType.Equals("LOAD"))
                                //{
                                    //20240605 DS swap command loadport 1/loadport 2 command id 
                                //    if (!_lastDest.Equals(workinProcessSch.Dest))
                                //        workinProcessSch.Cmd_Id = Tools.GetCommandID(_dbTool);
                                //}
                            //}

                            if (workinProcessSch.UUID.Equals(""))
                                workinProcessSch.UUID = Tools.GetUnitID(_dbTool);

                            if(_holdcarrier)
                            {
                                try {

                                    if (workinProcessSch.Cmd_Type.Equals("UNLOAD"))
                                    {
                                        if (workinProcessSch.LotID.Equals(""))
                                            _holdcarrier = true;
                                    }
                                }
                                catch(Exception ex) { }
                            }

                            sql = _BaseDataService.InsertTableWorkinprocess_Sch(workinProcessSch, tableOrder);

                            if (_DebugMode)
                            {
                                _logger.Debug(string.Format("----do insert sql [{0}] ", sql));
                            }

                            if (_dbTool.SQLExec(sql, out tmpMsg, true))
                            {
                                if (tmpMsg.Equals(""))
                                {
                                    string _portID = "";
                                    if (workinProcessSch.Cmd_Type.Equals("LOAD"))
                                        _portID = workinProcessSch.Dest;
                                    if (workinProcessSch.Cmd_Type.Equals("UNLOAD"))
                                        _portID = workinProcessSch.Source;

                                    if(!_portID.Equals(""))
                                        _dbTool.SQLExec(_BaseDataService.LockEquipPortByPortId(_portID, true), out tmpMsg, true);

                                    if (tmpMsg.Equals(""))
                                    {
                                        _logger.Debug(string.Format("----Port [{0}] has been Lock.", _portID));
                                    }
                                    else
                                        _logger.Debug(string.Format("----Port [{0}] lock faile. Message:[{1}]", _portID, tmpMsg));
                                }
                                Thread.Sleep(5);
                            }

                            if (!tmpMsg.Equals(""))
                            {
                                _logger.Debug(string.Format("--Error. Insert Workinprocess_Sch error. [command ID {0}, command Type {1}, source {2}, error:{3}].", workinProcessSch.Cmd_Id, workinProcessSch.Cmd_Type, workinProcessSch.Source, tmpMsg));
                            }

                            normalTransfer.CommandID = workinProcessSch.Cmd_Id;

                            if (!isSwap)
                            {
                                workinProcessSch.Cmd_Id = "";
                                workinProcessSch.UUID = "";
                            }
                        }

                        _arrayOfCmds.Add(normalTransfer.CommandID);
                    }

                }
                ////加入_arrayOfCmds
                //if (normalTransfer.Transfer.Count <= 0)
                //    OracleSequence.BackOne(_dbTool, "command_streamCode");
                //else 
                //    _arrayOfCmds.Add(normalTransfer.CommandID);

                ///Release Equipment
                _dbTool.SQLExec(_BaseDataService.LockEquip(_Equip, false), out tmpMsg, true);
            }
            catch (Exception ex)
            {
                //Do Nothing
                logger.Debug(string.Format("CreateTransferCommandByPortModel [Exception]: {0}", ex.Message));
                //20240905 Add, when database disconnect will brack out program, need unlock equipment.
                _dbTool.SQLExec(_BaseDataService.LockEquip(_Equip, false), out tmpMsg, true);
            }
            finally
            {
                //Do Nothing
            }

            return result;
        }
        public bool CreateTransferCommandByTransferList(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, TransferList _transferList, out List<string> _arrayOfCmds)
        {
            bool result = false;
            string tmpMsg = "";
            _arrayOfCmds = new List<string>();
            DataTable dtTemp = new DataTable();
            string tableOrder = "";
            string _keyOfEnv = "";
            string _keyRTDEnv = "";
            string _keyCmd = "";

            try
            {
                _keyRTDEnv = _configuration["RTDEnvironment:type"];
                _keyOfEnv = string.Format("RTDEnvironment:commandsTable:{0}", _keyRTDEnv);
                //"RTDEnvironment:commandsTable:PROD"
                tableOrder = _configuration[_keyOfEnv] is null ? "workinprocess_sch" : _configuration[_keyOfEnv];

                string sql = "";
                string lotID = "";
                string CarrierID = "";

                //上機失敗皆回(in erack)
                NormalTransferModel normalTransfer = new NormalTransferModel();
                TransferList lstTransfer = new TransferList();
                normalTransfer.EquipmentID = "";
                normalTransfer.PortModel = "";
                normalTransfer.Transfer = new List<TransferList>();
                normalTransfer.LotID = lotID;

                CarrierID = _transferList.CarrierID;

                lstTransfer.CommandType = _transferList.CommandType.Equals("") ? "DIRECT-TRANS" : _transferList.CommandType;
                lstTransfer.Source = _transferList.Source.Equals("") ? "*" : _transferList.Source;
                lstTransfer.Dest = _transferList.Dest.Equals("") ? "*" : _transferList.Dest;
                lstTransfer.CarrierID = CarrierID;
                lstTransfer.Quantity = _transferList.Quantity;
                lstTransfer.CarrierType = _transferList.CarrierType;

                normalTransfer.Transfer.Add(lstTransfer);

                normalTransfer.Replace = 1;

                //產生派送指令識別碼
                //U + 2022081502 + 00001
                //將所產生的指令加入WorkInProcess_Sch
                tmpMsg = "";
                if (normalTransfer.Transfer is not null)
                {
                    _keyCmd = Tools.GetCommandID(_dbTool);
                    if (lstTransfer.CommandType.Equals("MANUAL-DIRECT"))
                    {
                        normalTransfer.CommandID = _keyCmd.Insert(12, "M");
                    }
                    else
                    {
                        normalTransfer.CommandID = _keyCmd.Insert(12, "P");
                    }

                    SchemaWorkInProcessSch workinProcessSch = new SchemaWorkInProcessSch();
                    workinProcessSch.Cmd_Id = normalTransfer.CommandID;
                    workinProcessSch.Cmd_State = "";    //派送前為NULL
                    workinProcessSch.EquipId = normalTransfer.EquipmentID;
                    workinProcessSch.Cmd_Current_State = "";    //派送前為NULL
                    workinProcessSch.Priority = 20;             //預設優先權為10
                    workinProcessSch.Replace = normalTransfer.Transfer.Count > 0 ? normalTransfer.Transfer.Count - 1 : 0;

                    foreach (TransferList trans in normalTransfer.Transfer)
                    {
                        workinProcessSch.CarrierId = trans.CarrierID.Equals("") ? "*" : trans.CarrierID;
                        if (!workinProcessSch.CarrierId.Equals("*")) {
                            dtTemp = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSchByCarrier(trans.CarrierID, tableOrder));

                            if (dtTemp.Rows.Count > 0)
                                continue;
                        }

                        tmpMsg = "";
                        workinProcessSch.UUID = Tools.GetUnitID(_dbTool);
                        workinProcessSch.Cmd_Type = trans.CommandType.Equals("") ? "DIRECT-TRANS" : trans.CommandType;
                        workinProcessSch.CarrierType = trans.CarrierType.Equals("") ? "" : trans.CarrierType;
                        workinProcessSch.Source = trans.Source.Equals("*") ? "*" : trans.Source;
                        workinProcessSch.Dest = trans.Dest.Equals("*") ? "*" : trans.Dest;
                        workinProcessSch.Back = "*";

                        DataTable dtInfo = new DataTable { };
                        if (trans.CarrierID is not null)
                        {
                            dtInfo = _dbTool.GetDataTable(_BaseDataService.QueryLotInfoByCarrierID(trans.CarrierID));
                        }
                        workinProcessSch.LotID = dtInfo.Rows.Count <= 0 ? "" : dtInfo.Rows[0]["lotid"].ToString();
                        workinProcessSch.Customer = dtInfo.Rows.Count <= 0 ? "" : dtInfo.Rows[0]["customername"].ToString();

                        sql = _BaseDataService.InsertTableWorkinprocess_Sch(workinProcessSch, tableOrder);
                        if (_dbTool.SQLExec(sql, out tmpMsg, true))
                        { }

                    }
                }
                //加入_arrayOfCmds
                if (normalTransfer.Transfer.Count <= 0)
                    OracleSequence.BackOne(_dbTool, "command_streamCode");
                else
                { result = true; _arrayOfCmds.Add(normalTransfer.CommandID); }
            }
            catch (Exception ex)
            {
                //Do Nothing
            }
            finally
            {
                if (dtTemp != null)
                    dtTemp.Dispose(); 
                //Do Nothing
            }
            dtTemp = null;

            return result;
        }
        //Port Status=========================
        //0. None, 1. Init, 2. Swap, 3. Unload.
        //====================================
        public int CheckPortStatus(DataTable _dt, string _portModel)
        {
            int iState = 0;
            //0. None, 1. Init, 2. Swap, 3. Unload.

            DataRow[] dr1 = null;
            DataRow[] dr2 = null;

            string sql = "";
            //Port Type:
            //0. Out of Service
            //1. Transfer Blocked
            //2. Near Completion
            //3. Ready to Unload
            //4. Empty (Ready to load)
            //5. Reject and Ready to unload
            //6. Port Alarm
            try
            {
                if (int.Parse(_dt.Rows[0]["Port_Number"].ToString()) > 0)
                {
                    if (int.Parse(_dt.Rows[0]["Port_Number"].ToString()).Equals(1))
                    {
                        dr1 = _dt.Select("Port_Type='IO'");

                        if (dr1.Length > 0)
                        {
                            if (dr1[0]["Port_State"].Equals(4))
                            {   //Waiting to load ---Init

                                iState = 1;
                            }
                            else if (dr1[0]["Port_State"].Equals(3))
                            {   //Waiting to unload  --Swap, Unload

                                //如果有PM或關機
                                if (true)
                                {   //PM
                                    iState = 3; //Unload
                                }
                                else
                                {   //None PM
                                    iState = 2; //Swap
                                }
                            }
                            else
                            {   //None State is 0
                            }
                        }
                    }
                    else
                    {
                        int iRun = 0;
                        int iResult = 0;
                        while (iRun < int.Parse(_dt.Rows[0]["Port_Number"].ToString()))
                        {
                            switch (_dt.Rows[iRun]["Port_Type"].ToString())
                            {
                                case "IN":
                                    if (int.Parse(_dt.Rows[0]["Port_State"].ToString()).Equals(3))
                                    {
                                        iResult += 1;
                                    }
                                    break;
                                case "OUT":
                                    if (int.Parse(_dt.Rows[0]["Port_State"].ToString()).Equals(3))
                                    {
                                        iResult += 10;
                                    }
                                    break;
                                default:
                                    break;
                            }

                            iRun++;
                        }

                        switch (_portModel)
                        {
                            case "1I1OT1":
                            case "1I1OT2":
                                if (iResult == 0)
                                {   //Unload or Swap
                                    iState = 3;
                                }
                                else if (iResult == 11)
                                {   //Load
                                    iState = 4;
                                }
                                else
                                {   //None iState = 0
                                }
                                break;
                            case "2I2OT1":
                                if (iResult == 0)
                                {   //Unload or Swap
                                    iState = 3;
                                }
                                else if (iResult == 11)
                                {   //Load
                                    iState = 4;
                                }
                                else
                                {   //None iState = 0
                                }
                                break;
                            case "1IOT1":
                                // None
                                break;
                            default:
                                int iSample = 11 * int.Parse(_dt.Rows[0]["Port_Number"].ToString());

                                if (iResult == 0)
                                {   //Unload or Swap
                                    iState = 3;
                                }
                                else if (iResult == iSample)
                                {   //Load
                                    iState = 4;
                                }
                                else
                                {   //None iState = 0
                                }
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            { }
            
            dr1 = null;
            dr2 = null;

            return iState;
        }
        //Port Type===========================
        //Port Type:
        //0. Out of Service
        //1. Transfer Blocked
        //2. Near Completion
        //3. Ready to Unload
        //4. Empty (Ready to load)
        //5. Reject and Ready to unload
        //6. Port Alarm
        //9. Unknow
        //====================================
        public int GetPortStatus(DataTable _dt, string _portID, out string _portDesc)
        {
            DataRow[] dr1 = null;

            int iState = 0;
            _portDesc = "";
            //Port Type:
            //0. Out of Service
            //1. Transfer Blocked
            //2. Near Completion
            //3. Ready to Unload
            //4. Empty (Ready to load)
            //5. Reject and Ready to unload
            //6. Port Alarm
            try
            {
                dr1 = _dt.Select("Port_ID = '" + _portID + "'");
                switch (int.Parse(dr1[0]["Port_State"].ToString()))
                {
                    case 0:
                        iState = 0;
                        _portDesc = "Out of Service";
                        break;
                    case 1:
                        iState = 1;
                        _portDesc = "Transfer Blocked";
                        break;
                    case 2:
                        iState = 2;
                        _portDesc = "Near Completion";
                        break;
                    case 3:
                        iState = 3;
                        _portDesc = "Ready to Unload";
                        break;
                    case 4:
                        iState = 4;
                        _portDesc = "Ready to load";
                        break;
                    case 5:
                        iState = 5;
                        _portDesc = "Reject and Ready to unload";
                        break;
                    case 6:
                        iState = 6;
                        _portDesc = "Port Alarm";
                        break;
                    default:
                        iState = 9;
                        _portDesc = "Unknow";
                        break;
                }

            }
            catch (Exception ex)
            { iState = 99; _portDesc = string.Format("Exception:{0}", ex.Message); }
            
            dr1 = null;

            return iState;
        }
        public DataTable GetAvailableCarrier(DBTool _dbTool, string _carrierType, bool _isFull, string _RTDEnv)
        {
            string sql = "";
            DataTable dtAvailableCarrier = null;

            try
            {
                //sql = string.Format(_BaseDataService.SelectAvailableCarrierByCarrierType(_carrierType, _isFull));

                if (_RTDEnv.Equals("PROD"))
                    sql = string.Format(_BaseDataService.SelectAvailableCarrierByCarrierType(_carrierType, _isFull));
                else if (_RTDEnv.Equals("UAT"))
                    sql = string.Format(_BaseDataService.SelectAvailableCarrierForUatByCarrierType(_carrierType, _isFull));

                dtAvailableCarrier = _dbTool.GetDataTable(sql);
            }
            catch (Exception ex)
            { dtAvailableCarrier = null; }

            return dtAvailableCarrier;
        }
        public DataTable GetAvailableCarrier2(DBTool _dbTool, string _carrierType, bool _isFull, string _RTDEnv)
        {
            string sql = "";
            DataTable dtAvailableCarrier = null;

            try
            {
                //sql = string.Format(_BaseDataService.SelectAvailableCarrierByCarrierType(_carrierType, _isFull));

                if (_RTDEnv.Equals("PROD"))
                    sql = string.Format(_BaseDataService.SelectAvailableCarrierIncludeEQPByCarrierType(_carrierType, _isFull));
                else if (_RTDEnv.Equals("UAT"))
                    sql = string.Format(_BaseDataService.SelectAvailableCarrierForUatByCarrierType(_carrierType, _isFull));

                dtAvailableCarrier = _dbTool.GetDataTable(sql);
            }
            catch (Exception ex)
            { dtAvailableCarrier = null; }

            return dtAvailableCarrier;
        }
        public DataTable GetAvailableCarrierFromEQP(DBTool _dbTool, string _portid, string _carrierType, bool _isFull, string _RTDEnv)
        {
            string sql = "";
            DataTable dtAvailableCarrier = null;

            try
            {
                //sql = string.Format(_BaseDataService.SelectAvailableCarrierByCarrierType(_carrierType, _isFull));

                if (_RTDEnv.Equals("PROD"))
                    sql = string.Format(_BaseDataService.SelectAvailableCarrierByEQP(_portid, _carrierType, _isFull));
                else if (_RTDEnv.Equals("UAT"))
                    sql = string.Format(_BaseDataService.SelectAvailableCarrierForUatByEQP(_portid, _carrierType, _isFull));

                dtAvailableCarrier = _dbTool.GetDataTable(sql);
            }
            catch (Exception ex)
            { dtAvailableCarrier = null; }

            return dtAvailableCarrier;
        }
        public string GetLocatePort(string _locate, int _portNo, string _locationType)
        {
            string LocatePort = "";
            string tmpLocate = "";
            //
            try
            {
                switch (_locationType)
                {
                    case "ERACK":
                    case "STOCKER":
                    case "EQUIPMENT":
                        tmpLocate = "{0}_LP{1}";
                        LocatePort = string.Format(tmpLocate, _locate.Trim(), _portNo.ToString().PadLeft(2, '0'));
                        break;
                    default:
                        tmpLocate = "{0}_LP{1}";
                        LocatePort = string.Format(tmpLocate, _locate.Trim(), _portNo.ToString().PadLeft(2, '0'));
                        break;
                }

            }
            catch (Exception ex)
            { }

            return LocatePort;
        }
        public double TimerTool(string unit, string lastDateTime)
        {
            double dbTime = 0;
            DateTime curDT = DateTime.Now;
            try
            {
                DateTime tmpDT = Convert.ToDateTime(lastDateTime);
                TimeSpan timeSpan = new TimeSpan(curDT.Ticks - tmpDT.Ticks);

                switch (unit.ToLower())
                {
                    case "day":
                        dbTime = timeSpan.TotalDays;
                        break;
                    case "hours":
                        dbTime = timeSpan.TotalHours;
                        break;
                    case "minutes":
                        dbTime = timeSpan.TotalMinutes;
                        break;
                    case "seconds":
                        dbTime = timeSpan.TotalSeconds;
                        break;
                    case "milliseconds":
                        dbTime = timeSpan.TotalMilliseconds;
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            { }
            return dbTime;
        }
        public double TimerTool(string unit, string startDateTime, string lastDateTime)
        {
            double dbTime = 0;
            DateTime StartDT;
            DateTime LastDT;
            try
            {
                StartDT = Convert.ToDateTime(startDateTime);
                LastDT = Convert.ToDateTime(lastDateTime);
                TimeSpan timeSpan = new TimeSpan(LastDT.Ticks - StartDT.Ticks);

                switch (unit.ToLower())
                {
                    case "day":
                        dbTime = timeSpan.TotalDays;
                        break;
                    case "hours":
                        dbTime = timeSpan.TotalHours;
                        break;
                    case "minutes":
                        dbTime = timeSpan.TotalMinutes;
                        break;
                    case "seconds":
                        dbTime = timeSpan.TotalSeconds;
                        break;
                    case "milliseconds":
                        dbTime = timeSpan.TotalMilliseconds;
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            { }
            return dbTime;
        }
        public int GetExecuteMode(DBTool _dbTool)
        {
            int iExecMode = 1;
            string sql = "";
            DataTable dt = null;

            try
            {
                sql = string.Format(_BaseDataService.SelectRTDDefaultSet("ExecuteMode"));
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    iExecMode = int.Parse(dt.Rows[0]["PARAMVALUE"].ToString());
                }
            }
            catch (Exception ex)
            { dt = null; }
            finally
            {
                if (dt != null)
                    dt.Dispose(); 
            }
            dt = null;

            return iExecMode;
        }
        public bool CheckIsAvailableLot(DBTool _dbTool, string _lotId, string _machine)
        {
            bool isAvailableLot = false;
            string sql = "";
            DataTable dt=null;

            try
            {
                sql = string.Format(_BaseDataService.CheckIsAvailableLot(_lotId, _machine));
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                    isAvailableLot = true;
                else
                    isAvailableLot = false;
            }
            catch (Exception ex)
            { dt = null; }
            finally
            {
                if (dt != null)
                    dt.Dispose(); 
            }
            dt = null;

            return isAvailableLot;
        }
        public bool VerifyCustomerDevice(DBTool _dbTool, ILogger _logger, string _machine, string _customerName, string _lotid, out string _resultCode)
        {
            /*
            //1xxx 機台狀態 [1001] 不同客戶, 為換客戶後的第一批, 可執行但發提示, [1002] 同客戶最後一批,可以執行上機但需發Alarm
            //2xxx 機台狀態 [2001] Not Load Port Status 不可執行, [2002] Not Production Running 可以執行上機
            ///檢驗結果True: 屬於同一客戶批號產品, False: 屬於不同客戶批號產品
            ///結果為True, Result Code: 0000/0001:當前最後一筆
             */
            bool bResult = false;
            DataTable dtCurrentLot = null;
            DataTable dtLoadPortCurrentState = null;
            DataTable dt = null;
            string tmpSql = "";
            //string _lotid = "";
            string tmpMsg = "";
            _resultCode = "0000";

            try
            {
                //20230413V1.0 Modify by Vance
                tmpSql = _BaseDataService.GetLoadPortCurrentState(_machine);
                dtLoadPortCurrentState = _dbTool.GetDataTable(tmpSql);

                if (dtLoadPortCurrentState.Rows.Count <= 0)
                {
                    tmpMsg = "No any relevant information of load port at this machine.";
                    _resultCode = "2001";
                    return bResult;
                }

                foreach (DataRow dr in dtLoadPortCurrentState.Rows)
                {
                    if (dr["CustomerName"].ToString().Equals(""))
                        continue;

                    if (dr["CustomerName"].ToString().Equals(_customerName))
                        bResult = true;
                    else
                    {
                        //it is last record of this Customer lot.  //will change Customer.
                        _resultCode = "1001";
                        bResult = true;
                        break;
                    }
                }

                if (bResult)
                {
                    if (_resultCode.Equals("0000"))
                    {
                        //Not include Hold/Proc/Delete/Complite 4 status.
                        tmpSql = _BaseDataService.SelectTableProcessLotInfoByCustomer(_customerName, _machine);
                        dt = _dbTool.GetDataTable(tmpSql);

                        if (dt.Rows.Count == 1)
                        {
                            _resultCode = "1002";
                            //it is the Customer last lot. need alarm eng to clean line.
                        }

                        // 0000 is normal state of this customer.
                    }
                }
                else
                {
                    tmpMsg = "No product running on this machine.";
                    _resultCode = "2002";
                }
            }
            catch (Exception ex)
            { }
            finally
            {
                if (dt != null)
                    dt.Dispose();
                if (dtCurrentLot != null)
                    dtCurrentLot.Dispose();
                if (dtLoadPortCurrentState != null)
                    dtLoadPortCurrentState.Dispose(); 
            }
            dt = null;
            dtCurrentLot = null;
            dtLoadPortCurrentState = null;

            return bResult;
        }
        public bool GetLockState(DBTool _dbTool)
        {
            bool isLock = false;
            string sql = "";
            DataTable dt=null;

            try
            {
                sql = string.Format(_BaseDataService.GetLockStateLotInfo());
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    if (dt.Rows[0]["lockstate"].ToString().Equals("1"))
                        isLock = true;
                    else
                        isLock = false;

                }
            }
            catch (Exception ex)
            { dt = null; }
            finally
            {
                if (dt != null)
                    dt.Dispose(); 
            }
            dt = null;

            return isLock;
        }
        public bool ThreadLimitTraffice(Dictionary<string, string> _threadCtrl, string key, double _time, string _timeUnit, string _symbol)
        {
            bool bCtrl;
            double timediff = 0;
            string lastDateTime = "";
            string unit;

            try
            {
                lock (_threadCtrl)
                {
                    _threadCtrl.TryGetValue(key, out lastDateTime);
                }

                bCtrl = false;

                switch (_timeUnit.ToLower())
                {
                    case "dd":
                        unit = "Day";
                        timediff = TimerTool(unit, lastDateTime);
                        break;
                    case "hh":
                        unit = "Hours";
                        timediff = TimerTool(unit, lastDateTime);
                        break;
                    case "mi":
                        unit = "Minutes";
                        timediff = TimerTool(unit, lastDateTime);
                        break;
                    case "ms":
                        unit = "MilliSeconds";
                        timediff = TimerTool(unit, lastDateTime);
                        break;
                    case "ss":
                        unit = "seconds";
                        timediff = TimerTool(unit, lastDateTime);
                        break;
                    default:
                        break;
                }

                switch (_symbol.ToLower())
                {
                    case "<=":
                        if (timediff <= _time)
                            bCtrl = true;
                        else
                            bCtrl = false;
                        break;
                    case ">=":
                        if (timediff >= _time)
                            bCtrl = true;
                        else
                            bCtrl = false;
                        break;
                    case "=":
                        if (timediff == _time)
                            bCtrl = true;
                        else
                            bCtrl = false;
                        break;
                    case ">":
                        if (timediff > _time)
                            bCtrl = true;
                        else
                            bCtrl = false;
                        break;
                    case "<":
                    default:
                        if (timediff < _time)
                            bCtrl = true;
                        else
                            bCtrl = false;

                        break;
                }
            }
            catch (Exception ex)
            { bCtrl = false; }

            return bCtrl;
        }
        public bool AutoAssignCarrierType(DBTool _dbTool, out string tmpMessage)
        {
            bool bResult = false;
            string sql = "";
            string tmpSql = "";
            string tmpMsg = "";
            tmpMessage = "";
            DataTable dt = null;
            DataTable dt2 = null;

            try
            {
                sql = string.Format(_BaseDataService.SelectRTDDefaultSet("CarrierType"));
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    string keyValue = "";
                    string Desc = "";

                    foreach (DataRow dr in dt.Rows)
                    {
                        keyValue = "";
                        Desc = "";

                        keyValue = dr["ParamValue"] is not null ? dr["ParamValue"].ToString() : "";
                        Desc = dr["description"] is not null ? dr["description"].ToString() : "";

                        tmpSql = string.Format(_BaseDataService.QueryCarrierType(keyValue, Desc));
                        dt2 = _dbTool.GetDataTable(tmpSql);

                        if (dt2.Rows.Count > 0)
                        {
                            _dbTool.SQLExec(_BaseDataService.UpdateCarrierType(keyValue, Desc), out tmpMsg, true);
                        }
                    }

                    if (dt.Rows[0]["lockstate"].ToString().Equals("1"))
                    {
                        tmpMessage = "";
                        bResult = true;
                    }
                    else
                    {
                        tmpMessage = "";
                        bResult = false;
                    }

                }
            }
            catch (Exception ex)
            {

            }
            finally
            {
                if (dt != null)
                    dt.Dispose();
                if (dt2 != null)
                    dt2.Dispose();
            }
            dt = null;
            dt2 = null;

            return bResult;
        }
        public bool AutoSentInfoUpdate(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, out string tmpMessage)
        {
            bool bResult = false;
            string sql = "";
            string tmpSql = "";
            string tmpMsg = "";
            tmpMessage = "";
            DataTable dt = null;
            DataTable dt2 = null;
            DataTable dtTemp = null;
            List<string> args = new();
            APIResult apiResult = new APIResult();
            string _table = "";
            string _infoupdate = "";
            int _reflushTime = 0;
            string _reflushUnit = "";

            try
            {
                //_args.Split(',')
                _table = _configuration["eRackDisplayInfo:contained"].ToString().Split(',')[1];
                _reflushUnit = _configuration["ReflushTime:ReflusheRack:TimeUnit"] is null ? "Minutes" : _configuration["ReflushTime:ReflusheRack:TimeUnit"].ToString();
                _reflushTime = _configuration["ReflushTime:ReflusheRack:Time"] is null ? 3 : int.Parse(_configuration["ReflushTime:ReflusheRack:Time"].ToString());

                sql = _BaseDataService.QueryCarrierAssociateWhenOnErack(_table);
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    _infoupdate = dt.Rows[0]["info_update_dt"].ToString().Equals("NULL") ? "" : dt.Rows[0]["info_update_dt"].ToString();

                    string lotid;
                    string _tmplotid = "";
                    string carrierId;
                    string _quantity;
                    string _totalQty;
                    foreach (DataRow dr in dt.Rows)
                    {
                        args = new();
                        apiResult = new APIResult();
                        carrierId = "";

                        lotid = dr["LOT_ID"].ToString().Equals("") ? "" : dr["LOT_ID"].ToString();
                        if(lotid.Contains("R") || lotid.Contains("S") )
                            _tmplotid = lotid.Replace("R", "").Replace("S", "");
                        else
                            _tmplotid = lotid;

                        carrierId = dr["carrier_id"].ToString().Equals("") ? "" : dr["carrier_id"].ToString();

                        sql = _BaseDataService.QueryLotInfoByCarrierID(carrierId);
                        dt2 = _dbTool.GetDataTable(sql);

                        if (dt2.Rows.Count > 0)
                        {
                            _quantity = dt2.Rows[0]["quantity"].ToString().Equals("") ? "0" : dt2.Rows[0]["quantity"].ToString().Trim();
                            _totalQty = dt2.Rows[0]["total_qty"].ToString().Equals("") ? "0" : dt2.Rows[0]["total_qty"].ToString().Trim(); ;
                        }
                        else
                        {
                            _quantity = "0";
                            _totalQty = "0";
                        }

                        if (dr["location_type"].ToString().Trim().Equals("ERACK"))
                        {

                            if (carrierId.Length > 0)
                            {
                                tmpMsg = string.Format("[AutoSentInfoUpdate: Flag LotId {0}]", lotid);
                                _logger.Info(tmpMsg);

                                string v_STAGE = "";
                                string v_CUSTOMERNAME = "";
                                string v_PARTID = "";
                                string v_LOTTYPE = "";
                                string v_AUTOMOTIVE = "";
                                string v_STATE = "";
                                string v_HOLDCODE = "";
                                string v_TURNRATIO = "0";
                                string v_EOTD = "";
                                string v_HOLDREAS = "";
                                string v_POTD = "";
                                string v_WAFERLOT = "";
                                string v_Quantity = _quantity;
                                string v_TotalQty = _totalQty;
                                string v_Force = "";
                                try
                                {
                                    if (v_CUSTOMERNAME.Equals(""))
                                    {
                                        sql = _BaseDataService.SelectTableLotInfoByLotid(_tmplotid);
                                        dt2 = _dbTool.GetDataTable(sql);

                                        if (dt2.Rows.Count > 0)
                                        {
                                            v_CUSTOMERNAME = dt2.Rows[0]["CUSTOMERNAME"].ToString().Equals("") ? "" : dt2.Rows[0]["CUSTOMERNAME"].ToString();
                                            v_PARTID = dt2.Rows[0]["PARTID"].ToString().Equals("") ? "" : dt2.Rows[0]["PARTID"].ToString();
                                            v_LOTTYPE = dt2.Rows[0]["LOTTYPE"].ToString().Equals("") ? "" : dt2.Rows[0]["LOTTYPE"].ToString();
                                        }
                                    }

                                    sql = _BaseDataService.QueryErackInfoByLotID(_configuration["eRackDisplayInfo:contained"], _tmplotid);
                                    dtTemp = _dbTool.GetDataTable(sql);

                                    if (dtTemp.Rows.Count > 0)
                                    {
                                        v_STAGE = dtTemp.Rows[0]["STAGE"].ToString().Equals("") ? "" : dtTemp.Rows[0]["STAGE"].ToString();
                                        v_AUTOMOTIVE = dtTemp.Rows[0]["AUTOMOTIVE"].ToString().Equals("") ? "" : dtTemp.Rows[0]["AUTOMOTIVE"].ToString();
                                        v_STATE = dtTemp.Rows[0]["STATE"].ToString().Equals("") ? "" : dtTemp.Rows[0]["STATE"].ToString();
                                        v_HOLDCODE = dtTemp.Rows[0]["HOLDCODE"].ToString().Equals("") ? "" : dtTemp.Rows[0]["HOLDCODE"].ToString();
                                        v_TURNRATIO = dtTemp.Rows[0]["TURNRATIO"].ToString().Equals("") ? "0" : dtTemp.Rows[0]["TURNRATIO"].ToString();
                                        v_EOTD = dtTemp.Rows[0]["EOTD"].ToString().Equals("") ? "" : dtTemp.Rows[0]["EOTD"].ToString();
                                        v_POTD = dtTemp.Rows[0]["POTD"].ToString().Equals("") ? "" : dtTemp.Rows[0]["POTD"].ToString();
                                        v_HOLDREAS = dtTemp.Rows[0]["HoldReas"].ToString().Equals("") ? "" : dtTemp.Rows[0]["HoldReas"].ToString();
                                        v_WAFERLOT = dtTemp.Rows[0]["waferlotid"].ToString().Equals("") ? "" : dtTemp.Rows[0]["waferlotid"].ToString();

                                        sql = _BaseDataService.CheckDuplicatelot(_tmplotid);
                                        dt2 = _dbTool.GetDataTable(sql);

                                        if (dt2.Rows.Count > 0)
                                        {
                                            if (int.Parse(dt2.Rows[0]["LOTCOUNT"].ToString()) > 1)
                                                v_HOLDREAS = "duplicate";
                                        }

                                        if (_infoupdate.Equals(""))
                                        {
                                            v_Force = "false";
                                        }
                                        else
                                        {
                                            if (TimerTool(_reflushUnit, _infoupdate) >= _reflushTime)
                                            { v_Force = "true"; }
                                            else
                                            { v_Force = "false"; }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    tmpMsg = string.Format("[AutoSentInfoUpdate: Column Issue. {0}]", ex.Message);
                                    _logger.Debug(tmpMsg);
                                }

                                if (CheckMCSStatus(_dbTool, _logger))
                                {
                                    args.Add(lotid);
                                    args.Add(v_STAGE.Equals("") ? dr["STAGE"].ToString() : v_STAGE);
                                    args.Add("");//("machine");
                                    args.Add("");//("desc");
                                    args.Add(carrierId);
                                    args.Add(v_CUSTOMERNAME);
                                    args.Add(v_PARTID);//("PartID");
                                    args.Add(v_LOTTYPE);//("LotType");
                                    args.Add(v_AUTOMOTIVE);//("Automotive");
                                    args.Add(v_STATE);//("State");
                                    args.Add(v_HOLDCODE);//("HoldCode");
                                    args.Add(v_TURNRATIO);//("TURNRATIO");
                                    args.Add(v_EOTD);//("EOTD");
                                    args.Add(v_HOLDREAS);//("EOTD");
                                    args.Add(v_POTD);//("EOTD");
                                    args.Add(v_WAFERLOT);//("EOTD");
                                    args.Add(v_Quantity);//("Quantity");
                                    args.Add(v_TotalQty);//("TotalQty");
                                    args.Add(v_Force);//("v_Force");
                                    apiResult = SentCommandtoMCSByModel(_dbTool, _configuration, _logger, "InfoUpdate", args);

                                    if (!carrierId.Equals(""))
                                    {
                                        if (v_Force.ToLower().Equals("true"))
                                        {
                                            sql = _BaseDataService.CarrierTransferDTUpdate(carrierId, "InfoUpdate");
                                            _dbTool.SQLExec(sql, out tmpMsg, true);
                                        }
                                    }
                                }
                                else
                                {
                                    _logger.Info(string.Format("MCS Status incorrect, [{0}][{1}] not success. [{2}]", "CarrierLocationUpdate", "InfoUpdate", carrierId));
                                }
                            }
                            else
                            {
                                tmpMsg = string.Format("[CarrierLocationUpdate: Carrier [{0}] Not Exist.]", carrierId);
                                _logger.Debug(tmpMsg);

                                if (CheckMCSStatus(_dbTool, _logger))
                                {
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");//18 Force
                                    apiResult = SentCommandtoMCSByModel(_dbTool, _configuration, _logger, "InfoUpdate", args);
                                }
                                else
                                {
                                    _logger.Info(string.Format("MCS Status incorrect, [{0}][{1}] not success. [{2}]", "CarrierLocationUpdate", "InfoUpdate", carrierId));
                                }
                            }
                            Thread.Sleep(300);
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
            finally
            {
                if (dt != null)
                    dt.Dispose();
                if (dt2 != null)
                    dt2.Dispose();
                if (dtTemp != null)
                    dtTemp.Dispose(); 
            }
            dt = null;
            dt2 = null;
            dtTemp = null;

            return bResult;
        }
        public bool AutoSentInfoUpdateForSTK(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, out string tmpMessage)
        {
            bool bResult = false;
            string sql = "";
            string tmpSql = "";
            string tmpMsg = "";
            tmpMessage = "";
            DataTable dt = null;
            DataTable dt2 = null;
            DataTable dtTemp = null;
            List<string> args = new();
            APIResult apiResult = new APIResult();
            string _table = "";
            string _infoupdate = "";
            int _reflushTime = 0;
            string _reflushUnit = "";

            try
            {
                //_args.Split(',')
                _table = _configuration["eRackDisplayInfo:contained"].ToString().Split(',')[1];
                _reflushUnit = _configuration["ReflushTime:ReflusheSTK:TimeUnit"] is null ? "Minutes" : _configuration["ReflushTime:ReflusheSTK:TimeUnit"].ToString();
                _reflushTime = _configuration["ReflushTime:ReflusheSTK:Time"] is null ? 10 : int.Parse(_configuration["ReflushTime:ReflusheSTK:Time"].ToString());

                sql = _BaseDataService.QueryCarrierAssociateWhenOnErack(_table);
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    _infoupdate = dt.Rows[0]["info_update_dt"].ToString().Equals("NULL") ? "" : dt.Rows[0]["info_update_dt"].ToString();

                    string lotid;
                    string _tmplotid = "";
                    string carrierId;
                    string _quantity = "";
                    string _totalQty;
                    foreach (DataRow dr in dt.Rows)
                    {
                        args = new();
                        apiResult = new APIResult();
                        carrierId = "";

                        lotid = dr["LOT_ID"].ToString().Equals("") ? "" : dr["LOT_ID"].ToString();
                        if (lotid.Contains("R") || lotid.Contains("S"))
                            _tmplotid = lotid.Replace("R", "").Replace("S", "");
                        else
                            _tmplotid = lotid;

                        carrierId = dr["carrier_id"].ToString().Equals("") ? "" : dr["carrier_id"].ToString();

                        sql = _BaseDataService.QueryLotInfoByCarrierID(carrierId);
                        dt2 = _dbTool.GetDataTable(sql);

                        if (dt2.Rows.Count > 0)
                        {
                            _quantity = dt2.Rows[0]["quantity"].ToString().Equals("") ? "0" : dt2.Rows[0]["quantity"].ToString().Trim();
                            _totalQty = dt2.Rows[0]["total_qty"].ToString().Equals("") ? "0" : dt2.Rows[0]["total_qty"].ToString().Trim(); ;
                        }
                        else
                        {
                            _quantity = "0";
                            _totalQty = "0";
                        }

                        if (dr["location_type"].ToString().Trim().Equals("STK"))
                        {

                            if (carrierId.Length > 0)
                            {
                                tmpMsg = string.Format("[AutoSentInfoUpdate: Flag LotId {0}]", lotid);
                                _logger.Info(tmpMsg);

                                string v_STAGE = "";
                                string v_CUSTOMERNAME = "";
                                string v_PARTID = "";
                                string v_LOTTYPE = "";
                                string v_AUTOMOTIVE = "";
                                string v_STATE = "";
                                string v_HOLDCODE = "";
                                string v_TURNRATIO = "0";
                                string v_EOTD = "";
                                string v_HOLDREAS = "";
                                string v_POTD = "";
                                string v_WAFERLOT = "";
                                string v_Quantity = _quantity;
                                string v_TotalQty = _totalQty;
                                string v_Force = "";
                                try
                                {
                                    v_CUSTOMERNAME = dr["CUSTOMERNAME"].ToString().Equals("") ? "" : dr["CUSTOMERNAME"].ToString();
                                    v_PARTID = dr["PARTID"].ToString().Equals("") ? "" : dr["PARTID"].ToString();
                                    v_LOTTYPE = dr["LOTTYPE"].ToString().Equals("") ? "" : dr["LOTTYPE"].ToString();

                                    sql = _BaseDataService.QueryErackInfoByLotID(_configuration["eRackDisplayInfo:contained"], _tmplotid);
                                    dtTemp = _dbTool.GetDataTable(sql);

                                    if (dtTemp.Rows.Count > 0)
                                    {
                                        if (v_CUSTOMERNAME.Equals(""))
                                        {
                                            sql = _BaseDataService.SelectTableLotInfoByLotid(_tmplotid);
                                            dt2 = _dbTool.GetDataTable(sql);

                                            if (dt2.Rows.Count > 0)
                                            {
                                                v_CUSTOMERNAME = dt2.Rows[0]["CUSTOMERNAME"].ToString().Equals("") ? "" : dt2.Rows[0]["CUSTOMERNAME"].ToString();
                                                v_PARTID = dt2.Rows[0]["PARTID"].ToString().Equals("") ? "" : dt2.Rows[0]["PARTID"].ToString();
                                                v_LOTTYPE = dt2.Rows[0]["LOTTYPE"].ToString().Equals("") ? "" : dt2.Rows[0]["LOTTYPE"].ToString();
                                            }
                                        }

                                        v_STAGE = dtTemp.Rows[0]["STAGE"].ToString().Equals("") ? "" : dtTemp.Rows[0]["STAGE"].ToString();
                                        v_AUTOMOTIVE = dtTemp.Rows[0]["AUTOMOTIVE"].ToString().Equals("") ? "" : dtTemp.Rows[0]["AUTOMOTIVE"].ToString();
                                        v_STATE = dtTemp.Rows[0]["STATE"].ToString().Equals("") ? "" : dtTemp.Rows[0]["STATE"].ToString();
                                        v_HOLDCODE = dtTemp.Rows[0]["HOLDCODE"].ToString().Equals("") ? "" : dtTemp.Rows[0]["HOLDCODE"].ToString();
                                        v_TURNRATIO = dtTemp.Rows[0]["TURNRATIO"].ToString().Equals("") ? "0" : dtTemp.Rows[0]["TURNRATIO"].ToString();
                                        v_EOTD = dtTemp.Rows[0]["EOTD"].ToString().Equals("") ? "" : dtTemp.Rows[0]["EOTD"].ToString();
                                        v_POTD = dtTemp.Rows[0]["POTD"].ToString().Equals("") ? "" : dtTemp.Rows[0]["POTD"].ToString();
                                        v_HOLDREAS = dtTemp.Rows[0]["HoldReas"].ToString().Equals("") ? "" : dtTemp.Rows[0]["HoldReas"].ToString();
                                        v_WAFERLOT = dtTemp.Rows[0]["waferlotid"].ToString().Equals("") ? "" : dtTemp.Rows[0]["waferlotid"].ToString();

                                        sql = _BaseDataService.CheckDuplicatelot(_tmplotid);
                                        dt2 = _dbTool.GetDataTable(sql);

                                        if (dt2.Rows.Count > 0)
                                        {
                                            if (int.Parse(dt2.Rows[0]["LOTCOUNT"].ToString()) > 1)
                                                v_HOLDREAS = "duplicate";
                                        }

                                        if (_infoupdate.Equals(""))
                                        {
                                            v_Force = "false";
                                        }
                                        else
                                        {
                                            if (TimerTool(_reflushUnit, _infoupdate) >= _reflushTime)
                                            { v_Force = "true"; }
                                            else
                                            { v_Force = "false"; }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    tmpMsg = string.Format("[AutoSentInfoUpdate: Column Issue. {0}]", ex.Message);
                                    _logger.Debug(tmpMsg);
                                }

                                if (CheckMCSStatus(_dbTool, _logger))
                                {
                                    args.Add(lotid);
                                    args.Add(v_STAGE.Equals("") ? dr["STAGE"].ToString() : v_STAGE);
                                    args.Add("");//("machine");
                                    args.Add("");//("desc");
                                    args.Add(carrierId);
                                    args.Add(v_CUSTOMERNAME);
                                    args.Add(v_PARTID);//("PartID");
                                    args.Add(v_LOTTYPE);//("LotType");
                                    args.Add(v_AUTOMOTIVE);//("Automotive");
                                    args.Add(v_STATE);//("State");
                                    args.Add(v_HOLDCODE);//("HoldCode");
                                    args.Add(v_TURNRATIO);//("TURNRATIO");
                                    args.Add(v_EOTD);//("EOTD");
                                    args.Add(v_HOLDREAS);//("EOTD");
                                    args.Add(v_POTD);//("EOTD");
                                    args.Add(v_WAFERLOT);//("EOTD");
                                    args.Add(v_Quantity);//("Quantity");
                                    args.Add(v_TotalQty);//("TotalQty");
                                    args.Add(v_Force);//("v_Force");
                                    apiResult = SentCommandtoMCSByModel(_dbTool, _configuration, _logger, "InfoUpdate", args);

                                    if (!carrierId.Equals(""))
                                    {
                                        if (v_Force.ToLower().Equals("true"))
                                        {
                                            sql = _BaseDataService.CarrierTransferDTUpdate(carrierId, "InfoUpdate");
                                            _dbTool.SQLExec(sql, out tmpMsg, true);
                                        }
                                    }
                                }
                                else
                                {
                                    _logger.Info(string.Format("MCS Status incorrect, [{0}][{1}] not success. [{2}]", "AutoSentInfoUpdate", "InfoUpdate", carrierId));
                                }
                            }
                            else
                            {
                                tmpMsg = string.Format("[CarrierLocationUpdate: Carrier [{0}] Not Exist.]", carrierId);
                                _logger.Debug(tmpMsg);

                                if (CheckMCSStatus(_dbTool, _logger))
                                {
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");//Quantity
                                    args.Add("");//TotalQty
                                    args.Add("");//18 Force
                                    apiResult = SentCommandtoMCSByModel(_dbTool, _configuration, _logger, "InfoUpdate", args);
                                }
                                else
                                {
                                    _logger.Info(string.Format("MCS Status incorrect, [{0}][{1}] not success. [{2}]", "AutoSentInfoUpdate", "InfoUpdate", carrierId));
                                }                                
                            }
                            Thread.Sleep(300);
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
            finally
            {
                if (dt != null)
                    dt.Dispose();
                if (dt2 != null)
                    dt2.Dispose();
                if (dtTemp != null)
                    dtTemp.Dispose();
            }
            dt = null;
            dt2 = null;
            dtTemp = null;

            return bResult;
        }
        public bool AutoBindAndSentInfoUpdate(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, out string tmpMessage)
        {
            bool bResult = false;
            string sql = "";
            string tmpSql = "";
            string tmpMsg = "";
            tmpMessage = "";
            DataTable dt = null;
            DataTable dt2 = null;
            DataTable dtTemp = null;
            List<string> args = new();
            APIResult apiResult = new APIResult();

            try
            {
                sql = _BaseDataService.QueryCarrierAssociateWhenIsNewBind();
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    string lotid;
                    string _tmpLot;
                    string carrierId;
                    string _quantity;
                    string _totalQty;
                    foreach (DataRow dr in dt.Rows)
                    {
                        args = new();
                        apiResult = new APIResult();
                        carrierId = "";

                        lotid = dr["LOT_ID"].ToString().Equals("") ? "" : dr["LOT_ID"].ToString();
                        carrierId = dr["carrier_id"].ToString().Equals("") ? "" : dr["carrier_id"].ToString();

                        sql = _BaseDataService.QueryLotInfoByCarrierID(carrierId);
                        dt2 = _dbTool.GetDataTable(sql);

                        if (dt2.Rows.Count > 0)
                        {
                            _quantity = dt2.Rows[0]["quantity"].ToString().Equals("") ? "0" : dt2.Rows[0]["quantity"].ToString().Trim();
                            _totalQty = dt2.Rows[0]["total_qty"].ToString().Equals("") ? "0" : dt2.Rows[0]["total_qty"].ToString().Trim(); ;
                        }
                        else
                        {
                            _quantity = "0";
                            _totalQty = "0";
                        }

                        if (lotid.Contains("R") || lotid.Contains("S"))
                            _tmpLot = lotid.Replace("R", "").Replace("S", "");
                        else
                            _tmpLot = lotid;

                        if (dr["location_type"].ToString().Trim().Equals("ERACK") || dr["location_type"].ToString().Trim().Equals("A") || dr["location_type"].ToString().Trim().Equals("STK"))
                        {

                            if (carrierId.Length > 0)
                            {
                                tmpMsg = string.Format("[AutoSentInfoUpdate: Flag LotId {0}]", _tmpLot);
                                _logger.Info(tmpMsg);

                                string v_STAGE = "";
                                string v_CUSTOMERNAME = "";
                                string v_PARTID = "";
                                string v_LOTTYPE = "";
                                string v_AUTOMOTIVE = "";
                                string v_STATE = "";
                                string v_HOLDCODE = "";
                                string v_TURNRATIO = "0";
                                string v_EOTD = "";
                                string v_HOLDREAS = "";
                                string v_POTD = "";
                                string v_WAFERLOT = "";
                                string v_Quantity = _quantity;
                                string v_totalQty = _totalQty;
                                string v_Force = "";
                                try
                                {
                                    v_CUSTOMERNAME = dr["CUSTOMERNAME"].ToString().Equals("") ? "" : dr["CUSTOMERNAME"].ToString();
                                    v_PARTID = dr["PARTID"].ToString().Equals("") ? "" : dr["PARTID"].ToString();
                                    v_LOTTYPE = dr["LOTTYPE"].ToString().Equals("") ? "" : dr["LOTTYPE"].ToString();

                                    sql = _BaseDataService.QueryErackInfoByLotID(_configuration["eRackDisplayInfo:contained"], _tmpLot);
                                    dtTemp = _dbTool.GetDataTable(sql);

                                    if (dtTemp.Rows.Count > 0)
                                    {
                                        v_STAGE = dtTemp.Rows[0]["STAGE"].ToString().Equals("") ? "" : dtTemp.Rows[0]["STAGE"].ToString();
                                        v_AUTOMOTIVE = dtTemp.Rows[0]["AUTOMOTIVE"].ToString().Equals("") ? "" : dtTemp.Rows[0]["AUTOMOTIVE"].ToString();
                                        v_STATE = dtTemp.Rows[0]["STATE"].ToString().Equals("") ? "" : dtTemp.Rows[0]["STATE"].ToString();
                                        v_HOLDCODE = dtTemp.Rows[0]["HOLDCODE"].ToString().Equals("") ? "" : dtTemp.Rows[0]["HOLDCODE"].ToString();
                                        v_TURNRATIO = dtTemp.Rows[0]["TURNRATIO"].ToString().Equals("") ? "0" : dtTemp.Rows[0]["TURNRATIO"].ToString();
                                        v_EOTD = dtTemp.Rows[0]["EOTD"].ToString().Equals("") ? "" : dtTemp.Rows[0]["EOTD"].ToString();
                                        v_POTD = dtTemp.Rows[0]["POTD"].ToString().Equals("") ? "" : dtTemp.Rows[0]["POTD"].ToString();
                                        v_HOLDREAS = dtTemp.Rows[0]["HoldReas"].ToString().Equals("") ? "" : dtTemp.Rows[0]["HoldReas"].ToString();
                                        v_WAFERLOT = dtTemp.Rows[0]["waferlotid"].ToString().Equals("") ? "" : dtTemp.Rows[0]["waferlotid"].ToString();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    tmpMsg = string.Format("[AutoSentInfoUpdate: Column Issue. {0}]", ex.Message);
                                    _logger.Debug(tmpMsg);
                                }

                                if (CheckMCSStatus(_dbTool, _logger))
                                {
                                    args.Add(lotid);
                                    args.Add(v_STAGE.Equals("") ? dr["STAGE"].ToString() : v_STAGE);
                                    args.Add("");//("machine");
                                    args.Add("");//("desc");
                                    args.Add(carrierId);
                                    args.Add(v_CUSTOMERNAME);
                                    args.Add(v_PARTID);//("PartID");
                                    args.Add(v_LOTTYPE);//("LotType");
                                    args.Add(v_AUTOMOTIVE);//("Automotive");
                                    args.Add(v_STATE);//("State");
                                    args.Add(v_HOLDCODE);//("HoldCode");
                                    args.Add(v_TURNRATIO);//("TURNRATIO");
                                    args.Add(v_EOTD);//("EOTD");
                                    args.Add(v_HOLDREAS);//("EOTD");
                                    args.Add(v_POTD);//("EOTD");
                                    args.Add(v_WAFERLOT);//("EOTD");
                                    args.Add(v_Quantity);//("Quantity");
                                    args.Add(v_totalQty);//("totalQty");
                                    args.Add(v_Force);//("v_Force");
                                    apiResult = SentCommandtoMCSByModel(_dbTool, _configuration, _logger, "InfoUpdate", args);
                                }
                                else
                                {
                                    _logger.Info(string.Format("MCS Status incorrect, [{0}][{1}] not success. [{2}]", "AutoBindAndSentInfoUpdate", "InfoUpdate", carrierId));
                                }

                                if (!lotid.Equals(""))
                                {
                                    //檢查Lot的Total Qty是否為0 用Quantity取代 Total Qty
                                    //如果不同, 用Quantity 取代 Total Qty
                                    //相同則不變更
                                    tmpSql = _BaseDataService.QueryLotinfoQuantity(lotid);
                                    dt2 = _dbTool.GetDataTable(tmpSql);

                                    if (dt2.Rows.Count > 0)
                                    {
                                        int iTotalQty = int.Parse(dt2.Rows[0]["Total_Qty"].ToString());
                                        int iQuantity = int.Parse(dr["Quantity"].ToString());
                                        if ((iTotalQty == 0) || (iQuantity > iTotalQty))
                                        {
                                            //Sync Total and Quantity
                                            _dbTool.SQLExec(_BaseDataService.UpdateLotinfoTotalQty(lotid, iQuantity), out tmpMsg, true);
                                        }
                                    }
                                    else
                                    {
                                        //Lot 還未被Release時, New Bind狀態不被改掉 
                                        continue;
                                    }
                                }
                            }
                            else
                            {
                                tmpMsg = string.Format("[CarrierLocationUpdate: Carrier [{0}] Not Exist.]", carrierId);
                                _logger.Debug(tmpMsg);

                                if (CheckMCSStatus(_dbTool, _logger))
                                {
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");
                                    args.Add("");//Quantity
                                    args.Add("");//TotalQty
                                    args.Add("");//18 Force
                                    apiResult = SentCommandtoMCSByModel(_dbTool, _configuration, _logger, "InfoUpdate", args);
                                }
                                else
                                {
                                    _logger.Info(string.Format("MCS Status incorrect, [{0}][{1}] not success. [{2}]", "AutoBindAndSentInfoUpdate", "InfoUpdate", carrierId));
                                }


                            }

                            //Clean EquipList when Rebind Lot
                            if (!lotid.Equals(""))
                            {
                                sql = _BaseDataService.EQPListReset(lotid);
                                _dbTool.SQLExec(sql, out tmpMsg, true);
                            }
                        }
                        else if (dr["location_type"].ToString().Trim().Equals("Sync"))
                        {
                            //Do Nothing. when lot info data been released.
                            continue;
                        }
                        else
                        {
                            if (!carrierId.Equals(""))
                            {
                                sql = _BaseDataService.ResetCarrierLotAssociateNewBind(carrierId);
                                _dbTool.SQLExec(sql, out tmpMessage, true);
                            }
                        }

                        if (apiResult.Success)
                        {
                            tmpMessage = "";
                            bResult = true;
                            sql = _BaseDataService.ResetCarrierLotAssociateNewBind(carrierId);
                            _dbTool.SQLExec(sql, out tmpMessage, true);
                        }
                        else
                        {
                            tmpMessage = apiResult.Message;
                            bResult = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
            finally
            {
                if (dt != null)
                    dt.Dispose();
                if (dt2 != null)
                    dt2.Dispose();
                if (dtTemp != null)
                    dtTemp.Dispose();
                args.Clear();
                if (apiResult != null)
                    apiResult.Dispose();
            }
            dt = null;
            dt2 = null;
            dtTemp = null;
            args = null;
            apiResult = null;

            return bResult;
        }
        public bool AutoUpdateRTDStatistical(DBTool _dbTool, out string tmpMessage)
        {
            bool bResult = false;
            string sql = "";
            string tmpSql = "";
            string tmpMsg = "";
            tmpMessage = "";
            DataTable dt = null;
            DataTable dt2 = null;

            try
            {
                sql = string.Format(_BaseDataService.QueryRTDStatisticalRecord(""));
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {

                    foreach (DataRow dr in dt.Rows)
                    {
                        DateTime tmpDateTime;
                        if (dr["recordtime"] is null)
                            continue;
                        tmpDateTime = DateTime.Parse(dr["recordtime"].ToString());
                        tmpSql = string.Format(_BaseDataService.QueryRTDStatisticalByCurrentHour(tmpDateTime));
                        dt2 = _dbTool.GetDataTable(tmpSql);

                        if (dt2.Rows.Count > 0)
                        {
                            string cdtType = String.Format("Type = '{0}'", dr["type"].ToString());
                            DataRow[] dr2 = dt2.Select(cdtType);
                            int iTimes = dr2.Length > 0 ? int.Parse(dr2[0]["times"].ToString()) + int.Parse(dr["times"].ToString()) : int.Parse(dr["times"].ToString());
                            _dbTool.SQLExec(_BaseDataService.UpdateRTDStatistical(tmpDateTime, dr["type"].ToString(), iTimes), out tmpMsg, true);
                            _dbTool.SQLExec(_BaseDataService.CleanRTDStatisticalRecord(tmpDateTime, dr["type"].ToString()), out tmpMsg, true);
                        }
                        else
                        {
                            _dbTool.SQLExec(_BaseDataService.InitialRTDStatistical(tmpDateTime.ToString("yyyy-MM-dd HH:mm:ss"), dr["type"].ToString()), out tmpMsg, true);
                            _dbTool.SQLExec(_BaseDataService.UpdateRTDStatistical(tmpDateTime, dr["type"].ToString(), int.Parse(dr["times"].ToString())), out tmpMsg, true);
                            _dbTool.SQLExec(_BaseDataService.CleanRTDStatisticalRecord(tmpDateTime, dr["type"].ToString()), out tmpMsg, true);
                        }
                    }

                    bResult = true;
                }
                else
                {
                    //Do Nothing
                }
            }
            catch (Exception ex)
            {
                bResult = false;
            }
            finally
            {
                if(dt != null)
                    dt.Dispose();
                if (dt2 != null)
                    dt2.Dispose();
            }
            dt = null;
            dt2 = null;

            return bResult;
        }
        private Dictionary<int, string> _lstAlarmCode = new Dictionary<int, string>();
        public Dictionary<int, string> ListAlarmCode
        {
            get { return _lstAlarmCode; }
            set { _lstAlarmCode = value; }
        }
        public bool CallRTDAlarm(DBTool _dbTool, int _alarmCode, string[] argv)
        {
            bool bResult = false;
            string tmpSQL = "";
            string error = "";
            string alarmMsg = "";
            Dictionary<int, string> lstAlarm = new Dictionary<int, string>();
            string _commandid = "";
            string _params = "";
            string _desc = "";
            string _eventTrigger = "";

            try
            {
                if (argv is not null)
                {

                    _commandid = argv is null ? "" : argv[0];
                    _params = argv is null ? "" : argv[1];
                    _desc = argv is null ? "" : argv[2];
                    _eventTrigger = argv is null ? "" : argv[3];
                }

                string[] tmpAryay = new string[11];
                if (ListAlarmCode.Count <= 0)
                {
                    string[] aryAlarm = {
                        "10100,System,MCS,Error,Sent Command Failed,0,,{0},{1},{2},{3}",
                        "10101,System,MCS,Alarm,MCS Connection Failed,0,,{0},{1},{2},{3}",
                        "20100,System,Database Access,Alarm,Database Access Error,100,AlarmSet,{0},{1},{2},{3}",
                        "20101,System,Database Access,Alarm,Database Access Success,101,AlarmReset,{0},{1},{2},{3}",
                        "30000,System,RTD,Issue,Dispatch overtime,1,Auto Clean Commands,{0},{1},{2},{3}",
                        "30001,System,RTD,Issue,Dispatch overtime,2,Auto Hold Lot,{0},{1},{2},{3}",
                        "30100,System,RTD,Alarm,MCS serious issue,1,Auto turn off MCS,{0},{1},{2},{3}",
                        "90000,System,RTD,INFO,TESE ALARM,0,,{0},{1},{2},{3}"
                    };

                    int _iAlarmCode = 0;
                    foreach (string alarm in aryAlarm)
                    {
                        tmpAryay = alarm.Split(',');
                        _iAlarmCode = int.Parse(tmpAryay[0]);
                        lstAlarm.Add(_iAlarmCode, alarm);
                    }

                    ListAlarmCode = lstAlarm;
                }

                alarmMsg = ListAlarmCode[_alarmCode];
                tmpAryay = alarmMsg.Split(',');

                if(!tmpAryay[1].Equals(""))
                    tmpSQL = _BaseDataService.InsertRTDAlarm(tmpAryay);

                if (_commandid.Equals(""))
                    tmpSQL = string.Format(tmpSQL, _commandid, _params, _desc, _eventTrigger);
                else
                    tmpSQL = string.Format(tmpSQL, _commandid, _params, _desc, _eventTrigger);

                _dbTool.SQLExec(tmpSQL, out error, true);

                if (error.Equals(""))
                    bResult = true;
            }
            catch (Exception ex)
            {
                //Do Nothing
            }

            return bResult;
        }
        public ResultMsg CheckCurrentLotStatebyWebService(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, string _lotId)
        {
            ResultMsg retMsg = new ResultMsg();
            string tmpMsg = "";
            string errMsg = "";
            string funcCode = "CheckCurrentLotStatebyWebService";
            List<string> TesterMachineList = new List<string>();

            string url = _configuration["WebService:url"];
            string username = _configuration["WebService:username"];
            string password = _configuration["WebService:password"];
            string webServiceMode = "soap11";

            DataTable dt = null;
            string tmpSql = "";
            JCETWebServicesClient jcetWebServiceClient = new JCETWebServicesClient();
            JCETWebServicesClient.ResultMsg resultMsg = new JCETWebServicesClient.ResultMsg();
            try
            {
                jcetWebServiceClient = new JCETWebServicesClient();
                jcetWebServiceClient._url = url;
                resultMsg = new JCETWebServicesClient.ResultMsg();
                resultMsg = jcetWebServiceClient.CurrentLotState(webServiceMode, username, password, _lotId);
                string result3 = resultMsg.retMessage;

#if DEBUG
                //_logger.Info(string.Format("Info:{0}", tmpMsg));
#else
#endif

                if (resultMsg.status)
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(result3);
                    XmlNode xn = xmlDoc.SelectSingleNode("Beans");

                    XmlNodeList xnlA = xn.ChildNodes;
                    String member_valodation = "";
                    String member_validation_message = "";
                    string member_currentState = "";
                    foreach (XmlNode xnA in xnlA)
                    {
                        Console.WriteLine(xnA.Name);
                        if ((xnA.Name) == "Status")
                        {
                            XmlElement xeB = (XmlElement)xnA;
                            if ((xeB.GetAttribute("Value")) == "SUCCESS")
                            {
                                member_valodation = "OK";
                                continue;
                            }
                            else
                            {
                                member_valodation = "NG";
                            }
                        }
                        if (member_valodation.Equals("OK"))
                        {
                            if ((xnA.Name) == "Msg")
                            {
                                XmlElement xeB = (XmlElement)xnA;
                                member_currentState = xeB.GetAttribute("Value").Equals("") ? "" : xeB.GetAttribute("Value");
                            }
                            break;
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
                        string lotState = "";
                        if (member_currentState.Equals("D"))
                        {
                            lotState = "WAIT";
                            //Check Lot Info, if state not in WAIT, update to WAIT
                            tmpSql = _BaseDataService.ConfirmLotinfoState(_lotId, "HOLD");
                            dt = _dbTool.GetDataTable(tmpSql);
                            if (dt.Rows.Count > 0)
                                _dbTool.SQLExec(_BaseDataService.UpdateLotinfoState(_lotId, lotState), out errMsg, true);
                        }
                        else
                        {
                            lotState = "HOLD";
                            //Hold Lot (Lot State)
                            tmpSql = _BaseDataService.ConfirmLotinfoState(_lotId, "WAIT");
                            dt = _dbTool.GetDataTable(tmpSql);
                            if (dt.Rows.Count > 0)
                                _dbTool.SQLExec(_BaseDataService.UpdateLotinfoState(_lotId, lotState), out errMsg, true);
                        }

                        if (errMsg.Equals(""))
                        {
                            retMsg.status = true;
                            retMsg.retMessage = String.Format("The Lot state [{0}] has been change to [{1}].", _lotId, lotState);
                        }
                        else
                        {
                            retMsg.status = false;
                            retMsg.retMessage = string.Format("DB update issue: {0}", errMsg);
                        }
                    }
                    else
                    {
                        tmpMsg = string.Format("The Lot Id [{0}] is not invalid.", _lotId);
                        retMsg.status = false;
                        retMsg.retMessage = string.Format("lotid issue: {0}", tmpMsg);
                    }
                }
                else
                {
                    retMsg.status = false;
                    retMsg.retMessage = string.Format("WebService issue: {0}", resultMsg.retMessage);
                }
            }
            catch (Exception ex)
            {
                tmpMsg = string.Format("Unknow issue: [{0}][Exception] {1}", funcCode, ex.Message);
                _logger.Debug(ex.Message);
            }
            finally
            {
                jcetWebServiceClient = null;
                resultMsg = null;
            }

            return retMsg;
        }
        public class ResultMsg
        {
            public bool status { get; set; }
            public string retMessage { get; set; }
            public string remark { get; set; }
        }
        public bool AutoGeneratePort(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, string _equipId, string _portModel, out string _errMessage)
        {
            bool bResult = false;
            _errMessage = "";
            string tmpMsg = "";
            string tmp = "";
            DataTable dt = new DataTable();
            DataRow dr;
            DataTable dtDefaultSet = new DataTable();
            DataRow[] drDefaultSet;
            DataTable dtEeqPortSet = new DataTable();
            string _Port_Model = "";
            int _Port_Number = 0;
            string _WorkGroup = "";
            string _Port_Type = "";
            string tmpPortId = "{0}_LP{1}";
            string EqpPortId = "";
            string portType = "";
            string carrierType = "";
            string EqpTypeID = "";
            string tmpMessage = "";
            DataSet dtSet = new DataSet();
            DataTable dtPortType = new DataTable();
            DataTable dtCarrierType = new DataTable();

            try
            {
                dt = _dbTool.GetDataTable(_BaseDataService.SelectTableEquipmentPortsInfoByEquipId(_equipId));
                if (dt.Rows.Count > 0)
                {
                    _Port_Model = dt.Rows[0]["Port_Model"] is null ? "" : dt.Rows[0]["Port_Model"].ToString();
                    _Port_Number = dt.Rows[0]["Port_Number"] is null ? 0 : dt.Rows[0]["Port_Number"].ToString().Equals("") ? 0 : int.Parse(dt.Rows[0]["Port_Number"].ToString());
                    _WorkGroup = dt.Rows[0]["WorkGroup"] is null ? "" : dt.Rows[0]["WorkGroup"].ToString();
                    EqpTypeID = dt.Rows[0]["Equip_TypeId"] is null ? "" : dt.Rows[0]["Equip_TypeId"].ToString();

                    try
                    {
                        if (!_portModel.Equals(_Port_Model))
                        {
                            tmpMsg = "[AutoGeneratePort] Equipment [{0}] Port Model been Change. From [{1}] To [{2}]";
                            tmpMessage = String.Format(tmpMsg, _equipId, _Port_Model, _portModel);
                            _logger.Debug(tmpMessage);

                            _dbTool.SQLExec(_BaseDataService.DeleteEqpPortSet(_equipId, _Port_Model), out tmpMsg, true);
                        }

                        if (_Port_Number <= 0)
                        {
                            bResult = false;
                            tmpMsg = "[SetEquipmentPortModel] The Equipment [{0}] Port Number not set. please check : {0}";
                            _errMessage = String.Format(tmpMsg, _equipId);
                            _logger.Debug(_errMessage);
                            return bResult;
                        }

                        if (_WorkGroup.Equals(""))
                        {
                            bResult = false;
                            tmpMsg = "[SetEquipmentPortModel] The Equipment [{0}] Workgroup not set. please check.";
                            _errMessage = String.Format(tmpMsg, _equipId);
                            _logger.Debug(_errMessage);
                            return bResult;
                        }

                        //dtDefaultSet = _dbTool.GetDataTable(_BaseDataService.SelectRTDDefaultSet("PortTypeMapping"));
                        //drDefaultSet = dtDefaultSet.Select(string.Format("paramtype = \'{0}\'", EqpTypeID));
                        dtDefaultSet = _dbTool.GetDataTable(_BaseDataService.QueryPortModelMapping(EqpTypeID));

                        if (dtDefaultSet.Rows.Count > 0)
                        {
                            portType = dtDefaultSet.Rows[0]["PortTypeMapping"].ToString().Equals("") ? "" : dtDefaultSet.Rows[0]["PortTypeMapping"].ToString();

                            if (portType.Equals(""))
                            {
                                tmp = "This Equipment Port Type [{0}] have not set Port Type Mapping in Default set.";
                                tmpMsg = tmpMsg.Equals("") ? string.Format(tmp, EqpTypeID) : string.Format(tmpMsg + " & " + tmp, EqpTypeID);
                            }

                            carrierType = dtDefaultSet.Rows[0]["CarrierTypeMapping"].ToString().Equals("") ? "" : dtDefaultSet.Rows[0]["CarrierTypeMapping"].ToString();

                            if (carrierType.Equals(""))
                            {
                                tmp = "This Equipment Port Type [{0}] have not set Carrier Type Mapping in Default set.";
                                tmpMsg = tmpMsg.Equals("") ? string.Format(tmp, EqpTypeID) : string.Format(tmpMsg + " & " + tmp, EqpTypeID);
                            }

                            if (!tmpMsg.Equals(""))
                            {
                                bResult = false;
                                _errMessage = String.Format("[AutoGeneratePort] {0}", tmpMsg);
                                _logger.Debug(_errMessage);
                                return bResult;
                            }
                        }
                        else
                        {
                            bResult = false;
                            tmp = "This Equipment Port Type [{0}] have not set for Port Type and Carrier Type Mapping in default set. Please check.";
                            tmpMsg = tmpMsg.Equals("") ? string.Format(tmp, EqpTypeID) : string.Format(tmpMsg + " & " + tmp, EqpTypeID);
                            _errMessage = String.Format("[AutoGeneratePort] {0}", tmpMsg);
                            _logger.Debug(_errMessage);
                            return bResult;
                        }

                        switch (_portModel)
                        {
                            case "1IOT1":
                            case "1I1OT1":
                            case "1I1OT2":
                            case "2I2OT1":
                                string s1IOT1 = "{'ID':1,'TYPE':'IN'}";
                                string sCarrierType = "{'CarrierType':[{'ID':1,'TYPE':'MetalCassette'}, {'ID':2,'TYPE':'MetalCassette'}]}";
                                dtSet = JsonConvert.DeserializeObject<DataSet>(portType);
                                dtPortType = new DataTable();
                                dtPortType = dtSet.Tables[_portModel];
                                dtSet = JsonConvert.DeserializeObject<DataSet>(carrierType);
                                dtCarrierType = dtSet.Tables[_portModel];
                                break;
                            default:
                                tmpMsg = "[SetEquipmentPortModel] Alarm : Equipment Id [{0}]. PortModel is invalid. please check.";
                                _errMessage = String.Format(tmpMsg, _equipId);
                                break;
                        }

                        string[] tmpArray = new string[7];
                        //(equipid, port_model, port_seq, port_type, port_id, carrier_type, near_stocker, create_dt, modify_dt, lastmodify_dt, port_state, workgroup)
                        tmpArray[0] = _equipId;
                        tmpArray[1] = _portModel;
                        for (int i = 1; i <= _Port_Number; i++)
                        {
                            tmpArray[2] = i.ToString();
                            //port Type
                            DataRow[] drAA;
                            drAA = dtPortType.Select(string.Format("ID = {0}", i.ToString()));
                            tmpArray[3] = drAA[0]["TYPE"].ToString();
                            //Eqp port Id
                            EqpPortId = string.Format(tmpPortId, _equipId, i);
                            tmpArray[4] = EqpPortId;

                            drAA = dtCarrierType.Select(string.Format("ID = {0}", i.ToString()));
                            tmpArray[5] = drAA[0]["TYPE"].ToString();

                            //Workgroup
                            tmpArray[6] = _WorkGroup;

                            dtEeqPortSet = _dbTool.GetDataTable(_BaseDataService.QueryEqpPortSet(_equipId, i.ToString()));
                            if (dtEeqPortSet.Rows.Count <= 0)
                            {
                                _dbTool.SQLExec(_BaseDataService.InsertTableEqpPortSet(tmpArray), out tmpMsg, true);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        bResult = false;
                        tmpMsg = "[SetEquipmentPortModel] Exception occurred : Equipment Id [{0}].  {1}";
                        _errMessage = String.Format(tmpMsg, _equipId, ex.Message);
                    }
                }
                else
                {
                    bResult = false;
                    tmpMsg = "The Equipment Id [{0}] is not exists.";
                    _errMessage = String.Format(tmpMsg, _equipId);
                }
            }
            catch (Exception ex)
            {
                bResult = false;
                tmpMsg = "[SetEquipmentPortModel] Unknow Error : Equipment Id [{0}]. {1}";
                _errMessage = String.Format(tmpMsg, _equipId, ex.Message);
            }
            finally
            {
                if (dt != null)
                    dt.Dispose();
                if (dtCarrierType != null)
                    dtCarrierType.Dispose();
                if (dtDefaultSet != null)
                    dtDefaultSet.Dispose();
                if (dtEeqPortSet != null)
                    dtEeqPortSet.Dispose();
                if (dtPortType != null)
                    dtPortType.Dispose();
                if (dtSet != null)
                    dtSet.Dispose();
            }
            dt = null;
            dtCarrierType = null;
            dtDefaultSet = null;
            dtEeqPortSet = null;
            dtPortType = null;
            dtSet = null;
            dr = null;
            drDefaultSet = null;

            return bResult;
        }
        public class TypeContent
        {
            /// <summary>
            /// 
            /// </summary>
            public int ID { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public string TYPE { get; set; }
        }
        public class TypeMapping
        {
            public TypeContent TypeContent { get; set; }
        }
        public bool AutoHoldForDispatchIssue(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, out string tmpMessage)
        {
            bool bResult = false;
            string sql = "";
            string tmpSql = "";
            string tmpMsg = "";
            string _errMessage = "";
            tmpMessage = "";
            DataTable dt = null;
            DataTable dt2 = null;

            string tableOrder = "";
            string _keyOfEnv = "";
            string _keyRTDEnv = "";

            try
            {
                _keyRTDEnv = _configuration["RTDEnvironment:type"];
                _keyOfEnv = string.Format("RTDEnvironment:commandsTable:{0}", _keyRTDEnv);
                //"RTDEnvironment:commandsTable:PROD"
                tableOrder = _configuration[_keyOfEnv] is null ? "workinprocess_sch" : _configuration[_keyOfEnv];

                sql = _BaseDataService.QueryProcLotInfo();
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    string lotid;
                    string carrierId;
                    string lastModifyDT;
                    string commandId;
                    string GId;
                    bool bAutoHold = false;
                    foreach (DataRow dr in dt.Rows)
                    {
                        bAutoHold = false;
                        lotid = dr["LOTID"].ToString().Equals("") ? "" : dr["LOTID"].ToString();
                        carrierId = "";

                        if (!lotid.Equals(""))
                        {
                            sql = _BaseDataService.SelectTableWorkInProcessSchByLotId(lotid, tableOrder);
                            dt2 = _dbTool.GetDataTable(sql);

                            if (dt2.Rows.Count > 0)
                            {
                                foreach (DataRow dr2 in dt2.Rows)
                                {
                                    carrierId = dr2["carrierid"].ToString().Equals("") ? "" : dr2["carrierid"].ToString();
                                    lastModifyDT = dr2["lastModify_dt"].ToString().Equals("") ? "" : dr2["lastModify_dt"].ToString();
                                    commandId = dr2["cmd_id"].ToString().Equals("") ? "" : dr2["cmd_id"].ToString();
                                    GId = dr2["uuid"].ToString().Equals("") ? "" : dr2["uuid"].ToString();

                                    if (TimerTool("minutes", lastModifyDT) >= 25)
                                    {
                                        sql = _BaseDataService.UpdateTableWorkInProcessSchHisByUId(GId);
                                        _dbTool.SQLExec(sql, out tmpMsg, true);
                                        sql = _BaseDataService.DeleteWorkInProcessSchByGId(GId, tableOrder);
                                        _dbTool.SQLExec(sql, out tmpMsg, true);

                                        if (tmpMsg.Equals(""))
                                        {
                                            string[] tmpString = new string[] { GId, "", "", "" };
                                            CallRTDAlarm(_dbTool, 30000, tmpString);

                                            bAutoHold = true;
                                            break;
                                        }
                                        else
                                        {
                                            bResult = false;
                                            tmpMsg = "[AutoHoldForDispatchIssue] WorkinProcessSch Clean Failed : {0}";
                                            _errMessage = String.Format(tmpMsg, tmpMsg);
                                            _logger.Debug(_errMessage);
                                        }
                                    }
                                }
                            }
                        }

                        if (bAutoHold)
                        {
                            //_dbTool.SQLExec(_BaseDataService.UpdateTableLotInfoState(lotid, "HOLD"), out tmpMsg, true);
                            _dbTool.SQLExec(_BaseDataService.UpdateTableCarrierTransferByCarrier(carrierId, "SYSHOLD"), out tmpMsg, true);

                            if (tmpMsg.Equals(""))
                            {
                                string[] tmpString = new string[] { lotid, "", "", "" };
                                CallRTDAlarm(_dbTool, 30001, tmpString);

                                bResult = true;
                                break;
                            }
                            else
                            {
                                bResult = false;
                                tmpMsg = "[AutoHoldForDispatchIssue] Auto Hold Lot Failed : {0}";
                                _errMessage = String.Format(tmpMsg, tmpMsg);
                                _logger.Debug(_errMessage);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                bResult = false;
                tmpMsg = "[AutoHoldForDispatchIssue] Unknow Error : {0}";
                _errMessage = String.Format(tmpMsg, ex.Message);
                _logger.Debug(_errMessage);
            }
            finally
            {
                if (dt != null)
                    dt.Dispose();
                if (dt2 != null)
                    dt2.Dispose();
            }
            dt = null;
            dt2 = null;

            return bResult;
        }
        public bool TriggerCarrierInfoUpdate(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, string _lotid)
        {
            bool bResult = false;
            string tmpMsg = "";
            string sql = "";
            DataTable dt = null;
            DataTable dt2 = null;
            DataTable dtTemp = null;
            string _tmpLot = "";
            string v_carrier_id = "";
            string v_LOT_ID = "";
            string _quantity = "";
            string _totalQty = "";
            string _infoupdate = "";

            try
            {
                //.R and .S 都使用原本 lotID 8111.1.R / 8111.1.S >>皆用 8111.1信息回覆
                //if (_lotid.IndexOf("R") > 0 || _lotid.IndexOf("S") > 0)
                //{
                //    if (_lotid.IndexOf("R") > 0)
                //        _tmpLot = _lotid.Replace("R", "");
                //    else
                //        _tmpLot = _lotid.Replace("S", "");
                //}
                //else
                //    _tmpLot = _lotid;

                v_LOT_ID = _lotid;
                sql = string.Format(_BaseDataService.CheckLocationByLotid(_lotid));
                dtTemp = _dbTool.GetDataTable(sql);
                v_carrier_id = dtTemp.Rows[0]["carrier_id"].ToString().Equals("") ? "" : dtTemp.Rows[0]["carrier_id"].ToString();

                sql = _BaseDataService.QueryLotInfoByCarrierID(v_carrier_id);
                dt2 = _dbTool.GetDataTable(sql);

                if (dt2.Rows.Count > 0)
                {
                    _quantity = dt2.Rows[0]["quantity"].ToString().Equals("") ? "0" : dt2.Rows[0]["quantity"].ToString().Trim();
                    _totalQty = dt2.Rows[0]["total_qty"].ToString().Equals("") ? "0" : dt2.Rows[0]["total_qty"].ToString().Trim(); ;
                }
                else
                {
                    _quantity = "0";
                    _totalQty = "0";
                }

                if (_lotid.Contains("R") || _lotid.Contains("S"))
                    _tmpLot = _lotid.Replace("R", "").Replace("S", "");
                else
                    _tmpLot = _lotid;

                sql = string.Format(_BaseDataService.CheckLocationByLotid(_tmpLot.Trim()));
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    tmpMsg = string.Format("[TriggerCarrierInfoUpdate: CheckLocationByLotid. {0} / {1}]", _lotid, _configuration["eRackDisplayInfo:contained"]);
                    _logger.Debug(tmpMsg);

                    _infoupdate = dt.Rows[0]["info_update_dt"].ToString().Equals("NULL") ? "" : dt.Rows[0]["info_update_dt"].ToString();

                    List<string> args = new();
                    string v_STAGE = "";
                    string v_CUSTOMERNAME = "";
                    string v_PARTID = "";
                    string v_LOTTYPE = "";
                    string v_AUTOMOTIVE = "";
                    string v_STATE = "";
                    string v_HOLDCODE = "";
                    string v_TURNRATIO = "0";
                    string v_EOTD = "";
                    string v_HOLDREAS = "";
                    string v_POTD = "";
                    string v_WAFERLOT = "";
                    string v_Quantity = _quantity;
                    string v_TotalQty = _totalQty;
                    string v_Force = "";
                    try
                    {
                        //v_carrier_id = dt.Rows[0]["carrier_id"].ToString().Equals("") ? "" : dt.Rows[0]["carrier_id"].ToString();
                        v_LOT_ID = _lotid;//_lotid;
                        v_CUSTOMERNAME = dt.Rows[0]["CUSTOMERNAME"].ToString().Equals("") ? "" : dt.Rows[0]["CUSTOMERNAME"].ToString();
                        v_PARTID = dt.Rows[0]["PARTID"].ToString().Equals("") ? "" : dt.Rows[0]["PARTID"].ToString();
                        v_LOTTYPE = dt.Rows[0]["LOTTYPE"].ToString().Equals("") ? "" : dt.Rows[0]["LOTTYPE"].ToString();
                        v_STAGE = dt.Rows[0]["stage"].ToString().Equals("") ? "" : dt.Rows[0]["stage"].ToString();

                        sql = _BaseDataService.QueryErackInfoByLotID(_configuration["eRackDisplayInfo:contained"], _tmpLot.Trim());
                        dtTemp = _dbTool.GetDataTable(sql);

                        if (dtTemp.Rows.Count > 0)
                        {
                            v_STAGE = v_STAGE.Equals("") ? dtTemp.Rows[0]["STAGE"].ToString() : "";
                            v_AUTOMOTIVE = dtTemp.Rows[0]["AUTOMOTIVE"].ToString().Equals("") ? "" : dtTemp.Rows[0]["AUTOMOTIVE"].ToString();
                            v_STATE = dtTemp.Rows[0]["STATE"].ToString().Equals("") ? "" : dtTemp.Rows[0]["STATE"].ToString();
                            v_HOLDCODE = dtTemp.Rows[0]["HOLDCODE"].ToString().Equals("") ? "" : dtTemp.Rows[0]["HOLDCODE"].ToString();
                            v_TURNRATIO = dtTemp.Rows[0]["TURNRATIO"].ToString().Equals("") ? "0" : dtTemp.Rows[0]["TURNRATIO"].ToString();
                            v_EOTD = dtTemp.Rows[0]["EOTD"].ToString().Equals("") ? "" : dtTemp.Rows[0]["EOTD"].ToString();
                            v_POTD = dtTemp.Rows[0]["POTD"].ToString().Equals("") ? "" : dtTemp.Rows[0]["POTD"].ToString();
                            v_HOLDREAS = dtTemp.Rows[0]["HoldReas"].ToString().Equals("") ? "" : dtTemp.Rows[0]["HoldReas"].ToString();
                            v_WAFERLOT = dtTemp.Rows[0]["waferlotid"].ToString().Equals("") ? "" : dtTemp.Rows[0]["waferlotid"].ToString();
                        }

                        if (_infoupdate.Equals(""))
                        {
                            v_Force = "false";
                        }
                        else
                        {
                            if (TimerTool("minutes", _infoupdate) >= 3)
                            { v_Force = "true"; }
                            else
                            { v_Force = "false"; }
                        }
                    }
                    catch (Exception ex)
                    {
                        tmpMsg = string.Format("[TriggerCarrierInfoUpdate: Column Issue. {0}]", ex.Message);
                        _logger.Debug(tmpMsg);
                    }

                    if (CheckMCSStatus(_dbTool, _logger))
                    {
                        args.Add(v_LOT_ID);
                        args.Add(v_STAGE);
                        args.Add("");//("machine");
                        args.Add("");//("desc");
                        args.Add(v_carrier_id);
                        args.Add(v_CUSTOMERNAME);
                        args.Add(v_PARTID);//("PartID");
                        args.Add(v_LOTTYPE);//("LotType");
                        args.Add(v_AUTOMOTIVE);//("Automotive");
                        args.Add(v_STATE);//("State");
                        args.Add(v_HOLDCODE);//("HoldCode");
                        args.Add(v_TURNRATIO);//("TURNRATIO");
                        args.Add(v_EOTD);//("EOTD");
                        args.Add(v_HOLDREAS);//("EOTD");
                        args.Add(v_POTD);//("EOTD");
                        args.Add(v_WAFERLOT);//("EOTD");
                        args.Add(v_Quantity);//("Quantity");
                        args.Add(v_TotalQty);//("TotalQty");
                        args.Add(v_Force);//("Force");
                        SentCommandtoMCSByModel(_dbTool, _configuration, _logger, "InfoUpdate", args);

                        if (!v_carrier_id.Equals(""))
                        {
                            if (v_Force.ToLower().Equals("true"))
                            {
                                sql = _BaseDataService.CarrierTransferDTUpdate(v_carrier_id, "InfoUpdate");
                                _dbTool.SQLExec(sql, out tmpMsg, true);
                            }
                        }

                    }
                    else
                    {
                        _logger.Info(string.Format("MCS Status incorrect, [{0}][{1}] not success. [{2}]", "TriggerCarrierInfoUpdate", "InfoUpdate", v_carrier_id));
                    }
                }

                bResult = true;
            }
            catch (Exception ex)
            {
                bResult = false;
                tmpMsg = string.Format("Send InfoUpdate Fail, Exception: {0}", ex.Message);
                _logger.Debug(tmpMsg);
            }
            finally
            {
                if (dt != null)
                    dt.Dispose();
                if (dtTemp != null)
                    dtTemp.Dispose(); 
            }
            dt = null;
            dtTemp = null;

            return bResult;
        }
        public bool DoInsertPromisStageEquipMatrix(DBTool _dbTool, ILogger _logger, string _stage, string _EqpType, string _EquipIds, string _userId, out string _errMsg)
        {
            bool bResult = false;
            string tmpMsg = "";
            string sql = "";
            DataTable dt = null;
            string[] lstEquip;
            string Equips = "";

            try
            {
                if (!_EquipIds.Equals(""))
                {
                    if (_EquipIds.IndexOf(',') > 0)
                    {
                        lstEquip = _EquipIds.Split(',');

                        foreach (string equipid in lstEquip)
                        {
                            if (Equips.Equals(""))
                            {
                                Equips = string.Format("'{0}'", equipid.Trim());
                            }
                            else
                            {
                                Equips = Equips + string.Format(", '{0}'", equipid.Trim());
                            }
                        }
                    }
                    else
                        Equips = string.Format("'{0}'", _EquipIds.Trim());
                }

                sql = string.Format(_BaseDataService.InsertPromisStageEquipMatrix(_stage, _EqpType, Equips, _userId));
                _dbTool.SQLExec(sql, out tmpMsg, true);
                if (tmpMsg.Equals(""))
                {
                    bResult = true;
                    _errMsg = tmpMsg;
                }
                else
                {
                    bResult = false;
                    _errMsg = tmpMsg;
                    _logger.Debug(tmpMsg);
                }

                _errMsg = tmpMsg;
                bResult = true;
            }
            catch (Exception ex)
            {
                bResult = false;
                tmpMsg = string.Format("Unknow Issue, Exception: {0}", ex.Message);
                _errMsg = tmpMsg;
                _logger.Debug(tmpMsg);
            }
            finally
            {
                if (dt != null)
                    dt.Dispose();              
            }
            dt = null;

            return bResult;
        }
        public bool SyncEQPStatus(DBTool _dbTool, ILogger _logger)
        {
            bool bResult = false;
            string tmpMsg = "";
            string tmpEquipid = "";
            string sql = "";
            DataTable dt = null;

#if DEBUG
            //Do Nothing
            bResult = true;
#else
            try
            {
                sql = string.Format(_BaseDataService.CheckRealTimeEQPState());
                dt = _dbTool.GetDataTable(sql);
                if (dt.Rows.Count > 0)
                {
                    //有不同時, 進行同步
                    foreach (DataRow dr in dt.Rows)
                    {
                        tmpEquipid = dr["equipid"].ToString().Trim();

                        try
                        {
                            _dbTool.SQLExec(_BaseDataService.UpdateCurrentEQPStateByEquipid(tmpEquipid), out tmpMsg, true);
                        }
                        catch (Exception ex)
                        { }
                    }
                }

                bResult = true;
            }
            catch (Exception ex)
            {
            }
            finally
            {
                if (dt != null)
                    dt.Dispose(); 
            }
             dt = null;  
#endif

            return bResult;
        }
        public bool RecoverDatabase(DBTool _dbTool, ILogger _logger, string _message)
        {
            string tmpMsg;
            string tmp2Msg;

            if (_message.IndexOf("ORA-03150") > 0 || _message.IndexOf("ORA-02063") > 0 || _message.IndexOf("ORA-12614") > 0
                || _message.IndexOf("Object reference not set to an instance of an object") > 0
                || _message.IndexOf("Database access problem") > 0)
            {
                //Jcet database connection exception occurs, the connection will automatically re-established 
                tmpMsg = "";
                tmp2Msg = "";

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
                        if (CallRTDAlarm(_dbTool, 20100, _argvs))
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
                    if (CallRTDAlarm(_dbTool, 20101, _argvs))
                    {
                        _logger.Debug(string.Format("DB re-connection sucess", tmp2Msg));
                    }
                }
            }
            return true;
        }
        public bool SyncExtenalCarrier(DBTool _dbTool, IConfiguration _configuration, ILogger _logger)
        {
            bool bResult = false;
            string tmpMsg = "";
            string tmpEquipid = "";
            string sql = "";
            DataTable dt = null;
            DataTable dtTemp = null;
            string dblinkLcas = "";
            bool enableSync = false;
            bool isTable = false;

            try
            {
                if (_configuration["SyncExtenalData:SyncCST:Model"].Equals("Table"))
                    isTable = true;

                if (_configuration["SyncExtenalData:SyncCST:Enable"].Equals("True"))
                    enableSync = true;

                if (enableSync)
                {
                    if (isTable)
                    {
#if DEBUG
                        dblinkLcas = _configuration["SyncExtenalData:SyncCST:Table:Debug"];
#else
                        dblinkLcas = _configuration["SyncExtenalData:SyncCST:Table:Prod"];
#endif
                        sql = string.Format(_BaseDataService.QueryExtenalCarrierInfo(dblinkLcas));
                    }
                    else
                    {
                        sql = "get sql from sql file";
                    }
                }
                else
                { return true; }

                dt = _dbTool.GetDataTable(sql);
                if (dt.Rows.Count > 0)
                {
                    CarrierLotAssociate carrierLotAssociate = new CarrierLotAssociate();

                    string dateTime = "";
                    //有不同時, 進行同步
                    foreach (DataRow dr in dt.Rows)
                    {

                        try
                        {
                            carrierLotAssociate = new CarrierLotAssociate();

                            dateTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");

                            carrierLotAssociate.CarrierID = dr["CSTID"].ToString().Trim();
                            carrierLotAssociate.TagType = "LF";
                            carrierLotAssociate.CarrierType = dr["CSTTYPE"].ToString().Trim();
                            carrierLotAssociate.AssociateState = "Associated With Lot";
                            carrierLotAssociate.ChangeStateTime = "";
                            carrierLotAssociate.LotID = dr["LOTID"].ToString().Trim();
                            carrierLotAssociate.Quantity = dr["LOTQTY"].ToString().Trim();
                            carrierLotAssociate.ChangeStation = "SyncEwlbCarrier";
                            carrierLotAssociate.ChangeStationType = "A";
                            carrierLotAssociate.UpdateTime = dateTime;
                            carrierLotAssociate.UpdateBy = "RTD";
                            carrierLotAssociate.CreateBy = dr["USERID"].ToString().Trim();
                            carrierLotAssociate.NewBind = "1";

                            int doLogic = 0;

                            dtTemp = _dbTool.GetDataTable(_BaseDataService.QueryCarrierInfoByCarrierId(carrierLotAssociate.CarrierID));
                            if (dtTemp.Rows.Count <= 0)
                            {
                                doLogic = 1;
                            }
                            else
                            {
                                if (!carrierLotAssociate.LotID.Equals(dtTemp.Rows[0]["lot_id"].ToString()))
                                {
                                    doLogic = 2;
                                }
                                else if (!carrierLotAssociate.Quantity.Equals(dtTemp.Rows[0]["quantity"].ToString()))
                                {
                                    doLogic = 2;
                                }
                                else
                                {
                                    doLogic = 0;
                                }

                            }

                            if (doLogic.Equals(1))
                            {
                                tmpMsg = "";
                                _dbTool.SQLExec(_BaseDataService.InsertCarrierLotAsso(carrierLotAssociate), out tmpMsg, true);
                                if (!tmpMsg.Equals(""))
                                    _logger.Debug(string.Format("[InsertCarrierLotAsso Failed.][Exception: {0}][Carrier ID: {1}]", tmpMsg, carrierLotAssociate.CarrierID));
                                tmpMsg = "";
                                _dbTool.SQLExec(_BaseDataService.InsertCarrierTransfer(carrierLotAssociate.CarrierID, "Foup", carrierLotAssociate.Quantity), out tmpMsg, true);
                                if (!tmpMsg.Equals(""))
                                    _logger.Debug(string.Format("[InsertCarrierTransfer Failed.][Exception: {0}][Carrier ID: {1}]", tmpMsg, carrierLotAssociate.CarrierID));
                            }
                            else if (doLogic.Equals(2))
                            {
                                tmpMsg = "";
                                _dbTool.SQLExec(_BaseDataService.UpdateLastCarrierLotAsso(carrierLotAssociate), out tmpMsg, true);
                                if (!tmpMsg.Equals(""))
                                    _logger.Debug(string.Format("[UpdateLastCarrierLotAsso Failed.][Exception: {0}][Carrier ID: {1}]", tmpMsg, carrierLotAssociate.CarrierID));
                                tmpMsg = "";
                                _dbTool.SQLExec(_BaseDataService.UpdateCarrierLotAsso(carrierLotAssociate), out tmpMsg, true);
                                if (!tmpMsg.Equals(""))
                                    _logger.Debug(string.Format("[UpdateCarrierLotAsso Failed.][Exception: {0}][Carrier ID: {1}]", tmpMsg, carrierLotAssociate.CarrierID));
                                tmpMsg = "";
                                _dbTool.SQLExec(_BaseDataService.UpdateCarrierTransfer(carrierLotAssociate.CarrierID, "Foup", carrierLotAssociate.Quantity), out tmpMsg, true);
                                if (!tmpMsg.Equals(""))
                                    _logger.Debug(string.Format("[UpdateCarrierTransfer Failed.][Exception: {0}][Carrier ID: {1}]", tmpMsg, carrierLotAssociate.CarrierID));
                            }
                        }
                        catch (Exception ex)
                        { }
                    }
                }

                bResult = true;
            }
            catch (Exception ex)
            {
                _logger.Debug(string.Format("Sync Extenal Carrier Data Failed. [Exception: {0}]", ex.Message));
            }
            finally
            {
                if (dt != null)
                    dt.Dispose();
                if (dtTemp != null)
                    dtTemp.Dispose();
            }
            dt = null;
            dtTemp = null;

            return bResult;
        }
        public string GetExtenalTables(IConfiguration _configuration, string _method, string _func)
        {
            string strTable = "";
            bool enableSync = false;
            bool isTable = false;
            string cfgString = "";
            string tmpKey = "";

            try
            {
                cfgString = string.Format("{0}:{1}:Model", _method, _func);
                tmpKey = _configuration[cfgString] is null ? "None" : _configuration[cfgString].ToString();

                if (tmpKey.Equals("Table"))
                    isTable = true;

                cfgString = string.Format("{0}:{1}:Enable", _method, _func);
                tmpKey = _configuration[cfgString] is null ? "None" : _configuration[cfgString].ToString();
                if (tmpKey.Equals("True"))
                    enableSync = true;

                if (enableSync)
                {
                    if (isTable)
                    {
#if DEBUG
                        cfgString = string.Format("{0}:{1}:Table:Debug", _method, _func);
                        tmpKey = _configuration[cfgString] is null ? "None" : _configuration[cfgString].ToString();
#else
                        cfgString = string.Format("{0}:{1}:Table:Prod", _method, _func);
                        tmpKey = _configuration[cfgString] is null ? "None" : _configuration[cfgString].ToString();
#endif
                        strTable = tmpKey;
                    }
                }
                else
                {

#if DEBUG
                    strTable = "ADS_INFO";
#else
                    strTable = "semi_int.rtd_cis_ads_vw@SEMI_INT";
#endif
                }
            }
            catch (Exception ex)
            { }

            return strTable;
        }

        //Equipment State===========================
        //0. DOWN (disable)
        //1. PM (Error)
        //2. IDLE 
        //3. UP (Run)
        //12. IDLE With Warinning
        //13. UP With Warning
        //====================================
        public string GetEquipStat(int _equipState)
        {
            string eqState = "DOWN";

            try
            {
                switch (_equipState)
                {
                    case 1:
                        eqState = "PM";
                        break;
                    case 2:
                        eqState = "IDLE";
                        break;
                    case 3:
                        eqState = "UP";
                        break;
                    case 12:
                        eqState = "IDLE With Warning";
                        break;
                    case 13:
                        eqState = "UP With Warning";
                        break;
                    case 0:
                    default:
                        eqState = "DOWN";
                        break;
                }
            }
            catch (Exception ex)
            { }

            return eqState;
        }
        public Global loadGlobalParams(DBTool _dbTool)
        {
            Global _global = new Global();
            string sql = "";
            DataTable dtTemp = null;

            try
            {
                sql = _BaseDataService.SelectRTDDefaultSetByType("GlobalParams");
                dtTemp = _dbTool.GetDataTable(sql);
                //Default
                _global.CheckQueryAvailableTestercuteMode.Time = 60;
                _global.CheckQueryAvailableTestercuteMode.TimeUnit = "seconds";
                _global.ChkLotInfo.Time = 60;
                _global.ChkLotInfo.TimeUnit = "seconds";

                //read from setting
                if (dtTemp.Rows.Count > 0)
                {
                    foreach(DataRow dr in dtTemp.Rows)
                    {
                        if(dr["Parameter"].Equals("CheckLotInfo.Time"))
                        {
                            _global.ChkLotInfo.Time = int.Parse(dr["ParamValue"].ToString());
                        }
                        if (dr["Parameter"].Equals("CheckLotInfo.TimeUnit"))
                        {
                            _global.ChkLotInfo.TimeUnit = dr["ParamValue"].ToString();
                        }
                        if (dr["Parameter"].Equals("CheckQueryAvailableTestercute.Time"))
                        {
                            _global.CheckQueryAvailableTestercuteMode.Time = int.Parse(dr["ParamValue"].ToString());
                        }
                        if (dr["Parameter"].Equals("CheckQueryAvailableTestercute.TimeUnit"))
                        {
                            _global.CheckQueryAvailableTestercuteMode.TimeUnit = dr["ParamValue"].ToString();
                        }
                    }
                    
                }
            }
            catch (Exception ex)
            {
                
            }
            finally
            {
                if (dtTemp != null)
                    dtTemp.Dispose(); 
            }
            dtTemp = null;

            return _global;
        }
        public bool PreDispatchToErack(DBTool _dbTool, IConfiguration _configuration, ConcurrentQueue<EventQueue> _eventQueue, ILogger _logger)
        {
            bool bResult = false;
            string tmpMsg = "";
            string sql = "";
            DataTable dtTemp = null;
            DataTable dtTemp2 = null;
            DataTable dtTemp3 = null;
            EventQueue _eventQ = new EventQueue();
            string funcName = "MoveCarrier";
            TransferList transferList = new TransferList();
            string carrierId = "";
            string _workgroup = "";
            string _customer = "";
            string _partid = "";
            string _values1 = "";
            string tableName = "";
            string tableOrder = "";
            string _keyOfEnv = "";
            string _keyRTDEnv = "";
            string _locationType = "";
            string _inErack = "";
            bool _pretransfer = false;
            string _destStage = "";
            string _eqpWorkgroup = "";

            string _sideWarehouse = "";
            bool _swSideWh = false;
            bool _onSideWH = false;

            try
            {
                _keyRTDEnv = _configuration["RTDEnvironment:type"];
                _keyOfEnv = string.Format("RTDEnvironment:commandsTable:{0}", _keyRTDEnv);
                //"RTDEnvironment:commandsTable:PROD"
                tableOrder = _configuration[_keyOfEnv] is null ? "workinprocess_sch" : _configuration[_keyOfEnv];
                tableName = _configuration["PreDispatchToErack:lotState:tableName"] is null ? "lot_Info" : _configuration["PreDispatchToErack:lotState:tableName"];

                if (_keyRTDEnv.ToUpper().Equals("PROD"))
                    sql = _BaseDataService.QueryPreTransferList(tableName);
                else if (_keyRTDEnv.ToUpper().Equals("UAT"))
                    sql = _BaseDataService.QueryPreTransferListForUat(tableName);

                dtTemp = _dbTool.GetDataTable(sql);

                if (dtTemp is not null && dtTemp.Rows.Count > 0)
                {
                    foreach (DataRow dr in dtTemp.Rows)
                    {
                        _workgroup = "";
                        _customer = "";
                        _partid = "";
                        _values1 = "";
                        //workgroup, customername, partid, in_eRack, locate, carrier_ID, lot_ID, in_eRack
                        _workgroup = dr["workgroup"].ToString().Equals("") ? "*" : dr["workgroup"].ToString();
                        _customer = dr["customername"].ToString().Equals("") ? "*" : dr["customername"].ToString();
                        _partid = dr["partid"].ToString().Equals("") ? "*" : dr["partid"].ToString();
                        _pretransfer = dr["pretransfer"].ToString().Equals("0") ? false : true;

                        if (!_pretransfer)
                            continue;

                        //lot locate
                        _locationType = dr["location_type"].ToString().Equals("") ? "" : dr["location_type"].ToString();

                        sql = _BaseDataService.GetWorkgroupDetailSet("pretransfer", _workgroup, "", "partid");
                        dtTemp2 = _dbTool.GetDataTable(sql);

                        if (dtTemp2.Rows.Count > 0)
                        {
                            _values1 = dtTemp2.Rows[0]["values1"].ToString().Equals("") ? "*" : dtTemp2.Rows[0]["values1"].ToString();

                            if(!_partid.Equals(_values1))
                            {
                                continue;
                            }
                        }

                        transferList = new TransferList();
                        carrierId = "";

                        if (_locationType.Equals("STK"))
                        {
                            try
                            {
                                sql = _BaseDataService.QueryRackByGroupID(dr["in_eRack"].ToString());
                                dtTemp2 = _dbTool.GetDataTable(sql);
                                if (dtTemp2.Rows.Count > 0)
                                    _inErack = dtTemp2.Rows[0]["erackID"].ToString();
                                else
                                    _inErack = dr["in_eRack"].ToString();

                                sql = _BaseDataService.CheckCarrierLocate(dr["in_eRack"].ToString(), dr["locate"].ToString().Split('0')[0].ToString());
                            }
                            catch(Exception ex) {
                                _logger.Debug("[Exception STK]" + dr["locate"].ToString());
                            }
                        }
                        else
                        {
                            try
                            {
                                _inErack = dr["in_eRack"].ToString();
                                //in eRack STK1
                                sql = _BaseDataService.QueryRackByGroupID(dr["in_eRack"].ToString());
                                dtTemp2 = _dbTool.GetDataTable(sql);
                                //A/B
                                if (dtTemp2.Rows.Count > 0)
                                {
                                    if (dtTemp2.Rows[0]["MAC"].ToString().Equals("STOCK"))
                                    {
                                        _inErack = dtTemp2.Rows[0]["erackID"].ToString();
                                    }
                                    else
                                    {
                                        _inErack = dtTemp2.Rows[0]["erackID"].ToString();
                                    }
                                }

                                sql = _BaseDataService.CheckCarrierLocate(dr["in_eRack"].ToString(), dr["locate"].ToString());
                            }
                            catch (Exception ex)
                            {
                                _logger.Debug("[Exception STK]" + dr["locate"].ToString());
                            }
                        }

                        dtTemp2 = _dbTool.GetDataTable(sql);

                        if (dtTemp2.Rows.Count <= 0)
                        {

                            carrierId = dr["carrier_ID"].ToString().Equals("") ? "*" : dr["carrier_ID"].ToString();
                            sql = _BaseDataService.CheckPreTransfer(carrierId, tableOrder);
                            dtTemp3 = _dbTool.GetDataTable(sql);
                            if (dtTemp3.Rows.Count > 0)
                                continue;


                            try
                            {
                                _destStage = dr["stage"].ToString();

                                sql = _BaseDataService.QueryWorkgroupSet(_eqpWorkgroup, _destStage);
                                dtTemp3 = _dbTool.GetDataTable(sql);
                                if (dtTemp3.Rows.Count > 0)
                                {
                                    _sideWarehouse = dtTemp3.Rows[0]["SideWarehouse"].ToString();
                                    _swSideWh = dtTemp3.Rows[0]["swsidewh"].ToString().Equals("1") ? true : false;
                                }

                                if (_swSideWh)
                                {
                                    try { 
                                        sql = _BaseDataService.CheckLocateofSideWh(dr["locate"].ToString(), _sideWarehouse);
                                        dtTemp3 = _dbTool.GetDataTable(sql);
                                        if (dtTemp3.Rows.Count > 0)
                                        {
                                            _onSideWH = true;
                                            _logger.Debug(string.Format("[SideWH][{0}][{1}][{2}]", dr["locate"].ToString(), _sideWarehouse, _onSideWH));
                                        }
                                        else
                                        {
                                            _onSideWH = false;
                                        }
                                    }
                                    catch (Exception ex) {
                                        _logger.Debug(string.Format("[Exception SideWH][{0}][{1}][{2}]" , dr["locate"].ToString(), _sideWarehouse, ex.Message));
                                    }
                                }
                            }
                            catch (Exception ex) { }

                            if (_onSideWH)
                            { continue; }
                            else
                            {

                                _eventQ = new EventQueue();
                                _eventQ.EventName = funcName;

                                transferList.CarrierID = carrierId;
                                transferList.LotID = dr["lot_ID"].ToString().Equals("") ? "*" : dr["lot_ID"].ToString();
                                transferList.Source = "*";
                                //transferList.Dest = dr["in_eRack"].ToString();
                                transferList.Dest = _inErack;
                                transferList.CommandType = "Pre-Transfer";
                                transferList.CarrierType = dr["carrier_type"].ToString();

                                tmpMsg = string.Format("[{0}][{1} / {2} / {3} / {4} / {5}]", transferList.CommandType, transferList.LotID, transferList.CarrierID, transferList.Source, transferList.Dest, transferList.CarrierType);
                                _logger.Debug(tmpMsg);

                                _eventQ.EventObject = transferList;
                                _eventQueue.Enqueue(_eventQ);
                            }
                        }
                    }
                }

                bResult = true;
            }
            catch (Exception ex)
            {
                _logger.Debug(string.Format("PreDispatchToErack Unknow Error. [Exception: {0}]", ex.Message));
            }
            finally
            {
                if (dtTemp != null)
                    dtTemp.Dispose();
                if (dtTemp2 != null)
                    dtTemp2.Dispose();
                if (dtTemp3 != null)
                    dtTemp3.Dispose();
            }
            dtTemp = null;
            dtTemp2 = null;
            dtTemp3 = null;

            return bResult;
        }
        public DataTable GetLotInfo(DBTool _dbTool, string _department, ILogger _logger)
        {
            string funcName = "GetLotInfo";
            string tmpMsg = "";
            string strResult = "";
            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";

            try
            {

                //// 查詢資料
                dt = _dbTool.GetDataTable(_BaseDataService.SelectTableLotInfoByDept(_department));
                dr = dt.Select();
                if (dt.Rows.Count > 0)
                {
                    return dt;
                }
            }
            catch (Exception ex)
            {
                tmpMsg = string.Format("Function:{0}, Received:[{1}]", funcName, ex.Message);
                _logger.Debug(tmpMsg);

                return null;
            }
            finally
            {
                
            }
            dr = null;

            return null;
        }
        public bool CarrierLocationUpdate(DBTool _dbTool, IConfiguration _configuration, CarrierLocationUpdate value, ILogger _logger)
        {
            string funcName = "CarrierLocationUpdate";
            string tmpMsg = "";
            DataTable dt = null;
            DataTable dtTemp = null;
            DataRow[] dr = null;
            string sql = "";

            try
            {
                var jsonStringName = new JavaScriptSerializer();
                var jsonStringResult = jsonStringName.Serialize(value);
                _logger.Info(string.Format("Function:{0}, Received:[{1}]", funcName, jsonStringResult));

                //// 查詢資料
                dt = _dbTool.GetDataTable(_BaseDataService.SelectTableCarrierTransferByCarrier(value.CarrierID.Trim()));
                dr = dt.Select();
                if (dt.Rows.Count > 0)
                {
                    /*
                        1.	Load Carrier
                        2.	Unload Carrier
                    */
                    dr = dt.Select();

                    if (!value.CarrierID.Equals(""))
                    {
                        string strLocate = "";
                        string strPort = "0";
                        if (value.Location.Contains("_LP"))
                        {
                            strLocate = value.Location.Split("_LP")[0].ToString();
                            strPort = value.Location.Split("_LP")[1].ToString();
                        }
                        else
                        {
                            strLocate = value.Location;
                            strPort = "1";
                        }
                        string lstMetalRing = _configuration["CarrierTypeSet:MetalRing"];
                        int haveMetalRing = 0;
                        if (lstMetalRing.Contains(strLocate))
                            haveMetalRing = 1;
                        else
                            haveMetalRing = 0;

                        sql = String.Format(_BaseDataService.CarrierLocateReset(value, haveMetalRing));
                        _dbTool.SQLExec(sql, out tmpMsg, true);

                        tmpMsg = string.Format("Reset carrier locate by func [{0}]. carrier id [{1}]", funcName, value.CarrierID);
                        _logger.Info(tmpMsg);

                        sql = String.Format(_BaseDataService.UpdateTableCarrierTransfer(value, haveMetalRing));
                        _dbTool.SQLExec(sql, out tmpMsg, true);

                        sql = _BaseDataService.QueryLotInfoByCarrier(value.CarrierID);
                        dtTemp = _dbTool.GetDataTable(sql);
                        if (dtTemp.Rows.Count > 0)
                        {
                            if (dtTemp.Rows[0]["isLock"].ToString().Equals("1"))
                            {
                                sql = String.Format(_BaseDataService.UnLockLotInfoWhenReady(dtTemp.Rows[0]["lot_id"].ToString()));
                                _dbTool.SQLExec(sql, out tmpMsg, true);
                            }
                        }
                    }
                }
                else { 
                    return false; 
                }
            }
            catch (Exception ex)
            {
                tmpMsg = string.Format("Function:{0}, Received:[{1}]", funcName, ex.Message);
                _logger.Debug(tmpMsg);

                return false;
            }
            finally
            {
                if (dt != null)
                    dt.Dispose();
                if (dtTemp != null)
                    dtTemp.Dispose();
            }
            dt = null;
            dtTemp = null;
            dr = null;

            return true;
        }
        public bool CommandStatusUpdate(DBTool _dbTool, IConfiguration _configuration, CommandStatusUpdate value, ILogger _logger)
        {
            string funcName = "CommandStatusUpdate";
            string tmpMsg = "";
            DataTable dt = null;
            DataTable dtTemp = null;
            DataRow[] dr = null;
            string sql = "";
            bool bExecSql = false;
            int FailedNum = 0;
            string tableOrder = "";
            string _keyOfEnv = "";
            string _keyRTDEnv = "";

            try
            {
                _keyRTDEnv = _configuration["RTDEnvironment:type"];
                _keyOfEnv = string.Format("RTDEnvironment:commandsTable:{0}", _keyRTDEnv);
                //"RTDEnvironment:commandsTable:PROD"
                tableOrder = _configuration[_keyOfEnv] is null ? "workinprocess_sch" : _configuration[_keyOfEnv];

                var jsonStringName = new JavaScriptSerializer();
                var jsonStringResult = jsonStringName.Serialize(value);
                _logger.Info(string.Format("Function:{0}, Received:[{1}]", funcName, jsonStringResult));

                _dbTool.SQLExec(_BaseDataService.UpdateTableWorkInProcessSchHisByCmdId(value.CommandID.Trim()), out tmpMsg, true);

                while (true)
                {
                    try
                    {
                        sql = _BaseDataService.SelectTableWorkInProcessSchByCmdId(value.CommandID, tableOrder);
                        dt = _dbTool.GetDataTable(sql);

                        if (dt.Rows.Count > 0)
                        {
                            //if (!dt.Rows[0]["cmd_type"].ToString().Equals("Pre-Transfer"))
                            //{
                                bExecSql = _dbTool.SQLExec(_BaseDataService.UpdateTableWorkInProcessSchByCmdId(value.Status, value.LastStateTime, value.CommandID.Trim(), tableOrder), out tmpMsg, true);

                                if (bExecSql)
                                    break;
                            //}
                        }
                        else
                            break;
                    }
                    catch (Exception ex)
                    {
                        //tmpMsg = String.Format("UpdateTableWorkInProcessSchByCmdId fail. {0}", ex.Message);
                        tmpMsg = String.Format("UpdateTableWorkInProcessSchByCmdId fail. {0}", ex.ToString()); //ModifyByBird@20230421_秀出更多錯誤資訊
                        _logger.Debug(tmpMsg);
                        FailedNum++; //AddByBird@20230421_跳出迴圈
                    }

                    //AddByBird@20230421_跳出迴圈
                    if (FailedNum >=3)
                    {
                        tmpMsg = String.Format("Execute UpdateTableWorkInProcessSchByCmdId Failed (Retry 3 Times). Received:[{0}]", jsonStringResult);
                        _logger.Error(tmpMsg);
                        break; //AddByBird@20230421_跳出迴圈
                    }
                }

                if (!bExecSql)
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                tmpMsg = string.Format("Function:{0}, Received:[{1}]", funcName, ex.Message);
                _logger.Debug(tmpMsg);

                return false;
            }
            finally
            {
                if (dt != null)
                    dt.Dispose();
                if (dtTemp != null)
                    dtTemp.Dispose();
            }
            dt = null;
            dtTemp = null;
            dr = null;

            return true;
        }
        public bool EquipmentPortStatusUpdate(DBTool _dbTool, IConfiguration _configuration, AEIPortInfo value, ILogger _logger)
        {
            string funcName = "EquipmentPortStatusUpdate";
            string tmpMsg = "";
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

                /// 查詢資料
                sql = string.Format(_BaseDataService.SelectTableEQP_Port_SetByPortId(value.PortID)) ;
                dt = _dbTool.GetDataTable(sql);
                string tCondition = string.Format("Port_Seq = {0}", value.PortID);
                dr = dt.Select("");
                
                if (dt.Rows.Count > 0)
                {
                    if (!dr[0]["Port_State"].ToString().Equals(value.PortTransferState))
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

                        return true;

                    }
                }
            }
            catch (Exception ex)
            {
                tmpMsg = string.Format("Function:{0}, Received:[{1}]", funcName, ex.Message);
                _logger.Debug(tmpMsg);

                return false;
            }
            finally
            {
                if (dt != null)
                    dt.Dispose();
                if (dtTemp != null)
                    dtTemp.Dispose();
            }
            dt = null;
            dtTemp = null;
            dr = null;

            return true;
        }
        public bool EquipmentStatusUpdate(DBTool _dbTool, IConfiguration _configuration, AEIEQInfo value, ILogger _logger)
        {
            string funcName = "EquipmentStatusUpdate";
            string tmpMsg = "";
            DataTable dt = null;
            DataTable dtTemp = null;
            DataRow[] dr = null;
            string sql = "";
            string eqState = "";
            Boolean _isDisabled = false;

            try
            {
                var jsonStringName = new JavaScriptSerializer();
                var jsonStringResult = jsonStringName.Serialize(value);
                _logger.Info(string.Format("Function:{0}, Received:[{1}]", funcName, jsonStringResult));

                eqState = GetEquipStat(value.EqState);
                //// 查詢資料
                sql = string.Format(_BaseDataService.SelectTableEQP_STATUSByEquipId(value.EqID));
                dt = _dbTool.GetDataTable(sql);
                dr = dt.Select();

                if (dt.Rows.Count > 0)
                {
                    string _currState = "";
                    string _machineState = "";
                    string _downState = "";

                    sql = string.Format(_BaseDataService.GetRTSEquipStatus(GetExtenalTables(_configuration, "SyncExtenalData", "RTSEQSTATE"), value.EqID));
                    dtTemp = _dbTool.GetDataTable(sql);
                    if(dtTemp.Rows.Count >0)
                    {
                        //Machine_state, Curr_Status, Down_State
                        try
                        {
                            _currState = dtTemp.Rows[0]["Curr_Status"].ToString().Length <= 10 ? dtTemp.Rows[0]["Curr_Status"].ToString() : dtTemp.Rows[0]["Curr_Status"].ToString().Split(',')[0].Trim();
                            _machineState = dtTemp.Rows[0]["Machine_state"].ToString().Length <= 10 ? dtTemp.Rows[0]["Machine_state"].ToString() : dt.Rows[0]["Machine_state"].ToString().Substring(0, 10).Trim();
                            _downState = dtTemp.Rows[0]["Down_State"].ToString().Length <= 10 ? dtTemp.Rows[0]["Down_State"].ToString() : dtTemp.Rows[0]["Down_State"].ToString().Substring(0, 10).Trim();
                        }
                        catch (Exception ex)
                        {
                            _currState = dt.Rows[0]["Curr_Status"].ToString().Substring(0, 10).Trim();
                            _machineState = dt.Rows[0]["Machine_state"].ToString().Substring(0, 10).Trim();
                            _downState = dt.Rows[0]["Down_State"].ToString().Substring(0, 10).Trim();
                        }
                    }

                    if (!dt.Rows[0]["Curr_Status"].ToString().Equals(eqState))
                    {
                        sql = string.Format(_BaseDataService.UpdateTableEQP_STATUS(value.EqID, value.EqState, _machineState, _downState));
                        _dbTool.SQLExec(sql, out tmpMsg, true);
                    }
                    else if (!dt.Rows[0]["Machine_state"].ToString().Equals(_machineState))
                    {
                        sql = string.Format(_BaseDataService.UpdateTableEQP_STATUS(value.EqID, value.EqState, _machineState, _downState));
                        _dbTool.SQLExec(sql, out tmpMsg, true);
                    }
                    else if (!dt.Rows[0]["Down_State"].ToString().Equals(_downState))
                    {
                        sql = string.Format(_BaseDataService.UpdateTableEQP_STATUS(value.EqID, value.EqState, _machineState, _downState));
                        _dbTool.SQLExec(sql, out tmpMsg, true);
                    }
                }

                foreach (var strPort in value.PortInfoList)
                {
                    //SelectTableEQP_Port_SetByPortId
                    sql = string.Format(_BaseDataService.SelectTableEQP_Port_SetByPortId(strPort.PortID));
                    dtTemp = _dbTool.GetDataTable(sql);
                    if (dtTemp.Rows.Count > 0)
                    {
                        if (dtTemp.Rows.Count > 0)
                        {
                            _isDisabled = dtTemp.Rows[0]["DISABLE"].ToString().Equals("1") ? true : false;
                        }

                        if (_isDisabled)
                            continue;

                        int _portstate = dtTemp.Rows[0]["port_state"] is null ? 0 : int.Parse(dtTemp.Rows[0]["port_state"].ToString());//port_state
                        if (!strPort.PortTransferState.Equals(_portstate))
                        {
                            //update port
                            //UpdateTableEQP_Port_Set
                            _dbTool.SQLExec(_BaseDataService.UpdateTableEQP_Port_Set(value.EqID, strPort.PortID.Split("_LP")[1].ToString(), strPort.PortTransferState.ToString()), out tmpMsg, true);

                            try {

                                int haveMetalRing = 0;
                                if (!strPort.CarrierID.Equals(""))
                                {
                                    //更新Carrier 位置
                                    sql = string.Format(_BaseDataService.GetCarrierByLocate(value.EqID, int.Parse(strPort.PortID.Split("_LP")[1].ToString())));
                                    dtTemp = _dbTool.GetDataTable(sql);

                                    if (dtTemp.Rows.Count > 0)
                                    {
                                        /** 最新的Carrier: value.CarrierID, 舊的Carrier: dtTemp.Rows[0]["carrier_id"].ToString()*/
                                        if (dtTemp.Rows[0]["carrier_id"].ToString().Equals(strPort.CarrierID))
                                        {
                                            //不更新
                                        }
                                        else
                                        {
                                            CarrierLocationUpdate oCarrierLoc = new CarrierLocationUpdate();
                                            oCarrierLoc.CarrierID = strPort.CarrierID;
                                            oCarrierLoc.TransferState = strPort.PortTransferState.ToString();
                                            oCarrierLoc.Location = strPort.PortID;
                                            oCarrierLoc.LocationType = "EQP";

                                            if (!strPort.CarrierID.Equals(""))
                                            {
                                                //清除舊的Carrier Locate
                                                sql = String.Format(_BaseDataService.CarrierLocateReset(oCarrierLoc, haveMetalRing));
                                                _dbTool.SQLExec(sql, out tmpMsg, true);

                                                tmpMsg = string.Format("Reset carrier locate by func [{0},1]. carrier id [{1}]", funcName, strPort.CarrierID);
                                                _logger.Info(tmpMsg);

                                                //更新新的Carrier Locate
                                                sql = String.Format(_BaseDataService.UpdateTableCarrierTransfer(oCarrierLoc, haveMetalRing));
                                                _dbTool.SQLExec(sql, out tmpMsg, true);
                                            }

                                        }
                                    }
                                    else
                                    {
                                        CarrierLocationUpdate oCarrierLoc = new CarrierLocationUpdate();
                                        oCarrierLoc.CarrierID = strPort.CarrierID;
                                        oCarrierLoc.TransferState = strPort.PortTransferState.ToString();
                                        oCarrierLoc.Location = strPort.PortID;
                                        oCarrierLoc.LocationType = "EQP";

                                        //更新新的Carrier Locate
                                        sql = String.Format(_BaseDataService.UpdateTableCarrierTransfer(oCarrierLoc, haveMetalRing));
                                        _dbTool.SQLExec(sql, out tmpMsg, true);

                                    }

                                }
                                else
                                {
                                    //No carrier did not reset carrier locate

                                    ////更新Carrier 位置
                                    //sql = string.Format(_BaseDataService.GetCarrierByLocate(value.EqID, int.Parse(strPort.PortID.Split("_LP")[1].ToString())));
                                    //dtTemp = _dbTool.GetDataTable(sql);

                                    //if (dtTemp.Rows.Count > 0)
                                    //{
                                    //    CarrierLocationUpdate oCarrierLoc = new CarrierLocationUpdate();
                                    //    oCarrierLoc.CarrierID = strPort.CarrierID;
                                    //    oCarrierLoc.TransferState = strPort.PortTransferState.ToString();
                                    //    oCarrierLoc.Location = strPort.PortID;
                                    //    oCarrierLoc.LocationType = "EQP";

                                    //    //清除舊的Carrier Locate
                                    //    sql = String.Format(_BaseDataService.CarrierLocateReset(oCarrierLoc, haveMetalRing));
                                    //    _dbTool.SQLExec(sql, out tmpMsg, true);

                                    //    tmpMsg = string.Format("Reset carrier locate by func [{0},2]. carrier id [{1}]", funcName, strPort.CarrierID);
                                    //    _logger.Info(tmpMsg);
                                    //}
                                }

                            } catch(Exception ex) { }

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tmpMsg = string.Format("Function:{0}, Received:[{1}]", funcName, ex.Message);
                _logger.Debug(tmpMsg);

                return false;
            }
            finally
            {
                if (dt != null)
                    dt.Dispose();
                if (dtTemp != null)
                    dtTemp.Dispose(); 
            }
            dt = null;
            dtTemp = null;
            dr = null;

            return true;
        }
        public bool NightOrDay(string _currDateTime)
        {
            bool _Night = false;

            try
            {
                if (_currDateTime.Equals(""))
                    return _Night;

                //將時間轉換為DateTime
                DateTime date = Convert.ToDateTime(_currDateTime);

                //處於開始時間和結束時間的判斷式 //目前班別為早8晚8為日班/晚8早8為夜班
                if (date.Hour >= 20 || date.Hour < 8)
                {
                    _Night = true;
                }
                else
                {
                    _Night = false;
                }
            }
            catch(Exception ex)
            {

            }

            return _Night;
        }
        public List<HisCommandStatus> GetHistoryCommands(DBTool _dbTool, Dictionary<string, string> _alarmDetail, string StartDateTime, string CurrentDateTime, string Unit, string Zone)
        {
            List<HisCommandStatus> foo;
            string funcName = "GetHistoryCommands";
            string tmpMsg = "";
            DataTable dt = null;
            DataTable dtTemp = null;
            DataRow[] dr = null;
            string sql = "";
            IBaseDataService _BaseDataService = new BaseDataService();
            foo = new List<HisCommandStatus>();
            DateTime dtStart;
            DateTime dtEnd;
            double dHour = 0;
            double dunit = 1;
            DateTime dtStartUTCTime;
            DateTime dtCurrUTCTime;
            DateTime dtLocStartTime;
            DateTime dtLocTime;
            bool isNightShift = false;

            try
            {
                if (Zone.Contains("-"))
                {
                    dunit = 1;
                    dHour = dunit * double.Parse(Zone.Replace("-", ""));
                }
                else if (Zone.Contains("+"))
                {
                    dunit = -1;
                    dHour = dunit * double.Parse(Zone.Replace("+", ""));
                }
                else
                {
                    dunit = -1;
                    dHour = dunit * double.Parse(Zone);
                }

                if (CurrentDateTime.Equals(""))
                {
                    dtLocStartTime = DateTime.Now;
                    dtLocTime = DateTime.Now;
                    dtStartUTCTime = DateTime.Now;
                    dtCurrUTCTime = DateTime.Now;
                }
                else
                {
                    dtLocTime = DateTime.Parse(CurrentDateTime);
                    dtCurrUTCTime = DateTime.Parse(CurrentDateTime).AddHours(dHour);

                    if (StartDateTime is null || StartDateTime.Equals(""))
                    {
                        dtLocStartTime = dtLocTime;
                        dtStartUTCTime = DateTime.Parse(CurrentDateTime).AddHours(dHour);
                    }
                    else
                    {
                        dtLocStartTime = DateTime.Parse(StartDateTime);
                        dtStartUTCTime = DateTime.Parse(StartDateTime).AddHours(dHour);
                    }
                }

                isNightShift = NightOrDay(CurrentDateTime);

                switch(Unit.ToUpper())
                {
                    case "YEAR":
                            dtStart = DateTime.Parse(dtLocStartTime.ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour);
                            dtEnd = DateTime.Parse(dtLocTime.AddDays(1).ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour);
                        break;
                    case "MONTH":
                            dtStart = DateTime.Parse(dtLocStartTime.ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour);
                            dtEnd = DateTime.Parse(dtLocTime.AddDays(1).ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour);
                        break;
                    case "WEEK":
                            dtStart = DateTime.Parse(dtLocStartTime.ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour);
                            dtEnd = DateTime.Parse(dtLocTime.AddDays(1).ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour);
                        break;
                    case "SHIFT":

                        DateTime date = Convert.ToDateTime(CurrentDateTime);

                        if (isNightShift)
                        {
                            if(date.Hour < 8)
                            {
                                dtStart = DateTime.Parse(dtLocStartTime.AddDays(-1).ToString("yyyy-MM-dd ") + " 20:00").AddHours(dHour);
                                dtEnd = DateTime.Parse(dtLocTime.ToString("yyyy-MM-dd ") + " 08:00").AddHours(dHour);
                            }
                            else
                            {
                                dtStart = DateTime.Parse(dtLocStartTime.ToString("yyyy-MM-dd ") + " 20:00").AddHours(dHour);
                                dtEnd = DateTime.Parse(dtLocTime.AddDays(1).ToString("yyyy-MM-dd ") + " 08:00").AddHours(dHour);
                            }
                        }
                        else
                        {
                            if (StartDateTime is null || StartDateTime.Equals(""))
                            {
                                dtStart = DateTime.Parse(dtLocStartTime.ToString("yyyy-MM-dd ") + " 08:00").AddHours(dHour);
                                dtEnd = DateTime.Parse(dtLocTime.ToString("yyyy-MM-dd ") + " 20:00").AddHours(dHour);
                            }
                            else
                            {
                                dtStart = DateTime.Parse(dtLocStartTime.ToString("yyyy-MM-dd HH:mm:ss")).AddHours(dHour);
                                dtEnd = DateTime.Parse(dtLocTime.ToString("yyyy-MM-dd HH:mm:ss")).AddHours(dHour);
                            }
                        }
                        break;
                    case "DAY":
                    default:

                        dtStart = DateTime.Parse(dtLocStartTime.ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour);
                        dtEnd = DateTime.Parse(dtLocTime.AddDays(1).ToString("yyyy-MM-dd ") + " 00:00").AddHours(dHour);

                        break;
                }

                sql = _BaseDataService.GetHistoryCommands(dtStart.ToString("yyyy/MM/dd HH:mm:ss"), dtEnd.ToString("yyyy/MM/dd HH:mm:ss"));
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    HisCommandStatus HisCommand;
                    string sReason = "";
                    foreach (DataRow row in dt.Rows)
                    {
                        sReason = "";
                        //"CommandID", "CarrierID", "LotID", "CommandType", "Source", "Dest", "AlarmCode", "Reason", "createdAt", "LastStateTime"
                        HisCommand = new HisCommandStatus();
                        HisCommand.CommandID = row["CommandID"].ToString();
                        HisCommand.CarrierID = row["CarrierID"].ToString();
                        HisCommand.LotID = row["LotID"].ToString();
                        HisCommand.CommandType = row["CommandType"].ToString();
                        HisCommand.Source = row["Source"].ToString();
                        HisCommand.Dest = row["Dest"].ToString();
                        try
                        { 
                            sReason = _alarmDetail[row["AlarmCode"].ToString()] is null ? "" : _alarmDetail[row["AlarmCode"].ToString()];
                        }
                        catch (Exception ex) { }
                        HisCommand.AlarmCode = row["AlarmCode"].ToString();
                        HisCommand.Reason = sReason;
                        DateTime dtCreatedAt = DateTime.Parse(row["CreatedAt"].ToString());
                        HisCommand.CreatedAt = dtCreatedAt.ToString("yyyy/MM/dd HH:mm:ss");
                        DateTime dtLastStateTime = DateTime.Parse(row["LastStateTime"].ToString());
                        HisCommand.LastStateTime = dtLastStateTime.ToString("yyyy/MM/dd HH:mm:ss");

                        foo.Add(HisCommand);
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
        public bool TriggerAlarms(DBTool _dbTool, IConfiguration configuration, ILogger _logger)
        {
            //sent eMail & SMS & Call JCET CIM Actions

            string funcName = "TriggerAlarms";
            string tmpMsg = "";
            string ErrMsg = "";
            DataTable dt = null;
            DataTable dtTemp = null;
            DataRow[] dr = null;
            string sql = "";
            bool result = false;
            RTDAlarms rtdAlarms = new RTDAlarms();

            string apiFormat = "";
            string apiFunc = "";
            string _currentStep = ""; 
            string eventTrigger = "";

            try
            {
                sql = _BaseDataService.QueryRTDAlarms();
                dt = _dbTool.GetDataTable(sql);

                /*
                Sample email alert.
Email Group: CISRTDALERT.SCS @jcetglobal.com
1.
Subject: Device Setup Pre - Alert

Tool: XXXXX
Next Lot: XXXXXXXX.1
Current Lot: 83747807.1
Mfg Device: 3SDC00011A - WL - B - 00.01
Customer Device: XXXXXXXX
2.
Subject: Device Setup Alert
Tool: XXXXX
Next Lot: XXXXXXXX.1
Mfg Device for next lot: XXXXXXXXXXXXXXX
Customer Device for next lot: XXXXXXXX
Last lot: YYYYYYYY.1
Mfg Device for last lot: YYYYYYYYYYYYYYYYYYYYYYYYYYYYYYY
Customer Device for last lot: YYYYYYYYYYY

                */

                if (dt.Rows.Count > 0)
                {
                    string idxAlarm = ""; 
                    string AlarmCode = "";
                    eventTrigger = configuration["RTDAlarm:Condition"] is null ? "eMail:true$SMS:true$repeat:false$hours:0$mints:10" : configuration["RTDAlarm:Condition"];
                    string tmpAlarmType = "";
                    string tempMsg = "";
                    List<string> tmpParams = new List<string>();
                    MailMessage JcetAlarmMsg = new MailMessage();
                    string _tempEqpID = "";
                    string _tempPortID = "";

                    try
                    {
                        foreach(DataRow drTemp in dt.Rows)
                        {
                            try {
                                _tempEqpID = "";
                                _tempPortID = "";

                                rtdAlarms = new RTDAlarms();

                                rtdAlarms.UnitType = drTemp["UnitType"] is null ? "" : drTemp["UnitType"].ToString();
                                rtdAlarms.UnitID = drTemp["UnitID"] is null ? "" : drTemp["UnitID"].ToString();
                                rtdAlarms.Level = drTemp["Level"] is null ? "" : drTemp["Level"].ToString();
                                rtdAlarms.Code = drTemp["Code"] is null ? 0 : int.Parse(drTemp["Code"].ToString());
                                rtdAlarms.Cause = drTemp["Cause"] is null ? "" : drTemp["Cause"].ToString();
                                rtdAlarms.SubCode = drTemp["SubCode"] is null ? "" : drTemp["SubCode"].ToString();
                                rtdAlarms.Detail = drTemp["Detail"] is null ? "" : drTemp["Detail"].ToString();
                                rtdAlarms.CommandID = drTemp["CommandID"] is null ? "" : drTemp["CommandID"].ToString();
                                rtdAlarms.Params = drTemp["Params"] is null ? "" : drTemp["Params"].ToString();
                                rtdAlarms.Description = drTemp["Description"] is null ? "" : drTemp["Description"].ToString();
                                //rtdAlarms.CreateAt = drTemp["CreateAt"] is null ? "" : drTemp["CreateAt"].ToString();
                                //rtdAlarms.lastUpdated = drTemp["lastUpdated"] is null ? "" : drTemp["Description"].ToString();

                                idxAlarm = drTemp["IDX"].ToString();
                                AlarmCode = drTemp["code"].ToString();
                                eventTrigger = drTemp["EVENTTRIGGER"] is null ? "" : drTemp["EVENTTRIGGER"].ToString();
                                tmpParams = new List<string>();


                                JcetAlarmMsg = new MailMessage();
                                JcetAlarmMsg.To.Add(configuration["MailSetting:AlarmMail"]);
                                //msg.To.Add("b@b.com");可以發送給多人
                                //msg.CC.Add("c@c.com");
                                //msg.CC.Add("c@c.com");可以抄送副本給多人 
                                //這裡可以隨便填，不是很重要
                                JcetAlarmMsg.From = new MailAddress(configuration["MailSetting:username"], configuration["MailSetting:EntryBy"], Encoding.UTF8);

                                if (!eventTrigger.Equals(""))
                                {
                                    string strTemp = "";

                                    try {
                                        //"eMail:true$SMS:true$repeat:true$hours:0$mints:10";
                                        string[] tmpTrigger = eventTrigger.Split('$');
                                        foreach( var parm in tmpTrigger)
                                        {
                                            string[] tmpKey = parm.Split(':');

                                            if (strTemp.Equals(""))
                                                strTemp = string.Format("'{0}':{1}", tmpKey[0], tmpKey[1]);
                                            else
                                                strTemp = string.Format("{0},'{1}':{2}", strTemp, tmpKey[0], tmpKey[1]);
                                        }

                                        if(eventTrigger.Equals(""))
                                        {
                                            sql = _BaseDataService.UpdateRTDAlarms(true, string.Format("{0},{1},{2},{3},{4}", drTemp["UnitID"].ToString(), drTemp["Code"].ToString(), drTemp["SubCode"].ToString(), drTemp["CommandID"].ToString(), drTemp["Params"].ToString()), "");
                                            _dbTool.SQLExec(sql, out tmpMsg, true);
                                            continue;
                                        }

                                        strTemp = "{" + strTemp + "}";

                                        try {
                                            var TempJsonConvert = JsonConvert.DeserializeObject<JcetAlarm>(strTemp);//反序列化
                                        }
                                        catch (Exception ex) { continue; }
                                    }
                                    catch (Exception ex) { continue; }
                                    //--params= "eMail":true, "SMS":true, "repeat":true, "hours":0, "mints":10, 
                                    //Newtonsoft.Json反序列化
                                    var JsonJcetAlarm = JsonConvert.DeserializeObject<JcetAlarm>(strTemp);//反序列化
                                    var JsonObject = JObject.Parse(strTemp);

                                    //tmpSmsMsg = "lotid:{0}$equipid:{1}$partid:{2}$customername:{3}$stage:{4}$nextlot:{5}$nextpart:{6}";
                                    try
                                    {
                                        //"eMail:true$SMS:true$repeat:true$hours:0$mints:10";
                                        strTemp = "";
                                        strTemp = rtdAlarms.Params.Replace('{',' ').Replace('}',' ');

                                        if (!strTemp.Equals(""))
                                        {
                                            strTemp = "{" + strTemp + "}";

                                            try
                                            {
                                                var TempJsonConvert = JsonConvert.DeserializeObject<JcetAlarm>(strTemp);//反序列化
                                            }
                                            catch (Exception ex) { continue; }
                                        }
                                        else
                                        {
                                            strTemp = "{ \"Result\":\"None\"}";
                                        }
                                    }
                                    catch (Exception ex) { }

                                    //--params= "eMail":true, "SMS":true, "repeat":true, "hours":0, "mints":10, 
                                    //Newtonsoft.Json反序列化
                                    var JsonJcetParams = JsonConvert.DeserializeObject<JcetAlarm>(strTemp);//反序列化
                                    //var JsonParamsObject = JObject.Parse(strTemp);

                                    //_tempEqpID = GetJArrayValue((JObject)JsonParamsObject, "EquipID");
                                    //_tempPortID = GetJArrayValue((JObject)JsonParamsObject, "PortID");
                                    _tempEqpID = JsonJcetParams.EquipID;
                                    _tempPortID = JsonJcetParams.PortID;

                                    switch (AlarmCode)
                                    {
                                        case "1001":
                                            try
                                            {
                                                tmpParams.Add(string.Format("Device Setup Pre-Alert"));
                                                tmpParams.Add(string.Format(JsonJcetParams.lotid));
                                                tmpParams.Add(string.Format(JsonJcetParams.EquipID));
                                                tempMsg = string.Format(@"Tool: {0}
Next Lot: {1}
Current Lot: {2}
Mfg Device: {3}
Customer Device: {4}", JsonJcetParams.EquipID, JsonJcetParams.nextlot, JsonJcetParams.lotid, JsonJcetParams.stage, JsonJcetParams.partid);

                                                //tmpSmsMsg = "lotid:{0}$equipid:{1}$partid:{2}$customername:{3}$stage:{4}$nextlot:{5}$nextpart:{6}";

                                                _logger.Info(tempMsg);
                                                tmpParams.Add(string.Format(tempMsg));
                                            } catch (Exception ex)
                                            {
                                                tmpMsg = String.Format("[Exception]:[{0}][{1}]", AlarmCode, ex.Message);
                                                _logger.Info(tmpMsg);
                                            }
                                            break;
                                        case "1002":
                                            try
                                            {
                                                tmpParams.Add(string.Format("Device Setup Alert"));
                                                tmpParams.Add(string.Format(JsonJcetParams.nextlot));
                                                tmpParams.Add(string.Format(JsonJcetParams.EquipID));
                                                tempMsg = string.Format(@"Tool: {0}
Next Lot: {1}
Mfg Device for next lot: {2}
Customer Device for next lot: {3}
Last lot: {4}
Mfg Device for last lot: {5}
Customer Device for last lot: {6}", JsonJcetParams.EquipID, JsonJcetParams.nextlot, JsonJcetParams.nextpart, JsonJcetParams.customername, JsonJcetParams.lotid, JsonJcetParams.partid, JsonJcetParams.customername);

                                                _logger.Info(tempMsg);
                                                tmpParams.Add(string.Format(tempMsg));
                                            } catch (Exception ex)
                                            {
                                                tmpMsg = String.Format("[Exception]:[{0}][{1}]", AlarmCode, ex.Message);
                                                _logger.Info(tmpMsg);
                                            }
                                            break;
                                        case "20051":
                                            //eRack Offline
                                            tmpParams.Add(string.Format("e-Rack {0} offline, Setup Alert", rtdAlarms.UnitID));
                                            //target.
                                            tmpParams.Add(rtdAlarms.UnitID);
                                            //Type.
                                            tmpParams.Add(rtdAlarms.UnitType);
                                            //Body
                                            tempMsg = string.Format(@"UnitType: {0}
UnitID: {1}
Code: {2}
Cause: {3}
Last lot: {4}
SubCode: {5}
Detail: {6}", rtdAlarms.UnitType, rtdAlarms.UnitID, rtdAlarms.Code, rtdAlarms, "", rtdAlarms.SubCode, rtdAlarms.Detail);

                                            tmpParams.Add(string.Format(tempMsg));

                                            break;
                                        case "20052":
                                            //eRack water level full
                                            //Subject content.
                                            tmpParams.Add(string.Format("e-Rack {0} water level Full, Setup Alert", rtdAlarms.UnitID));
                                            //target.
                                            tmpParams.Add(rtdAlarms.UnitID);
                                            //Type.
                                            tmpParams.Add(rtdAlarms.UnitType);
                                            //Body
                                            tempMsg = string.Format(@"UnitType: {0}
UnitID: {1}
Code: {2}
Cause: {3}
Last lot: {4}
SubCode: {5}
Detail: {6}", rtdAlarms.UnitType, rtdAlarms.UnitID, rtdAlarms.Code, rtdAlarms, "", rtdAlarms.SubCode, rtdAlarms.Detail);

                                            tmpParams.Add(string.Format(tempMsg));
                                            break;
                                        case "20053":
                                            //eRack water level full
                                            //Subject content.
                                            tmpParams.Add(string.Format("e-Rack {0} water level Full, Setup Alert", rtdAlarms.UnitID));
                                            //target.
                                            tmpParams.Add(rtdAlarms.UnitID);
                                            //Type.
                                            tmpParams.Add(rtdAlarms.UnitType);
                                            //Body
                                            tempMsg = string.Format(@"UnitType: {0}
UnitID: {1}
Code: {2}
Cause: {3}
Last lot: {4}
SubCode: {5}
Detail: {6}", rtdAlarms.UnitType, rtdAlarms.UnitID, rtdAlarms.Code, rtdAlarms, "", rtdAlarms.SubCode, rtdAlarms.Detail);

                                            tmpParams.Add(string.Format(tempMsg));
                                            break;
                                        case "30100":
                                            //MCS Serious Issue. can not connect
                                            //Subject content.
                                            tmpParams.Add(string.Format("MCS Serious Issue, turn off mcs."));
                                            //target.
                                            tmpParams.Add(rtdAlarms.UnitID);
                                            //Type.
                                            tmpParams.Add(rtdAlarms.UnitType);
                                            //Body
                                            tempMsg = string.Format(@"MCS been disabled 1 minutes. over 1 minutes will auto turn on.");

                                            tmpParams.Add(string.Format(tempMsg));

                                            sql = _BaseDataService.ChangeMCSStatus("mcsstate", false);
                                            _dbTool.SQLExec(sql, out tempMsg, false);

                                            break;
                                        default:
                                            tmpParams.Add(string.Format("TSC Alarm, the toolid [{0}] get alarm [{1}].", _tempPortID, rtdAlarms.Code));
                                            //target.
                                            tmpParams.Add(rtdAlarms.UnitID);
                                            //Type.
                                            tmpParams.Add(rtdAlarms.UnitType);
                                            //Body
                                            tempMsg = string.Format(@"Please check the tool ID [{0}] get the {1} : {2} now.", _tempPortID, rtdAlarms.Code, rtdAlarms.Cause);

                                            tmpParams.Add(string.Format(tempMsg));
                                            break;
                                    }

                                    /* 上面3個參數分別是發件人地址（可以隨便寫），發件人姓名，編碼*/
                                    JcetAlarmMsg.Subject = tmpParams[0];//郵件標題
                                    JcetAlarmMsg.SubjectEncoding = Encoding.UTF8;//郵件標題編碼
                                    JcetAlarmMsg.Body = tmpParams[3]; //郵件內容
                                    JcetAlarmMsg.BodyEncoding = Encoding.UTF8;//郵件內容編碼 
                                    JcetAlarmMsg.IsBodyHtml = true;//是否是HTML郵件 

                                    if (JsonJcetAlarm.eMail)
                                    {
                                        _currentStep = "JsonJcetAlarm.eMail";
                                        string _alarmBy = "";
                                        string _tmpKey = "";
                                        ///寄送Mail
                                        try
                                        {
                                            JcetAlarmMsg = new MailMessage();

                                            _alarmBy = configuration[string.Format("MailSetting:AlarmBy")];
                                            if (_alarmBy.ToUpper().Equals("ALARMBYWORKGROUP"))
                                            {
                                                //string _tempEqpID = JsonJcetParams.EquipID;
                                                //string _tempPortID = JsonJcetParams.PortID;

                                                //rtdAlarms.CommandID
                                                sql = _BaseDataService.QueryPortInfobyPortID(_tempPortID);
                                                dtTemp = _dbTool.GetDataTable(sql);
                                                if (dtTemp.Rows.Count > 0)
                                                {
                                                    _tmpKey = dtTemp.Rows[0]["workgroup"].ToString().Trim();
                                                    if (configuration[string.Format("MailSetting:{0}:{1}", _alarmBy, _tmpKey)] is null)
                                                    {
                                                        //no set send to default alarm mail
                                                        JcetAlarmMsg.To.Add(configuration["MailSetting:AlarmMail"]);
                                                    }
                                                    else
                                                    {
                                                        if (configuration[string.Format("MailSetting:{0}:{1}", _alarmBy, _tmpKey)].Contains(","))
                                                        {
                                                            string[] lsMail = configuration[string.Format("MailSetting:{0}:{1}", _alarmBy, _tmpKey)].Split(',');
                                                            foreach (string theMail in lsMail)
                                                            {
                                                                JcetAlarmMsg.To.Add(theMail.Trim());
                                                            }
                                                        }
                                                        else
                                                        {
                                                            JcetAlarmMsg.To.Add(configuration[string.Format("MailSetting:{0}:{1}", _alarmBy, _tmpKey)]);
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    tmpMsg = string.Format("[{0}][{1}][{2}]", "QueryPortInfobyPortID", _tempPortID, configuration["MailSetting:AlarmMail"]);
                                                    _logger.Info(tmpMsg);
                                                    JcetAlarmMsg.To.Add(configuration["MailSetting:AlarmMail"]);
                                                }
                                            }
                                            else
                                            {
                                                if (configuration[string.Format("MailSetting:{0}:{1}", _alarmBy, AlarmCode)] is null)
                                                {
                                                    //no set send to default alarm mail
                                                    if (configuration["MailSetting:AlarmMail"].Contains(","))
                                                    {
                                                        string[] lsMail = configuration["MailSetting:AlarmMail"].Split(',');
                                                        foreach (string theMail in lsMail)
                                                        {
                                                            JcetAlarmMsg.To.Add(theMail.Trim());
                                                        }
                                                    }
                                                    else
                                                    {
                                                        JcetAlarmMsg.To.Add(configuration["MailSetting:AlarmMail"]);
                                                    }
                                                }
                                                else
                                                {
                                                    if (configuration[string.Format("MailSetting:{0}:{1}", _alarmBy, AlarmCode)].Contains(","))
                                                    {
                                                        string[] lsMail = configuration[string.Format("MailSetting:{0}:{1}", _alarmBy, AlarmCode)].Split(',');
                                                        foreach (string theMail in lsMail)
                                                        {
                                                            JcetAlarmMsg.To.Add(theMail.Trim());
                                                        }
                                                    }
                                                    else
                                                    {
                                                        JcetAlarmMsg.To.Add(configuration[string.Format("MailSetting:{0}:{1}", _alarmBy, AlarmCode)]);
                                                    }
                                                }
                                            }

                                            //msg.To.Add("b@b.com");可以發送給多人
                                            //msg.CC.Add("c@c.com");
                                            //msg.CC.Add("c@c.com");可以抄送副本給多人 
                                            //這裡可以隨便填，不是很重要
                                            JcetAlarmMsg.From = new MailAddress(configuration["MailSetting:username"], configuration["MailSetting:EntryBy"], Encoding.UTF8);
                                            /* 上面3個參數分別是發件人地址（可以隨便寫），發件人姓名，編碼*/
                                            JcetAlarmMsg.Subject = tmpParams[0];//郵件標題
                                            JcetAlarmMsg.SubjectEncoding = Encoding.UTF8;//郵件標題編碼
                                            JcetAlarmMsg.Body = tmpParams[3]; //郵件內容
                                            JcetAlarmMsg.BodyEncoding = Encoding.UTF8;//郵件內容編碼 
                                            JcetAlarmMsg.IsBodyHtml = true;//是否是HTML郵件 

                                            //tmpMsg = string.Format("{0}{1}", tmpAlarmMsg.Subject, tmpAlarmMsg.Body);

                                            MailController MailCtrl = new MailController();
                                            MailCtrl.Config = configuration;
                                            MailCtrl.Logger = _logger;
                                            MailCtrl.DB = _dbTool;
                                            MailCtrl.MailMsg = JcetAlarmMsg;

                                            MailCtrl.SendMail();

                                            tmpMsg = string.Format("SendMail: [{0}], [{1}]", JcetAlarmMsg.Subject, JcetAlarmMsg.Body);
                                            _logger.Info(tmpMsg);
                                        }
                                        catch (Exception ex)
                                        {
                                            tmpMsg = String.Format("SendMail failed. [Exception]: {0}", ex.Message);
                                            _logger.Debug(tmpMsg);
                                        }
                                    }

                                    if (JsonJcetAlarm.SMS)
                                    {
                                        _currentStep = "JsonJcetAlarm.SMS";
                                        //tmpMsg = string.Format("{0}{1}", JcetAlarmMsg.Subject, JcetAlarmMsg.Body);

                                        ///發送SMS 
                                        try
                                        {
                                            tmpMsg = "";
                                            sql = string.Format(_BaseDataService.InsertSMSTriggerData(tmpParams[1], tmpParams[2], tmpParams[0], "N", configuration["MailSetting:EntryBy"]));
                                            tmpMsg = string.Format("Send SMS: SQLExec[{0}]", sql);
                                            _logger.Info(tmpMsg);
                                            _dbTool.SQLExec(sql, out tmpMsg, true);
                                        }
                                        catch (Exception ex)
                                        {
                                            tmpMsg = String.Format("Insert SMS trigger data failed. [Exception]: {0}", ex.Message);
                                            _logger.Debug(tmpMsg);
                                        }
                                    }

                                    if (JsonJcetAlarm.action)
                                    {
                                        _currentStep = "JsonJcetAlarm.action";
                                        tmpMsg = string.Format("{0}{1}", JcetAlarmMsg.Subject, JcetAlarmMsg.Body);

                                        string scenario = JsonObject.Property("scenario") == null ? "Shutdown" : JsonObject["scenario"].ToString();
                                        if (scenario.Equals("Shutdown") || JsonJcetAlarm.scenario.Equals("Shutdown"))
                                        {
                                            string webServiceMode = "soap11";
                                            string webUrl = "http://scscimapp006.jcim-sg.jsg.jcetglobal.com/C10_RTD_API";

                                            JCETWebServicesClient jcetWebServiceClient = new JCETWebServicesClient();
                                            JCETWebServicesClient.ResultMsg resultMsg = new JCETWebServicesClient.ResultMsg();
                                            JObject oResp = null;
                                            WebServiceResponse webServiceResp;

                                            try
                                            {
                                                //jcetWebServiceClient = _logger;

                                                webUrl = configuration["WebService:CIMAPP"] is null ? "http://scscimapp006.jcim-sg.jsg.jcetglobal.com/C10_RTD_API" : configuration["WebService:CIMAPP"];

                                                jcetWebServiceClient._url = ""; //CIMAPP006 會依function自動生成url
                                                resultMsg = new JCETWebServicesClient.ResultMsg();
                                                apiFunc = "rtsdown";
                                                apiFormat = configuration[string.Format("WebService:CIMAPIFormat:{0}", apiFunc.ToUpper())] is null ? "" : configuration[string.Format("WebService:CIMAPIFormat:{0}", apiFunc.ToUpper())];
                                                resultMsg = jcetWebServiceClient.CIMAPP006("rtsdown", webUrl, apiFormat, webServiceMode, JsonJcetAlarm.EquipID, configuration["WebService:username"], configuration["WebService:password"], "lotid");
                                                string result3 = resultMsg.retMessage;
                                                string tmpCert = "";
                                                string resp_Code = "";
                                                string resp_Msg = "";

                                                if (resultMsg.status)
                                                {
                                                    //oResp = JObject.Parse(resultMsg.retMessage);
                                                    webServiceResp = JsonConvert.DeserializeObject<WebServiceResponse>(resultMsg.retMessage);
                                                }
                                                else
                                                {
                                                    tmpMsg = string.Format("An unknown exception occurred in the web service. Please call IT-CIM deportment. [Exception][{0}][{1}]", apiFunc, resultMsg.retMessage);
                                                    _logger.Debug(tmpMsg);
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                tmpMsg = string.Format("An unknown exception occurred in the web service. Please call IT-CIM deportment. [Exception][{0}][{1}]", apiFunc, ex.Message);
                                                _logger.Debug(tmpMsg);
                                            }
                                        }
                                    }

                                    if (JsonJcetAlarm.repeat)
                                    {
                                        _currentStep = "JsonJcetAlarm.repeat";
                                        tmpMsg = string.Format("{0}{1}", JcetAlarmMsg.Subject, JcetAlarmMsg.Body);
                                    }
                                    else
                                    {
                                        sql = _BaseDataService.UpdateRTDAlarms(true, string.Format("{0},{1},{2},{3},{4}", drTemp["UnitID"].ToString(), drTemp["Code"].ToString(), drTemp["SubCode"].ToString(), drTemp["CommandID"].ToString(), drTemp["Params"].ToString()), "");
                                        _dbTool.SQLExec(sql, out tmpMsg, true);
                                    }
                                } 
                                else
                                {
                                    //沒有設定的直接切換為None new 
                                    sql = _BaseDataService.UpdateRTDAlarms(true, string.Format("{0},{1},{2},{3},{4}", drTemp["UnitID"].ToString(), drTemp["Code"].ToString(), drTemp["SubCode"].ToString(), drTemp["CommandID"].ToString(), drTemp["Params"].ToString()), "");
                                    _dbTool.SQLExec(sql, out tmpMsg, true);
                                }
                            }
                            catch(Exception ex)
                            {
                                result = false;
                                ErrMsg = string.Format("[{0}][{1}][{2}][{3}][{4}][{5}]", "Exception", funcName, "InForeach", _currentStep, idxAlarm, ex.Message);
                            }

                            if (!ErrMsg.Equals(""))
                                _logger.Info(ErrMsg);
                        }

                        result = true;
                    }
                    catch(Exception ex)
                    {
                        result = false;
                        ErrMsg = string.Format("[{0}][{1}][{2}][{3}]", "Exception", funcName, "Foreach",ex.Message);
                    }
                }
                else
                {
                    result = true;
                }
            } catch(Exception ex)
            {
                result = false;
                ErrMsg = string.Format("[{0}][{1}][{2}][{3}]", "Exception", funcName, "OutSide", ex.Message);
            }

            if(!ErrMsg.Equals(""))
                _logger.Info(ErrMsg);

            return result;
        }
        public bool AutoRemoveCommandWhenSentMCSFailed(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, out string tmpMessage)
        {
            bool bResult = false;
            string _funcName = "AutoRemoveCommandWhenSentMCSFailed";
            string sql = "";
            string tmpSql = "";
            string tmpMsg = "";
            string _errMessage = "";
            tmpMessage = "";
            DataTable dt = null;
            DataTable dt2 = null;
            string tableOrder = "";
            string _keyOfEnv = "";
            string _keyRTDEnv = "";

            try
            {
                _keyRTDEnv = _configuration["RTDEnvironment:type"];
                _keyOfEnv = string.Format("RTDEnvironment:commandsTable:{0}", _keyRTDEnv);
                //"RTDEnvironment:commandsTable:PROD"
                tableOrder = _configuration[_keyOfEnv] is null ? "workinprocess_sch" : _configuration[_keyOfEnv];

                sql = _BaseDataService.QueryNoCommandStateOrderOvertime(tableOrder);
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    string lotid;
                    string carrierId;
                    string createDT;
                    string lastModifyDT;
                    string commandId;
                    string GId;
                    string cmdType;
                    string cmdSource;
                    string cmdDest;
                    foreach (DataRow dr in dt.Rows)
                    {
                        lotid = dr["LOTID"].ToString().Equals("") ? "" : dr["LOTID"].ToString();

                        try
                        {
                            sql = _BaseDataService.SelectTableWorkInProcessSchByLotId(lotid, tableOrder);
                            dt2 = _dbTool.GetDataTable(sql);

                            if (dt2.Rows.Count > 0)
                            {
                                carrierId = "";
                                foreach (DataRow dr2 in dt2.Rows)
                                {
                                    try
                                    {
                                        carrierId = dr2["carrierid"].ToString().Trim().Equals("") ? "" : dr2["carrierid"].ToString().Trim();
                                        createDT = dr2["create_dt"].ToString().Trim().Equals("") ? "" : dr2["create_dt"].ToString().Trim();
                                        lastModifyDT = dr2["lastModify_dt"].ToString().Trim().Equals("") ? "" : dr2["lastModify_dt"].ToString().Trim();
                                        commandId = dr2["cmd_id"].ToString().Trim().Equals("") ? "" : dr2["cmd_id"].ToString().Trim();
                                        lotid = dr2["lotid"].ToString().Trim().Equals("") ? "" : dr2["lotid"].ToString().Trim();
                                        GId = dr2["uuid"].ToString().Trim().Equals("") ? "" : dr2["uuid"].ToString().Trim();
                                        cmdType = dr2["cmd_type"].ToString().Trim().Equals("") ? "" : dr2["cmd_type"].ToString().Trim();
                                        cmdSource = dr2["source"].ToString().Trim().Equals("") ? "" : dr2["source"].ToString().Trim();
                                        cmdDest = dr2["dest"].ToString().Trim().Equals("") ? "" : dr2["dest"].ToString().Trim();

                                        if (TimerTool("minutes", createDT) >= 5)
                                        {
                                            tmpMsg = "[{0}] dispatch overtime 5 minutes.[{1}][{2}][{3}][{4}][{5}]";
                                            _errMessage = String.Format(tmpMsg, _funcName, GId, commandId, cmdSource, cmdDest);
                                            _logger.Debug(_errMessage);

                                            sql = _BaseDataService.UpdateTableWorkInProcessSchHisByUId(GId);
                                            _dbTool.SQLExec(sql, out tmpMsg, true);
                                            if (!tmpMsg.Equals(""))
                                            {
                                                tmpMsg = "[{0}] WorkinProcessSch Clean Failed : {1}";
                                                _errMessage = String.Format(tmpMsg, _funcName, tmpMsg);
                                                _logger.Debug(_errMessage);
                                                continue;
                                            }

                                            _dbTool.SQLExec(_BaseDataService.DeleteWorkInProcessSchByCmdId(commandId, tableOrder), out tmpMsg, true);

                                            sql = _BaseDataService.DeleteWorkInProcessSchByGId(GId, tableOrder);
                                            _dbTool.SQLExec(sql, out tmpMsg, true);

                                            if (!tmpMsg.Equals(""))
                                            {
                                                tmpMsg = "[{0}] WorkinProcessSch Clean Failed : {1}";
                                                _errMessage = String.Format(tmpMsg, _funcName, tmpMsg);
                                                _logger.Debug(_errMessage);
                                                continue;
                                            }
                                            else
                                            {
                                                tmpMsg = "[{0}] Dispatch to MCS overtime. Auto Clean {1}. retry!";
                                                _errMessage = String.Format(tmpMsg, _funcName, commandId);
                                                _logger.Debug(_errMessage);
                                            }

                                            if (!carrierId.Equals(""))
                                            {
                                                sql = _BaseDataService.UpdateTableReserveCarrier(carrierId, false);
                                                _dbTool.SQLExec(sql, out tmpMsg, true);

                                                if (!tmpMsg.Equals(""))
                                                {
                                                    tmpMsg = "[{0}] UpdateTableReserveCarrier : {1}";
                                                    _errMessage = String.Format(tmpMsg, _funcName, tmpMsg);
                                                    _logger.Debug(_errMessage);
                                                }
                                            }

                                            switch (cmdType.ToUpper())
                                            {
                                                case "LOAD":
                                                    sql = _BaseDataService.LockEquipPortByPortId(cmdDest, false);
                                                    _dbTool.SQLExec(sql, out tmpMsg, true);

                                                    if (!tmpMsg.Equals(""))
                                                    {
                                                        tmpMsg = "[{0}] Unlock Equipment port. {1}";
                                                        _errMessage = String.Format(tmpMsg, _funcName, tmpMsg);
                                                        _logger.Debug(_errMessage);
                                                    }
                                                    break;
                                                case "UNLOAD":
                                                    sql = _BaseDataService.LockEquipPortByPortId(cmdSource, false);
                                                    _dbTool.SQLExec(sql, out tmpMsg, true);

                                                    if (!tmpMsg.Equals(""))
                                                    {
                                                        tmpMsg = "[{0}] Unlock Equipment port. {1}";
                                                        _errMessage = String.Format(tmpMsg, _funcName, tmpMsg);
                                                        _logger.Debug(_errMessage);
                                                    }
                                                    break;
                                                default:
                                                    break;
                                            }
                                        }
                                        else if (TimerTool("minutes", createDT) >= 3)
                                        {
                                            sql = _BaseDataService.UpdateTableWorkInProcessSchHisByUId(GId);
                                            _dbTool.SQLExec(sql, out tmpMsg, true);
                                            if (!tmpMsg.Equals(""))
                                            {
                                                tmpMsg = "[{0}] WorkinProcessSch Clean Failed. {1}";
                                                _errMessage = String.Format(tmpMsg, _funcName, tmpMsg);
                                                _logger.Debug(_errMessage);
                                                continue;
                                            }

                                            sql = _BaseDataService.UnlockOrderForRetry(commandId, tableOrder);
                                            _dbTool.SQLExec(sql, out tmpMsg, true);

                                            if (!tmpMsg.Equals(""))
                                            {
                                                tmpMsg = "[{0}] Unlock Order Failed. {1}";
                                                _errMessage = String.Format(tmpMsg, _funcName, tmpMsg);
                                                _logger.Debug(_errMessage);
                                                continue;
                                            }
                                            else
                                            {
                                                tmpMsg = "[{0}] Dispatch to MCS overtime. Auto retry [{1}]!";
                                                _errMessage = String.Format(tmpMsg, _funcName, commandId);
                                                _logger.Debug(_errMessage);
                                            }
                                        }

                                    }
                                    catch(Exception ex)
                                    {
                                        tmpMsg = "[{0}] Exception: Delete [{1}] Failed!";
                                        _errMessage = String.Format(tmpMsg, _funcName, carrierId);
                                        _logger.Debug(_errMessage);
                                    }
                                }
                            }
                        }
                        catch(Exception ex)
                        {
                            //pass
                        }
                    }
                    bResult = true;
                }
            }
            catch (Exception ex)
            {
                bResult = false;
                tmpMsg = "[{0}] Unknow Error : {1}";
                _errMessage = String.Format(tmpMsg, _funcName, ex.Message);
                _logger.Debug(_errMessage);
            }
            finally
            {
                if (dt != null)
                    dt.Dispose();
                if (dt2 != null)
                    dt2.Dispose();
            }
            dt = null;
            dt2 = null;

            return bResult;
        }
        public bool AutounlockportWhenNoOrder(DBTool _dbTool, IConfiguration _configuration, ILogger _logger)
        {
            bool bResult = false;
            string sql = "";
            DataTable dt;
            DataTable dt2;
            DataTable dtTemp;
            string _equip = "";
            string _portId = "";
            string _tableOrder = "workinprocess_sch";
            string _lastModifyDt = "";
            string errMsg = "";
            string _funcName = "AutounlockportWhenNoOrder";
            string _portState = "";

            try
            {
                //QueryFurneceEQP
                sql = string.Format(_BaseDataService.QueryIslockPortId());
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        try
                        {
                            _equip = row["equipid"].ToString();
                            _portId = row["port_id"].ToString();
                            _lastModifyDt = row["lastModify_dt"].ToString();
                            _portState = row["port_state"].ToString();

                            dt2 = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSchByEquipPort(_equip, _portId, _tableOrder));
                            if (dt2.Rows.Count <= 0)
                            {
                                //重取lastModify Dt 防止中間有command 產生
                                sql = string.Format(_BaseDataService.SelectTableEQP_Port_SetByPortId(_portId));
                                dtTemp = _dbTool.GetDataTable(sql);

                                if (dtTemp.Rows.Count > 0)
                                {
                                    _lastModifyDt = dtTemp.Rows[0]["lastModify_dt"].ToString();
                                }

                                if (TimerTool("minutes", _lastModifyDt) >= 5)
                                {
                                    _dbTool.SQLExec(_BaseDataService.LockEquipPortByPortId(_portId, false), out errMsg, true);

                                    if (!errMsg.Equals(""))
                                    {
                                        _logger.Info(string.Format("[{0}][{1}][Unlock Fail][{2}][{3}]", _funcName, _portId, _lastModifyDt, _portState));
                                    }
                                    else
                                    {
                                        _logger.Info(string.Format("[{0}][{1}][Auto Unlock][{2}][{3}]", _funcName, _portId, _lastModifyDt, _portState));
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { }
                    }
                }
            }
            catch (Exception ex)
            { }

            return bResult;
        }
        public bool TransferCarrierToSideWH(DBTool _dbTool, IConfiguration _configuration, ConcurrentQueue<EventQueue> _eventQueue, ILogger _logger)
        {
            bool bResult = false;
            string tmpMsg = "";
            string sql = "";
            DataTable dtTemp = null;
            DataTable dtTemp2 = null;
            DataTable dtTemp3 = null;
            DataTable dtTemp4 = null;
            EventQueue _eventQ = new EventQueue();
            string funcName = "MoveCarrier";
            string eventName = "TransferCarrierToSideWH";
            TransferList transferList = new TransferList();
            string tableName = "";
            string tableOrder = "";
            string carrierId = "";
            string _keyOfEnv = "";
            string _keyRTDEnv = "";

            string _targetpoint = "";
            string _currentLocate = "";
            string _destStage = "";
            string _eqpWorkgroup = "";
            string _lotID = "";
            string _adsTable = "";

            string _adsStage = "";
            string _adsPkg = "";
            string _sideWarehouse = "";
            string _sector = "";
            string[] _sectorlist;

            int _processQty = 0;
            int _loadportQty = 0;
            int _pretransferNow = 0;
            int _preparecarrierForSideWH = 0;
            int _preparesettingforSideWh = 0;
            bool isFurnace = false;
            string _lotStage = "";
            string _keywork = "SideWH";
            int _calcPreransferQty = 0;

            try
            {
                _keyRTDEnv = _configuration["RTDEnvironment:type"];
                _keyOfEnv = string.Format("RTDEnvironment:commandsTable:{0}", _keyRTDEnv);
                //"RTDEnvironment:commandsTable:PROD"
                tableOrder = _configuration[_keyOfEnv] is null ? "workinprocess_sch" : _configuration[_keyOfEnv];
                tableName = _configuration["PreDispatchToErack:lotState:tableName"] is null ? "lot_Info" : _configuration["PreDispatchToErack:lotState:tableName"];

                _adsTable = _configuration["CheckLotStage:Table"] is null ? "lot_Info" : _configuration["CheckLotStage:Table"];

                if (_keyRTDEnv.ToUpper().Equals("PROD"))
                    sql = _BaseDataService.QueryTransferListForSideWH(tableName);
                else if (_keyRTDEnv.ToUpper().Equals("UAT"))
                    sql = _BaseDataService.QueryTransferListForSideWH(tableName);
                else
                    _logger.Debug(string.Format("RTDEnvironment setting failed. current set is [{0}]", _keyRTDEnv));

                dtTemp = _dbTool.GetDataTable(sql);

                if (dtTemp.Rows.Count > 0)
                {
                    _logger.Info(string.Format("[{0}][{1}][{2}]", _keywork, funcName, dtTemp.Rows.Count));

                    foreach (DataRow dr in dtTemp.Rows)
                    {
                        _currentLocate = dr["locate"].ToString().Equals("") ? "*" : dr["locate"].ToString();
                        _destStage = dr["stage"].ToString().Equals("") ? "NA" : dr["stage"].ToString();
                        _eqpWorkgroup = dr["workgroup"].ToString().Equals("") ? "NA" : dr["workgroup"].ToString();
                        _lotStage = dr["lotstage"].ToString().Equals("") ? "NA" : dr["lotstage"].ToString();

                        //isFurnace = dr["isFurnace"].ToString().Equals("1") ? true : false;
                        ///check is lot locate in SideWarehouse
                        /////workgroup , stage, SideWarehouse
                        _processQty = 0;
                        _loadportQty = 0;

                        _sideWarehouse = dr["SideWarehouse"].ToString().Equals("") ? "*" : dr["SideWarehouse"].ToString();

                        if (_sideWarehouse.Equals("*"))
                            continue;

                        try
                        {
                            _logger.Info(string.Format("[{0}][{1}][{2}]", _keywork, "SideWarehouse", _sideWarehouse));

                            dtTemp3 = null;
                            sql = _BaseDataService.QueryRackByGroupID(_sideWarehouse);
                            dtTemp3 = _dbTool.GetDataTable(sql);
                        }
                        catch (Exception ex) { }

                        if (dtTemp3.Rows.Count > 0)
                        {
                            try
                            {
                                _targetpoint = dtTemp3.Rows[0]["erackID"].ToString();
                                _sector = dtTemp3.Rows[0]["sector"].ToString().Replace("\"", "").Replace("}", "").Replace("{", "");

                                _logger.Info(string.Format("[{0}][{1}][{2}]", _keywork, _targetpoint, _sector));
                            }
                            catch (Exception ex)
                            {
                                _logger.Info(string.Format("[{0}][{1}][{2}]", _keywork, "EXCEPTION", ex.Message));
                            }
                        }
                        else
                        {
                            _logger.Info(string.Format("[{0}][{1}][{2}]", _keywork, "QueryRackByGroupID", dtTemp3.Rows.Count));
                            continue;
                        }

                        try
                        {
                            //Get setting from workgroup set
                            sql = _BaseDataService.QueryWorkgroupSet(_eqpWorkgroup, _destStage);
                            dtTemp3 = _dbTool.GetDataTable(sql);
                            if (dtTemp3.Rows.Count > 0)
                            {
                                _preparesettingforSideWh = int.Parse(dtTemp3.Rows[0]["preparecarrierForSideWH"].ToString());
                            }

                            //calculate process qty by stage
                            if (isFurnace)
                            {
                                _logger.Info(string.Format("[{0}][{1}][{2}]", _keywork, "preparesettingforSideWh", _preparesettingforSideWh));
                                //get all carrier locate 
                                //when locate and portno are met,  +1
                                //get all dest in workinprocess sch
                                //when dest is _targetpoint +1
                                int _vfcstatus = 0;
                                string _equipment = "";
                                string _portNo = "";

                                sql = _BaseDataService.GetEqpInfoByWorkgroupStage(_eqpWorkgroup, _destStage, _lotStage);
                                dtTemp3 = _dbTool.GetDataTable(sql);
                                if (dtTemp3.Rows.Count > 0)
                                {
                                    _vfcstatus = int.Parse(dtTemp3.Rows[0]["FVCSTATUS"].ToString());
                                    _equipment = dtTemp3.Rows[0]["EQPID"].ToString();

                                    _logger.Info(string.Format("[{0}][{1}][{2}]", _keywork, "_vfcstatus", _vfcstatus));

                                    //sent batch checkin before
                                    if (!_vfcstatus.Equals(0))
                                        continue;

                                    //string[] _contantSecter = 
                                    _logger.Info(string.Format("[{0}][{1}][{2}]", _keywork, "_sector", _sector));
                                    if (!_sector.Trim().Equals(""))
                                        _sectorlist = _sector.Split(':')[1].Split(',');
                                    else
                                        _sectorlist = _sector.Split(':');

                                    sql = _BaseDataService.GetCarrierByLocate(_targetpoint);
                                    dtTemp2 = _dbTool.GetDataTable(sql);
                                    if (dtTemp2.Rows.Count > 0)
                                    {
                                        foreach (DataRow drCarrier in dtTemp2.Rows)
                                        {
                                            _portNo = drCarrier["portno"].ToString();

                                            if (((IList)_sectorlist).Contains(_portNo))
                                                _processQty++;
                                        }

                                        _logger.Info(string.Format("[{0}][{1}][{2}]", _keywork, "_processQty", _processQty));
                                    }
                                }
                            }
                            else
                            {
                                //side warehouse logic for normally workgroup 
                                sql = _BaseDataService.CalculateProcessQtyByStage(_eqpWorkgroup, _destStage, _lotStage);
                                dtTemp3 = _dbTool.GetDataTable(sql);
                                if (dtTemp3.Rows.Count > 0)
                                {
                                    _processQty = int.Parse(dtTemp3.Rows[0]["processQty"].ToString());
                                    _logger.Info(string.Format("[{0}][{1}][{2}][{3}][{4}][{5}]", _keywork, "_processQty", _processQty, _eqpWorkgroup, _destStage, _lotStage));
                                }
                            }

                            //calculate loadport qty by stage
                            sql = _BaseDataService.CalculateLoadportQtyByStage(_eqpWorkgroup, _destStage, _lotStage);
                            dtTemp3 = _dbTool.GetDataTable(sql);
                            if (dtTemp3.Rows.Count > 0)
                            {
                                _loadportQty = int.Parse(dtTemp3.Rows[0]["totalportqty"].ToString());
                                _preparecarrierForSideWH = _loadportQty * _preparesettingforSideWh;
                                _logger.Info(string.Format("[{0}][{1}][{2}]", _keywork, "_loadportQty", _loadportQty));
                                _logger.Info(string.Format("[{0}][{1}][{2}]", _keywork, "_preparecarrierForSideWH", _preparecarrierForSideWH));
                            }
                        }
                        catch (Exception ex) { }

                        if (_processQty + _calcPreransferQty >= _preparecarrierForSideWH)
                            continue;

                        transferList = new TransferList();
                        carrierId = "";

                        carrierId = dr["carrier_ID"].ToString().Equals("") ? "*" : dr["carrier_ID"].ToString();

                        sql = _BaseDataService.CheckPreTransfer(carrierId, tableOrder);
                        dtTemp3 = _dbTool.GetDataTable(sql);
                        if (dtTemp3.Rows.Count > 0)
                        {
                            _logger.Info(string.Format("[{0}][{1}][{2}]", _keywork, "CheckPreTransfer", carrierId));
                            continue;
                        }

                        _logger.Info(string.Format("[{0}][{1}][{2}]", _keywork, "Create Transfer", carrierId));

                        //20240202 Add midways logic for pre-transfer
                        _eqpWorkgroup = dr["workgroup"].ToString().Equals("") ? "" : dr["workgroup"].ToString();
                        _lotID = dr["lot_ID"].ToString().Equals("") ? "*" : dr["lot_ID"].ToString();

                        _eventQ = new EventQueue();
                        _eventQ.EventName = funcName;

                        transferList.CarrierID = carrierId;
                        transferList.LotID = dr["lot_ID"].ToString().Equals("") ? "*" : dr["lot_ID"].ToString();
                        transferList.Source = "*";
                        transferList.Dest = _sideWarehouse;
                        transferList.CommandType = "Pre-Transfer";
                        transferList.CarrierType = dr["CarrierType"].ToString();

                        try
                        {
                            _adsStage = "";
                            _adsPkg = "";
                            tmpMsg = "";
                            dtTemp4 = null;
                            sql = _BaseDataService.QueryDataByLot(tableName, _lotID);
                            dtTemp4 = _dbTool.GetDataTable(sql);

                            if (dtTemp4.Rows.Count > 0)
                            {
                                _adsStage = dtTemp4.Rows[0]["stage"].ToString();
                                _adsPkg = dtTemp4.Rows[0]["pkgfullname"].ToString();

                                //log ads information for debug 20240313
                                tmpMsg = string.Format("[{0}][{1}][{2}][{3}][ADS: {4} / {5}]", "Pre-Transfer", transferList.LotID, transferList.CarrierID, transferList.Dest, _adsStage, _adsPkg);
                            }
                            else
                            {
                                tmpMsg = string.Format("[{0}][{1}][{2}][{3}][ADS: No Data]", "Pre-Transfer", transferList.LotID, transferList.CarrierID, transferList.Dest);
                            }
                            _logger.Info(tmpMsg);
                        }
                        catch (Exception ex)
                        {
                            tmpMsg = string.Format("[{0}][{1}][{2}]", "Exception", "Pre-Transfer", transferList.LotID);
                            _logger.Info(tmpMsg);
                        }

                        tmpMsg = string.Format("[{0}][{1} / {2} / {3} / {4} / {5} / {6}]", funcName, transferList.CommandType, transferList.LotID, transferList.CarrierID, transferList.Source, transferList.Dest, transferList.CarrierType);
                        _eventQ.EventObject = transferList;
                        _eventQueue.Enqueue(_eventQ);

                        _calcPreransferQty++;
                    }
                }

                bResult = true;
            }
            catch (Exception ex)
            {
                _logger.Debug(string.Format("TransferCarrierToSideWH Unknow Error. [Exception: {0}]", ex.Message));
            }
            finally
            {
                if (dtTemp != null)
                    dtTemp.Dispose();
                if (dtTemp2 != null)
                    dtTemp2.Dispose();
                if (dtTemp3 != null)
                    dtTemp3.Dispose();
            }
            dtTemp = null;
            dtTemp2 = null;
            dtTemp3 = null;

            return bResult;
        }
        public EQPLastWaferTime GetLastWaferTimeByEQP(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, string _equip, string _lotid)
        {
            EQPLastWaferTime _lastWaferTime = new EQPLastWaferTime();
            string _funcName = "GetLastWaferTimeByEQP";
            string sql = "";
            DataTable dt;
            DataTable dt2;
            DataTable dtTemp;
            string _lastModifyDt = "";
            string errMsg = "";
            float _avgMRprocessingTime = 0;
            string _tmpMsg = "";
            float _avgScale = 1;

            try
            {
                _lastWaferTime.Hours = 0;
                _lastWaferTime.Minutes = 0;
                _avgScale = 1;

                sql = string.Format(_BaseDataService.QueryScale(""));
                dt = _dbTool.GetDataTable(sql);
                if(dt.Rows.Count > 0)
                {
                    _avgScale = float.Parse(dt.Rows[0]["paramvalue"].ToString());
                }

                if (!_equip.Equals(""))
                    _lastWaferTime.EquipID = _equip;

                if (!_lotid.Equals(""))
                    _lastWaferTime.LotID = _lotid;

                _avgMRprocessingTime = GetMRProcessTime(_dbTool, _configuration, _logger, _equip);

                sql = string.Format(_BaseDataService.GetAvgProcessingTime(_equip, _lotid));
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    _lastWaferTime.Hours = float.Parse(dt.Rows[0]["avgHours"].ToString());
                    _lastWaferTime.Minutes = float.Parse(dt.Rows[0]["avgMinutes"].ToString()) - (_avgMRprocessingTime * _avgScale);
                }

                _logger.Info(string.Format("Avg last wafer time: [{0}] hours [{1}] minutes/ MR Processing: [{2}] minutes  [Equipment ID: {3} | Lot ID: {4} | Scale: {5}]", _lastWaferTime.Hours, _lastWaferTime.Minutes, _avgMRprocessingTime.ToString(), _equip, _lotid, _avgScale));
            }
            catch (Exception ex)
            { }

            return _lastWaferTime;
        }
        public string TryConvertDatetime(string _datetime)
        {
            string value = "";
            DateTime _tmpDate;

            try
            {

                DateTime.TryParse(_datetime, out _tmpDate);
                value = _tmpDate.ToString("yyyy/MM/dd HH:mm:ss");
            }
            catch (Exception ex) { }

            return value;
        }
        public string GetJArrayValue(JObject _JArray, string key)
        {
            string value = "";
            //foreach (JToken item in _JArray.Children())
            //{
            //var itemProperties = item.Children<JProperty>();
            //If the property name is equal to key, we get the value
            //var myElement = itemProperties.FirstOrDefault(x => x.Name == key.ToString());
            //value = myElement.Value.ToString(); //It run into an exception here because myElement is null
            //break;
            //}
            try
            {
                if (_JArray.TryGetValue(key, out JToken makeToken))
                {
                    value = (string)makeToken;
                }
            }
            catch (Exception ex) { }

            return value;
        }
        public bool CheckMCSStatus(DBTool _dbTool, ILogger _logger)
        {
            bool bResult = false;
            string sql = "";
            DataTable dt;
            DataTable dt2;
            DataTable dtTemp;
            bool _bMCSStatus = false;
            string _strLastDatetime;
            string _errMsg = "";
            string _funcName = "CheckMCSStatus";

            try
            {

                //QueryFurneceEQP
                
                sql = _BaseDataService.QueryMCSStatus("mcsstate");
                dt = _dbTool.GetDataTable(sql);
                
                if (dt.Rows.Count > 0)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        try
                        {
                            _bMCSStatus = row["paramvalue"].ToString().Equals("1") ? true :false;
                            _strLastDatetime = row["lastModify_dt"].ToString();

                            if(_bMCSStatus)
                            {
                                //send command to mcs
                            }
                            else
                            {
                                //大於1分鐘, 再嘗試送
                                if (TimerTool("minutes", _strLastDatetime) >= 1)
                                {
                                    sql = _BaseDataService.ChangeMCSStatus("mcsstate", true);
                                    _dbTool.SQLExec(sql, out _errMsg, true);

                                    _logger.Info(string.Format("[{0}][{1}][{2}][{3}]", _funcName, "mcsstate", _strLastDatetime, "1 minutes"));
                                }
                            }
                        }
                        catch (Exception ex) { }
                    }
                }
                
            }
            catch (Exception ex)
            { }

            return _bMCSStatus;
        }

        public bool Heartbeat(DBTool _dbTool, IConfiguration _configuration, ILogger _logger)
        {
            bool bResult = false;
            string _sql = "";
            DataTable dt;
            DataTable dtTemp;
            DataTable dtTemp2;
            bool _bHeartbeat = false;
            string _responseTime;
            string _errMsg = "";
            string tmpMsg = "";
            string _funcName = "Heartbeat";
            string RTDServerName = "";

            try
            {
                RTDServerName = _configuration["AppSettings:Server"] is not null ? _configuration["AppSettings:Server"] : "RTDServer";
                _sql = _BaseDataService.UadateRTDServer(RTDServerName);
                _dbTool.SQLExec(_sql, out tmpMsg, true);

                if(tmpMsg.Equals(""))
                    _bHeartbeat = true;
            }
            catch (Exception ex)
            { _bHeartbeat = false; }

            return _bHeartbeat;
        }
        public float GetMRProcessTime(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, string _equip)
        {
            float _spendTime = 0;
            string _funcName = "GetMRProcessTime";
            string sql = "";
            DataTable dt;
            DataTable dt2;
            DataTable dtTemp;
            string tmpMsg = "";

            try
            {
                _spendTime = 0;
                
                if (!_equip.Equals(""))
                {
                    sql = string.Format(_BaseDataService.GetMRProcessingTime(_equip));
                    dt = _dbTool.GetDataTable(sql);

                    if (dt.Rows.Count > 0)
                    {
                        _spendTime = float.Parse(dt.Rows[0]["avgMinute"].ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                tmpMsg = string.Format("Function [{0}] exception: [{1}]", _funcName, ex.Message);
            }

            return _spendTime;
        }
    }
}
