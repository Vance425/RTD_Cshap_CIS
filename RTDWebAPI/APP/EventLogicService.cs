﻿using Microsoft.Extensions.Configuration;
using NLog;
using RTDWebAPI.Commons.Method.Database;
using RTDWebAPI.Commons.Method.Tools;
using RTDWebAPI.Interface;
using RTDWebAPI.Models;
using RTDWebAPI.Service;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Threading;

namespace RTDWebAPI.APP
{
    public class EventLogicService: BasicService
    {
        string curProcessName = "EventLogicService";
        string tmpMsg = String.Empty;
        IFunctionService _functionService = new FunctionService();
        DBTool dbTool;

        public void Start()
        {
            int minThdWorker = 3;
            int minPortThdComp = 3;
            int maxThdWorker = 5;
            int maxPortThdComp = 5;
            try
            {
                minThdWorker = _configuration["ThreadPools:minThread:workerThread"] is not null ? int.Parse(_configuration["ThreadPools:minThread:workerThread"]) : minThdWorker;
                minPortThdComp = _configuration["ThreadPools:minThread:portThread"] is not null ? int.Parse(_configuration["ThreadPools:minThread:portThread"]) : minThdWorker; ;
                maxThdWorker = _configuration["ThreadPools:maxThread:workerThread"] is not null ? int.Parse(_configuration["ThreadPools:maxThread:workerThread"]) : minThdWorker; ;
                maxPortThdComp = _configuration["ThreadPools:maxThread:portThread"] is not null ? int.Parse(_configuration["ThreadPools:maxThread:portThread"]) : minThdWorker; ;
            }
            catch (Exception ex)
            {
                tmpMsg = "";
                _logger.Debug(tmpMsg);
            }
            int iTime = 0;
            _IsAlive = true;
            
            tmpMsg = String.Format("{0} is Start", curProcessName);
            Console.WriteLine(tmpMsg);
            _logger.Info(string.Format("Info: Start: {0}", tmpMsg));

            ThreadPool.SetMinThreads(minThdWorker, minPortThdComp);//設定執行緒池最小執行緒數
            ThreadPool.SetMaxThreads(maxThdWorker, maxPortThdComp);//設定執行緒池最大執行緒數
            //引數一：執行緒池按需建立的最小工作執行緒數。引數二：執行緒池按需建立的最小非同步I/O執行緒數。

            try
            {
                string tmpDataSource = string.Format("{0}:{1}/{2}", _configuration["DBconnect:Oracle:ip"], _configuration["DBconnect:Oracle:port"], _configuration["DBconnect:Oracle:Name"]);
                string tmpConnectString = string.Format(_configuration["DBconnect:Oracle:connectionString"], tmpDataSource, _configuration["DBconnect:Oracle:user"], _configuration["DBconnect:Oracle:pwd"]);
                string tmpDatabase = _configuration["DBConnect:Oracle:providerName"];
                string tmpAutoDisconn = _configuration["DBConnect:Oracle:autoDisconnect"];
                List<string> _lstDB = new List<string>();
                _lstDB.Add(tmpDataSource);
                _lstDB.Add(tmpConnectString);
                _lstDB.Add(tmpDatabase);
                _lstDB.Add(tmpAutoDisconn);
                String msg = "";

                double uidbAccessNum = 2;
                double percentThread = 0;
                int uiThread = 0;
                //int inUse = 0;
                _inUse = 0;
                int iCurUse = 0;
                int iCompThd = 0;

                
                try
                {
                    /*
                    uiThread = int.Parse(_configuration["ThreadPools:UIThread"]);
                    percentThread = ((double) 100 - (double) uiThread) / 100;
                    if (percentThread <= 0) { 
                        uidbAccessNum = 2; 
                    }
                    else
                    {
                        uidbAccessNum = Math.Ceiling(maxThdWorker * percentThread);
                    }
                    uidbAccessNum = 2;
                    */
                    //_listDBSession.add();
                    //_DBSessionQueue = new ConcurrentQueue<int>();
                    for (int i=1; i<= uidbAccessNum; i++)
                    {
                        dbTool = new DBTool(tmpConnectString, tmpDatabase, tmpAutoDisconn, out msg);
                        _listDBSession.Add(dbTool);
                        //_DBSessionQueue.Enqueue(i);
                    }

                }
                catch(Exception ex)
                {
                    percentThread = 5;
                    uidbAccessNum = 2;
                }
                //percentThread = maxThdWorker - uidbAccessNum;
                
                //uidbAccessNum = 2;

                while (true)
                {
                    //dbTool = new DBTool(tmpConnectString, tmpDatabase, tmpAutoDisconn, out msg);

                    object obj = new object[]
                    {
                        _eventQueue,
                        _lstDB,
                        _configuration,
                        _logger,
                        _threadConntroll,
                        _functionService
                    };

                    //tmpMsg = string.Format("iCurUse][{0}] CompThd [{1}]", iCurUse, iCompThd);
                    //_logger.Debug(tmpMsg);

                    //20230428 Modify by Vance,  keep thread for API/UI
                    ThreadPool.GetAvailableThreads(out iCurUse, out iCompThd);
                    if (iCurUse <= uidbAccessNum)
                        continue;

                    tmpMsg = string.Format("iCurUse][{0}] CompThd [{1}]", iCurUse, iCompThd);
                    Console.WriteLine(tmpMsg);

                    System.Threading.WaitCallback waitCallback = new WaitCallback(listeningStart);
                    ThreadPool.QueueUserWorkItem(waitCallback, obj);

                    for (int i = 0; i < _listDBSession.Count; i++)
                    {
                        try
                        {
                            dbTool = _listDBSession[i];
                            if (!dbTool.IsConnected)
                            {
                                lock (_listDBSession)
                                {
                                    //_listDBSession.RemoveAt(i);

                                    dbTool = new DBTool(tmpConnectString, tmpDatabase, tmpAutoDisconn, out msg);
                                    _listDBSession[i] = dbTool;
                                }
                            }

                        }
                        catch(Exception ex) { }
                    }
                    //inUse--;
                    Thread.Sleep(500);
                }
            }
            catch (Exception ex)
            { _IsAlive = false; }
            finally
            { }
        }

