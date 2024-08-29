﻿using Microsoft.Extensions.Configuration;
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
using System.Threading.Tasks;
using System.Xml;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace RTDWebAPI.Service
{
    public class FunctionService : IFunctionService
    {
        public IBaseDataService _BaseDataService = new BaseDataService();

        public bool AutoCheckEquipmentStatus(DBTool _dbTool, out ConcurrentQueue<EventQueue> _evtQueue)
        {

            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            string tmpMsg = "";
            _evtQueue = new ConcurrentQueue<EventQueue>();
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

            return bResult;
        }
        public bool AbnormalyEquipmentStatus(DBTool _dbTool, ILogger _logger, bool DebugMode, ConcurrentQueue<EventQueue> _evtQueue, out List<NormalTransferModel> _lstNormalTransfer)
        {

            DataTable dt = null;
            DataRow[] dr = null;
            string sql = "";
            EventQueue oEventQ = new EventQueue();
            oEventQ.EventName = "AbnormalyEquipmentStatus";
            _lstNormalTransfer = new List<NormalTransferModel>();

            bool bResult = false;
            try
            {
                NormalTransferModel normalTransfer = new NormalTransferModel();
                sql = string.Format(_BaseDataService.SelectEqpStatusWaittoUnload());
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    DataTable dt2 = null;

                    foreach (DataRow dr2 in dt.Rows)
                    {
                        normalTransfer = new NormalTransferModel();

                        sql = string.Format(_BaseDataService.SelectTableWorkInProcessSchByEquipPort(dr2["EQUIPID"].ToString(), dr2["PORT_ID"].ToString()));
                        dt2 = _dbTool.GetDataTable(sql);

                        if (dt2.Rows.Count > 0)
                        {
                            if (dt2.Rows[0]["SOURCE"].ToString().Equals("*"))
                                continue;
                            else
                            {
                                DataTable dt3;
                                sql = string.Format(_BaseDataService.SelectTableWorkInProcessSchByPortId(dr2["PORT_ID"].ToString()));
                                dt3 = _dbTool.GetDataTable(sql);
                                if (dt3.Rows.Count > 0)
                                    continue;
                                dt3 = null;
                            }
                        }


                        normalTransfer.EquipmentID = dr2["EQUIPID"].ToString();
                        normalTransfer.PortModel = dr2["PORT_MODEL"].ToString();

                        dt2 = null;
                        sql = string.Format(_BaseDataService.QueryCarrierByLocate(dr2["EQUIPID"].ToString()));
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
                        normalTransfer = new NormalTransferModel();

                        //sql = string.Format(_BaseDataService.SelectTableWorkInProcessSchByEquip(dr2["EQUIPID"].ToString()));
                        sql = string.Format(_BaseDataService.SelectTableWorkInProcessSchByEquipPort(dr2["EQUIPID"].ToString(), dr2["PORT_ID"].ToString()));
                        dt2 = _dbTool.GetDataTable(sql);

                        if (dt2.Rows.Count > 0)
                        {
                            if (dt2.Rows[0]["SOURCE"].ToString().Equals("*"))
                                continue;
                            else
                            {
                                DataTable dt3;
                                sql = string.Format(_BaseDataService.SelectTableWorkInProcessSchByPortId(dr2["PORT_ID"].ToString()));
                                dt3 = _dbTool.GetDataTable(sql);
                                if (dt3.Rows.Count > 0)
                                    continue;
                                dt3 = null;
                            }
                        }


                        normalTransfer.EquipmentID = dr2["EQUIPID"].ToString();
                        normalTransfer.PortModel = dr2["PORT_MODEL"].ToString();

                        dt2 = null;
                        sql = string.Format(_BaseDataService.QueryCarrierByLocate(dr2["EQUIPID"].ToString()));
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
                        normalTransfer = new NormalTransferModel();

                        sql = string.Format(_BaseDataService.SelectTableWorkInProcessSchByEquipPort(dr2["EQUIPID"].ToString(), dr2["PORT_ID"].ToString()));
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
                        sql = string.Format(_BaseDataService.QueryCarrierByLocateType("ERACK", dr2["EQUIPID"].ToString()));
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

            return bResult;
        }
        public bool CheckLotInfo(DBTool _dbTool, IConfiguration _configuration, ILogger _logger)
        {
            DataTable dt = null;
            DataTable dt2 = null;
            DataRow[] dr = null;
            string sql = "";

            bool bResult = false;
            try
            {
                sql = string.Format(_BaseDataService.SelectTableCheckLotInfo(GetExtenalTables(_configuration, "SyncExtenalData", "AdsInfo")));
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    string sql2 = "";
                    string sqlMsg = "";

                    foreach (DataRow dr2 in dt.Rows)
                    {
                        sql2 = "";
                        sqlMsg = "";

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
                                }
                            }
                            else if (!dr2["OriState"].ToString().Equals("INIT"))
                            {
                                //歸零
                                sql2 = string.Format(_BaseDataService.UpdateTableLotInfoReset(dr2["lotid"].ToString()));
                                _dbTool.SQLExec(sql2, out sqlMsg, true);
                            }
                            else
                            {
                                //增加
                                sql2 = string.Format(_BaseDataService.InsertTableLotInfo(GetExtenalTables(_configuration, "SyncExtenalData", "AdsInfo"), dr2["lotid"].ToString()));
                                _dbTool.SQLExec(sql2, out sqlMsg, true);

                                if (TriggerCarrierInfoUpdate(_dbTool, _configuration, _logger, dr2["lotid"].ToString()))
                                {
                                    //Send InfoUpdate
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
                            }
                        }
                        else if (dr2["State"].ToString().Equals("DELETED"))
                        {
                            if (dr2["OriState"].ToString().Equals("INIT"))
                            {
                                //歸零
                                sql2 = string.Format(_BaseDataService.UpdateTableLotInfoReset(dr2["lotid"].ToString()));
                                _dbTool.SQLExec(sql2, out sqlMsg, true);
                            }
                            else if (dr2["OriState"].ToString().Equals("DELETED"))
                            {
                                if (!dr2["lastmodify_dt"].ToString().Equals(""))
                                {
                                    _dbTool.SQLExec(_BaseDataService.SyncNextStageOfLot(GetExtenalTables(_configuration, "SyncExtenalData", "AdsInfo"), dr2["lotid"].ToString()), out sqlMsg, true);
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
                                _dbTool.SQLExec(_BaseDataService.SyncNextStageOfLot(GetExtenalTables(_configuration, "SyncExtenalData", "AdsInfo"), dr2["lotid"].ToString()), out sqlMsg, true);

                                if (TriggerCarrierInfoUpdate(_dbTool, _configuration, _logger, dr2["lotid"].ToString()))
                                {
                                    //Send InfoUpdate
                                }
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
                        dt2 = _dbTool.GetDataTable(sql);

                        if (dt2.Rows.Count > 0)
                        {
                            if (TimerTool("day", dt2.Rows[0]["lastmodify_dt"].ToString()) <= 1)
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

            return bResult;
        }
        public bool CheckLotEquipmentAssociate(DBTool _dbTool, out ConcurrentQueue<EventQueue> _evtQueue)
        {

            DataTable dt = null;
            string sql = "";
            string tmpMsg = "";
            bool bResult = false;
            bool bReflush = false;
            _evtQueue = new ConcurrentQueue<EventQueue>();

            try
            {
                sql = string.Format(_BaseDataService.SchSeqReflush());
                dt = _dbTool.GetDataTable(sql);

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

                if (!bReflush)
                {
                    sql = _BaseDataService.ReflushWhenSeqZeroStateWait();
                    dt = _dbTool.GetDataTable(sql);
                    if (dt.Rows.Count > 0)
                    {
                        bReflush = true;
                    }
                }

                sql = string.Format(_BaseDataService.ReflushProcessLotInfo());
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
                            if (TimerTool("minutes", dr2["lastmodify_dt"].ToString()) <= 3)
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
                                if (TimerTool("minutes", dr2["lastmodify_dt"].ToString()) <= 10)
                                { continue; }
                                else
                                    bagain = true;
                            }
                            else
                            {
                                if (TimerTool("minutes", dr2["lastmodify_dt"].ToString()) <= 5)
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

            return bResult;
        }
        public bool UpdateEquipmentAssociateToReady(DBTool _dbTool, out ConcurrentQueue<EventQueue> _evtQueue)
        {

            DataTable dt = null;
            string sql = "";

            bool bResult = false;
            _evtQueue = new ConcurrentQueue<EventQueue>();

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
                    iPortNo = !tmpPortNo.Trim().Equals("") ? int.Parse(tmpPortNo.Replace("SP", "")) : 0;
                }

                if (iPortNo > 0)
                {
                    sql = string.Format(_BaseDataService.GetCarrierByLocate(_portId, iPortNo));
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

            return strResult;
        }
        public HttpClient GetHttpClient(IConfiguration _configuration, string _tarMcsSvr)
        {
            HttpClient client = new HttpClient();
            try
            {
                string cfgPath_ip = "";
                string cfgPath_port = "";
                string cfgPath_timeSpan = "";

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
                client.Timeout = TimeSpan.Parse(_configuration[cfgPath_timeSpan]);
                client.DefaultRequestHeaders.Accept.Clear();
            }
            catch (Exception ex)
            {

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

            try
            {

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
                    DataRow[] dr = null;
                    DataRow[] dr2 = null;
                    string sql = "";
                    bool bRetry = false;

                    foreach (string theCmdId in ListCmds)
                    {
                        exceptionCmdId = theCmdId;
                        _logger.Trace(string.Format("[SentDispatchCommandtoMCS]: Command ID [{0}]", theCmdId));
                        //// 查詢資料
                        ///
                        dt = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSchByCmdId(theCmdId));

                        if (dt.Rows.Count <= 0)
                            continue;
                        //已有其它線程正在處理
                        DateTime curDT = DateTime.UtcNow;
                        if (int.Parse(dt.Rows[0]["ISLOCK"].ToString()).Equals(1))
                        {
                            try
                            {
                                string createDateTime = dt.Rows[0]["CREATE_DT"].ToString();
                                string lastDateTime = dt.Rows[0]["LASTMODIFY_DT"].ToString();
                                string tmpCarrierId = dt.Rows[0]["CARRIERID"].ToString();
                                bool bHaveSent = dt.Rows[0]["CMD_CURRENT_STATE"].ToString().Equals("Init") ? true : false;

                                if (bHaveSent)
                                    continue;  //已送至TSC, 不主動刪除。等待TSC回傳結果！或人員自行操作刪除

                                if (!lastDateTime.Equals(""))
                                {
                                    bRetry = false;
                                    curDT = DateTime.UtcNow;
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
                                    else if (Math.Abs(totalSpan.TotalMinutes) > 10)
                                    {
                                        dr = dt.Select("CMD_CURRENT_STATE not in ('Init', 'Running', 'Success')");

                                        if (dr.Length > 0)
                                        {
                                            //嘗試發送, 大於10分鐘仍未送出, 刪除
                                            _dbTool.SQLExec(_BaseDataService.UpdateTableWorkInProcessSchHisByCmdId(theCmdId), out tmpMsg, true);
                                            _dbTool.SQLExec(_BaseDataService.DeleteWorkInProcessSchByCmdId(theCmdId), out tmpMsg, true);

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
                                        dr = dt.Select("CMD_CURRENT_STATE in ('Failed', '')");
                                        if (dr.Length > 0)
                                        {
                                            _dbTool.SQLExec(_BaseDataService.UpdateUnlockWorkInProcessSchByCmdId(theCmdId), out tmpMsg, true);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            { }

                            continue;
                        }
                        else
                            _dbTool.SQLExec(_BaseDataService.UpdateLockWorkInProcessSchByCmdId(theCmdId), out tmpMsg, true);

                        dr = dt.Select("CMD_CURRENT_STATE=''");
                        if (dr.Length <= 0)
                        {
                            sql = _BaseDataService.UpdateTableWorkInProcessSchByCmdId(" ", curDT.ToString("yyyy-M-d hhmmss"), theCmdId);
                            try
                            {
                                curDT = DateTime.UtcNow;
                                dr2 = dt.Select("CMD_CURRENT_STATE='Failed'");
                                if (dr2.Length <= 0)
                                    continue;
                            }
                            catch (Exception ex)
                            {
                                curDT = DateTime.UtcNow;
                                _dbTool.SQLExec(sql, out tmpMsg, true);
                            }
                        }

                        string tmp00 = "{{0}, {1}, {2}, {3}}";
                        string tmp01 = string.Format("\"CommandID\": \"{0}\"", theCmdId);
                        string tmp02 = string.Format("\"Priority\": \"{0}\"", dt.Rows[0]["PRIORITY"].ToString());
                        string tmp03 = string.Format("\"Replace\": \"{0}\"", dt.Rows.Count.ToString());
                        string strTransferCmd = "\"CommandID\": \"" + theCmdId + "\",\"Priority\": " + dt.Rows[0]["PRIORITY"].ToString() + ",\"Replace\": " + dt.Rows[0]["REPLACE"].ToString() + "{0}";
                        string strTransCmd = "";
                        string tmpUid = "";
                        string tmpCarrierID = "";
                        foreach (DataRow tmDr in dt.Rows)
                        {
                            tmpUid = tmDr["UUID"].ToString();

                            if (!strTransCmd.Equals(""))
                            {
                                strTransCmd = strTransCmd + ",";
                            }
                            tmpCarrierID = tmDr["CARRIERID"].ToString().Equals("*") ? "" : tmDr["CARRIERID"].ToString();

                            strTransCmd = strTransCmd + "{" +
                                        "\"CarrierID\": \"" + tmpCarrierID + "\", " +
                                        "\"Source\": \"" + tmDr["SOURCE"].ToString() + "\", " +
                                        "\"Dest\": \"" + tmDr["DEST"].ToString() + "\", " +
                                        "\"LotID\": \"" + tmDr["LotID"].ToString() + "\", " +
                                        "\"Quantity\":" + tmDr["Quantity"].ToString() + ", " +
                                        "\"Total\":" + tmDr["Total"].ToString() + ", " +
                                        "\"CarrierType\": \"" + tmDr["CARRIERTYPE"].ToString() + "\"} ";
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

                        if (response != null)
                        {
                            //不為response, 即表示total加1 || 改成MCS回報後才統計總數
                            if (response.IsSuccessStatusCode)
                            {
                                //需等待回覆後再記錄
                                _logger.Info(string.Format("Info: SendCommand is OK. "));
                                _dbTool.SQLExec(_BaseDataService.UpdateTableWorkInProcessSchHisByCmdId(theCmdId), out tmpMsg, true);
                                _dbTool.SQLExec(_BaseDataService.UpdateTableWorkInProcessSchByCmdId("Initial", DateTime.UtcNow.ToString("yyyy-M-d hh:mm:ss"), theCmdId), out tmpMsg, true);
                                //新增一筆Total Record
                                _dbTool.SQLExec(_BaseDataService.InsertRTDStatisticalRecord(curDT.ToString("yyyy-MM-dd HH:mm:ss"), theCmdId, "T"), out tmpMsg, true);

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
                                _logger.Info(string.Format("Info: SendCommand Failed. "));
                                //傳送失敗, 即計算為Failed
                                //新增一筆Total Record
                                //_dbTool.SQLExec(_BaseDataService.InsertRTDStatisticalRecord(curDT.ToString("yyyy-MM-dd HH:mm:ss"), theCmdId, "F"), out tmpMsg, true);

                                //_dbTool.SQLExec(_BaseDataService.UpdateTableWorkInProcessSchHisByUId(tmpUid), out tmpMsg, true);
                                _dbTool.SQLExec(_BaseDataService.UpdateTableWorkInProcessSchByCmdId("Failed", DateTime.UtcNow.ToString("yyyy-M-d hh:mm:ss"), theCmdId), out tmpMsg, true);
                                _dbTool.SQLExec(_BaseDataService.UpdateUnlockWorkInProcessSchByCmdId(theCmdId), out tmpMsg, true);
                                //Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                                bResult = false;
                                tmpState = "NG";
                                tmpMsg = string.Format("State Code : {0}, Reason : {1}.", response.StatusCode, response.ReasonPhrase);
                                //_logger.Info(string.Format("Info: SendCommand Failed. {0}", tmpMsg));

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
                            tmpMsg = "應用程式呼叫 API 發生異常";
                        }

                        //if(tmpState.Equals("NG"))
                        //_dbTool.SQLExec(_BaseDataService.UpdateUnlockWorkInProcessSchByCmdId(theCmdId), out tmpMsg, true);


                    }

                    //Release Resource
                    client.Dispose();
                    response.Dispose();
                }
            }
            catch (Exception ex)
            {
                //_dbTool.SQLExec(_BaseDataService.UpdateUnlockWorkInProcessSchByCmdId(exceptionCmdId), out tmpMsg, true);
                //需要Alarm to Front UI
                tmpMsg = string.Format("Info: SendCommand Failed. Exception is {0}, ", ex.Message);
                _logger.Info(tmpMsg);
                tmpMsg = "";
                _dbTool.SQLExec(_BaseDataService.UpdateTableWorkInProcessSchByCmdId(" ", DateTime.UtcNow.ToString("yyyy-M-d hh:mm:ss"), exceptionCmdId), out tmpMsg, true);
                if (!tmpMsg.Equals(""))
                    _logger.Info(tmpMsg);
                tmpMsg = "";
                _dbTool.SQLExec(_BaseDataService.UpdateUnlockWorkInProcessSchByCmdId(exceptionCmdId), out tmpMsg, true);
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

            try
            {

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
                        dt = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSchByCmdId(theUuid));
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

            try
            {
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
        public APIResult SentCommandtoMCSByModel(IConfiguration _configuration, ILogger _logger, string _model, List<string> args)
        {
            APIResult foo;
            bool bResult = false;
            string tmpFuncName = "SentCommandtoMCSByModel";
            string tmpState = "";
            string tmpMsg = "";
            string remoteCmd = "None";

            HttpClient client;
            HttpResponseMessage response;

            try
            {
                _logger.Info(string.Format("Run Function [{0}]: Model is {1}", tmpFuncName, _model));
                client = GetHttpClient(_configuration, "");
                // Add an Accept header for JSON format.
                // 為JSON格式添加一個Accept表頭
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                Uri gizmoUri = null;
                string strGizmo = "{ }";

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
                    default:
                        remoteCmd = string.Format("api/{0}", _model);
                        break;
                }

                response = new HttpResponseMessage();

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
            catch (Exception ex)
            {
                _logger.Info(string.Format("Exception [{0}]: ex.Message [{1}]", tmpFuncName, ex.Message));
                foo = new APIResult()
                {
                    Success = false,
                    State = "NG",
                    Message = ex.Message
                };
            }

            _logger.Info(string.Format("[{0}]: Result is {1}, Reason : [{2}]", tmpFuncName, foo.State, foo.Message));

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


            try
            {
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

            try
            {
                DataTable dt = null;
                DataTable dtTemp = null;
                DataRow[] dr = null;// strPortModel

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

                            //檢查狀態 Current Status 需要為UP
                            if (dt.Rows[0]["CURR_STATUS"].ToString().Equals("UP"))
                            {
                                //可用
                                strEquip = dt.Rows[0]["EQUIPID"].ToString();
                            }
                            else if (dt.Rows[0]["CURR_STATUS"].ToString().Equals("PM"))
                            {

                            }
                            else if (dt.Rows[0]["CURR_STATUS"].ToString().Equals("DOWN"))
                            {
                                if (dt.Rows[0]["DOWN_STATE"].ToString().Equals("IDLE"))
                                {

                                }
                                if (dt.Rows[0]["DOWN_STATE"].ToString().Equals("DOWN"))
                                {
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
                                    sql = string.Format(_BaseDataService.SelectTableWorkInProcessSchByEquipPort(Equip, tmpCurrPort));
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
                            _threadControll[strEquip] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                        }
                        else
                        {
                            return bResult;
                        }
                    }
                    else
                    {
                        if (!_threadControll.ContainsKey(strEquip))
                            _threadControll.Add(strEquip, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
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

                        switch (resultCode)
                        {
                            case "1001":
                                //不同客戶後的第一批
                                tmpSmsMsg = string.Format("LOT {0} NEED TO SETUP NOW. EQUIP DOWN FROM RTS.", lotid);

                                /* 上面3個參數分別是發件人地址（可以隨便寫），發件人姓名，編碼*/
                                tmpMailMsg.Subject = "Device Setup Alert";//郵件標題
                                tmpMailMsg.SubjectEncoding = Encoding.UTF8;//郵件標題編碼
                                tmpMailMsg.Body = string.Format("LOT {0} ({1}) NEED TO SETUP NOW. EQUIP DOWN FROM RTS.", lotid, tmpPartId); //郵件內容
                                tmpMailMsg.BodyEncoding = Encoding.UTF8;//郵件內容編碼 
                                tmpMailMsg.IsBodyHtml = true;//是否是HTML郵件 
                                bAlarm = true;
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
                                tmpSmsMsg = string.Format("NEXT LOT {0} NEED TO SETUP AFTER CURRENT LOT END.", tmpNextLot);

                                /* 上面3個參數分別是發件人地址（可以隨便寫），發件人姓名，編碼*/
                                tmpMailMsg.Subject = "Setup Pre-Alert";//郵件標題
                                tmpMailMsg.SubjectEncoding = Encoding.UTF8;//郵件標題編碼
                                tmpMailMsg.Body = string.Format("NEXT LOT {0} ({1}) NEED TO SETUP AFTER CURRENT LOT {2} ({3}) END.", tmpNextLot, tmpNextPartId, lotid, tmpPartId); //郵件內容
                                tmpMailMsg.BodyEncoding = Encoding.UTF8;//郵件內容編碼 
                                tmpMailMsg.IsBodyHtml = true;//是否是HTML郵件 

                                bAlarm = true;
                                break;
                            default:
                                bAlarm = false;
                                break;
                        }

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

            try
            {
                DataTable dtTemp = null;
                DataTable dtPortsInfo = null;
                DataTable dtCarrierInfo = null;
                DataTable dtAvaileCarrier = null;
                DataTable dtWorkgroupSet = null;
                DataTable dtLoadPortCarrier = null;
                DataTable dtWorkInProcessSch;
                DataRow[] drIn = null;
                DataRow[] drOut = null;
                DataRow[] drCarrierData = null;
                DataRow drCarrier = null;
                DataRow[] drPortState;

                string sql = "";
                string lotID = "";
                string CarrierID = "";
                string MatalRingCarrier = "";
                int Quantity = 0;

                _DebugMode = DebugMode;

                //防止同一機台不同線程執行
                dtTemp = _dbTool.GetDataTable(_BaseDataService.QueryEquipLockState(_Equip));
                if (dtTemp.Rows.Count <= 0)
                {
                    _dbTool.SQLExec(_BaseDataService.LockEquip(_Equip, true), out tmpMsg, true);
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
                    default:
                        break;
                }

                //站點不當前站點不同, 不產生指令
                if (!lotID.Equals(""))
                {
                    sql = _BaseDataService.CheckLotStage(configuration["CheckLotStage:Table"], lotID);
                    dtTemp = _dbTool.GetDataTable(sql);

                    if (dtTemp.Rows.Count > 0)
                    {
                        if (!dtTemp.Rows[0]["stage1"].ToString().Equals(dtTemp.Rows[0]["stage2"].ToString()))
                        {
                            _logger.Debug(string.Format("---LotID= {0}, RTD_Stage={1}, MES_Stage={2}, RTD_State={3}, MES_State={4}", lotID, dtTemp.Rows[0]["stage1"].ToString(), dtTemp.Rows[0]["stage2"].ToString(), dtTemp.Rows[0]["state1"].ToString(), dtTemp.Rows[0]["state2"].ToString()));
                            if (configuration["AppSettings:Work"].ToUpper().Equals("EWLB"))
                            {
                                if (bStateChange)
                                {
                                    bStageIssue = true;
                                }
                                else
                                {
                                    //_logger.Debug(string.Format("[CreateTransferCommandByPortModel][{0}] lot [{1}] stage not correct. can not build command.", _oEventQ.EventName, lotID));
                                    _logger.Debug(string.Format("[CreateTransferCommandByPortModel][{0}] lot [{1}] Check Stage Failed.", _oEventQ.EventName, lotID));
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
                foreach (DataRow drRecord in dtPortsInfo.Rows)
                {
                    //dtWorkInProcessSch = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSchByPortId(drRecord["PORT_ID"].ToString()));
                    //if (dtWorkInProcessSch.Rows.Count > 0)
                    //    continue;

                    lstTransfer = new TransferList();
                    dtAvaileCarrier = null;
                    dtLoadPortCarrier = null;
                    
                    try
                    {
                        dtTemp = _dbTool.GetDataTable(_BaseDataService.GetEquipCustDevice(drRecord["EQUIPID"].ToString()));
                        if (dtTemp.Rows.Count > 0)
                        {
                            EquipCustDevice = dtTemp.Rows[0]["device"].ToString();
                            _logger.Debug(string.Format("[GetEquipCustDevice]: {0}, [CustDevice]: {1} ", drRecord["EQUIPID"].ToString(), EquipCustDevice));
                        }
                    }catch(Exception ex)
                    {
                        _logger.Debug(string.Format("[GetEquipCustDevice][{0}] {1} , Exception: {2}", drRecord["EQUIPID"].ToString(), EquipCustDevice, ex.Message));
                    }
                    //Select Workgroup Set
                    dtWorkgroupSet = _dbTool.GetDataTable(_BaseDataService.SelectWorkgroupSet(drRecord["EQUIPID"].ToString()));

                    if (_DebugMode)
                    {
                        _logger.Debug(string.Format("[Port_Type] {0} / {1}", drRecord["EQUIPID"].ToString(), drRecord["Port_Type"]));
                    }

                    //Port_Type: in 找一個載具Carrier, Out 找目的地Dest 
                    switch (drRecord["Port_Type"])
                    {
                        case "IN":
                            //0. wait to load 找到適合的Carrier, 併產生Load指令
                            //1. wait to unload 且沒有其它符合的Carrier 直接產生Unload指令
                            //2. wait to unload 而且有其它適用的Carrier (full), 產生Swap指令(Load + Unload)
                            //2.1. Unload, 如果out port的 carrier type 相同, 產生transfer 指令至out port
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
                                    break;
                                case 2:
                                    break;
                                case 3:
                                case 5:
                                    //2. Near Completion
                                    //3.Ready to Unload
                                    //5. Reject and Ready to unload
                                    try
                                    {
                                        bIsMatch = false;
                                        dtWorkInProcessSch = _dbTool.GetDataTable(_BaseDataService.QueryWorkInProcessSchByPortIdForUnload(drRecord["PORT_ID"].ToString()));
                                        if (dtWorkInProcessSch.Rows.Count > 0)
                                            continue;

                                        //取得當前Load Port上的Carrier
                                        dtLoadPortCarrier = _dbTool.GetDataTable(_BaseDataService.SelectLoadPortCarrierByEquipId(drRecord["EQUIPID"].ToString()));
                                        if (dtLoadPortCarrier.Rows.Count > 0)
                                            drIn = dtLoadPortCarrier.Select("PORTNO = '" + drRecord["PORT_SEQ"].ToString() + "'");

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

                                        dtAvaileCarrier = GetAvailableCarrier(_dbTool, drRecord["CARRIER_TYPE"].ToString(), true);
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

                                                if (_oEventQ.EventName.Equals("AbnormalyEquipmentStatus"))
                                                {
                                                    //由機台來找lot時, Equipment 需為主要機台(第一台)
                                                    sql = string.Format(_BaseDataService.QueryEquipListFirst(draCarrier["lot_id"].ToString(), drRecord["EQUIPID"].ToString()));
                                                    dtTemp = _dbTool.GetDataTable(sql);

                                                    if (dtTemp.Rows.Count <= 0)
                                                        continue;
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

                                                //Check Equipment CustDevice / Lot CustDevice is same.
                                                if (!EquipCustDevice.Equals(""))
                                                {
                                                    string device = "";
                                                    sql = _BaseDataService.QueryLotInfoByCarrierID(draCarrier["lot_id"].ToString());
                                                    dtTemp = _dbTool.GetDataTable(sql);
                                                    if(dtTemp.Rows.Count > 0)
                                                    {
                                                        device = dtTemp.Rows[0]["custdevice"].ToString();
                                                        if (!device.Equals(EquipCustDevice))
                                                        {
                                                            continue;
                                                        }
                                                    }
                                                }
                                                

                                                iQty = 0; iTotalQty = 0; iQuantity = 0; iCountOfCarr = 0;

                                                //Check Workgroup Set 
                                                bNoFind = true;
                                                sql = _BaseDataService.QueryRackByGroupID(dtWorkgroupSet.Rows[0]["in_erack"].ToString());
                                                dtTemp = _dbTool.GetDataTable(sql);
                                                foreach (DataRow drRack in dtTemp.Rows)
                                                {
                                                    if (draCarrier["locate"].ToString().Equals(drRack["erackID"]))
                                                    {
                                                        bNoFind = false;
                                                        break;
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

                                        if (dtAvaileCarrier.Rows.Count <= 0 || !bIsMatch || bNoFind)
                                        {
                                            CarrierID = drIn is null ? "" : drIn.Length > 0 ? drIn[0]["CARRIER_ID"].ToString() : "";

                                            lstTransfer.CommandType = "UNLOAD";
                                            lstTransfer.Source = drRecord["PORT_ID"].ToString();
                                            lstTransfer.Dest = drRecord["IN_ERACK"].ToString();
                                            lstTransfer.CarrierID = CarrierID.Equals("") ? "*" : CarrierID;
                                            lstTransfer.Quantity = 0;
                                            lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : "";
                                            break;
                                        }
                                        //AvaileCarrier is true
                                        if (bIsMatch)
                                        {
                                            drCarrierData = dtAvaileCarrier.Select("carrier_id = '" + CarrierID + "'");
                                        }

                                        lstTransfer.CommandType = "LOAD";
                                        lstTransfer.Source = "*";
                                        lstTransfer.Dest = drRecord["PORT_ID"].ToString();
                                        lstTransfer.CarrierID = CarrierID;
                                        lstTransfer.Quantity = int.Parse(drCarrierData.Length > 0 ? drCarrierData[0]["QUANTITY"].ToString() : "0");
                                        lstTransfer.CarrierType = drCarrierData.Length > 0 ? drCarrierData[0]["COMMAND_TYPE"].ToString() : "";
                                        lstTransfer.Total = iTotalQty;
                                        lstTransfer.IsLastLot = isLastLot ? 1 : 0;
                                        //normalTransfer.Transfer.Add(lstTransfer);
                                        //iReplace++;

                                        CarrierID = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["CARRIER_ID"].ToString() : "";

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
                                                    lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : "";
                                                    //normalTransfer.Transfer.Add(lstTransfer);
                                                    break;
                                                default:
                                                    CarrierID = drIn[0]["CARRIER_ID"].ToString();

                                                    normalTransfer.Transfer.Add(lstTransfer);
                                                    iReplace++;

                                                    lstTransfer = new TransferList();

                                                    lstTransfer.CommandType = "UNLOAD";
                                                    lstTransfer.Source = drRecord["PORT_ID"].ToString();
                                                    lstTransfer.Dest = drRecord["IN_ERACK"].ToString();
                                                    lstTransfer.CarrierID = CarrierID.Equals("") ? "*" : CarrierID;
                                                    lstTransfer.Quantity = 0;
                                                    lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : "";
                                                    break;
                                            }

                                        }
                                        else
                                        {
                                            //Input vs Output Carrier Type is diffrence. do unload
                                            CarrierID = drIn[0]["CARRIER_ID"].ToString();

                                            normalTransfer.Transfer.Add(lstTransfer);
                                            iReplace++;

                                            lstTransfer = new TransferList();

                                            lstTransfer.CommandType = "UNLOAD";
                                            lstTransfer.Source = drRecord["PORT_ID"].ToString();
                                            lstTransfer.Dest = drRecord["IN_ERACK"].ToString();
                                            lstTransfer.CarrierID = CarrierID.Equals("") ? "*" : CarrierID;
                                            lstTransfer.Quantity = 0;
                                            lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : "";
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

                                        dtWorkInProcessSch = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSchByPortId(drRecord["PORT_ID"].ToString()));
                                        if (dtWorkInProcessSch.Rows.Count > 0)
                                            continue;

                                        bIsMatch = false;
                                        dtAvaileCarrier = GetAvailableCarrier(_dbTool, drRecord["CARRIER_TYPE"].ToString(), true);
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

                                            foreach (DataRow draCarrier in dtAvaileCarrier.Rows)
                                            {
                                                if (_DebugMode)
                                                {
                                                    _logger.Debug(string.Format("---locate is {0}", draCarrier["locate"].ToString()));
                                                    _logger.Debug(string.Format("---in erack is {0}", dtWorkgroupSet.Rows[0]["in_erack"].ToString()));
                                                }

                                                if (_oEventQ.EventName.Equals("AbnormalyEquipmentStatus"))
                                                {
                                                    //由機台來找lot時, Equipment 需為主要機台(第一台)
                                                    sql = string.Format(_BaseDataService.QueryEquipListFirst(draCarrier["lot_id"].ToString(), drRecord["EQUIPID"].ToString()));
                                                    dtTemp = _dbTool.GetDataTable(sql);

                                                    if (dtTemp.Rows.Count <= 0)
                                                        continue;
                                                }

                                                if (_portModel.Equals("1I1OT2"))
                                                {
                                                    //1I1OT2 特定機台邏輯
                                                    //IN: Lotid 會對應一個放有Matal Ring 的Cassette, 需存在才進行派送
                                                    MatalRingCarrier = "";
                                                }

                                                iQty = 0; iTotalQty = 0; iQuantity = 0; iCountOfCarr = 0;

                                                //Check Workgroup Set 
                                                bool bNoFind = true;
                                                sql = _BaseDataService.QueryRackByGroupID(dtWorkgroupSet.Rows[0]["in_erack"].ToString());
                                                dtTemp = _dbTool.GetDataTable(sql);
                                                foreach (DataRow drRack in dtTemp.Rows)
                                                {
                                                    if (draCarrier["locate"].ToString().Equals(drRack["erackID"]))
                                                    {
                                                        bNoFind = false;
                                                        break;
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

                                        lstTransfer.CommandType = "LOAD";
                                        lstTransfer.Source = "*";
                                        lstTransfer.Dest = drRecord["PORT_ID"].ToString();
                                        lstTransfer.CarrierID = CarrierID;
                                        lstTransfer.Quantity = int.Parse(drCarrierData.Length > 0 ? drCarrierData[0]["QUANTITY"].ToString() : "0");
                                        lstTransfer.CarrierType = drCarrierData.Length > 0 ? drCarrierData[0]["COMMAND_TYPE"].ToString() : "";
                                        lstTransfer.Total = iTotalQty;
                                        lstTransfer.IsLastLot = isLastLot ? 1 : 0;
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
                                    break;
                                case 3:
                                case 5:
                                    //2. Near Completion
                                    //3.Ready to Unload
                                    //5. Reject and Ready to unload
                                    try
                                    {
                                        dtWorkInProcessSch = _dbTool.GetDataTable(_BaseDataService.QueryWorkInProcessSchByPortIdForUnload(drRecord["PORT_ID"].ToString()));
                                        if (dtWorkInProcessSch.Rows.Count > 0)
                                            continue;

                                        dtAvaileCarrier = GetAvailableCarrier(_dbTool, drRecord["CARRIER_TYPE"].ToString(), false);
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
                                                foreach (DataRow drRack in dtTemp.Rows)
                                                {
                                                    if (draCarrier["locate"].ToString().Equals(drRack["erackID"]))
                                                    {
                                                        bNoFind = false;
                                                        break;
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

                                        if (dtAvaileCarrier.Rows.Count <= 0 || !bIsMatch)
                                        {
                                            CarrierID = drOut.Length > 0 ? drOut[0]["CARRIER_ID"].ToString() : "";

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
                                            lstTransfer.CarrierID = CarrierID.Equals("") ? "*" : CarrierID;
                                            lstTransfer.Quantity = 0;
                                            lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : "";
                                            break;
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

                                            normalTransfer.Transfer.Add(lstTransfer);
                                            iReplace++;

                                            if (_DebugMode)
                                            {
                                                _logger.Debug(string.Format("----Logic true "));
                                            }
                                        }
                                        else
                                        {
                                            if (!bPortTypeSame)
                                            {
                                                CarrierID = CarrierID.Equals("") ? dtAvaileCarrier.Rows[0]["Carrier_ID"].ToString() : CarrierID;

                                                lstTransfer.CommandType = "LOAD";
                                                lstTransfer.Source = "*";
                                                lstTransfer.Dest = drRecord["PORT_ID"].ToString();
                                                lstTransfer.CarrierID = CarrierID;
                                                lstTransfer.Quantity = dtLoadPortCarrier.Rows.Count > 0 ? int.Parse(dtLoadPortCarrier.Rows[0]["QUANTITY"].ToString()) : 0;
                                                lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : "";

                                                normalTransfer.Transfer.Add(lstTransfer);
                                                iReplace++;
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
                                        lstTransfer.CarrierID = CarrierID.Equals("") ? "*" : CarrierID;
                                        lstTransfer.Quantity = 0;
                                        lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : "";
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
                                        dtWorkInProcessSch = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSchByPortId(drRecord["PORT_ID"].ToString()));
                                        if (dtWorkInProcessSch.Rows.Count > 0)
                                            continue;

                                        //drPortState = dtPortsInfo.Select("Port_State in (1, 4)");
                                        //if (drPortState.Length <= 0)
                                        //    continue;

                                        dtAvaileCarrier = GetAvailableCarrier(_dbTool, drRecord["CARRIER_TYPE"].ToString(), false);
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
                                                sql = _BaseDataService.QueryRackByGroupID(dtWorkgroupSet.Rows[0]["out_erack"].ToString());
                                                dtTemp = _dbTool.GetDataTable(sql);
                                                foreach (DataRow drRack in dtTemp.Rows)
                                                {
                                                    if (draCarrier["locate"].ToString().Equals(drRack["erackID"]))
                                                    {
                                                        bNoFind = false;
                                                        break;
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

                                        lstTransfer.CommandType = "LOAD";
                                        lstTransfer.Source = "*";
                                        lstTransfer.Dest = drRecord["PORT_ID"].ToString();
                                        lstTransfer.CarrierID = CarrierID;
                                        lstTransfer.Quantity = drCarrierData[0]["QUANTITY"].ToString().Equals("") ? 0 : int.Parse(drCarrierData[0]["QUANTITY"].ToString());
                                        lstTransfer.CarrierType = drCarrierData[0]["COMMAND_TYPE"].ToString();
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
                                        break;
                                    case 3:
                                    case 5:
                                        //2. Near Completion
                                        //3.Ready to Unload
                                        //5. Reject and Ready to unload
                                        try
                                        {
                                            dtWorkInProcessSch = _dbTool.GetDataTable(_BaseDataService.QueryWorkInProcessSchByPortIdForUnload(drRecord["PORT_ID"].ToString()));
                                            if (dtWorkInProcessSch.Rows.Count > 0)
                                                continue;

                                            //取得當前Load Port上的Carrier
                                            dtLoadPortCarrier = _dbTool.GetDataTable(_BaseDataService.QueryWorkInProcessSchByPortIdForUnload(drRecord["EQUIPID"].ToString()));
                                            if (dtLoadPortCarrier is not null)
                                            {
                                                if (dtLoadPortCarrier.Rows.Count > 0)
                                                {
                                                    drIn = dtLoadPortCarrier.Select("PORTNO = '" + drRecord["PORT_SEQ"].ToString() + "'");
                                                }
                                            }

                                            dtAvaileCarrier = GetAvailableCarrier(_dbTool, drRecord["CARRIER_TYPE"].ToString(), true);

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
                                                    iQty = 0; iTotalQty = 0; iQuantity = 0;

                                                    //站點不當前站點不同, 不取這批lot
                                                    if (!draCarrier["lot_id"].ToString().Equals(""))
                                                    {
                                                        if (configuration["AppSettings:Work"].ToUpper().Equals("EWLB"))
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
                                                    }

                                                    //Check Workgroup Set 
                                                    bNoFind = true;
                                                    sql = _BaseDataService.QueryRackByGroupID(dtWorkgroupSet.Rows[0]["in_erack"].ToString());
                                                    dtTemp = _dbTool.GetDataTable(sql);
                                                    foreach (DataRow drRack in dtTemp.Rows)
                                                    {
                                                        if (draCarrier["locate"].ToString().Equals(drRack["erackID"].ToString()))
                                                        {
                                                            bNoFind = false;
                                                            break;
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
                                                    _dbTool.SQLExec(_BaseDataService.UpdateTableReserveCarrier(CarrierID, false), out tmpMessage, true);
                                                    continue;
                                                }
                                            }

                                            if (_DebugMode)
                                            {
                                                _logger.Debug(string.Format("[Build Unload Command] {0} / {1} / {2}", drRecord["EQUIPID"].ToString(), bIsMatch, dtAvaileCarrier.Rows.Count));
                                            }

                                            if (dtAvaileCarrier.Rows.Count <= 0 || bIsMatch || CarrierID.Equals("") || bNoFind)
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
                                                    tempDest = drRecord[tmpReject].ToString();
                                                else
                                                    tempDest = drRecord["OUT_ERACK"].ToString();

                                                lstTransfer.CommandType = "UNLOAD";
                                                lstTransfer.Source = drRecord["PORT_ID"].ToString();
                                                lstTransfer.Dest = tempDest;
                                                lstTransfer.CarrierID = CarrierID.Equals("") ? "*" : "*";
                                                if (!lstTransfer.CarrierID.Equals(""))
                                                {
                                                    lstTransfer.LotID = "";
                                                }
                                                lstTransfer.Quantity = 0;
                                                lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : "";

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
                                            //AvaileCarrier is true
                                            if (bIsMatch)
                                            {
                                                drCarrierData = dtAvaileCarrier.Select("carrier_id = '" + CarrierID + "'");
                                            }

                                            if (drCarrierData.Length > 0)
                                            {
                                                lstTransfer = new TransferList();

                                                lstTransfer.CommandType = "LOAD";
                                                lstTransfer.Source = "*";
                                                lstTransfer.Dest = drRecord["PORT_ID"].ToString();
                                                lstTransfer.CarrierID = CarrierID;
                                                iQty = drCarrierData == null ? 0 : int.Parse(drCarrierData[0]["QUANTITY"].ToString());
                                                lstTransfer.Quantity = iQty;
                                                lstTransfer.CarrierType = drCarrierData == null ? "" : drCarrierData[0]["COMMAND_TYPE"].ToString();
                                                lstTransfer.Total = iTotalQty;
                                                lstTransfer.IsLastLot = isLastLot ? 1 : 0;
                                                

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

                                            if (_DebugMode)
                                            {
                                                _logger.Debug(string.Format("[dtAvaileCarrier] LOAD iReplace [{0}] / {1}", lstTransfer.CarrierID, iReplace));
                                            }

                                            if (CarrierID.Equals(""))
                                                CarrierID = dtLoadPortCarrier.Rows[0]["CARRIER_ID"].ToString();

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
                                                            break;
                                                        case 2:
                                                        case 3:
                                                        default:
                                                            CarrierID = drIn is not null ? drIn.Length > 0 ? drIn[0]["CARRIER_ID"].ToString() : "" : "";

                                                            lstTransfer.CommandType = "UNLOAD";
                                                            lstTransfer.Source = drRecord["PORT_ID"].ToString();
                                                            lstTransfer.Dest = drRecord["OUT_ERACK"].ToString();
                                                            lstTransfer.CarrierID = CarrierID.Equals("") ? "*" : CarrierID;
                                                            lstTransfer.Quantity = 0;
                                                            lstTransfer.CarrierType = dtLoadPortCarrier.Rows.Count > 0 ? dtLoadPortCarrier.Rows[0]["COMMAND_TYPE"].ToString() : "";
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

                                            dtWorkInProcessSch = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSchByPortId(drRecord["PORT_ID"].ToString()));
                                            if (dtWorkInProcessSch.Rows.Count > 0)
                                                continue;

                                            dtAvaileCarrier = GetAvailableCarrier(_dbTool, drRecord["CARRIER_TYPE"].ToString(), true);
                                            if (dtAvaileCarrier is null)
                                                continue;
                                            if (dtAvaileCarrier.Rows.Count <= 0)
                                                continue;

                                            if (_DebugMode)
                                            {
                                                _logger.Debug(string.Format("[GetAvailableCarrier] {0} / {1}", drRecord["EQUIPID"].ToString(), dtAvaileCarrier.Rows.Count));
                                            }

                                            int iQty = 0; int iQuantity = 0; int iTotalQty = 0;
                                            int iCountOfCarr = 0;
                                            bool isLastLot = false;

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
                                                    foreach (DataRow draCarrier in dtAvaileCarrier.Rows)
                                                    {
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
                                                                            _logger.Debug(string.Format("[drRack] {0} / {1} / {2}", drRecord["EQUIPID"].ToString(), draCarrier["locate"].ToString(), drRack["erackID"].ToString()));
                                                                        }

                                                                        if (draCarrier["locate"].ToString().Equals(drRack["erackID"].ToString()))
                                                                        {
                                                                            bNoFind = false;

                                                                            if (_DebugMode)
                                                                            {
                                                                                _logger.Debug(string.Format("[AvailableCarrier ErackID] {0} / {1}", drRecord["EQUIPID"].ToString(), drRack["erackID"].ToString()));
                                                                            }
                                                                            break;
                                                                        }

                                                                        if (_DebugMode)
                                                                        {
                                                                            _logger.Debug(string.Format("[No Find] {0} / {1} / {2}", drRecord["EQUIPID"].ToString(), draCarrier["locate"].ToString(), drRack["erackID"].ToString()));
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
                                                            _logger.Debug(string.Format("[Check Workgroup Set ] {0} / {1}", drRecord["EQUIPID"].ToString(), dtTemp.Rows.Count.ToString()));
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
                                                    _dbTool.SQLExec(_BaseDataService.UpdateTableReserveCarrier(CarrierID, false), out tmpMessage, true);
                                                    continue;
                                                }
                                                drCarrierData = dtAvaileCarrier.Select("carrier_id = '" + CarrierID + "'");
                                            }

                                            lstTransfer.CommandType = "LOAD";
                                            lstTransfer.Source = "*";
                                            lstTransfer.Dest = drRecord["PORT_ID"].ToString();
                                            lstTransfer.CarrierID = CarrierID;
                                            lstTransfer.Quantity = drCarrierData[0]["QUANTITY"].ToString().Equals("") ? 0 : int.Parse(drCarrierData[0]["QUANTITY"].ToString());
                                            lstTransfer.CarrierType = drCarrierData[0]["COMMAND_TYPE"].ToString();
                                            lstTransfer.Total = iTotalQty;
                                            lstTransfer.IsLastLot = isLastLot ? 1 : 0;
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
                }
                normalTransfer.Replace = iReplace > 0 ? iReplace - 1 : 0;

                ////////////////output normalTransfer

                //由Carrier Type找出符合的Carrier 


                //產生派送指令識別碼
                //U + 2022081502 + 00001
                //將所產生的指令加入WorkInProcess_Sch
                bool single = true;
                DataTable dtExist = new DataTable();

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

                        if (!single)
                            normalTransfer.CommandID = Tools.GetCommandID(_dbTool);

                        SchemaWorkInProcessSch workinProcessSch = new SchemaWorkInProcessSch();
                        workinProcessSch.UUID = Tools.GetUnitID(_dbTool);
                        workinProcessSch.Cmd_Id = normalTransfer.CommandID;
                        workinProcessSch.Cmd_State = "";    //派送前為NULL
                        workinProcessSch.EquipId = normalTransfer.EquipmentID;
                        workinProcessSch.Cmd_Current_State = "";    //派送前為NULL
                        workinProcessSch.Priority = 10;             //預設優先權為10
                        if (single)
                            workinProcessSch.Replace = 0;
                        else
                            workinProcessSch.Replace = normalTransfer.Transfer.Count > 0 ? normalTransfer.Transfer.Count - 1 : 0;

                        foreach (TransferList trans in normalTransfer.Transfer)
                        {
                            //檢查Dest是否已存在於WorkinprocessSch裡, 存在不能再送, 
                            dtExist = new DataTable();

                            if (trans.CommandType.Equals("LOAD"))
                            {
                                dtExist = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSchByPortId(trans.Dest));
                                if (dtExist.Rows.Count > 0)
                                {
                                    logger.Debug(string.Format("Dublicate Command Type:[{0}], Command Source: {1}, Dest: {2}", trans.CommandType, trans.Source, trans.Dest));
                                    continue;   //目的地已有Carrier, 跳過
                                }
                                tmpCarrierid = trans.CarrierID.Equals("") ? "*" : trans.CarrierID;

                                //check carrier
                                dtExist = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSchByCarrier(tmpCarrierid));
                                if (dtExist.Rows.Count > 0)
                                {
                                    logger.Debug(string.Format("Dublicate Carrier [{0}], command [{1}] been auto cancel.", tmpCarrierid, normalTransfer.CommandID));
                                    if (dtExist.Rows[0]["cmd_state"].ToString().Equals("Init"))
                                    {
                                        //UpdateTableReserveCarrier
                                        _dbTool.SQLExec(_BaseDataService.UpdateTableReserveCarrier(tmpCarrierid, true), out tmpMsg, true);
                                        continue;   //目的地已有Carrier, 跳過
                                    }
                                    if (dtExist.Rows[0]["cmd_state"].ToString().Equals("Initial"))
                                    {
                                        //DeleteWorkInProcessSchByCmdId
                                        _dbTool.SQLExec(_BaseDataService.DeleteWorkInProcessSchByCmdId(dtExist.Rows[0]["cmd_id"].ToString()), out tmpMsg, true);
                                    }
                                }
                            }
                            if (trans.CommandType.Equals("UNLOAD"))
                            {
                                dtExist = _dbTool.GetDataTable(_BaseDataService.QueryWorkInProcessSchByPortIdForUnload(trans.Source));
                                if (dtExist.Rows.Count > 0)
                                {
                                    logger.Debug(string.Format("Dublicate Command Type:[{0}], Command Source: {1}, Dest: {2}", trans.CommandType, trans.Source, trans.Dest));
                                    continue;   //目的地已有Carrier, 跳過
                                }
                                tmpCarrierid = "*";
                            }

                            if (single)
                                workinProcessSch.Cmd_Id = Tools.GetCommandID(_dbTool);

                            tmpMsg = "";
                            //workinProcessSch.UUID = Tools.GetUnitID(_dbTool);  //變更為GID使用, 可查詢同一批派出的指令
                            workinProcessSch.Cmd_Type = trans.CommandType.Equals("") ? "TRANS" : trans.CommandType;
                            workinProcessSch.CarrierId = tmpCarrierid;
                            workinProcessSch.CarrierType = trans.CarrierType.Equals("") ? "" : trans.CarrierType;
                            workinProcessSch.Source = trans.Source.Equals("*") ? "*" : trans.Source;
                            workinProcessSch.Dest = trans.Dest.Equals("") ? "*" : trans.Dest;
                            workinProcessSch.Quantity = trans.Quantity;
                            workinProcessSch.Total = trans.Total;
                            workinProcessSch.IsLastLot = trans.IsLastLot;
                            workinProcessSch.Back = "*";

                            DataTable dtInfo = new DataTable { };
                            if (trans.CarrierID is not null)
                            {
                                if (!trans.CarrierID.Equals("*") || trans.CarrierID.Trim().Equals(""))
                                    dtInfo = _dbTool.GetDataTable(_BaseDataService.QueryLotInfoByCarrierID(trans.CarrierID));
                            }
                            workinProcessSch.LotID = dtInfo.Rows.Count <= 0 ? " " : dtInfo.Rows[0]["lotid"].ToString();
                            workinProcessSch.Customer = dtInfo.Rows.Count <= 0 ? " " : dtInfo.Rows[0]["customername"].ToString();

                            if (_DebugMode)
                            {
                                _logger.Debug(string.Format("----do insert "));
                            }

                            sql = _BaseDataService.InsertTableWorkinprocess_Sch(workinProcessSch);

                            if (_DebugMode)
                            {
                                _logger.Debug(string.Format("----do insert sql [{0}] ", sql));
                            }

                            if (_dbTool.SQLExec(sql, out tmpMsg, true))
                            { }
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
            }
            finally
            {
                //Do Nothing
            }

            return result;
        }
        public bool CreateTransferCommandByTransferList(DBTool _dbTool, ILogger _logger, TransferList _transferList, out List<string> _arrayOfCmds)
        {
            bool result = false;
            string tmpMsg = "";
            _arrayOfCmds = new List<string>();
            DataTable dtTemp = new DataTable();
            try
            {
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
                    normalTransfer.CommandID = Tools.GetCommandID(_dbTool);

                    SchemaWorkInProcessSch workinProcessSch = new SchemaWorkInProcessSch();
                    workinProcessSch.Cmd_Id = normalTransfer.CommandID;
                    workinProcessSch.Cmd_State = "";    //派送前為NULL
                    workinProcessSch.EquipId = normalTransfer.EquipmentID;
                    workinProcessSch.Cmd_Current_State = "";    //派送前為NULL
                    workinProcessSch.Priority = 10;             //預設優先權為10
                    workinProcessSch.Replace = normalTransfer.Transfer.Count > 0 ? normalTransfer.Transfer.Count - 1 : 0;

                    foreach (TransferList trans in normalTransfer.Transfer)
                    {
                        workinProcessSch.CarrierId = trans.CarrierID.Equals("") ? "*" : trans.CarrierID;
                        if (!workinProcessSch.CarrierId.Equals("*")) {
                            dtTemp = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSchByCarrier(trans.CarrierID));

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

                        sql = _BaseDataService.InsertTableWorkinprocess_Sch(workinProcessSch);
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
                //Do Nothing
            }

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

            return iState;
        }
        public DataTable GetAvailableCarrier(DBTool _dbTool, string _carrierType, bool _isFull)
        {
            string sql = "";
            DataTable dtAvailableCarrier = null;

            try
            {
                sql = string.Format(_BaseDataService.SelectAvailableCarrierByCarrierType(_carrierType, _isFull));
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
            DateTime curDT = DateTime.UtcNow;
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
            DataTable dt;

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

            return iExecMode;
        }
        public bool CheckIsAvailableLot(DBTool _dbTool, string _lotId, string _machine)
        {
            bool isAvailableLot = false;
            string sql = "";
            DataTable dt;

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

            return bResult;
        }
        public bool GetLockState(DBTool _dbTool)
        {
            bool isLock = false;
            string sql = "";
            DataTable dt;

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
            DataTable dt;
            DataTable dt2;

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

            return bResult;
        }
        public bool AutoSentInfoUpdate(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, out string tmpMessage)
        {
            bool bResult = false;
            string sql = "";
            string tmpSql = "";
            string tmpMsg = "";
            tmpMessage = "";
            DataTable dt;
            DataTable dt2;
            DataTable dtTemp;
            List<string> args = new();
            APIResult apiResult = new APIResult();
            string _table = "";

            try
            {
                //_args.Split(',')
                _table = _configuration["eRackDisplayInfo:contained"].ToString().Split(',')[1];

                sql = _BaseDataService.QueryCarrierAssociateWhenOnErack(_table);
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    string lotid;
                    string carrierId;
                    foreach (DataRow dr in dt.Rows)
                    {
                        args = new();
                        apiResult = new APIResult();
                        carrierId = "";

                        lotid = dr["LOT_ID"].ToString().Equals("") ? "" : dr["LOT_ID"].ToString();
                        carrierId = dr["carrier_id"].ToString().Equals("") ? "" : dr["carrier_id"].ToString();

                        if (dr["location_type"].ToString().Trim().Equals("ERACK"))
                        {

                            if (carrierId.Length > 0)
                            {
                                tmpMsg = string.Format("[AutoSentInfoUpdate: Flag LotId {0}]", lotid);
                                _logger.Debug(tmpMsg);

                                string v_STAGE = "";
                                string v_CUSTOMERNAME = "";
                                string v_PARTID = "";
                                string v_LOTTYPE = "";
                                string v_AUTOMOTIVE = "";
                                string v_STATE = "";
                                string v_HOLDCODE = "";
                                string v_TURNRATIO = "0";
                                string v_EOTD = "";
                                string v_POTD = "";
                                try
                                {
                                    v_CUSTOMERNAME = dr["CUSTOMERNAME"].ToString().Equals("") ? "" : dr["CUSTOMERNAME"].ToString();
                                    v_PARTID = dr["PARTID"].ToString().Equals("") ? "" : dr["PARTID"].ToString();
                                    v_LOTTYPE = dr["LOTTYPE"].ToString().Equals("") ? "" : dr["LOTTYPE"].ToString();

                                    sql = _BaseDataService.QueryErackInfoByLotID(_configuration["eRackDisplayInfo:contained"], lotid);
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
                                    }
                                }
                                catch (Exception ex)
                                {
                                    tmpMsg = string.Format("[AutoSentInfoUpdate: Column Issue. {0}]", ex.Message);
                                    _logger.Debug(tmpMsg);
                                }

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
                                apiResult = SentCommandtoMCSByModel(_configuration, _logger, "InfoUpdate", args);
                            }
                            else
                            {
                                tmpMsg = string.Format("[CarrierLocationUpdate: Carrier [{0}] Not Exist.]", carrierId);
                                _logger.Debug(tmpMsg);

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
                                apiResult = SentCommandtoMCSByModel(_configuration, _logger, "InfoUpdate", args);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }

            return bResult;
        }
        public bool AutoBindAndSentInfoUpdate(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, out string tmpMessage)
        {
            bool bResult = false;
            string sql = "";
            string tmpSql = "";
            string tmpMsg = "";
            tmpMessage = "";
            DataTable dt;
            DataTable dt2;
            DataTable dtTemp;
            List<string> args = new();
            APIResult apiResult = new APIResult();

            try
            {
                sql = _BaseDataService.QueryCarrierAssociateWhenIsNewBind();
                dt = _dbTool.GetDataTable(sql);

                if (dt.Rows.Count > 0)
                {
                    string lotid;
                    string carrierId;
                    foreach (DataRow dr in dt.Rows)
                    {
                        args = new();
                        apiResult = new APIResult();
                        carrierId = "";

                        lotid = dr["LOT_ID"].ToString().Equals("") ? "" : dr["LOT_ID"].ToString();
                        carrierId = dr["carrier_id"].ToString().Equals("") ? "" : dr["carrier_id"].ToString();

                        if (dr["location_type"].ToString().Trim().Equals("ERACK") || dr["location_type"].ToString().Trim().Equals("A"))
                        {

                            if (carrierId.Length > 0)
                            {
                                tmpMsg = string.Format("[AutoSentInfoUpdate: Flag LotId {0}]", lotid);
                                _logger.Debug(tmpMsg);

                                string v_STAGE = "";
                                string v_CUSTOMERNAME = "";
                                string v_PARTID = "";
                                string v_LOTTYPE = "";
                                string v_AUTOMOTIVE = "";
                                string v_STATE = "";
                                string v_HOLDCODE = "";
                                string v_TURNRATIO = "0";
                                string v_EOTD = "";
                                string v_POTD = "";
                                try
                                {
                                    v_CUSTOMERNAME = dr["CUSTOMERNAME"].ToString().Equals("") ? "" : dr["CUSTOMERNAME"].ToString();
                                    v_PARTID = dr["PARTID"].ToString().Equals("") ? "" : dr["PARTID"].ToString();
                                    v_LOTTYPE = dr["LOTTYPE"].ToString().Equals("") ? "" : dr["LOTTYPE"].ToString();

                                    sql = _BaseDataService.QueryErackInfoByLotID(_configuration["eRackDisplayInfo:contained"], lotid);
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
                                    }
                                }
                                catch (Exception ex)
                                {
                                    tmpMsg = string.Format("[AutoSentInfoUpdate: Column Issue. {0}]", ex.Message);
                                    _logger.Debug(tmpMsg);
                                }

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
                                apiResult = SentCommandtoMCSByModel(_configuration, _logger, "InfoUpdate", args);

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
                                apiResult = SentCommandtoMCSByModel(_configuration, _logger, "InfoUpdate", args);
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

            return bResult;
        }
        public bool AutoUpdateRTDStatistical(DBTool _dbTool, out string tmpMessage)
        {
            bool bResult = false;
            string sql = "";
            string tmpSql = "";
            string tmpMsg = "";
            tmpMessage = "";
            DataTable dt;
            DataTable dt2;

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

            try
            {
                if (argv is not null)
                {

                    _commandid = argv is null ? "" : argv[0];
                    _params = argv is null ? "" : argv[1];
                    _desc = argv is null ? "" : argv[2];
                }

                if (ListAlarmCode.Count <= 0)
                {
                    string[] aryAlarm = {
                        "10100,System,MCS,Error,Sent Command Failed,0,,{0},{1},{2}",
                        "10101,System,MCS,Alarm,MCS Connection Failed,0,,{0},{1},{2}",
                        "20100,System,Database Access,Alarm,Database Access Error,100,AlarmSet,{0},{1},{2}",
                        "20101,System,Database Access,Alarm,Database Access Success,101,AlarmReset,{0},{1},{2}",
                        "30000,System,RTD,Issue,Dispatch overtime,1,Auto Clean Commands,{0},{1},{2}",
                        "30001,System,RTD,Issue,Dispatch overtime,2,Auto Hold Lot,{0},{1},{2}",
                        "90000,System,RTD,INFO,TESE ALARM,0,,{0},{1},{2}"
                    };

                    int _iAlarmCode = 0;
                    foreach (string alarm in aryAlarm)
                    {
                        string[] tmp = alarm.Split(',');
                        _iAlarmCode = int.Parse(tmp[0]);
                        lstAlarm.Add(_iAlarmCode, alarm);
                    }

                    ListAlarmCode = lstAlarm;
                }

                alarmMsg = ListAlarmCode[_alarmCode];

                tmpSQL = _BaseDataService.InsertRTDAlarm(alarmMsg);

                if (_commandid.Equals(""))
                    tmpSQL = string.Format(tmpSQL, _commandid, _params, _desc);
                else
                    tmpSQL = string.Format(tmpSQL, _commandid, _params, _desc);

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

            try
            {
                JCETWebServicesClient jcetWebServiceClient = new JCETWebServicesClient();
                jcetWebServiceClient._url = url;
                JCETWebServicesClient.ResultMsg resultMsg = new JCETWebServicesClient.ResultMsg();
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
            DataTable dt;
            DataTable dt2;

            try
            {
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

                        if (!lotid.Equals(""))
                        {
                            sql = _BaseDataService.SelectTableWorkInProcessSchByLotId(lotid);
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
                                        sql = _BaseDataService.DeleteWorkInProcessSchByGId(GId);
                                        _dbTool.SQLExec(sql, out tmpMsg, true);

                                        if (tmpMsg.Equals(""))
                                        {
                                            string[] tmpString = new string[] { GId, "", "" };
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
                            _dbTool.SQLExec(_BaseDataService.UpdateTableLotInfoState(lotid, "HOLD"), out tmpMsg, true);

                            if (tmpMsg.Equals(""))
                            {
                                string[] tmpString = new string[] { lotid, "", "" };
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

            return bResult;
        }
        public bool TriggerCarrierInfoUpdate(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, string _lotid)
        {
            bool bResult = false;
            string tmpMsg = "";
            string sql = "";
            DataTable dt = null;
            DataTable dtTemp = null;

            try
            {
                sql = string.Format(_BaseDataService.CheckLocationByLotid(_lotid));
                dt = _dbTool.GetDataTable(sql);
                if (dt.Rows.Count > 0)
                {
                    tmpMsg = string.Format("[TriggerCarrierInfoUpdate: CheckLocationByLotid. {0} / {1}]", _lotid, _configuration["eRackDisplayInfo:contained"]);
                    _logger.Debug(tmpMsg);

                    List<string> args = new();
                    string v_LOT_ID = "";
                    string v_STAGE = "";
                    string v_carrier_id = "";
                    string v_CUSTOMERNAME = "";
                    string v_PARTID = "";
                    string v_LOTTYPE = "";
                    string v_AUTOMOTIVE = "";
                    string v_STATE = "";
                    string v_HOLDCODE = "";
                    string v_TURNRATIO = "0";
                    string v_EOTD = "";
                    string v_POTD = "";
                    try
                    {
                        v_carrier_id = dt.Rows[0]["carrier_id"].ToString().Equals("") ? "" : dt.Rows[0]["carrier_id"].ToString();
                        v_LOT_ID = _lotid;
                        v_CUSTOMERNAME = dt.Rows[0]["CUSTOMERNAME"].ToString().Equals("") ? "" : dt.Rows[0]["CUSTOMERNAME"].ToString();
                        v_PARTID = dt.Rows[0]["PARTID"].ToString().Equals("") ? "" : dt.Rows[0]["PARTID"].ToString();
                        v_LOTTYPE = dt.Rows[0]["LOTTYPE"].ToString().Equals("") ? "" : dt.Rows[0]["LOTTYPE"].ToString();
                        v_STAGE = dt.Rows[0]["stage"].ToString().Equals("") ? "" : dt.Rows[0]["stage"].ToString();

                        sql = _BaseDataService.QueryErackInfoByLotID(_configuration["eRackDisplayInfo:contained"], v_LOT_ID);
                        dtTemp = _dbTool.GetDataTable(sql);

                        if (dtTemp.Rows.Count > 0)
                        {
                            v_STAGE = !v_STAGE.Equals("") ? "" : dtTemp.Rows[0]["STAGE"].ToString();
                            v_AUTOMOTIVE = dtTemp.Rows[0]["AUTOMOTIVE"].ToString().Equals("") ? "" : dtTemp.Rows[0]["AUTOMOTIVE"].ToString();
                            v_STATE = dtTemp.Rows[0]["STATE"].ToString().Equals("") ? "" : dtTemp.Rows[0]["STATE"].ToString();
                            v_HOLDCODE = dtTemp.Rows[0]["HOLDCODE"].ToString().Equals("") ? "" : dtTemp.Rows[0]["HOLDCODE"].ToString();
                            v_TURNRATIO = dtTemp.Rows[0]["TURNRATIO"].ToString().Equals("") ? "0" : dtTemp.Rows[0]["TURNRATIO"].ToString();
                            v_EOTD = dtTemp.Rows[0]["EOTD"].ToString().Equals("") ? "" : dtTemp.Rows[0]["EOTD"].ToString();
                            v_POTD = dtTemp.Rows[0]["POTD"].ToString().Equals("") ? "" : dtTemp.Rows[0]["POTD"].ToString();
                        }
                    }
                    catch (Exception ex)
                    {
                        tmpMsg = string.Format("[TriggerCarrierInfoUpdate: Column Issue. {0}]", ex.Message);
                        _logger.Debug(tmpMsg);
                    }

                    args.Add(v_LOT_ID);
                    args.Add(v_STAGE.Equals("") ? dt.Rows[0]["STAGE"].ToString() : v_STAGE);
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
                    SentCommandtoMCSByModel(_configuration, _logger, "InfoUpdate", args);
                }

                bResult = true;
            }
            catch (Exception ex)
            {
                bResult = false;
                tmpMsg = string.Format("Send InfoUpdate Fail, Exception: {0}", ex.Message);
                _logger.Debug(tmpMsg);
            }

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

                            dateTime = DateTime.UtcNow.ToString("yyyy/MM/dd HH:mm:ss");

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
                                if(!carrierLotAssociate.LotID.Equals(dtTemp.Rows[0]["lot_id"].ToString()))
                                {
                                    doLogic = 2;
                                } else if (!carrierLotAssociate.Quantity.Equals(dtTemp.Rows[0]["quantity"].ToString())) {
                                    doLogic = 2;
                                } else
                                {
                                    doLogic = 0;
                                }
                                
                            }

                            if(doLogic.Equals(1))
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


            return bResult;
        }
        public string GetExtenalTables(IConfiguration _configuration, string _method, string _func)
        {
            string strTable = "";
            bool enableSync = false;
            bool isTable = false;
            string cfgString = "";

            try
            {
                cfgString = string.Format("{0}:{1}:Model", _method, _func);
                if (_configuration[cfgString].Equals("Table"))
                    isTable = true;

                cfgString = string.Format("{0}:{1}:Enable", _method, _func);
                if (_configuration[cfgString].Equals("True"))
                    enableSync = true;

                if (enableSync)
                {
                    if (isTable)
                    {
#if DEBUG
                        cfgString = string.Format("{0}:{1}:Table:Debug", _method, _func);
#else
                        cfgString = string.Format("{0}:{1}:Table:Prod", _method, _func);
#endif
                        strTable = _configuration[cfgString];
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
            DataTable dtTemp;

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
            string tableName = "";

            try
            {
                tableName = _configuration["PreDispatchToErack:lotState:tableName"]　is null ? "lot_Info" : _configuration["PreDispatchToErack:lotState:tableName"];

                sql = _BaseDataService.QueryPreTransferList(tableName);
                dtTemp = _dbTool.GetDataTable(sql);

                if (dtTemp.Rows.Count > 0)
                {
                    foreach (DataRow dr in dtTemp.Rows)
                    {

                        sql = _BaseDataService.CheckCarrierLocate(dr["in_eRack"].ToString(), dr["locate"].ToString());
                        dtTemp2 = _dbTool.GetDataTable(sql);

                        if (dtTemp2.Rows.Count <= 0)
                        {
                            string carrierId = dr["carrier_ID"].ToString().Equals("") ? "*" : dr["carrier_ID"].ToString();
                            sql = _BaseDataService.CheckPreTransfer(carrierId);
                            dtTemp3 = _dbTool.GetDataTable(sql);
                            if (dtTemp3.Rows.Count > 0)
                                continue;

                            _eventQ = new EventQueue();
                            _eventQ.EventName = funcName;

                            transferList.CarrierID = carrierId;
                            transferList.LotID = dr["lot_ID"].ToString().Equals("") ? "*" : dr["lot_ID"].ToString();
                            transferList.Source = "*";
                            transferList.Dest = dr["in_eRack"].ToString();
                            transferList.CommandType = "Pre-Transfer";
                            transferList.CarrierType = dr["carrier_type"].ToString();

                            _eventQ.EventObject = transferList;
                            _eventQueue.Enqueue(_eventQ);
                        }
                    }
                }

                bResult = true;
            }
            catch (Exception ex)
            {
                _logger.Debug(string.Format("PreDispatchToErack Unknow Error. [Exception: {0}]", ex.Message));
            }

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
            }

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

            try
            {
                var jsonStringName = new JavaScriptSerializer();
                var jsonStringResult = jsonStringName.Serialize(value);
                _logger.Info(string.Format("Function:{0}, Received:[{1}]", funcName, jsonStringResult));

                _dbTool.SQLExec(_BaseDataService.UpdateTableWorkInProcessSchHisByCmdId(value.CommandID.Trim()), out tmpMsg, true);

                while (true)
                {
                    try
                    {
                        sql = _BaseDataService.SelectTableWorkInProcessSchByCmdId(value.CommandID);
                        dt = _dbTool.GetDataTable(sql);

                        if (dt.Rows.Count > 0)
                        {
                            //if (!dt.Rows[0]["cmd_type"].ToString().Equals("Pre-Transfer"))
                            //{
                                bExecSql = _dbTool.SQLExec(_BaseDataService.UpdateTableWorkInProcessSchByCmdId(value.Status, value.LastStateTime, value.CommandID.Trim()), out tmpMsg, true);

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
            }

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
            }

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
                    if (!dt.Rows[0]["Curr_Status"].ToString().Equals(value.EqState))
                    {
                        sql = string.Format(_BaseDataService.UpdateTableEQP_STATUS(value.EqID, value.EqState.ToString()));
                        _dbTool.SQLExec(sql, out tmpMsg, true);
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
            }

            return true;
        }
    }
}