        static void listeningStart(object parms)
        {
            string curPorcessName = "listeningStart";
            string currentThreadNo = "";
            string tmpThreadNo = "ThreadPoolNo_{0}";
            bool _DebugMode = true;
            //To Be
            object[] obj = (object[]) parms;
            ConcurrentQueue<EventQueue> _eventQueue = (ConcurrentQueue<EventQueue>)obj[0];
            Dictionary<string, string> _threadControll = (Dictionary<string, string>)obj[4];
            IConfiguration _configuration = (IConfiguration)obj[2];
            DBTool _dbTool = null; // (DBTool)obj[1];
            Logger _logger = (Logger)obj[3];
            //int inUse = (int)obj[6];
            DataTable dt = null;
            DataTable dtTemp = null;
            DataRow[] dr = null;
            string tmpMsg = "";
            string sql = "";
            //Timer for Sync Lot Info
            int iTimer01 = 0;
            string timeUnit = "";
            //arrayOfCmds is commmand list, 加入清單, 會執行 SentDispatchCommandtoMCS
            //需要先新增一筆WorkingProcess_Sch
            List<string> arrayOfCmds = new List<string>();
            List<string> lstEquipment = new List<string>();
            List<NormalTransferModel> lstNormalTransfer = new List<NormalTransferModel>();
            List<string> lstDB = new List<string>();
            
            IFunctionService _functionService = (FunctionService)obj[5];
            try
            {
                lstDB = (List<string>)obj[1];
                _dbTool = new DBTool(lstDB[1], lstDB[2], lstDB[3], out tmpMsg);

                //ThreadPool當前總數, 方便未來Debug使用
                ///tmpMsg = string.Format("Check: curPorcessName [{0}][ {1}]", curPorcessName, Thread.CurrentThread.Name);
                ///_logger.Debug(tmpMsg);
                //Console.WriteLine(tmpMsg);
                /*
                if (currentThreadNo.Equals(""))
                {
                    Thread.Sleep(30);
                    string curThread = "";
                    if (_threadControll.ContainsKey(curPorcessName))
                    {
                        string vNo = "";
                        int iNo = 0;
                        _threadControll.TryGetValue(curPorcessName, out vNo);
                        iNo = int.Parse(vNo) + 1;
                        currentThreadNo = string.Format(tmpThreadNo, iNo.ToString().Trim());
                        _threadControll[curPorcessName] = iNo.ToString().Trim();;
                    }
                    else
                    {
                        int iNo = 1;
                        _threadControll.Add(curPorcessName, iNo.ToString());
                    }
                    tmpMsg = string.Format("issue: curPorcessName [{0}][ {1}]", curPorcessName, Thread.CurrentThread.Name);
                    _logger.Debug(tmpMsg);
                }
                else
                {
                    tmpMsg = string.Format("thread id [{0}][ {1}]", curPorcessName, Thread.CurrentThread.Name);
                    _logger.Debug(tmpMsg);
                }
                */

                //tmpMsg = string.Format("PorcessName [{0}][ {1}]", curPorcessName, Thread.CurrentThread.Name);
                //_logger.Debug(tmpMsg); 

                string vKey = "false";
                bool passTicket = false;

                bool bCheckTimerState = false;
                string scheduleName = "";
                string timerID = "";
                if (_DebugMode)
                {
                    tmpMsg = string.Format("[DebugMode][{0}] {1}.", "Flag1", IsInitial);
                    //_logger.Debug(tmpMsg);
                }

                if (IsInitial)
                {
                    IsInitial = false;
                    global = _functionService.loadGlobalParams(_dbTool);
                }

                //僅for EWLB臨時使用的UI, 判斷為0時不跑Schedule邏輯
                if (!_configuration["ThreadPools:UIThread"].Equals("0"))
                {

                    //Get RTD Default Set - Timer
                    try
                    {
                        sql = _BaseDataService.SelectRTDDefaultSet("SchTimer");

                        if (_DebugMode)
                        {
                            tmpMsg = string.Format("[DebugMode][{0}] {1}.", "Flag2", sql);
                            //_logger.Debug(tmpMsg);
                        }
                        dt = _dbTool.GetDataTable(sql);
                        if (dt.Rows.Count > 0)
                        {
                            iTimer01 = int.TryParse(dt.Rows[0]["ParamValue"].ToString(), out iTimer01) ? int.Parse(dt.Rows[0]["ParamValue"].ToString()) : 180;
                        }
                        else
                        {
                            iTimer01 = 180;
                        }
                    }
                    catch (Exception ex)
                    {
                        //Default Set
                        iTimer01 = 180;
                    }


                    if (!_threadControll.ContainsKey("ipass"))
                    {
                        _threadControll.Add("ipass", "0");
                    }

                    if (_DebugMode)
                    {
                        tmpMsg = string.Format("[DebugMode][{0}] {1}.", "Flag3", _threadControll["ipass"]);
                        //_logger.Debug(tmpMsg);
                    }

                    if (_threadControll["ipass"].Equals("0"))
                        _threadControll["ipass"] = "1";
                    else
                        _threadControll["ipass"] = "0";


                    if (_threadControll["ipass"].Equals("0"))
                    {
                        //////Auto Check Every 1 Hour
                        scheduleName = "AutoCheckEveryHour";
                        timerID = "lastProcessTime";
                        passTicket = false;
                        if (_threadControll.ContainsKey(scheduleName))
                        {
                            try
                            {
                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag1", scheduleName);
                                    //_logger.Debug(tmpMsg);
                                }

                                lock (_threadControll)
                                {
                                    _threadControll.TryGetValue(scheduleName, out vKey);

                                    if (!vKey.Equals("true"))
                                    {
                                        _threadControll[scheduleName] = "true";
                                        passTicket = true;
                                    }
                                }

                                if (passTicket)
                                {
                                    try
                                    {
                                        DateTime dtCurrent = DateTime.UtcNow;
                                        string vlastDatetime = DateTime.UtcNow.ToString("yyyy-MM-dd HH");
                                        if (_threadControll.ContainsKey(timerID))
                                        {
                                            lock (_threadControll)
                                            {
                                                _threadControll.TryGetValue(timerID, out vlastDatetime);
                                            }

                                            if (!DateTime.UtcNow.ToString("yyyy-MM-dd HH").Equals(vlastDatetime))
                                            {
                                                //每小時重設一次command_streamCode  -- 0, 99999
                                                OracleSequence.SequenceReset(_dbTool, "command_streamCode");
                                                //每小時重設一次uid_streamCode  -- 0, 9999999
                                                OracleSequence.SequenceReset(_dbTool, "uid_streamCode");
                                                lock (_threadControll)
                                                {
                                                    _threadControll[timerID] = DateTime.UtcNow.ToString("yyyy-MM-dd HH");
                                                }

                                                dt = _dbTool.GetDataTable(_BaseDataService.QueryRTDStatisticalByCurrentHour(dtCurrent));
                                                List<string> lstType = new List<string> { "T", "S", "F" };
                                                if (dt.Rows.Count <= 0)
                                                {
                                                    foreach (string tmpType in lstType)
                                                    {
                                                        try
                                                        {
                                                            _dbTool.SQLExec(_BaseDataService.InitialRTDStatistical(dtCurrent.ToString("yyyy-MM-dd HH:mm:ss"), tmpType), out tmpMsg, true);
                                                        }
                                                        catch (Exception ex)
                                                        { }
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            _threadControll.Add("lastProcessTime", vlastDatetime);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        tmpMsg = string.Format("Listening exception [{0}: {1}]", timerID, ex.Message);
                                        _logger.Debug(tmpMsg);
                                    }

                                    if (bCheckTimerState)
                                        _logger.Info(scheduleName);

                                    if (ExcuteMode.Equals(1))
                                    {
                                    }

                                    lock (_threadControll)
                                    {
                                        _threadControll[scheduleName] = "false";
                                    }

                                }

                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag2", scheduleName);
                                    //_logger.Debug(tmpMsg);
                                }
                            }
                            catch (Exception ex)
                            {
                                tmpMsg = string.Format("Listening exception occurred. {0}", ex.Message);
                                _logger.Debug(tmpMsg);

                                lock (_threadControll)
                                {
                                    _threadControll[scheduleName] = "false";
                                }
                            }
                        }
                        else
                        {
                            _threadControll.Add(scheduleName, vKey);
                        }

                        //_logger.Debug("Issue Flag 0426.1");

                        //////Auto Check Every 5 Sec   last 9 ProcessTime
                        scheduleName = "AutoCheckEvery5Sec";
                        timerID = "last9ProcessTime";
                        iTimer01 = 5;
                        timeUnit = "seconds";
                        passTicket = false;
                        if (_threadControll.ContainsKey(scheduleName))
                        {
                            try
                            {
                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag1", scheduleName);
                                    //_logger.Debug(tmpMsg);
                                }

                                vKey = "false";
                                lock (_threadControll)
                                {
                                    _threadControll.TryGetValue(scheduleName, out vKey);

                                    if (!vKey.Equals("true"))
                                    {
                                        _threadControll[scheduleName] = "true";
                                        passTicket = true;
                                    }
                                }

                                if (passTicket)
                                {
                                    bool bDoLogic = false;
                                    try
                                    {
                                        DateTime dtCurrent = DateTime.UtcNow;
                                        string vlastDatetime = dtCurrent.ToString("yyyy-MM-dd HH:mm:ss");
                                        if (_threadControll.ContainsKey(timerID))
                                        {
                                            lock (_threadControll)
                                            {
                                                _threadControll.TryGetValue(timerID, out vlastDatetime);
                                            }

                                            if (_functionService.TimerTool(timeUnit, vlastDatetime) >= iTimer01)
                                            {
                                                bDoLogic = true;
                                                lock (_threadControll)
                                                {
                                                    _threadControll[timerID] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            _threadControll.Add(timerID, vlastDatetime);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        tmpMsg = string.Format("Listening exception [{0}: {1}]", timerID, ex.Message);
                                        _logger.Debug(tmpMsg);
                                    }

                                    if (bDoLogic)
                                    {
                                        if (bCheckTimerState)
                                            _logger.Info(scheduleName);

                                        ///Do Logic 
                                        //Auto Update RTD_Statistical
                                        try
                                        {
                                            tmpMsg = "";
                                            if (_functionService.AutoUpdateRTDStatistical(_dbTool, out tmpMsg))
                                            {
                                                //Console.
                                            }
                                            else
                                            {
                                                //Console.
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            tmpMsg = string.Format("Listening exception [AutoUpdateRTDStatistical: {0}]", ex.Message);
                                            _logger.Debug(tmpMsg);
                                        }


                                        bDoLogic = false;

                                        lock (_threadControll)
                                        {
                                            _threadControll[timerID] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                        }
                                    }
                                }

                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag2", scheduleName);
                                    //_logger.Debug(tmpMsg);
                                }
                            }
                            catch (Exception ex)
                            {
                                tmpMsg = string.Format("Listening exception occurred. {0}", ex.Message);
                                _logger.Debug(tmpMsg);

                                lock (_threadControll)
                                {
                                    _threadControll[scheduleName] = "false";
                                }
                            }
                            finally
                            {
                                lock (_threadControll)
                                {
                                    _threadControll[scheduleName] = "false";
                                }
                            }
                        }
                        else
                        {
                            _threadControll.Add(scheduleName, vKey);
                        }

                        //_logger.Debug("Issue Flag 0426.2");

                        //////Auto Check Every 10 Sec
                        scheduleName = "AutoCheckEvery10Sec";
                        timerID = "last2ProcessTime";
                        iTimer01 = 10;
                        timeUnit = "seconds";
                        passTicket = false;
                        if (_threadControll.ContainsKey(scheduleName))
                        {
                            try
                            {
                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag1", scheduleName);
                                    //_logger.Debug(tmpMsg);
                                }

                                vKey = "false";

                                _threadControll.TryGetValue(scheduleName, out vKey);

                                if (!vKey.Equals("true"))
                                {
                                    lock (_threadControll)
                                    {
                                        _threadControll[scheduleName] = "true";
                                        passTicket = true;
                                    }
                                }

                                if (passTicket)
                                {
                                    bool bDoLogic = false;
                                    try
                                    {
                                        DateTime dtCurrent = DateTime.UtcNow;
                                        string vlastDatetime = dtCurrent.ToString("yyyy-MM-dd HH:mm:ss");
                                        if (_threadControll.ContainsKey(timerID))
                                        {
                                            lock (_threadControll)
                                            {
                                                _threadControll.TryGetValue(timerID, out vlastDatetime);
                                            }

                                            if (_functionService.TimerTool(timeUnit, vlastDatetime) >= iTimer01)
                                            {
                                                bDoLogic = true;
                                                lock (_threadControll)
                                                {
                                                    _threadControll[timerID] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            _threadControll.Add(timerID, vlastDatetime);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        tmpMsg = string.Format("Listening exception [{0}: {1}]", timerID, ex.Message);
                                        _logger.Debug(tmpMsg);
                                    }

                                    if (bDoLogic)
                                    {
                                        if (bCheckTimerState)
                                            _logger.Info(scheduleName);

                                        //Auto Sent InfoUpdate to MCS for Bind lot
                                        

                                        try
                                        {
                                            tmpMsg = "";

                                            if (_functionService.AutoBindAndSentInfoUpdate(_dbTool, _configuration, _logger, out tmpMsg))
                                            {
                                                //Console.
                                            }
                                            else
                                            {
                                                //Console.
                                            }

                                            //AutoBindAndSentInfoUpdate

                                        }
                                        catch (Exception ex)
                                        {
                                            tmpMsg = string.Format("Listening exception [AutoSentInfoUpdate: {0}]", ex.Message);
                                            _logger.Debug(tmpMsg);
                                        }

                                        //UpdateEquipmentAssociateToReady
                                        try
                                        {
                                            if (_DebugMode)
                                            {
                                                tmpMsg = string.Format("[scheduleName][{0}].Flag1-2", scheduleName);
                                                //_logger.Debug(tmpMsg);
                                            }

                                            if (true)
                                            {
                                                tmpMsg = "";
                                                if (_functionService.UpdateEquipmentAssociateToReady(_dbTool, out _eventQueue))
                                                {
                                                    //Console.WriteLine(String.Format("Check Lot Info completed."));
                                                }
                                                else
                                                {
                                                    //Console.WriteLine(String.Format("Check lot info failed."));
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            tmpMsg = string.Format("Listening exception [UpdateEquipmentAssociateToReady: {0}]", ex.Message);
                                            _logger.Debug(tmpMsg);
                                        }

                                        //Auto Check Equipment Status 檢查即時的 Equipment Status
                                        bool byPass = false;
                                        try
                                        {
                                            if (_DebugMode)
                                            {
                                                tmpMsg = string.Format("[scheduleName][{0}].Flag1-3", scheduleName);
                                                //_logger.Debug(tmpMsg);
                                            }

                                            if (true)
                                            {
                                                tmpMsg = "";
                                                if (_functionService.AutoCheckEquipmentStatus(_dbTool, out _eventQueue))
                                                {
                                                    //Console.WriteLine(String.Format("Check Lot Info completed."));
                                                    byPass = true;
                                                }
                                                else
                                                {
                                                    //Console.WriteLine(String.Format("Check lot info failed."));
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            tmpMsg = string.Format("Listening exception [AutoCheckEquipmentStatus: {0}]", ex.Message);
                                            _logger.Debug(tmpMsg);
                                        }

                                        //Abnormaly Equipment Status 檢查即時的 Equipment Status
                                        if (byPass)
                                        {
                                            try
                                            {
                                                if (_DebugMode)
                                                {
                                                    tmpMsg = string.Format("[scheduleName][{0}].Flag1-4", scheduleName);
                                                    //_logger.Debug(tmpMsg);
                                                }

                                                tmpMsg = "";
                                                if (_functionService.AbnormalyEquipmentStatus(_dbTool, _logger, _DebugMode, _eventQueue, out lstNormalTransfer))
                                                {
                                                    //Console.WriteLine(String.Format("Check Lot Info completed."));
                                                }
                                                else
                                                {
                                                    //Console.WriteLine(String.Format("Check lot info failed."));
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                tmpMsg = string.Format("Listening exception [AbnormalyEquipmentStatus: {0}]", ex.Message);
                                                _logger.Debug(tmpMsg);
                                            }
                                        }

                                        if (_DebugMode)
                                        {
                                            tmpMsg = string.Format("[scheduleName][{0}].Flag1-5", scheduleName);
                                            //_logger.Debug(tmpMsg);
                                        }

                                        ExcuteMode = _functionService.GetExecuteMode(_dbTool);

                                        bDoLogic = false;
                                        lock (_threadControll)
                                        {
                                            _threadControll[timerID] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                        }
                                    }
                                }

                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag2", scheduleName);
                                    //_logger.Debug(tmpMsg);
                                }
                            }
                            catch (Exception ex)
                            {
                                tmpMsg = string.Format("Listening exception occurred. {0}", ex.Message);
                                _logger.Debug(tmpMsg);

                                lock (_threadControll)
                                {
                                    _threadControll[scheduleName] = "false";
                                }
                            }
                            finally
                            {
                                lock (_threadControll)
                                {
                                    _threadControll[scheduleName] = "false";
                                }
                            }
                        }
                        else
                        {
                            _threadControll.Add(scheduleName, vKey);
                        }

                        //////Auto Check Every 15 Sec last 7 ProcessTime
                        scheduleName = "AutoCheckEvery15Sec";
                        timerID = "last7ProcessTime";
                        iTimer01 = 15;
                        timeUnit = "seconds";
                        passTicket = false;
                        if (_threadControll.ContainsKey(scheduleName))
                        {
                            try
                            {
                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag1", scheduleName);
                                    //_logger.Debug(tmpMsg);
                                }

                                vKey = "false";
                                lock (_threadControll)
                                {
                                    _threadControll.TryGetValue(scheduleName, out vKey);

                                    if (!vKey.Equals("true"))
                                    {
                                        _threadControll[scheduleName] = "true";
                                        passTicket = true;
                                    }
                                }

                                if (passTicket)
                                {
                                    bool bDoLogic = false;
                                    try
                                    {
                                        DateTime dtCurrent = DateTime.UtcNow;
                                        string vlastDatetime = dtCurrent.ToString("yyyy-MM-dd HH:mm:ss");
                                        if (_threadControll.ContainsKey(timerID))
                                        {
                                            lock (_threadControll)
                                            {
                                                _threadControll.TryGetValue(timerID, out vlastDatetime);
                                            }

                                            if (_functionService.TimerTool(timeUnit, vlastDatetime) >= iTimer01)
                                            {
                                                bDoLogic = true;
                                                lock (_threadControll)
                                                {
                                                    _threadControll[timerID] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            _threadControll.Add(timerID, vlastDatetime);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        tmpMsg = string.Format("Listening exception [{0}: {1}]", timerID, ex.Message);
                                        _logger.Debug(tmpMsg);
                                    }

                                    if (bDoLogic)
                                    {
                                        if (bCheckTimerState)
                                            _logger.Info(scheduleName);
                                        ///Do Schdule Logic
                                        ///
                                        //Sync Equipment 同步RTS 的 Equipment信息
                                        try
                                        {
                                            if (_DebugMode)
                                            {
                                                tmpMsg = string.Format("[scheduleName][{0}].Flag1-1", scheduleName);
                                                //_logger.Debug(tmpMsg);
                                            }

                                            tmpMsg = "";
                                            if (_functionService.SyncEquipmentData(_dbTool))
                                            {
#if DEBUG
                                                //Debug mode will do this
#else

                                //这里在非 DEBUG 模式下编译
#endif
                                            }
                                            else
                                            {
                                                //Console.WriteLine(String.Format("Check lot info failed."));
                                            }

                                            //SelectRTDDefaultSet
                                            sql = string.Format(_BaseDataService.SelectRTDDefaultSet("DEBUGMODE"));
                                            dtTemp = _dbTool.GetDataTable(sql);
                                            if (dtTemp.Rows.Count > 0)
                                            {
                                                DebugMode = dtTemp.Rows[0]["paramvalue"].ToString().ToUpper().Equals("TRUE") ? true : false;
                                                _DebugMode = DebugMode;
                                                //_logger.Debug(DebugMode);
                                                //_logger.Debug(_DebugMode);
                                            }
                                            else
                                                _DebugMode = false;
                                        }
                                        catch (Exception ex)
                                        {
                                            tmpMsg = string.Format("Listening exception [SyncEquipmentData: {0}]", ex.Message);
                                            _logger.Debug(tmpMsg);
                                        }

                                        //Check Lot & Carrier Associate. 檢查 Lot與 Carrier的關聯
                                        try
                                        {
                                            if (_DebugMode)
                                            {
                                                tmpMsg = string.Format("[scheduleName][{0}].Flag1-2", scheduleName);
                                                //_logger.Debug(tmpMsg);
                                            }

                                            tmpMsg = "";
                                            if (_functionService.CheckLotCarrierAssociate(_dbTool, _logger))
                                            {
                                                //Console.WriteLine(String.Format("Check Lot Info completed."));
                                            }
                                            else
                                            {
                                                //Console.WriteLine(String.Format("Check lot info failed."));
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            tmpMsg = string.Format("Listening exception [CheckLotCarrierAssociate: {0}]", ex.Message);
                                            _logger.Debug(tmpMsg);
                                        }

                                        bDoLogic = false;

                                        lock (_threadControll)
                                        {
                                            _threadControll[timerID] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                        }
                                    }
                                }

                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag2", scheduleName);
                                    //_logger.Debug(tmpMsg);
                                }
                            }
                            catch (Exception ex)
                            {
                                tmpMsg = string.Format("Listening exception occurred. {0}", ex.Message);
                                _logger.Debug(tmpMsg);

                                lock (_threadControll)
                                {
                                    _threadControll[scheduleName] = "false";
                                }
                            }
                            finally
                            {
                                lock (_threadControll)
                                {
                                    _threadControll[scheduleName] = "false";
                                }
                            }
                        }
                        else
                        {
                            _threadControll.Add(scheduleName, vKey);
                        }

                        //////Auto Check Every 30 Sec
                        scheduleName = "AutoCheckEvery30Sec";
                        timerID = "last14ProcessTime";
                        iTimer01 = 30;
                        timeUnit = "seconds";
                        passTicket = false;
                        if (_threadControll.ContainsKey(scheduleName))
                        {
                            try
                            {
                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag1", scheduleName);
                                    //_logger.Debug(tmpMsg);
                                }

                                vKey = "false";
                                lock (_threadControll)
                                {
                                    _threadControll.TryGetValue(scheduleName, out vKey);

                                    if (!vKey.Equals("true"))
                                    {
                                        _threadControll[scheduleName] = "true";
                                        passTicket = true;
                                    }
                                }

                                if (passTicket)
                                {
                                    bool bDoLogic = false;
                                    try
                                    {
                                        DateTime dtCurrent = DateTime.UtcNow;
                                        string vlastDatetime = dtCurrent.ToString("yyyy-MM-dd HH:mm:ss");
                                        if (_threadControll.ContainsKey(timerID))
                                        {
                                            _threadControll.TryGetValue(timerID, out vlastDatetime);

                                            if (_functionService.TimerTool(timeUnit, vlastDatetime) >= iTimer01)
                                            {
                                                bDoLogic = true;
                                                lock (_threadControll)
                                                {
                                                    _threadControll[timerID] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            _threadControll.Add(timerID, vlastDatetime);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        tmpMsg = string.Format("Listening exception [{0}: {1}]", timerID, ex.Message);
                                        _logger.Debug(tmpMsg);
                                    }

                                    if (bDoLogic)
                                    {
                                        if (bCheckTimerState)
                                            _logger.Info(scheduleName);
                                        ///Do Schdule Logic
                                        try
                                        {
                                            tmpMsg = "";

                                            if (_functionService.AutoSentInfoUpdate(_dbTool, _configuration, _logger, out tmpMsg))
                                            {
                                                //Console.
                                            }
                                            else
                                            {
                                                //Console.
                                            }

                                            //AutoBindAndSentInfoUpdate

                                        }
                                        catch (Exception ex)
                                        {
                                            tmpMsg = string.Format("Listening exception [AutoSentInfoUpdate: {0}]", ex.Message);
                                            _logger.Debug(tmpMsg);
                                        }

                                        //Auto Assign Carrier Type to Carrier Transfer
                                        try
                                        {
                                            tmpMsg = "";
                                            if (_functionService.AutoAssignCarrierType(_dbTool, out tmpMsg))
                                            {
                                                //Console.WriteLine(String.Format("Check Lot Info completed."));
                                            }
                                            else
                                            {
                                                //Console.WriteLine(String.Format("Check lot info failed."));
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            tmpMsg = string.Format("Listening exception [AutoAssignCarrierType: {0}]", ex.Message);
                                            _logger.Debug(tmpMsg);
                                        }

                                        //Check Lot Info 同步上位所有的Lot信息
                                        try
                                        {
                                            tmpMsg = "";
                                            //SelectTableADSData

                                            dt = _dbTool.GetDataTable(_BaseDataService.SelectTableADSData(_functionService.GetExtenalTables(_configuration, "SyncExtenalData", "AdsInfo")));

                                            if (dt.Rows.Count > 0)
                                            {
                                                //Do Nothing
                                            }

                                        }
                                        catch (Exception ex)
                                        {
                                            tmpMsg = string.Format("Listening exception [CheckLotInfo: {0}]", ex.Message);
                                            _logger.Debug(tmpMsg);

                                            //異常發生, 不執行DBrecover的動作, 防止DB不正常斷線！
                                            //_functionService.RecoverDatabase(_dbTool, _logger, ex.Message);

                                            //if (ex.Message.IndexOf("ORA-03150") > 0 || ex.Message.IndexOf("ORA-02063") > 0)
                                            //{
                                            //    //Jcet database connection exception occurs, the connection will automatically re-established 

                                            //    string tmp2Msg = "";

                                            //    if (_dbTool.dbPool.CheckConnet(out tmpMsg))
                                            //    {
                                            //        _dbTool.DisConnectDB(out tmp2Msg);

                                            //        if (!tmp2Msg.Equals(""))
                                            //        {
                                            //            _logger.Debug(string.Format("DB disconnect failed [{0}]", tmp2Msg));
                                            //        }
                                            //        else
                                            //        {
                                            //            _logger.Debug(string.Format("Database disconect."));
                                            //        }

                                            //        if (!_dbTool.IsConnected)
                                            //        {
                                            //            _logger.Debug(string.Format("Database re-established."));
                                            //            _dbTool.ConnectDB(out tmp2Msg);
                                            //        }
                                            //    }
                                            //    else
                                            //    {
                                            //        if (!_dbTool.IsConnected)
                                            //        {
                                            //            string[] _argvs = new string[] { "", "", "" };
                                            //            if (_functionService.CallRTDAlarm(_dbTool, 20100, _argvs))
                                            //            {
                                            //                _logger.Debug(string.Format("Database re-established."));
                                            //                _dbTool.ConnectDB(out tmp2Msg);
                                            //            }
                                            //        }
                                            //    }

                                            //    if (!tmp2Msg.Equals(""))
                                            //    {
                                            //        _logger.Debug(string.Format("DB re-established failed [{0}]", tmp2Msg));
                                            //    }
                                            //    else
                                            //    {
                                            //        string[] _argvs = new string[] { "", "", "" };
                                            //        if (_functionService.CallRTDAlarm(_dbTool, 20101, _argvs))
                                            //        {
                                            //            _logger.Debug(string.Format("DB re-connection sucess", tmp2Msg));
                                            //        }
                                            //    }
                                            //}
                                        }

                                        bDoLogic = false;

                                        lock (_threadControll)
                                        {
                                            _threadControll[timerID] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                        }
                                    }
                                }

                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag2", scheduleName);
                                    //_logger.Debug(tmpMsg);
                                }
                            }
                            catch (Exception ex)
                            {
                                tmpMsg = string.Format("Listening exception occurred. {0}", ex.Message);
                                _logger.Debug(tmpMsg);

                                lock (_threadControll)
                                {
                                    _threadControll[scheduleName] = "false";
                                }
                            }
                            finally
                            {
                                lock (_threadControll)
                                {
                                    _threadControll[scheduleName] = "false";
                                }
                            }
                        }
                        else
                        {
                            _threadControll.Add(scheduleName, vKey);
                        }

                        //////Auto Check Every 60 Sec
                        scheduleName = "AutoCheckEvery60Sec";
                        timerID = "last4ProcessTime";
                        iTimer01 = 1;
                        timeUnit = "minutes";
                        passTicket = false;
                        if (_threadControll.ContainsKey(scheduleName))
                        {
                            try
                            {
                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag1", scheduleName);
                                    //_logger.Debug(tmpMsg);
                                }

                                vKey = "false";
                                lock (_threadControll)
                                {
                                    _threadControll.TryGetValue(scheduleName, out vKey);

                                    if (!vKey.Equals("true"))
                                    {
                                        _threadControll[scheduleName] = "true";
                                        passTicket = true;
                                    }
                                }

                                if (passTicket)
                                {
                                    bool bDoLogic = false;
                                    try
                                    {
                                        DateTime dtCurrent = DateTime.UtcNow;
                                        string vlastDatetime = dtCurrent.ToString("yyyy-MM-dd HH:mm:ss");
                                        if (_threadControll.ContainsKey(timerID))
                                        {
                                            lock (_threadControll)
                                            {
                                                _threadControll.TryGetValue(timerID, out vlastDatetime);
                                            }

                                            if (_functionService.TimerTool(timeUnit, vlastDatetime) >= iTimer01)
                                            {
                                                bDoLogic = true;
                                                lock (_threadControll)
                                                {
                                                    _threadControll[timerID] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            _threadControll.Add(timerID, vlastDatetime);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        tmpMsg = string.Format("Listening exception [{0}: {1}]", timerID, ex.Message);
                                        _logger.Debug(tmpMsg);
                                    }

                                    if (bDoLogic)
                                    {
                                        if (bCheckTimerState)
                                            _logger.Info(scheduleName);

                                        ///Do Logic 

                                        //Check Lot & Equipment Associate.檢查 Lot與 Equipment的關聯
                                        try
                                        {
                                            if (_DebugMode)
                                            {
                                                tmpMsg = string.Format("[scheduleName][{0}].Flag1-3", scheduleName);
                                                //_logger.Debug(tmpMsg);
                                            }

                                            tmpMsg = "";
                                            if (_functionService.CheckLotEquipmentAssociate(_dbTool, out _eventQueue))
                                            {
                                                //Console.WriteLine(String.Format("Check Lot Info completed."));
                                            }
                                            else
                                            {
                                                //Console.WriteLine(String.Format("Check lot info failed."));
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            tmpMsg = string.Format("Listening exception [CheckLotEquipmentAssociate: {0}]", ex.Message);
                                            _logger.Debug(tmpMsg);
                                        }

                                        tmpMsg = "";
                                        if (_dbTool.SQLExec(_BaseDataService.SyncStageInfo(), out tmpMsg, true))
                                        {
                                            if (!tmpMsg.Equals(""))
                                            {
                                                _logger.Debug(string.Format("[{0}] SyncStageInfo Failed. {1}", scheduleName, tmpMsg));
                                            }
                                        }

                                        //Sync Extenal Carrier Data.
                                        tmpMsg = "";
                                        if (_functionService.SyncExtenalCarrier(_dbTool, _configuration, _logger))
                                        {
                                            //Do Nothing
                                        }

                                        //PreDispatchToErack.
                                        tmpMsg = "";
                                        if (_functionService.PreDispatchToErack(_dbTool, _configuration, _eventQueue, _logger))
                                        {
                                            //Do Nothing
                                        }

                                        try
                                        {
                                            tmpMsg = "";
                                            string _args = "";

                                            DateTime dtCurrent = DateTime.UtcNow;
                                            DateTime tmpDT = DateTime.UtcNow;
                                            //Check eqp_reserve_time --effective=1 and expired=0
                                            sql = _BaseDataService.CheckReserveState("reserveend");
                                            dt = _dbTool.GetDataTable(sql);

                                            if (dt.Rows.Count > 0)
                                            {
                                                foreach (DataRow drTemp in dt.Rows)
                                                {
                                                    try
                                                    {
                                                        tmpDT = Convert.ToDateTime(drTemp["dt_end"].ToString());
                                                        if (dtCurrent > tmpDT)
                                                        {
                                                            sql = _BaseDataService.ManualModeSwitch(drTemp["equipid"].ToString(), false);
                                                            _logger.Debug(sql);
                                                            _dbTool.SQLExec(sql, out tmpMsg, true);
                                                            if (!tmpMsg.Equals(""))
                                                                _logger.Debug(tmpMsg);

                                                            _args = string.Format("{0},{1},{2},{3}", "expired", drTemp["equipid"].ToString(), "RTD", "1");
                                                            sql = _BaseDataService.UpdateEquipReserve(_args);
                                                            _logger.Debug(sql);
                                                            _dbTool.SQLExec(sql, out tmpMsg, true);
                                                            if (!tmpMsg.Equals(""))
                                                                _logger.Debug(tmpMsg);
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        tmpMsg = String.Format("[Exception] {0}", ex.Message);
                                                        _logger.Debug(tmpMsg);
                                                    }
                                                }
                                            }
                                            //Check eqp_reserve_time --effective=0 and expired=0
                                            sql = _BaseDataService.CheckReserveState("reservestart");
                                            dt = _dbTool.GetDataTable(sql);
                                            if (dt.Rows.Count > 0)
                                            {
                                                foreach (DataRow drTemp in dt.Rows)
                                                {
                                                    try
                                                    {
                                                        tmpDT = Convert.ToDateTime(drTemp["dt_start"].ToString());
                                                        if (dtCurrent > tmpDT)
                                                        {
                                                            sql = _BaseDataService.ManualModeSwitch(drTemp["equipid"].ToString(), true);
                                                            _logger.Debug(sql);
                                                            _dbTool.SQLExec(sql, out tmpMsg, true);
                                                            if (!tmpMsg.Equals(""))
                                                                _logger.Debug(tmpMsg);

                                                            _args = string.Format("{0},{1},{2},{3}", "effective", drTemp["equipid"].ToString(), "RTD", "1");
                                                            sql = _BaseDataService.UpdateEquipReserve(_args);
                                                            _logger.Debug(sql);
                                                            _dbTool.SQLExec(sql, out tmpMsg, true);
                                                            if (!tmpMsg.Equals(""))
                                                                _logger.Debug(tmpMsg);
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        tmpMsg = String.Format("[Exception] {0}", ex.Message);
                                                        _logger.Debug(tmpMsg);
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            tmpMsg = string.Format("Listening exception [Check Equipment Reserve Status: {0}]", ex.Message);
                                            _logger.Debug(tmpMsg);

                                        }

                                        bDoLogic = false;

                                        lock (_threadControll)
                                        {
                                            _threadControll[timerID] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                        }
                                    }
                                }

                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag2", scheduleName);
                                    //_logger.Debug(tmpMsg);
                                }
                            }
                            catch (Exception ex)
                            {
                                tmpMsg = string.Format("Listening exception occurred. {0}", ex.Message);
                                _logger.Debug(tmpMsg);

                                lock (_threadControll)
                                {
                                    _threadControll[scheduleName] = "false";
                                }
                            }
                            finally
                            {
                                lock (_threadControll)
                                {
                                    _threadControll[scheduleName] = "false";
                                }
                            }
                        }
                        else
                        {
                            _threadControll.Add(scheduleName, vKey);
                        }

                        //////Auto Check Lot Info
                        scheduleName = "AutoCheckLotInfo";
                        timerID = "last5ProcessTime";
                        iTimer01 = global.ChkLotInfo.Time;
                        timeUnit = global.ChkLotInfo.TimeUnit;
                        passTicket = false;
                        if (_threadControll.ContainsKey(scheduleName))
                        {
                            try
                            {
                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag1", scheduleName);
                                    //_logger.Debug(tmpMsg);
                                }

                                vKey = "false";
                                lock (_threadControll)
                                {
                                    _threadControll.TryGetValue(scheduleName, out vKey);

                                    if (!vKey.Equals("true"))
                                    {
                                        _threadControll[scheduleName] = "true";
                                        passTicket = true;
                                    }
                                }

                                if (passTicket)
                                {
                                    bool bDoLogic = false;
                                    try
                                    {
                                        DateTime dtCurrent = DateTime.UtcNow;
                                        string vlastDatetime = dtCurrent.ToString("yyyy-MM-dd HH:mm:ss");
                                        if (_threadControll.ContainsKey(timerID))
                                        {
                                            lock (_threadControll)
                                            {
                                                _threadControll.TryGetValue(timerID, out vlastDatetime);
                                            }

                                            if (_functionService.TimerTool(timeUnit, vlastDatetime) >= iTimer01)
                                            {
                                                bDoLogic = true;
                                                lock (_threadControll)
                                                {
                                                    _threadControll[timerID] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            _threadControll.Add(timerID, vlastDatetime);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        tmpMsg = string.Format("Listening exception [{0}: {1}]", timerID, ex.Message);
                                        _logger.Debug(tmpMsg);
                                    }

                                    if (bDoLogic)
                                    {
                                        if (bCheckTimerState)
                                            _logger.Info(scheduleName);
                                        ///Do Logic 

                                        //Check Lot Info 同步上位所有的Lot信息
                                        try
                                        {
                                            tmpMsg = "";
                                            if (_functionService.CheckLotInfo(_dbTool, _configuration, _logger))
                                            {
#if DEBUG
                                                //Debug mode will do this
#else

                                //这里在非 DEBUG 模式下编译
#endif
                                            }
                                            else
                                            {
                                                //Console.WriteLine(String.Format("Check lot info failed."));
                                            }

                                        }
                                        catch (Exception ex)
                                        {
                                            tmpMsg = string.Format("Listening exception [CheckLotInfo: {0}]", ex.Message);
                                            _logger.Debug(tmpMsg);
                                        }

                                        bDoLogic = false;

                                        lock (_threadControll)
                                        {
                                            _threadControll[timerID] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                        }
                                    }
                                }

                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag2", scheduleName);
                                    //_logger.Debug(tmpMsg);
                                }
                            }
                            catch (Exception ex)
                            {
                                tmpMsg = string.Format("Listening exception occurred. {0}", ex.Message);
                                _logger.Debug(tmpMsg);

                                lock (_threadControll)
                                {
                                    _threadControll[scheduleName] = "false";
                                }
                            }
                            finally
                            {
                                lock (_threadControll)
                                {
                                    _threadControll[scheduleName] = "false";
                                }
                            }
                        }
                        else
                        {
                            _threadControll.Add(scheduleName, vKey);
                        }

                        //////Auto Check Every 180 Sec = 3 mins
                        scheduleName = "AutoCheckEvery3mins";
                        timerID = "last13ProcessTime";
                        iTimer01 = int.Parse(_configuration["ReflushTime:ReflusheRack:Time"]);
                        timeUnit = _configuration["ReflushTime:ReflusheRack:TimeUnit"];
                        passTicket = false;
                        if (_threadControll.ContainsKey(scheduleName))
                        {
                            try
                            {
                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag1", scheduleName);
                                    //_logger.Debug(tmpMsg);
                                }

                                vKey = "false";
                                lock (_threadControll)
                                {
                                    _threadControll.TryGetValue(scheduleName, out vKey);

                                    if (!vKey.Equals("true"))
                                    {
                                        _threadControll[scheduleName] = "true";
                                        passTicket = true;
                                    }
                                }

                                if (passTicket)
                                {
                                    bool bDoLogic = false;
                                    try
                                    {
                                        DateTime dtCurrent = DateTime.UtcNow;
                                        string vlastDatetime = dtCurrent.ToString("yyyy-MM-dd HH:mm:ss");
                                        if (_threadControll.ContainsKey(timerID))
                                        {
                                            lock (_threadControll)
                                            {
                                                _threadControll.TryGetValue(timerID, out vlastDatetime);
                                            }

                                            if (_functionService.TimerTool(timeUnit, vlastDatetime) >= iTimer01)
                                            {
                                                bDoLogic = true;
                                                lock (_threadControll)
                                                {
                                                    _threadControll[timerID] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            _threadControll.Add(timerID, vlastDatetime);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        tmpMsg = string.Format("Listening exception [{0}: {1}]", timerID, ex.Message);
                                        _logger.Debug(tmpMsg);
                                    }

                                    if (bDoLogic)
                                    {
                                        if (bCheckTimerState)
                                            _logger.Info(scheduleName);
                                        ///Do Logic 

                                        //Check Lot Info 同步上位所有的Lot信息
                                        try
                                        {
                                            tmpMsg = "";


                                        }
                                        catch (Exception ex)
                                        {
                                            tmpMsg = string.Format("Listening exception [CheckLotInfo: {0}]", ex.Message);
                                            _logger.Debug(tmpMsg);

                                        }

                                        //auto sync stage 
                                        try
                                        {
                                            tmpMsg = "";

                                            sql = _BaseDataService.QueryLotStageWhenStageChange(_configuration["CheckLotStage:Table"]);
                                            dt = _dbTool.GetDataTable(sql);

                                            if (dt.Rows.Count > 0)
                                            {
                                                foreach (DataRow drTemp in dt.Rows)
                                                {
                                                    if (_functionService.TriggerCarrierInfoUpdate(_dbTool, _configuration, _logger, drTemp["lot_id"].ToString()))
                                                    {

                                                        if (_configuration["AppSettings:Work"].ToUpper().Equals("EWLB"))
                                                        {
                                                            //EWLB因為無法透過同步更新lotinfo的stage, 所以需要由程式判斷狀態已改變
                                                            //Send InfoUpdate
                                                            sql = _BaseDataService.UpdateStageByLot(drTemp["lot_id"].ToString(), drTemp["stage"].ToString());
                                                            _logger.Debug(sql);
                                                            _dbTool.SQLExec(sql, out tmpMsg, true);
                                                        }

                                                        if (!tmpMsg.Equals(""))
                                                            _logger.Debug(tmpMsg);
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            tmpMsg = string.Format("Listening exception [QueryLotStageWhenStageChange: {0}]", ex.Message);
                                            _logger.Debug(tmpMsg);

                                        }
                                    }
                                }

                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag2", scheduleName);
                                    //_logger.Debug(tmpMsg);
                                }
                            }
                            catch (Exception ex)
                            {
                                tmpMsg = string.Format("Listening exception occurred. {0}", ex.Message);
                                _logger.Debug(tmpMsg);

                                lock (_threadControll)
                                {
                                    _threadControll[scheduleName] = "false";
                                }
                            }
                            finally
                            {
                                lock (_threadControll)
                                {
                                    _threadControll[scheduleName] = "false";
                                }
                            }
                        }
                        else
                        {
                            _threadControll.Add(scheduleName, vKey);
                        }

                        //////Auto Check Every 300 Sec = 5 mins     last 6 ProcessTime
                        scheduleName = "AutoCheckEvery5mins";
                        timerID = "last6ProcessTime";
                        iTimer01 = 5;
                        timeUnit = "Minutes";
                        passTicket = false;
                        if (_threadControll.ContainsKey(scheduleName))
                        {
                            try
                            {
                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag1", scheduleName);
                                    //_logger.Debug(tmpMsg);
                                }

                                vKey = "false";
                                lock (_threadControll)
                                {
                                    _threadControll.TryGetValue(scheduleName, out vKey);

                                    if (!vKey.Equals("true"))
                                    {
                                        _threadControll[scheduleName] = "true";
                                        passTicket = true;
                                    }
                                }

                                if (passTicket)
                                {
                                    bool bDoLogic = false;
                                    try
                                    {
                                        DateTime dtCurrent = DateTime.UtcNow;
                                        string vlastDatetime = dtCurrent.ToString("yyyy-MM-dd HH:mm:ss");
                                        if (_threadControll.ContainsKey(timerID))
                                        {
                                            lock (_threadControll)
                                            {
                                                _threadControll.TryGetValue(timerID, out vlastDatetime);
                                            }

                                            if (_functionService.TimerTool(timeUnit, vlastDatetime) >= iTimer01)
                                            {
                                                bDoLogic = true;
                                                lock (_threadControll)
                                                {
                                                    _threadControll[timerID] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            _threadControll.Add(timerID, vlastDatetime);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        tmpMsg = string.Format("Listening exception [{0}: {1}]", timerID, ex.Message);
                                        _logger.Debug(tmpMsg);
                                    }

                                    if (bDoLogic)
                                    {
                                        if (bCheckTimerState)
                                            _logger.Info(scheduleName);
                                        ///Do Logic 

                                        if (_functionService.AutoHoldForDispatchIssue(_dbTool, _configuration, _logger, out tmpMsg))
                                        {
                                            //Do Nothing.
                                        }

                                        //與上位同步當前機台狀態 (超過5分鐘, 機台狀態不一致時同步)
                                        if (_functionService.SyncEQPStatus(_dbTool, _logger))
                                        {
                                            //Do Nothing.
                                        }

                                        bDoLogic = false;

                                        lock (_threadControll)
                                        {
                                            _threadControll[timerID] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                        }
                                    }
                                }

                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag2", scheduleName);
                                    //_logger.Debug(tmpMsg);
                                }
                            }
                            catch (Exception ex)
                            {
                                tmpMsg = string.Format("Listening exception occurred. {0}", ex.Message);
                                _logger.Debug(tmpMsg);

                                lock (_threadControll)
                                {
                                    _threadControll[scheduleName] = "false";
                                }
                            }
                            finally
                            {
                                lock (_threadControll)
                                {
                                    _threadControll[scheduleName] = "false";
                                }
                            }
                        }
                        else
                        {
                            _threadControll.Add(scheduleName, vKey);
                        }

                        //////Auto Check Every 600 Sec = 10 mins     last 8 ProcessTime
                        scheduleName = "AutoCheckEvery10mins";
                        timerID = "last8ProcessTime";
                        iTimer01 = 10;
                        timeUnit = "Minutes";
                        passTicket = false;
                        if (_threadControll.ContainsKey(scheduleName))
                        {
                            try
                            {
                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag1", scheduleName);
                                   //_logger.Debug(tmpMsg);
                                }

                                vKey = "false";
                                lock (_threadControll)
                                {
                                    _threadControll.TryGetValue(scheduleName, out vKey);

                                    if (!vKey.Equals("true"))
                                    {
                                        _threadControll["AutoCheckEvery10mins"] = "true";
                                        passTicket = true;
                                    }
                                }

                                if (passTicket)
                                {
                                    bool bDoLogic = false;
                                    try
                                    {
                                        DateTime dtCurrent = DateTime.UtcNow;
                                        string vlastDatetime = dtCurrent.ToString("yyyy-MM-dd HH:mm:ss");
                                        if (_threadControll.ContainsKey(timerID))
                                        {
                                            lock (_threadControll)
                                            {
                                                _threadControll.TryGetValue(timerID, out vlastDatetime);
                                            }

                                            if (_functionService.TimerTool(timeUnit, vlastDatetime) >= iTimer01)
                                            {
                                                bDoLogic = true;
                                                lock (_threadControll)
                                                {
                                                    _threadControll[timerID] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            _threadControll.Add(timerID, vlastDatetime);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        tmpMsg = string.Format("Listening exception [last8ProcessTime: {0}]", ex.Message);
                                        _logger.Debug(tmpMsg);
                                    }

                                    if (bDoLogic)
                                    {
                                        if (bCheckTimerState)
                                            _logger.Info(scheduleName);
                                        ///Do Logic 
                                        ///
                                        global = _functionService.loadGlobalParams(_dbTool);


                                        bDoLogic = false;

                                        lock (_threadControll)
                                        {
                                            _threadControll[timerID] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                        }
                                    }
                                }

                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag2", scheduleName);
                                    //_logger.Debug(tmpMsg);
                                }
                            }
                            catch (Exception ex)
                            {
                                tmpMsg = string.Format("Listening exception occurred. {0}", ex.Message);
                                _logger.Debug(tmpMsg);

                                lock (_threadControll)
                                {
                                    _threadControll[scheduleName] = "false";
                                }
                            }
                            finally
                            {
                                lock (_threadControll)
                                {
                                    _threadControll[scheduleName] = "false";
                                }
                            }
                        }
                        else
                        {
                            _threadControll.Add(scheduleName, vKey);
                        }

                        //////Auto Check Every 15 mins     last 12 ProcessTime
                        scheduleName = "AutoCheckEvery15mins";
                        timerID = "last12ProcessTime";
                        iTimer01 = 10;
                        timeUnit = "minutes";
                        passTicket = false;
                        if (_threadControll.ContainsKey(scheduleName))
                        {
                            try
                            {
                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag1", scheduleName);
                                    //_logger.Debug(tmpMsg);
                                }

                                vKey = "false";
                                lock (_threadControll)
                                {
                                    _threadControll.TryGetValue(scheduleName, out vKey);

                                    if (!vKey.Equals("true"))
                                    {
                                        _threadControll[scheduleName] = "true";
                                        passTicket = true;
                                    }
                                }

                                if (passTicket)
                                {
                                    bool bDoLogic = false;
                                    try
                                    {
                                        DateTime dtCurrent = DateTime.UtcNow;
                                        string vlastDatetime = dtCurrent.ToString("yyyy-MM-dd HH:mm:ss");
                                        if (_threadControll.ContainsKey(timerID))
                                        {
                                            lock (_threadControll)
                                            {
                                                _threadControll.TryGetValue(timerID, out vlastDatetime);
                                            }

                                            if (_functionService.TimerTool(timeUnit, vlastDatetime) >= iTimer01)
                                            {
                                                bDoLogic = true;
                                                lock (_threadControll)
                                                {
                                                    _threadControll[timerID] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            _threadControll.Add(timerID, vlastDatetime);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        tmpMsg = string.Format("Listening exception [{0}: {1}]", timerID, ex.Message);
                                        _logger.Debug(tmpMsg);
                                    }

                                    if (bDoLogic)
                                    {
                                        if (bCheckTimerState)
                                            _logger.Info(scheduleName);
                                        ///Do Logic 


                                        bDoLogic = false;

                                        lock (_threadControll)
                                        {
                                            _threadControll[timerID] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                        }
                                    }
                                }

                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag2", scheduleName);
                                    //_logger.Debug(tmpMsg);
                                }
                            }
                            catch (Exception ex)
                            {
                                tmpMsg = string.Format("Listening exception occurred. {0}", ex.Message);
                                _logger.Debug(tmpMsg);

                                lock (_threadControll)
                                {
                                    _threadControll[scheduleName] = "false";
                                }
                            }
                            finally
                            {
                                lock (_threadControll)
                                {
                                    _threadControll[scheduleName] = "false";
                                }
                            }
                        }
                        else
                        {
                            _threadControll.Add(scheduleName, vKey);
                        }

                        //////Auto Check Every 30 mins     last 10 ProcessTime
                        scheduleName = "AutoCheckEvery30mins";
                        timerID = "last10ProcessTime";
                        iTimer01 = 30;
                        timeUnit = "minutes";
                        passTicket = false;
                        if (_threadControll.ContainsKey(scheduleName))
                        {
                            try
                            {
                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag1", scheduleName);
                                    //_logger.Debug(tmpMsg);
                                }

                                vKey = "false";
                                lock (_threadControll)
                                {
                                    _threadControll.TryGetValue(scheduleName, out vKey);

                                    if (!vKey.Equals("true"))
                                    {
                                        _threadControll[scheduleName] = "true";
                                        passTicket = true;
                                    }
                                }

                                if (passTicket)
                                {
                                    bool bDoLogic = false;
                                    try
                                    {
                                        DateTime dtCurrent = DateTime.UtcNow;
                                        string vlastDatetime = dtCurrent.ToString("yyyy-MM-dd HH:mm:ss");
                                        if (_threadControll.ContainsKey(timerID))
                                        {
                                            lock (_threadControll)
                                            {
                                                _threadControll.TryGetValue(timerID, out vlastDatetime);
                                            }

                                            if (_functionService.TimerTool(timeUnit, vlastDatetime) >= iTimer01)
                                            {
                                                bDoLogic = true;
                                                lock (_threadControll)
                                                {
                                                    _threadControll[timerID] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            _threadControll.Add(timerID, vlastDatetime);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        tmpMsg = string.Format("Listening exception [{0}: {1}]", timerID, ex.Message);
                                        _logger.Debug(tmpMsg);
                                    }

                                    if (bDoLogic)
                                    {
                                        if (bCheckTimerState)
                                            _logger.Info(scheduleName);
                                        ///Do Logic 


                                        bDoLogic = false;

                                        lock (_threadControll)
                                        {
                                            _threadControll[timerID] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                        }
                                    }
                                }

                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag2", scheduleName);
                                    //_logger.Debug(tmpMsg);
                                }
                            }
                            catch (Exception ex)
                            {
                                tmpMsg = string.Format("Listening exception occurred. {0}", ex.Message);
                                _logger.Debug(tmpMsg);

                                lock (_threadControll)
                                {
                                    _threadControll[scheduleName] = "false";
                                }
                            }
                            finally
                            {
                                lock (_threadControll)
                                {
                                    _threadControll[scheduleName] = "false";
                                }
                            }
                        }
                        else
                        {
                            _threadControll.Add(scheduleName, vKey);
                        }

                        //////Auto Check Every 45 mins     last 11 ProcessTime
                        scheduleName = "AutoCheckEvery45mins";
                        timerID = "last11ProcessTime";
                        iTimer01 = 45;
                        timeUnit = "minutes";
                        passTicket = false;
                        if (_threadControll.ContainsKey(scheduleName))
                        {
                            try
                            {
                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag1", scheduleName);
                                   // _logger.Debug(tmpMsg);
                                }

                                vKey = "false";
                                lock (_threadControll)
                                {
                                    _threadControll.TryGetValue(scheduleName, out vKey);

                                    if (!vKey.Equals("true"))
                                    {
                                        _threadControll[scheduleName] = "true";
                                        passTicket = true;
                                    }
                                }

                                if (passTicket)
                                {
                                    bool bDoLogic = false;
                                    try
                                    {
                                        DateTime dtCurrent = DateTime.UtcNow;
                                        string vlastDatetime = dtCurrent.ToString("yyyy-MM-dd HH:mm:ss");
                                        if (_threadControll.ContainsKey(timerID))
                                        {
                                            lock (_threadControll)
                                            {
                                                _threadControll.TryGetValue(timerID, out vlastDatetime);
                                            }

                                            if (_functionService.TimerTool(timeUnit, vlastDatetime) >= iTimer01)
                                            {
                                                bDoLogic = true;
                                                lock (_threadControll)
                                                {
                                                    _threadControll[timerID] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            _threadControll.Add(timerID, vlastDatetime);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        tmpMsg = string.Format("Listening exception [{0}: {1}]", timerID, ex.Message);
                                        _logger.Debug(tmpMsg);
                                    }

                                    if (bDoLogic)
                                    {
                                        if (bCheckTimerState)
                                            _logger.Info(scheduleName);
                                        ///Do Logic 


                                        bDoLogic = false;

                                        lock (_threadControll)
                                        {
                                            _threadControll[timerID] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                        }
                                    }
                                }

                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[scheduleName][{0}].Flag2", scheduleName);
                                    //_logger.Debug(tmpMsg);
                                }
                            }
                            catch (Exception ex)
                            {
                                tmpMsg = string.Format("Listening exception occurred. {0}", ex.Message);
                                _logger.Debug(tmpMsg);

                                lock (_threadControll)
                                {
                                    _threadControll[scheduleName] = "false";
                                }
                            }
                            finally
                            {
                                lock (_threadControll)
                                {
                                    _threadControll[scheduleName] = "false";
                                }
                            }
                        }
                        else
                        {
                            _threadControll.Add(scheduleName, vKey);
                        }

                    }

                }

                EventQueue oEventQ = null;
                string LotID = "";
                string CarrierID = "";
                bool QueryAvailableTester = false;
                bool ManaulDispatch = false;
                NormalTransferModel evtObject2 = null;
                TransferList evtObject3 = null;
                bool keyBuildCommand = false;
                bool isCurrentStage = false;

                if (_DebugMode)
                {
                    tmpMsg = string.Format("[_EventQueue][{0}].Flag5", _eventQueue.Count);
                    //_logger.Debug(tmpMsg);
                }

                while (_eventQueue.Count > 0)
                {
                    if (_eventQueue.Count > 0)
                    {
                        oEventQ = new EventQueue();
                        lock (_eventQueue)
                        {
                            _eventQueue.TryDequeue(out oEventQ);
                        }

                        if (_DebugMode)
                        {
                            tmpMsg = string.Format("[EventQueue][{0}] {1}.", _eventQueue.Count, oEventQ.EventName);
                            _logger.Debug(tmpMsg);
                        }

                        if (oEventQ is null)
                            continue;
                        //Console.WriteLine(String.Format("{0}: Dequeue content is {1}", curPorcessName, oEventQ.EventName));
                        lstEquipment = new List<string>();
                        LotID = "";
                        QueryAvailableTester = false;
                        tmpMsg = "";
                        evtObject2 = null;
                        evtObject3 = null;
                        string currentTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

                        switch (oEventQ.EventName)
                        {
                            case "CarrierLocationUpdate":
                                
                                //Object type is CarrierLocationUpdate
                                //檢查Carrier所帶的lot id 是否符合當前待料的機台
                                CarrierLocationUpdate evtObject = (CarrierLocationUpdate)oEventQ.EventObject;
                                CarrierID = evtObject.CarrierID.Trim();
                                List<string> args = new();

                                try
                                {
                                    if (_DebugMode)
                                    {
                                        tmpMsg = string.Format("[Debug Message][{0}] {1}", oEventQ.EventName, CarrierID);
                                        _logger.Debug(tmpMsg);
                                    }
                                    //_functionService.CarrierLocationUpdate(_dbTool, _configuration, evtObject, _logger);

                                    sql = _BaseDataService.QueryLotInfoByCarrierID(CarrierID);
                                    _logger.Debug(sql);
                                    dt = _dbTool.GetDataTable(sql);
                                    if (dt.Rows.Count > 0)
                                    {
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
                                            //--carrier id, lot_id, lotType, customername, partid

                                            LotID = dt.Rows[0]["lot_id"].ToString().Equals("") ? "" : dt.Rows[0]["lot_id"].ToString().Trim();
                                            tmpMsg = string.Format("[CarrierLocationUpdate: Flag LotId {0}]", dt.Rows[0]["LOTID"].ToString());
                                            _logger.Debug(tmpMsg);

                                            //20230427 Add by Vance, 
                                            if (_configuration["AppSettings:Work"].ToUpper().Equals("EWLB"))
                                            {
                                                if (evtObject.LocationType.Equals("ERACK"))
                                                {
                                                    sql = _BaseDataService.EQPListReset(LotID);
                                                    _dbTool.SQLExec(sql, out tmpMsg, true);
                                                }
                                            }

                                            v_CUSTOMERNAME = dt.Rows[0]["customername"].ToString().Equals("") ? "" : dt.Rows[0]["customername"].ToString();
                                            v_PARTID = dt.Rows[0]["partid"].ToString().Equals("") ? "" : dt.Rows[0]["partid"].ToString();
                                            v_LOTTYPE = dt.Rows[0]["lotType"].ToString().Equals("") ? "" : dt.Rows[0]["lotType"].ToString();

                                            sql = _BaseDataService.QueryErackInfoByLotID(_configuration["eRackDisplayInfo:contained"], LotID);
                                            dtTemp = _dbTool.GetDataTable(sql);
                                            //--lotid, partname, customername, stage, state, lottype, hold_code, eotd, automotive, turnratio

                                            if (dtTemp.Rows.Count > 0)
                                            {
                                                v_STAGE = dtTemp.Rows[0]["stage"].ToString().Equals("") ? "" : dtTemp.Rows[0]["stage"].ToString();
                                                v_AUTOMOTIVE = dtTemp.Rows[0]["automotive"].ToString().Equals("") ? "" : dtTemp.Rows[0]["automotive"].ToString();
                                                v_STATE = dtTemp.Rows[0]["state"].ToString().Equals("") ? "" : dtTemp.Rows[0]["state"].ToString();
                                                v_HOLDCODE = dtTemp.Rows[0]["holdcode"].ToString().Equals("") ? "" : dtTemp.Rows[0]["holdcode"].ToString();
                                                v_TURNRATIO = dtTemp.Rows[0]["turnratio"].ToString().Equals("") ? "0" : dtTemp.Rows[0]["turnratio"].ToString();
                                                v_EOTD = dtTemp.Rows[0]["eotd"].ToString().Equals("") ? "" : dtTemp.Rows[0]["eotd"].ToString();
                                                v_POTD = dtTemp.Rows[0]["POTD"].ToString().Equals("") ? "" : dtTemp.Rows[0]["POTD"].ToString();
                                            }
                                        }
                                        catch(Exception ex)
                                        {
                                            tmpMsg = string.Format("[CarrierLocationUpdate: Column Issue. {0}]", ex.Message);
                                            _logger.Debug(tmpMsg);
                                        }

                                        args.Add(LotID);
                                        args.Add(v_STAGE.Equals("") ? dt.Rows[0]["stage"].ToString() : v_STAGE);
                                        args.Add("");// ("machine");
                                        args.Add("");//("desc");
                                        args.Add(CarrierID);
                                        args.Add(v_CUSTOMERNAME);
                                        args.Add(v_PARTID);//("PartID");
                                        args.Add(v_LOTTYPE);//("LotType");
                                        args.Add(v_AUTOMOTIVE);//("Automotive");
                                        args.Add(v_STATE);//("State");
                                        args.Add(v_HOLDCODE);//("HoldCode");
                                        args.Add(v_TURNRATIO);//("TURNRATIO");
                                        args.Add(v_EOTD);//("EOTD");

                                        //args.Add(dt.Rows[0]["HOLDCODE"].ToString().Equals("") ? "" : dt.Rows[0]["HOLDCODE"].ToString());
                                        //args.Add(dt.Rows[0]["HOLDREAS"].ToString().Equals("") ? "" : dt.Rows[0]["HOLDREAS"].ToString());
                                        _functionService.SentCommandtoMCSByModel(_configuration, _logger, "InfoUpdate", args);

                                        if (!LotID.Equals(""))
                                        {
                                            //Carrier Location Update時, 檢查 Carrier binding 的lotid 狀態 並更新到 lot_info
                                            _functionService.CheckCurrentLotStatebyWebService(_dbTool, _configuration, _logger, LotID);
                                        }
                                    }
                                    else
                                    {
                                        tmpMsg = string.Format("[CarrierLocationUpdate: Carrier [{0}] Not Exist.]", CarrierID);
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
                                        _functionService.SentCommandtoMCSByModel(_configuration, _logger, "InfoUpdate", args);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    tmpMsg = string.Format("[CarrierLocationUpdate: {0}]", ex.Message);
                                    _logger.Debug(tmpMsg);
                                }

                                break;
                            case "EquipmentPortStatusUpdate":
                                AEIPortInfo _aeiPortInfo = (AEIPortInfo)oEventQ.EventObject;
                                //CarrierID = evtObject.CarrierID.Trim();
                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[Debug Message][{0}] {1} / {2}", oEventQ.EventName, _aeiPortInfo.PortID, _aeiPortInfo.PortTransferState);
                                    _logger.Debug(tmpMsg);
                                }
                                //_functionService.EquipmentPortStatusUpdate(_dbTool, _configuration, _aeiPortInfo, _logger);

                                break;
                            case "EquipmentStatusUpdate":
                                AEIEQInfo _aeiEqInfo = (AEIEQInfo)oEventQ.EventObject;
                                //CarrierID = evtObject.CarrierID.Trim();
                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[Debug Message][{0}] {1} / {2}", oEventQ.EventName, _aeiEqInfo.EqID, _aeiEqInfo.EqState);
                                    _logger.Debug(tmpMsg);
                                }
                                _functionService.EquipmentStatusUpdate(_dbTool, _configuration, _aeiEqInfo, _logger);

                                break;
                            case "CommandStatusUpdate":
                                //CommandStatusUpdate
                                DataTable dtCmdStateUpdate = new DataTable();
                                CommandStatusUpdate tmpObject = (CommandStatusUpdate)oEventQ.EventObject;
                                string CommandID = tmpObject.CommandID;

                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[Debug Message][{0}] {1} ", oEventQ.EventName, CommandID);
                                    _logger.Debug(tmpMsg);
                                }

                                _functionService.CommandStatusUpdate(_dbTool, _configuration, tmpObject, _logger);

                                if (tmpObject.Status.Equals("Success") || tmpObject.Status.Equals("Failed") || tmpObject.Status.Equals("Init"))
                                {
                                    if (tmpObject.Status.Equals("Success"))
                                    {

                                        dtCmdStateUpdate = _dbTool.GetDataTable(_BaseDataService.QueryRunningWorkInProcessSchByCmdId(CommandID));
                                        if (dtCmdStateUpdate.Rows.Count > 0)
                                        {
                                            //cmd_type
                                            if (!dtCmdStateUpdate.Rows[0]["cmd_type"].Equals("Pre-Transfer"))
                                            {
                                                _dbTool.SQLExec(_BaseDataService.UpdateLotInfoWhenCOMP(CommandID), out tmpMsg, true);
                                                //新增一筆Success Record
                                                //InsertRTDStatisticalRecord
                                                _dbTool.SQLExec(_BaseDataService.InsertRTDStatisticalRecord(currentTime, CommandID, "S"), out tmpMsg, true);


                                                tmpMsg = string.Format("[CommandStatusUpdate: dtWIP.Rows.Count > 0]");
                                                _logger.Debug(tmpMsg);
                                                LotID = dtCmdStateUpdate.Rows[0]["lotid"].ToString().Equals("") ? "" : dtCmdStateUpdate.Rows[0]["lotid"].ToString();

                                                _logger.Debug(string.Format("Lot ID is {0}", LotID));
                                            }
                                            else
                                            {
                                                LotID = dtCmdStateUpdate.Rows[0]["lotid"].ToString().Equals("") ? "" : dtCmdStateUpdate.Rows[0]["lotid"].ToString();
                                                if(!LotID.Equals(""))
                                                    _dbTool.SQLExec(_BaseDataService.UpdateTableLotInfoState(LotID, "WAIT"), out tmpMsg, true);
                                            }
                                        }
                                        else
                                            LotID = "";

                                        if (!LotID.Equals(""))
                                        {
                                            //SelectTableLotInfoByLotid
                                            dtCmdStateUpdate = _dbTool.GetDataTable(_BaseDataService.SelectTableLotInfoByLotid(LotID));
                                            if (dtCmdStateUpdate.Rows.Count > 0)
                                            {
                                                _logger.Debug(string.Format("Lock Machine state is {0}", dtCmdStateUpdate.Rows[0]["lockmachine"].ToString()));

                                                if (dtCmdStateUpdate.Rows[0]["lockmachine"].ToString().Equals("1"))
                                                {
                                                    _dbTool.SQLExec(_BaseDataService.UpdateLotInfoForLockMachine(LotID), out tmpMsg, true);
                                                    //_dbTool.SQLExec(_BaseDataService.LockMachineByLot(LotID, 0), out tmpMsg, true);
                                                }
                                                _logger.Debug("[CommandStatusUpdate] Done");
                                            }
                                        }
                                    }
                                    else if (tmpObject.Status.Equals("Failed"))
                                    {
                                        dtCmdStateUpdate = _dbTool.GetDataTable(_BaseDataService.QueryRunningWorkInProcessSchByCmdId(CommandID));
                                        if (dtCmdStateUpdate.Rows.Count > 0)
                                        {
                                            _dbTool.SQLExec(_BaseDataService.UpdateLotInfoWhenFail(CommandID), out tmpMsg, true);
                                            //新增一筆Failed Record
                                            _dbTool.SQLExec(_BaseDataService.InsertRTDStatisticalRecord(currentTime, CommandID, "F"), out tmpMsg, true);
                                        }
                                    }
                                    else if (tmpObject.Status.Equals("Init"))
                                    {
                                        dtCmdStateUpdate = _dbTool.GetDataTable(_BaseDataService.QueryInitWorkInProcessSchByCmdId(CommandID));
                                        if (dtCmdStateUpdate.Rows.Count > 0)
                                        {
                                            //新增一筆Total Record
                                            //_dbTool.SQLExec(_BaseDataService.InsertRTDStatisticalRecord(currentTime, CommandID, "T"), out tmpMsg, true);
                                        }
                                        break;
                                    }
                                    else
                                    {
                                        break;
                                    }

                                    _dbTool.SQLExec(_BaseDataService.UpdateTableWorkInProcessSchHisByCmdId(CommandID), out tmpMsg, true);
                                    Thread.Sleep(5);
                                    _dbTool.SQLExec(_BaseDataService.DeleteWorkInProcessSchByCmdId(CommandID), out tmpMsg, true);
                                }
                                else
                                {
                                    tmpMsg = string.Format("[CommandStatusUpdate: command id is {0}, Status is {1} ]", CommandID, tmpObject.Status);
                                    _logger.Debug(tmpMsg);
                                }
                                break;
                            case "HoldLot":
                                //Object type is TransferList
                                break;
                            case "ReleaseLot":
                                //Object type is TransferList
                                break;
                            case "MoveCarrier":
                                //UI直接下搬移指令 (指定Carrier ID, 目的地(可為機台/貨架))
                                evtObject3 = (TransferList)oEventQ.EventObject;
                                if(_functionService.CreateTransferCommandByTransferList(_dbTool, _logger, evtObject3, out arrayOfCmds))
                                {
                                    LotID = evtObject3.LotID;
                                    if(!LotID.Equals(""))
                                        _dbTool.SQLExec(_BaseDataService.UpdateTableLotInfoState(LotID, "PROC"), out tmpMsg, true);
                                    ManaulDispatch = true;
                                }
                                break;
                            case "LotEquipmentAssociateUpdate":
                                //Object type is NormalTransferModel
                                //STATE=WAIT , EQUIP_ASSO=N >> Do this process.
                                evtObject2 = (NormalTransferModel) oEventQ.EventObject;
                                LotID = evtObject2.LotID;

                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[Debug Message][{0}] {1} ", oEventQ.EventName, LotID, evtObject2.EquipmentID);
                                    _logger.Debug(tmpMsg);
                                }

                                sql = string.Format(_BaseDataService.ConfirmLotInfo(LotID));
                                dtTemp = _dbTool.GetDataTable(sql);
                                if (dtTemp.Rows.Count > 0)
                                {
                                    QueryAvailableTester = true;
                                    keyBuildCommand = true;
                                }
                                else
                                {
                                    QueryAvailableTester = false;
                                    keyBuildCommand = false;

                                    sql = string.Format(_BaseDataService.IssueLotInfo(LotID));
                                    dtTemp = _dbTool.GetDataTable(sql);
                                    if (dtTemp.Rows.Count > 0)
                                    {
                                        tmpMsg = string.Format("[LotEquipmentAssociateUpdate] data issue: Lot:{0} state not correct. [STATE:{1}, STAGE:{2}, RTD_STATE:{3}]", LotID, dtTemp.Rows[0]["STATE"].ToString(), dtTemp.Rows[0]["STAGE"].ToString(), dtTemp.Rows[0]["RTD_STATE"].ToString());
                                        _logger.Debug(tmpMsg);
                                    }
                                }

                                break;
                            case "AutoCheckEquipmentStatus":

                                evtObject2 = (NormalTransferModel)oEventQ.EventObject;
                                LotID = evtObject2.LotID;

                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[Debug Message][{0}] {1} / {2} ", oEventQ.EventName, LotID, evtObject2.EquipmentID);
                                    _logger.Debug(tmpMsg);
                                }

                                string ctrlObject = string.Format("AutoCheckEquipmentStatus{0}", LotID);
                                //10秒內, 不跑讓相同lotid 的第2支Thread執行
                                if (_threadControll.ContainsKey(ctrlObject))
                                {
                                    if (_functionService.ThreadLimitTraffice(_threadControll, ctrlObject, 10, "ss", ">"))
                                    {
                                        _threadControll[ctrlObject] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                                else
                                {
                                    lock (_threadControll)
                                    {
                                        if (!_threadControll.ContainsKey(ctrlObject))
                                            _threadControll.Add(ctrlObject, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                                        else
                                            break;
                                    }
                                }

                                keyBuildCommand = true;
                                break;
                            case "AbnormalyEquipmentStatus":
                                keyBuildCommand = true;
                                if (_threadControll.ContainsKey(oEventQ.EventName))
                                {
                                    if (_functionService.ThreadLimitTraffice(_threadControll, oEventQ.EventName, 10, "ss", ">"))
                                    {
                                        _threadControll[oEventQ.EventName] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                    } else { 
                                        break; 
                                    }
                                }
                                else
                                {
                                    lock (_threadControll)
                                    {
                                        if (!_threadControll.ContainsKey(oEventQ.EventName))
                                            _threadControll.Add(oEventQ.EventName, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                                        else
                                            break;
                                    }
                                }

                                //lstNormalTransfer.Sort();//依寫入順序排序
                                foreach (NormalTransferModel NormalTransfer in lstNormalTransfer)
                                {
                                    oEventQ.EventObject = NormalTransfer;
                                    evtObject2 = NormalTransfer;
                                    LotID = NormalTransfer.LotID;

                                    if (ExcuteMode.Equals(0))
                                    {
                                        //0. 半自動模式, 不產生指令且不執行派送
                                        Thread.Sleep(100);
                                        continue;
                                    }

                                    if (_DebugMode)
                                    {
                                        tmpMsg = string.Format("[Debug Message IN][{0}] {1} ", oEventQ.EventName, LotID);
                                        _logger.Debug(tmpMsg);
                                    }

                                    if (_functionService.CreateTransferCommandByPortModel(_dbTool, _configuration, _logger, _DebugMode, NormalTransfer.EquipmentID, NormalTransfer.PortModel, oEventQ, out arrayOfCmds))
                                    { }
                                    else
                                    { }

                                    if (_DebugMode)
                                    {
                                        tmpMsg = string.Format("[Debug Message OUT][{0}] {1} ", oEventQ.EventName, LotID);
                                        _logger.Debug(tmpMsg);
                                    }
                                }

                                break;
                            default:
                                break;
                        }

                        if(LotID.Equals(""))
                        {
                            LotID = _functionService.GetLotIdbyCarrier(_dbTool, CarrierID, out tmpMsg);
                        }

                        if (!LotID.Equals(""))
                        {
                            sql = _BaseDataService.CheckLotStage(_configuration["CheckLotStage:Table"], LotID);
                            dtTemp = _dbTool.GetDataTable(sql);

                            if (dtTemp.Rows.Count > 0)
                            {
                                ///this lot rtd stage and promis stage is same. it can to get available qualified tester machine from promis.
                                if (dtTemp.Rows[0]["stage1"].ToString().Equals(dtTemp.Rows[0]["stage2"].ToString()))
                                    isCurrentStage = true;
                                else
                                    isCurrentStage = false;
                            }
                        }

                        /* 
                            1. Carrier Stored, 找出lot ID (更新TABLE), 找出機台 (更新TABLE), 產生搬移指令 (插入TABLE), 送出指令(更新TABLE), 
                            2. Port State Change (Waitting to Load), 找出符合條件的Lot 信息, 產生搬移指令 (插入TABLE), 送出指令(更新TABLE), 
                            3. EQUIP State Chagne (IDLE), 找出Port State (Waitting to Load), 找出符合條件的Lot 信息, 產生搬移指令 (插入TABLE), 送出指令(更新TABLE),
                            4. Port State Change (Waitting to Unload), 
                            5. EQUIP State Chagne (Down/PM), 找出正在執行的指令屬於此一機台的, 送出(Cancel/Abort)命令。
                            6. Transfer指令, 產生搬移指令 (插入TABLE), 送出指令(更新TABLE), 
                            7. Hold Lot指令, 查詢指令清單是否有相關Lot的指令(更新TABLE), 產生指令(Cancel/Abort), 送出指令(Cancel/Abort)
                            8. Release Lot

                        找出lot ID (更新TABLE)
                        找出機台 (CheckAvailableQualifiedTesterMachine)
                        產生搬移指令 (BuildTransferCommands) Input oEventQ  Output string[]
                        送出指令(SentDispatchCommandtoMCS): Input string[] Output bool
                        */
                        if (QueryAvailableTester)
                        {
                            string lastRunCheckQueryAvailableTester = "";
                            string currentKey = "";

                            if (!LotID.Equals(""))
                                currentKey = LotID;
                            else
                                currentKey = "CheckQueryAvailableTester";

                            bool doFunc = false;
                            lock (_threadControll)
                            {
                                if (_threadControll.ContainsKey(currentKey))
                                {
                                    _threadControll.TryGetValue(currentKey, out lastRunCheckQueryAvailableTester);
                                    //string tmpUnif = _configuration["ReflushTime:CheckQueryAvailableTester:TimeUnit"];
                                    //int tmpTime = _configuration["ReflushTime:CheckQueryAvailableTester:Time"] is null ? 0 : int.Parse(_configuration["ReflushTime:CheckQueryAvailableTester:Time"]);
                                    string tmpUnif = global.CheckQueryAvailableTestercuteMode.TimeUnit;
                                    int tmpTime = global.CheckQueryAvailableTestercuteMode.Time;
                                    if (_functionService.TimerTool(tmpUnif, lastRunCheckQueryAvailableTester) > tmpTime)
                                    {
                                        doFunc = true;
                                        lastRunCheckQueryAvailableTester = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                        _threadControll[currentKey] = lastRunCheckQueryAvailableTester;
                                    }
                                    else
                                        doFunc = false;
                                    
                                }
                                else
                                {
                                    lastRunCheckQueryAvailableTester = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                                    if (!_threadControll.ContainsKey(currentKey))
                                    {
                                        _threadControll.Add(currentKey, lastRunCheckQueryAvailableTester);
                                        doFunc = true;
                                    }
                                }
                            }

                            if (doFunc)
                            {
                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[Debug Message IN][{0}] {1} ", "CheckAvailableQualifiedTesterMachine", LotID);
                                    _logger.Debug(tmpMsg);
                                }

                                if (isCurrentStage)
                                {
                                    ///this lot rtd stage and promis stage is same. it can to get available qualified tester machine from promis.
                                    lstEquipment = _functionService.CheckAvailableQualifiedTesterMachine(_dbTool, _configuration, _DebugMode, _logger, LotID);
                                }
                                
                                if (lstEquipment.Count > 0)
                                {
                                    //Console.WriteLine(String.Format("do CheckAvailableQualifiedTesterMachine."));
                                    string joinedNames = String.Join(",", lstEquipment.ToArray());
                                    while (true)
                                    {
                                        try
                                        {
                                            sql = string.Format(_BaseDataService.UpdateTableLotInfoEquipmentList(LotID, joinedNames));
                                            _dbTool.SQLExec(sql, out tmpMsg, true);

                                            lock (_threadControll)
                                            {
                                                if (_threadControll.ContainsKey(currentKey))
                                                {
                                                    _threadControll.Remove(currentKey);
                                                }
                                            }
                                            break;
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine(ex.Message);
                                            _logger.Debug(string.Format("UpdateTableLotInfoEquipmentList fail: [{0}]", ex.StackTrace));
                                        }
                                    }
                                    Thread.Sleep(5);
                                }
                                else
                                    keyBuildCommand = false;
                            }
                        }
                        else
                        {
                            sql = _BaseDataService.CheckLotStage(_configuration["CheckLotStage:Table"], LotID);
                            dtTemp = _dbTool.GetDataTable(sql);
                            if (dtTemp.Rows.Count <= 0)
                            {
                                //_logger.Debug(string.Format("[CheckQueryAvailableTester][{0}] lot [{1}] stage not correct. can not build command.", oEventQ.EventName, LotID));
                                _logger.Debug(string.Format("[CheckQueryAvailableTester][{0}] lot [{1}] The lotid of stage Incorrect.", oEventQ.EventName, LotID));
                            }
                            else
                            {
                                if (isCurrentStage)
                                {
                                    //直接取已存在Lot_Info裡的
                                    sql = string.Format(_BaseDataService.SelectTableLotInfoByLotid(LotID));
                                    dt = _dbTool.GetDataTable(sql);
                                    if (dt.Rows.Count > 0)
                                    {
                                        foreach (DataRow dr2 in dt.Rows)
                                        {
                                            lstEquipment = new List<string>(dr2["EQUIPLIST"].ToString().Split(','));
                                        }
                                    }
                                }
                                else
                                {
                                    _logger.Debug(string.Format("[CheckQueryAvailableTester][{0}] lot [{1}] The lotid of stage Incorrect.", oEventQ.EventName, LotID));
                                    //clean column eqplist
                                    sql = _BaseDataService.EQPListReset(LotID);
                                    _dbTool.SQLExec(sql, out tmpMsg, true);
                                }

                                _logger.Debug(string.Format("---LotID= {0}, RTD_Stage={1}, MES_Stage={2}, RTD_State={3}, MES_State={4}", LotID, dtTemp.Rows[0]["stage1"].ToString(), dtTemp.Rows[0]["stage2"].ToString(), dtTemp.Rows[0]["state1"].ToString(), dtTemp.Rows[0]["state2"].ToString()));
                            }
                        }

                        if (!ManaulDispatch)
                        {
                            if (ExcuteMode.Equals(0))
                            {
                                //0. 半自動模式, 不產生指令且不執行派送
                                Thread.Sleep(100);
                                continue;
                            }
                        }

                        if (arrayOfCmds != null)
                        {
                            if (arrayOfCmds.Count <= 0)
                             {
                                if (keyBuildCommand)
                                {
                                    string vKey2 = "false";
                                    if (_threadControll.ContainsKey("BuildTransferCommands"))
                                    {
                                        _threadControll.TryGetValue("BuildTransferCommands", out vKey2);
                                        if (!vKey2.Equals("true"))
                                        {
                                            lock (_threadControll)
                                            {
                                                _threadControll["BuildTransferCommands"] = "true";
                                            }

                                            if (_functionService.BuildTransferCommands(_dbTool, _configuration, _logger, _DebugMode, oEventQ, _threadControll, lstEquipment, out arrayOfCmds))
                                            {
                                                //Console.WriteLine(String.Format("do BuildTransferCommands."));
                                            }

                                            lock (_threadControll)
                                            {
                                                _threadControll["BuildTransferCommands"] = "false";
                                            }
                                        }
                                    }
                                    else
                                    { _threadControll.Add("BuildTransferCommands", "false"); }
                                }
                            }
                        }

                        if (arrayOfCmds != null)
                        {
                            if (!ExcuteMode.Equals(1))
                            {
                                //3. 特殊模式, 產生指令且不執行派送
                                Thread.Sleep(100);
                                continue;
                            }

                            if (arrayOfCmds.Count > 0)
                            {
                                _logger.Debug(string.Format("SentDispatchCommandMCS.Build: arrayOfCmd qty is {0}", arrayOfCmds.Count.ToString()));
                                bool tmpResult = _functionService.SentDispatchCommandtoMCS(_dbTool, _configuration, _logger, arrayOfCmds);
                            }
                            else
                            {
                                
                                dt = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSchUnlock());
                                if (dt.Rows.Count > 0)
                                {
                                    _logger.Debug(string.Format("SentDispatchCommandMCS.Resend: arrayOfCmd qty is {0}", dt.Rows.Count.ToString()));

                                    List<string> _LstOfCmds = new List<string>();
                                    foreach (DataRow drCmd in dt.Rows)
                                    {
                                        _LstOfCmds.Add(drCmd["CMD_ID"].ToString());
                                    }

                                    bool tmpResult = _functionService.SentDispatchCommandtoMCS(_dbTool, _configuration, _logger, _LstOfCmds);
                                }

                            }
                        } 
                    }
                     Thread.Sleep(5);
                }
                //ThreadPool數量    
                /*
                if (!currentThreadNo.Equals(""))
                {
                    if (_threadControll.ContainsKey(curPorcessName))
                    {
                        string vNo = "";
                        int iNo = 0;
                        _threadControll.TryGetValue(curPorcessName, out vNo);
                        iNo = int.Parse(vNo) - 1;
                        _threadControll[curPorcessName] = iNo.ToString().Trim(); ;
                    }
                }
                */

                //}
                if (_DebugMode)
                {
                    tmpMsg = string.Format("[_EventQueue][{0}].", "Flag6", _eventQueue.Count);
                    //_logger.Debug(tmpMsg);
                }
            }
            catch(Exception ex)
            {
                
                Console.WriteLine(ex.Message);
                _logger.Debug(string.Format("listening Exception: [{0}]", ex.StackTrace));
                String msg = "";
                //_dbTool.DisConnectDB(out msg);
            }
            finally
            { }

            _dbTool.DisConnectDB(out tmpMsg);
            _dbTool = null;
        }
    }
}
