﻿using Microsoft.Extensions.Configuration;
using NLog;
using RTDWebAPI.Commons.Method.Database;
using RTDWebAPI.Commons.Method.Tools;
using RTDWebAPI.Interface;
using RTDWebAPI.Models;
using RTDWebAPI.Service;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading;

namespace RTDWebAPI.APP
{
    public class EventLogicService: BasicService
    {
        string curProcessName = "EventLogicService";
        string tmpMsg = "";
        IFunctionService _functionService = new FunctionService();
        DBTool dbTool;
        string RTDServerName = "";
        bool _bKeepUI = true;
        int _changeTime = 0;
        string _changeTimeUnit = "minutes";

        public void Start()
        {
            int minThdWorker = 3;
            int minPortThdComp = 3;
            int maxThdWorker = 5;
            int maxPortThdComp = 5;
            int threadTimeoutLimit = 2;
            string timeoutunit = "minutes";
            bool _swThredstimeout = false;
            ////Threads controll
            int iCurrentUI = 0;
            int iCurrentllyUse = 0;
            int iCurrentllySch = 0;
            int iCurrentllylis = 0;
            int iMaxThreads = 0;
            int iminThreads = 0;
            int iworkerThreads = 0;
            int iio1Threads = 0;
            int iio2Threads = 0;
            int iio3Threads = 0;

            try
            {
                minThdWorker = _configuration["ThreadPools:minThread:workerThread"] is not null ? int.Parse(_configuration["ThreadPools:minThread:workerThread"]) : minThdWorker;
                minPortThdComp = _configuration["ThreadPools:minThread:portThread"] is not null ? int.Parse(_configuration["ThreadPools:minThread:portThread"]) : minThdWorker; ;
                maxThdWorker = _configuration["ThreadPools:maxThread:workerThread"] is not null ? int.Parse(_configuration["ThreadPools:maxThread:workerThread"]) : minThdWorker;
                maxPortThdComp = _configuration["ThreadPools:maxThread:portThread"] is not null ? int.Parse(_configuration["ThreadPools:maxThread:portThread"]) : minThdWorker; ;
                threadTimeoutLimit = _configuration["ThreadPools:ThreadTimeoutLimit:timeoutlimit"] is not null ? int.Parse(_configuration["ThreadPools:ThreadTimeoutLimit:timeoutlimit"]) : threadTimeoutLimit;
                timeoutunit = _configuration["ThreadPools:ThreadTimeoutLimit:timeunit"] is not null ? _configuration["ThreadPools:ThreadTimeoutLimit:timeunit"] : timeoutunit;
                _swThredstimeout = _configuration["ThreadPools:ThreadTimeoutLimit:enabled"] is not null ? _configuration["ThreadPools:ThreadTimeoutLimit:enabled"].Equals(true) ? true : false : _swThredstimeout;
                RTDServerName = _configuration["AppSettings:Server"] is not null ? _configuration["AppSettings:Server"] : "RTDServer";
                _bKeepUI = _configuration["KeepUI:Enable"] is null ? true : _configuration["KeepUI:Enable"].ToLower().Equals("false") ? false : true;
                _changeTime = _configuration["ChangerServerTime:Time"] is null ? 5 : int.Parse(_configuration["ChangerServerTime:Time"].ToString());
                _changeTimeUnit = _configuration["ChangerServerTime:Unit"] is null ? "minutes" : (_configuration["ChangerServerTime:Unit"].ToString());
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

                double uidbAccessNum = _configuration["ThreadPools:UIThread"] is null ? maxThdWorker * 0.2 : maxThdWorker * int.Parse(_configuration["ThreadPools:UIThread"]) * 0.1;
                //double rtdUsageThread = _configuration["ThreadPools:KeepThread"] is null ? maxThdWorker * 0.2 : maxThdWorker * int.Parse(_configuration["ThreadPools:KeepThread"]) * 0.1;
                double percentThread = 0;
                int uiThread = 0;
                //int inUse = 0;
                _inUse = 0;
                int iCurUse = 0;
                int iCompThd = 0;
                string _sql = "";
                DataTable dtTemp = null;
                DataTable dtTemp2 = null;
                DataTable dtTemp3 = null;
                bool _serviceExist = false;
                bool _serviceOn = false;
                bool _isMasterServer = false;
                string _responseTime = "";

                try
                {
                    //20240828 Add RTD salve server auto on when master down
                    //Create Base db session
                    dbTool = new DBTool(tmpConnectString, tmpDatabase, tmpAutoDisconn, out msg);
                    dbTool._dblogger = _logger;
                    _listDBSession.Add(dbTool);

                    _sql = _BaseDataService.QueryRTDServer("");
                    dtTemp = dbTool.GetDataTable(_sql);
                    if (dtTemp.Rows.Count > 0)
                    {
                        _serviceExist = true;

                        //Get Master response time
                        _sql = _BaseDataService.QueryResponseTime(dtTemp.Rows[0]["paramvalue"].ToString());
                        dtTemp2 = dbTool.GetDataTable(_sql);
                        if (dtTemp2.Rows.Count > 0)
                        {
                            if (dtTemp.Rows[0]["paramvalue"].ToString().Equals(RTDServerName))
                            {
                                _isMasterServer = true;

                                _responseTime = dtTemp2.Rows[0]["responseTime"].ToString();

                                if (_functionService.TimerTool("minutes", _responseTime) > 1)
                                {
                                    _sql = _BaseDataService.UadateResponseTime(RTDServerName);
                                    _dbTool.SQLExec(_sql, out tmpMsg, true);

                                    _sql = _BaseDataService.UadateRTDServer(RTDServerName);
                                    _dbTool.SQLExec(_sql, out tmpMsg, true);
                                }
                                _serviceOn = true;
                            }
                            else
                            {
                                _sql = _BaseDataService.QueryResponseTime(RTDServerName);
                                dtTemp2 = dbTool.GetDataTable(_sql);
                                if (dtTemp2.Rows.Count > 0)
                                {
                                    //update response time for this machine.
                                    _responseTime = dtTemp2.Rows[0]["responseTime"].ToString();

                                    if (_functionService.TimerTool("seconds", _responseTime) > 15)
                                    {
                                        _serviceOn = true;
                                        _sql = _BaseDataService.UadateResponseTime(RTDServerName);
                                        _dbTool.SQLExec(_sql, out tmpMsg, true);
                                    }
                                    _serviceOn = false;
                                }
                                else
                                {
                                    //insert response time for this mahcine
                                    _sql = _BaseDataService.InsertResponseTime(RTDServerName);
                                    _dbTool.SQLExec(_sql, out tmpMsg, true);

                                    _serviceOn = false;
                                }
                                
                            }
                        }
                        else
                        {
                            ///Master Server no response time , 啟動新的
                            _serviceExist = false;

                            _sql = _BaseDataService.InsertResponseTime(RTDServerName);
                            _dbTool.SQLExec(_sql, out tmpMsg, true);

                            if (!tmpMsg.Equals(""))
                                _serviceOn = true;
                            else
                                _serviceOn = false;
                        }
                    }
                    else
                    {
                        ///沒有Server, 直接啟動新的
                        _serviceExist = false;

                        _sql = _BaseDataService.InsertResponseTime(RTDServerName);
                        _dbTool.SQLExec(_sql, out tmpMsg, true);

                        if(!tmpMsg.Equals(""))
                            _serviceOn = true;
                        else
                            _serviceOn = false;
                    }
                }
                catch(Exception ex)
                {
                }

                System.Threading.WaitCallback waitCallback = null;
                double dbusyThreads = 0;
                string _tempFunc = "";
                string _lastStepTime = "";
                string _lastProcessTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                string _ThreadsWaitingTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");

                while (true)
                {
                    try { 
                        //dbTool = new DBTool(tmpConnectString, tmpDatabase, tmpAutoDisconn, out msg);

                        object obj = new object[]
                        {
                            _eventQueue,
                            _lstDB,
                            _configuration,
                            _logger,
                            _threadConntroll,
                            _functionService,
                            _alarmDetail
                        };

                        if (_listDBSession.Count > 0)
                            _dbTool = _listDBSession[0];

                        if (_serviceOn)
                        {
                            try
                            {
                                if (_listDBSession.Count < uidbAccessNum)
                                {
                                    for (int i = _listDBSession.Count; i <= uidbAccessNum; i++)
                                    {
                                        ///build db connection for ui.
                                        dbTool = new DBTool(tmpConnectString, tmpDatabase, tmpAutoDisconn, out msg);
                                        dbTool._dblogger = _logger;
                                        _listDBSession.Add(dbTool);
                                    }
                                }

                                _sql = _BaseDataService.QueryRTDServer(RTDServerName);
                                dtTemp = _dbTool.GetDataTable(_sql);
                                if (dtTemp.Rows.Count <= 0)
                                {
                                    _serviceOn = false;
                                }
                                else
                                {
                                    _sql = _BaseDataService.QueryResponseTime(dtTemp.Rows[0]["paramvalue"].ToString());
                                    dtTemp2 = _dbTool.GetDataTable(_sql);
                                    if (dtTemp2.Rows.Count > 0)
                                    {
                                        _responseTime = dtTemp2.Rows[0]["responseTime"].ToString();

                                        if (_functionService.TimerTool("seconds", _responseTime) > 10)
                                        {
                                            _serviceOn = true;
                                            _sql = _BaseDataService.UadateResponseTime(RTDServerName);
                                            _dbTool.SQLExec(_sql, out tmpMsg, true);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                //Exception set default thread.
                                percentThread = 5;
                                uidbAccessNum = 2;
                            }
                        }
                        else
                        {
                            _sql = _BaseDataService.QueryRTDServer("");
                            dtTemp = _dbTool.GetDataTable(_sql);
                            if (dtTemp.Rows.Count > 0)
                            {
                                _serviceExist = true;

                                //Get Master response time
                                _sql = _BaseDataService.QueryResponseTime(dtTemp.Rows[0]["paramvalue"].ToString());
                                dtTemp2 = _dbTool.GetDataTable(_sql);
                                if (dtTemp2.Rows.Count > 0)
                                {
                                    if (!dtTemp.Rows[0]["paramvalue"].ToString().Equals(RTDServerName))
                                    {
                                        _responseTime = dtTemp2.Rows[0]["responseTime"].ToString();

                                        if (_functionService.TimerTool("minutes", _responseTime) > _changeTime)
                                        {
                                            //Master over 1 minutes no response. change slave to master
                                            _serviceOn = true;
                                            _sql = _BaseDataService.UadateResponseTime(RTDServerName);
                                            _dbTool.SQLExec(_sql, out tmpMsg, true);

                                            _sql = _BaseDataService.UadateRTDServer(RTDServerName);
                                            _dbTool.SQLExec(_sql, out tmpMsg, true);
                                        }
                                        else
                                        {
                                            _sql = _BaseDataService.QueryResponseTime(RTDServerName);
                                            dtTemp3 = _dbTool.GetDataTable(_sql);
                                            _responseTime = dtTemp3.Rows[0]["responseTime"].ToString();

                                            if (_functionService.TimerTool("seconds", _responseTime) > 15)
                                            {
                                                _serviceOn = true;
                                                _sql = _BaseDataService.UadateResponseTime(RTDServerName);
                                                _dbTool.SQLExec(_sql, out tmpMsg, true);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if(!_serviceOn)
                                            _serviceOn = true;
                                    }
                                }
                            }

                            continue;
                        }
                        //tmpMsg = string.Format("iCurUse][{0}] CompThd [{1}]", iCurUse, iCompThd);
                        //_logger.Debug(tmpMsg);

                        //20230428 Modify by Vance,  keep thread for API/UI
                        int iIdle = 0;

                        ThreadPool.GetMaxThreads(out iMaxThreads, out iio1Threads);
                        ThreadPool.GetMinThreads(out iminThreads, out iio2Threads);
                        ThreadPool.GetAvailableThreads(out iworkerThreads, out iio3Threads);
#if DEBUG
                        tmpMsg = string.Format("{6} Thread usage: ThreadID [{7}], iMaxThreads[{0}], iminThreads [{1}], iworkerThreads [{2}], iio1Threads [{3}], iio2Threads [{4}], iio3Threads [{5}], _listDBSession [{8}]", iMaxThreads, iminThreads, iworkerThreads, iio1Threads, iio2Threads, iio3Threads, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), Thread.CurrentThread.ManagedThreadId, _listDBSession.Count);
                        Console.WriteLine(tmpMsg);
#else
                        //do nothing
#endif
                        ///iworkerThraeads 可用的線程數量
                        ///iio3Threads 可用的異步線程數量
                        iCurrentllyUse = iMaxThreads - iworkerThreads;//最大線程數-當前可用線程數=當前使用線程數
                        iIdle = iMaxThreads - iCurrentllyUse;//最大線程數-可用線程數=idle線程數量
                        iCurrentUI = 0;


                        if (iIdle < uidbAccessNum)
                        {
                            tmpMsg = string.Format("{6} RTD Stop, keep the thread for UI to use: Max Thread[{0}], CurrentUse [{1}], Completed [{2}], Idle [{3}], UI use [{4}], " +
                                "iCurrentllyUse [{5}], _listDBSession [{7}]", maxThdWorker, iworkerThreads, iio3Threads, iIdle, iCurrentUI,
                                iCurrentllyUse, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), _listDBSession.Count);

                            if (_functionService.TimerTool("minutes", _lastProcessTime) >= 3)
                            {
                                _logger.Info(tmpMsg);
                                _lastProcessTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                            }

                            if (_functionService.TimerTool(timeoutunit, _ThreadsWaitingTime) >= threadTimeoutLimit)
                            {
                                tmpMsg = string.Format("Threads Issue, RTD will try to restart RTD service. [max:{0}, current use:{1}, idle:{2}]", maxThdWorker, iCurrentllyUse, iIdle);
                                _logger.Info(tmpMsg);
                                _ThreadsWaitingTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");

                                if(_swThredstimeout)
                                    goto IssueLogic;
                            }

                            //_ThreadsWaitingTime
                            //threadTimeoutLimit

                            if (_bKeepUI)
                                continue;
                        }
                        else
                        {
                            if (_swThredstimeout)
                                _ThreadsWaitingTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                        }

                        dbusyThreads = uidbAccessNum + iCurrentllyUse;
                        if (dbusyThreads >= maxThdWorker)
                        {
                            tmpMsg = string.Format("{6} RTD threads busy, the thread usage: Max Thread[{0}], AvailableUse [{1}],Completed [{2}], Idle [{3}], UI use [{4}], iCurrentllyUse [{5}], _listDBSession [{7}]"
                                , maxThdWorker, iworkerThreads, iio3Threads, iIdle, iCurrentUI, iCurrentllyUse, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), _listDBSession.Count);
                            _logger.Info(tmpMsg);
                        }
                        //Idle thread < ui access, do not run logic for schedule and listen

                        if (!_threadConntroll.ContainsKey("ipass"))
                        {
                            _threadConntroll.Add("ipass", "0");
                        }

                        if (_threadConntroll["ipass"].Equals("0"))
                            _threadConntroll["ipass"] = "1";
                        else
                            _threadConntroll["ipass"] = "0";


                        if (_threadConntroll["ipass"].Equals("0"))
                        {
                            ///keep 2 or 2 + UI access number for scheduled logic
                            if (iworkerThreads >= uidbAccessNum || !_bKeepUI)
                            {
                                ///do scheduled logic
                                ///int iCurrentUI = 0
                                _tempFunc = "scheduleProcess";

                                //iCurrentllySch++;

                                if (_lastStepTime.Equals("") || _functionService.TimerTool("seconds", _lastStepTime) >= 1)
                                {

                                    waitCallback = new WaitCallback(scheduleProcess);

                                    ThreadPool.QueueUserWorkItem(waitCallback, obj);


                                    _lastStepTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                                }

                            }
                            else
                            {
                                //_logger.Info(string.Format("Info: Thread CurUse: {0}.", iCurUse));
                            }
                        }
                        else
                        {
                            if(_eventQueue.Count > 0)
                            {
                                _tempFunc = "listeningStart";
                                //iCurrentllylis++;
                                ///When event queue count > 0 will do RTD task
                                waitCallback = new WaitCallback(listeningStart);
                                
                                ThreadPool.QueueUserWorkItem(waitCallback, obj);
                            }
                        }

                        int _totalsession = 0;
                        List<int> lstRemoveSeseion = new List<int>() { };
                        bool _exception = false;
                        if(_listDBSession.Count>0)
                        {
                            _totalsession = _listDBSession.Count;

                            for (int i = 0; i < _totalsession; i++)
                            {
                                ///Database re-connection logic
                                try
                                {
                                    dbTool = _listDBSession[i];
                                    if (!dbTool.IsConnected)
                                    {
                                        ///When db access disconnect will try to connection to database.
                                        lock (_listDBSession)
                                        {
                                            dbTool = new DBTool(tmpConnectString, tmpDatabase, tmpAutoDisconn, out msg);
                                            dbTool._dblogger = _logger;
                                            _listDBSession[i] = dbTool;

                                            if (!msg.Equals(""))
                                            {                                                
                                                _logger.Info(string.Format("[Database retry to connect.][{0}]", msg));
                                                lstRemoveSeseion.Add(i);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        try {
                                            _exception = false;

                                            _sql = _BaseDataService.QueryRTDServer(RTDServerName);
                                            dtTemp = dbTool.GetDataTable(_sql);
                                        }
                                        catch(Exception ex) {
                                            tmpMsg = "";
                                            lock (lstRemoveSeseion)
                                            {
                                                if (tmpMsg.Equals(""))
                                                {
                                                    _logger.Info(string.Format("[Database Issue, auto disabled session. cause [{0}]", ex.Message));
                                                    //lstRemoveSeseion.Add(i);
                                                    _exception = true;
                                                }
                                            }
                                        }

                                        if (_exception)
                                        {
                                            dbTool.DisConnectDB(out tmpMsg);
                                            if(!tmpMsg.Equals(""))
                                                _logger.Info(string.Format("Databse session disconnection result [{0}]", tmpMsg));
                                            lstRemoveSeseion.Add(i);
                                        }
                                    }
                                }
                                catch (Exception ex) { }
                            }

                            int j = 0;
                            foreach(int k in lstRemoveSeseion)
                            {
                                try {
                                    if (_listDBSession.Count > 0)
                                    {
                                        dbTool = _listDBSession[k-j];

                                        if (!dbTool.IsConnected)
                                        {
                                            lock (_listDBSession)
                                            {
                                                _logger.Info(string.Format("[Database session remove number [{0}]", k));
                                                _listDBSession.Remove(_listDBSession[k - j]);
                                            }
                                        }
                                        else
                                        {
                                            try
                                            {
                                                dbTool.DisConnectDB(out tmpMsg);
                                            }
                                            catch (Exception ex) { }

                                            lock (_listDBSession)
                                            {
                                                _logger.Info(string.Format("[dbTool state incorrect, still remove database session. listDBSession [{0}], Remove number [{1}]", _listDBSession.Count, k));
                                                _listDBSession.Remove(_listDBSession[k - j]);
                                            }
                                        }
                                        j++;
                                    }
                                }
                                catch(Exception ex) { }
                            }
                        }

                        if (_listDBSession.Count <= 0)
                            goto IssueLogic;

                            //inUse--;
                        Thread.Sleep(100);
                    }
                    catch (Exception ex) {
                        tmpMsg = string.Format("Major Exception: {0}", ex.Message);
                        Console.WriteLine(tmpMsg);
                        _logger.Info(tmpMsg);
                    }
                }
            }
            catch (Exception ex)
            { _IsAlive = false; }
            finally
            { }
        IssueLogic:
            try {
                string _tmpIssueLog = "";
                int _totalsession = 0;
                _tmpIssueLog = String.Format("EventLogicServer into issue logic..");
                _logger.Info(_tmpIssueLog);
                if (_listDBSession.Count > 1)
                {
                    _totalsession = _listDBSession.Count;
                    for (int i = 1; i < _totalsession; i++)
                    {
                        lock (_listDBSession)
                        {
                            tmpMsg = "";
                            _listDBSession[1].DisConnectDB(out tmpMsg);
                            Console.WriteLine(tmpMsg);
                            _listDBSession.Remove(_listDBSession[1]);
                        }
                    }
                }
                Thread.Sleep(5000 * iCurrentllyUse);

                _tmpIssueLog = String.Format("EventLogicServer been stop.");
                _logger.Info(_tmpIssueLog);
            }
            catch(Exception ex) { }
            _IsAlive = false;
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
            Dictionary<string, string> alarmDetail = (Dictionary<string, string>)obj[6];

            string tableOrder = "";
            string _keyOfEnv = "";
            string _keyRTDEnv = "";
            bool _NoConnect = true;
            int _retrytime = 0;
            string[] lsCtrlAlarmCode;

            IFunctionService _functionService = (FunctionService)obj[5];
            try
            {
#if DEBUG
                tmpMsg = string.Format("{2}, Function[{0}], Thread ID [{1}]", curPorcessName, Thread.CurrentThread.ManagedThreadId, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                Console.WriteLine(tmpMsg);
#else
#endif

                lstDB = (List<string>)obj[1];

                try {
                    _NoConnect = true;
                    while (_NoConnect)
                    {
                        _dbTool = new DBTool(lstDB[1], lstDB[2], lstDB[3], out tmpMsg);
                        if (!tmpMsg.Equals(""))
                        {
                            _retrytime++;
                            _logger.Info(string.Format("DB connect fail. retry {0} [{1}]", _retrytime, tmpMsg));
                            tmpMsg = "";
                        }
                        else
                        { 
                            _NoConnect = false; 
                        }

                        if (_retrytime > 3)
                            break;

                        if(_NoConnect)
                            Thread.Sleep(300);
                    }

                    if (_NoConnect)
                        goto DBissue;

                    _dbTool._dblogger = _logger;
                }
                catch (Exception ex) {
                    _logger.Info(string.Format("DB Initial fail. {0}", ex.Message));
                }

                _keyRTDEnv = _configuration["RTDEnvironment:type"];
                _keyOfEnv = string.Format("RTDEnvironment:commandsTable:{0}", _keyRTDEnv);
                //"RTDEnvironment:commandsTable:PROD"
                tableOrder = _configuration[_keyOfEnv] is null ? "workinprocess_sch" : _configuration[_keyOfEnv];

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

                    //與上位同步當前機台狀態 (RTD重開時, 即時同步機台狀態)
                    if (_functionService.SyncEQPStatus(_dbTool, _logger))
                    {
                        //Do Nothing.
                    }
                }

                EventQueue oEventQ = null;
                string LotID = "";
                string CarrierID = "";
                string Quantity = "0";
                string TotalQty = "0";
                bool QueryAvailableTester = false;
                bool ManaulDispatch = false;
                NormalTransferModel evtObject2 = null;
                TransferList evtObject3 = null;
                TSCAlarmCollect evtObject4 = null;
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
                        if (oEventQ.Equals(null))
                            break;

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
                        DateTime _currentTime = DateTime.Now;
                        string currentTime = _currentTime.ToString("yyyy-MM-dd HH:mm:ss");

                        switch (oEventQ.EventName)
                        {
                            case "CarrierLocationUpdate":
                                
                                //Object type is CarrierLocationUpdate
                                //檢查Carrier所帶的lot id 是否符合當前待料的機台
                                CarrierLocationUpdate evtObject = (CarrierLocationUpdate)oEventQ.EventObject;
                                CarrierID = evtObject.CarrierID.Trim();
                                List<string> args = new();
                                string _tmplotid = "";

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
                                    LotID = dt.Rows[0]["lot_id"].ToString().Equals("") ? "" : dt.Rows[0]["lot_id"].ToString().Trim();
                                    Quantity = dt.Rows[0]["quantity"].ToString().Equals("") ? "0" : dt.Rows[0]["quantity"].ToString().Trim();
                                    TotalQty = dt.Rows[0]["total_qty"].ToString().Equals("") ? "0" : dt.Rows[0]["total_qty"].ToString().Trim(); ;

                                    //--carrier id, lot_id, lotType, customername, partid
                                    tmpMsg = string.Format("[CarrierLocationUpdate: Flag LotId {0}]", LotID);
                                    _logger.Debug(tmpMsg);

                                    if (LotID.Contains("R") || LotID.Contains("S"))
                                        _tmplotid = LotID.Replace("R", "").Replace("S", "");
                                    else
                                        _tmplotid = LotID;

                                    sql = _BaseDataService.SelectTableLotInfoByLotid(_tmplotid);
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
                                        string v_HOLDREAS = "";
                                        string v_POTD = "";
                                        string v_WAFERLOT = "";
                                        string v_Quantity = "";
                                        string v_TotalQty = "";
                                        string v_Force = "true";
                                        try
                                        {
                                            //20230427 Add by Vance, 
                                            if (_configuration["AppSettings:Work"].ToUpper().Equals("EWLB"))
                                            {
                                                if (evtObject.LocationType.Equals("ERACK"))
                                                {
                                                    sql = _BaseDataService.EQPListReset(_tmplotid);
                                                    _dbTool.SQLExec(sql, out tmpMsg, true);
                                                }
                                            }

                                            v_CUSTOMERNAME = dt.Rows[0]["customername"].ToString().Equals("") ? "" : dt.Rows[0]["customername"].ToString();
                                            v_PARTID = dt.Rows[0]["partid"].ToString().Equals("") ? "" : dt.Rows[0]["partid"].ToString();
                                            v_LOTTYPE = dt.Rows[0]["lotType"].ToString().Equals("") ? "" : dt.Rows[0]["lotType"].ToString();
                                            v_Quantity = Quantity;
                                            v_TotalQty = TotalQty;

                                            sql = _BaseDataService.QueryErackInfoByLotID(_configuration["eRackDisplayInfo:contained"], _tmplotid);
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
                                                v_HOLDREAS = dtTemp.Rows[0]["HoldReas"].ToString().Equals("") ? "" : dtTemp.Rows[0]["HoldReas"].ToString();
                                                v_WAFERLOT = dtTemp.Rows[0]["waferlotid"].ToString().Equals("") ? "" : dtTemp.Rows[0]["waferlotid"].ToString();
                                            }

                                            if (dt.Rows[0]["rtd_state"].ToString().Equals("COMPLETED")|| dt.Rows[0]["rtd_state"].ToString().Equals("PROC"))
                                            {
                                                ///當RTD STATE是COMPLETED
                                                ///檢查ADS 信息, 站點與RTD站點相同時, ADS STATE 等於WAIT時, 
                                                ///RTD狀態還原為STATE=WAIT, RTD_STATE=WAIT
                                                try
                                                {
                                                    if (dt.Rows[0]["stage"].ToString().Equals(v_STAGE))
                                                    {
                                                        if (v_STATE.Equals("WAIT"))
                                                        {
                                                            ///Reset lotinfo state
                                                            sql = _BaseDataService.ResetRTDStateByLot(LotID);
                                                            _dbTool.SQLExec(sql, out tmpMsg, true);
                                                            tmpMsg = string.Format("[CarrierLocationUpdate: RTD State Reset. {0}]", _tmplotid);
                                                            _logger.Debug(tmpMsg);
                                                        }
                                                    }
                                                }catch (Exception ex)
                                                {
                                                    tmpMsg = string.Format("[CarrierLocationUpdate: RTD State Exception. {0}]", ex.Message);
                                                    _logger.Debug(tmpMsg);
                                                }
                                            }
                                        }
                                        catch(Exception ex)
                                        {
                                            tmpMsg = string.Format("[CarrierLocationUpdate: Column Issue. {0}]", ex.Message);
                                            _logger.Debug(tmpMsg);
                                        }

                                        if (_functionService.CheckMCSStatus(_dbTool, _logger))
                                        { 
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
                                            args.Add(v_HOLDREAS);//("EOTD");
                                            args.Add(v_POTD);//("EOTD");
                                            args.Add(v_WAFERLOT);//("EOTD");
                                            args.Add(v_Quantity);//("Quantity");
                                            args.Add(v_TotalQty);//("TotalQty");
                                            args.Add(v_Force);//("v_Force");

                                            //args.Add(dt.Rows[0]["HOLDCODE"].ToString().Equals("") ? "" : dt.Rows[0]["HOLDCODE"].ToString());
                                            //args.Add(dt.Rows[0]["HOLDREAS"].ToString().Equals("") ? "" : dt.Rows[0]["HOLDREAS"].ToString());
                                            _functionService.SentCommandtoMCSByModel(_dbTool, _configuration, _logger, "InfoUpdate", args);

                                            if (!CarrierID.Equals(""))
                                            {
                                                tmpMsg = "";
                                                if (v_Force.ToLower().Equals("true"))
                                                {
                                                    sql = _BaseDataService.CarrierTransferDTUpdate(CarrierID, "InfoUpdate");
                                                    _dbTool.SQLExec(sql, out tmpMsg, true);
                                                }

                                                //tmpMsg = "";
                                                //sql = _BaseDataService.CarrierTransferDTUpdate(CarrierID, "LocateUpdate");
                                                //_dbTool.SQLExec(sql, out tmpMsg, true);
                                            }
                                        }
                                        else
                                        {
                                            _logger.Info(string.Format("MCS Status incorrect, [{0}][{1}] not success. [{2}]", oEventQ.EventName, "InfoUpdate", CarrierID));
                                        }

                                        if (!_tmplotid.Equals(""))
                                        {
                                            //Carrier Location Update時, 檢查 Carrier binding 的lotid 狀態 並更新到 lot_info
                                            _functionService.CheckCurrentLotStatebyWebService(_dbTool, _configuration, _logger, _tmplotid);
                                        }
                                    }
                                    else
                                    {
                                        tmpMsg = string.Format("[CarrierLocationUpdate: Carrier [{0}] Not Exist.]", CarrierID);
                                        _logger.Debug(tmpMsg);

                                        if (_functionService.CheckMCSStatus(_dbTool, _logger))
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
                                            _functionService.SentCommandtoMCSByModel(_dbTool, _configuration, _logger, "InfoUpdate", args);

                                        }
                                        else
                                        {
                                            _logger.Info(string.Format("MCS Status incorrect, [{0}][{1}] not success. [{2}]", oEventQ.EventName, "InfoUpdate", CarrierID));
                                        }
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
                                Boolean _isDisabled = false;
                                string _keyEventTime = "";
                                List<string> _lsParams = new List<string>();

                                //--equipid, portid, recipeid, portstate carrierid, lotid
                                string _recipeid = "";
                                string _carrierTypeSet = "";
                                string _eqID = "";
                                string _stageOflot = "";
                                string _tableHostLot = "";

                                //CarrierID = evtObject.CarrierID.Trim();
                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[Debug Message][{0}] {1} / {2}", oEventQ.EventName, _aeiPortInfo.PortID, _aeiPortInfo.PortTransferState);
                                    _logger.Debug(tmpMsg);
                                }

                                try {
                                    sql = string.Format(_BaseDataService.SelectTableEQP_Port_SetByPortId(_aeiPortInfo.PortID));
                                    dtTemp = _dbTool.GetDataTable(sql);
                                    if (dtTemp.Rows.Count > 0)
                                    {
                                        _isDisabled = dtTemp.Rows[0]["DISABLE"].ToString().Equals("1") ? true : false;
                                        _carrierTypeSet = dtTemp.Rows[0]["carrier_type"] is not null ? dtTemp.Rows[0]["carrier_type"].ToString() : "EMPTY";
                                        _eqID = dtTemp.Rows[0]["equipid"] is not null ? dtTemp.Rows[0]["equipid"].ToString() : "EMPTY";
                                    }

                                    if (_isDisabled)
                                        break;

                                    ///record port state for history
                                    ///
                                    _tableHostLot = _configuration["eRackDisplayInfo:table"] is null ? "lot_Info" : _configuration["eRackDisplayInfo:table"];
                                    _keyEventTime = _currentTime.ToString("yyyyMMddHHmmssfffff");

                                    sql = string.Format(_BaseDataService.GetEquipCustDevice(_eqID));
                                    dtTemp = _dbTool.GetDataTable(sql);
                                    if (dtTemp.Rows.Count > 0)
                                    {
                                        _recipeid = dtTemp.Rows[0]["device"] is not null ? dtTemp.Rows[0]["device"].ToString() : "";
                                    }

                                    sql = string.Format(_BaseDataService.QueryDataByLot(_tableHostLot, _aeiPortInfo.LotID));
                                    dtTemp = _dbTool.GetDataTable(sql);
                                    if (dtTemp.Rows.Count > 0)
                                    {
                                        _stageOflot = dtTemp.Rows[0]["stage"] is not null ? dtTemp.Rows[0]["stage"].ToString() : "";
                                    }

                                    _lsParams.Add(_keyEventTime);
                                    _lsParams.Add(_eqID);
                                    _lsParams.Add(_aeiPortInfo.PortID);
                                    _lsParams.Add(_recipeid);
                                    _lsParams.Add(_aeiPortInfo.PortTransferState.ToString());
                                    _lsParams.Add(_aeiPortInfo.CarrierID);
                                    _lsParams.Add(_aeiPortInfo.LotID);
                                    _lsParams.Add(currentTime);
                                    _lsParams.Add(_carrierTypeSet);
                                    _lsParams.Add(_stageOflot);

                                    sql = string.Format(_BaseDataService.InsertPortStateChangeEvent(_lsParams));
                                    _dbTool.SQLExec(sql, out tmpMsg, true);
                                }
                                catch(Exception ex) { }
                                //_functionService.EquipmentPortStatusUpdate(_dbTool, _configuration, _aeiPortInfo, _logger);
                                NormalTransferModel eventObject = new NormalTransferModel();

                                if (_aeiPortInfo.PortTransferState.Equals(2) || _aeiPortInfo.PortTransferState.Equals(3) || _aeiPortInfo.PortTransferState.Equals(4) || _aeiPortInfo.PortTransferState.Equals(5))
                                {
                                    bool bDoUnlock = true;
                                    if(_aeiPortInfo.PortTransferState.Equals(4))
                                    {
                                        //"Position": "202311161021001"
                                        //2023/11/15 Add 狀態切換為Ready to Load時, Order裡仍有Load指令時, 不解鎖
                                        dtTemp = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSchByPortId(_aeiPortInfo.PortID, tableOrder));
                                        
                                        if (dtTemp.Rows.Count > 0)
                                            bDoUnlock = false;
                                        else
                                            bDoUnlock = true;
                                    }

                                    if (bDoUnlock)
                                    {
                                        //20230728 Add, 防止重複派送指令 收到run/ready to load時才解鎖
                                        _dbTool.SQLExec(_BaseDataService.LockEquipPortByPortId(_aeiPortInfo.PortID, false), out tmpMsg, true);

                                        if (_DebugMode)
                                        {
                                            tmpMsg = string.Format("[Debug Message][{0}] Port [{1}] has been unlock / State[{2}]", oEventQ.EventName, _aeiPortInfo.PortID, _aeiPortInfo.PortTransferState);
                                            _logger.Debug(tmpMsg);
                                        }
                                    }
                                }

                                if (_aeiPortInfo.PortTransferState.Equals(3) || _aeiPortInfo.PortTransferState.Equals(2))
                                {
                                    DataTable dtTemp2 = null;
                                    string tmpPortSeq = "0";
                                    eventObject.CarrierID = _aeiPortInfo.CarrierID;
                                    //SelectTableEQP_Port_SetByPortId
                                    dtTemp = _dbTool.GetDataTable(_BaseDataService.SelectTableEQP_Port_SetByPortId(_aeiPortInfo.PortID));

                                    if (dtTemp.Rows.Count > 0)
                                    {
                                        eventObject.EquipmentID = dtTemp.Rows[0]["EQUIPID"].ToString();
                                        eventObject.PortModel = dtTemp.Rows[0]["PORT_MODEL"].ToString();
                                        tmpPortSeq = dtTemp.Rows[0]["PORT_SEQ"].ToString();

                                        dtTemp2 = _dbTool.GetDataTable(_BaseDataService.SelectTableEquipmentPortsInfoByEquipId(eventObject.EquipmentID));
                                        if (dtTemp2.Rows.Count > 0)
                                        {
                                            if (dtTemp2.Rows[0]["Expired"].ToString().Equals("1"))
                                            {
                                                if (dtTemp2.Rows[0]["manualmode"].ToString().Equals("1"))
                                                    break;
                                            }
                                            else
                                            {
                                                //仍然要執行Unload
                                                //if (dtTemp2.Rows[0]["manualmode"].ToString().Equals("1"))
                                                //    break;
                                            }
                                        }
                                    }
                                    else
                                        eventObject.EquipmentID = "";

                                    if(!_aeiPortInfo.CarrierID.Trim().Equals(""))
                                    {
                                        dtTemp2 = _dbTool.GetDataTable(_BaseDataService.SelectTableEquipmentPortsInfoByEquipId(eventObject.EquipmentID));
                                        if (dtTemp2.Rows.Count > 0)
                                        {
                                            if (dtTemp2.Rows[0]["Expired"].ToString().Equals("1"))
                                            {
                                                if (dtTemp2.Rows[0]["manualmode"].ToString().Equals("1"))
                                                    break;
                                            }
                                            else
                                            {
                                                //仍然要執行Unload
                                                //if (dtTemp2.Rows[0]["manualmode"].ToString().Equals("1"))
                                                //    break;
                                            }
                                        }

                                        string _tmpCarrier = "";
                                        try
                                        {
                                            //"Position": "202310311415001"
                                            //Check RTD Carrier ID in loadport
                                            dtTemp2 = _dbTool.GetDataTable(_BaseDataService.QueryCarrierOnPort(_aeiPortInfo.PortID));
                                            if (dtTemp2.Rows.Count > 0)
                                            {
                                                _tmpCarrier = dtTemp2.Rows[0]["carrier_id"] is null? "" : dtTemp2.Rows[0]["carrier_id"].ToString();
                                            }

                                            if (!_aeiPortInfo.CarrierID.Trim().Equals(_tmpCarrier))
                                            {
                                                if (_tmpCarrier.Trim().Equals(""))
                                                {
                                                    tmpMsg = string.Format("[{0}][Sync] Carrier [{1}] Locate Sync from loadport [{2}].", oEventQ.EventName, _aeiPortInfo.CarrierID, _aeiPortInfo.PortID);
                                                    _logger.Debug(tmpMsg);

                                                    _dbTool.SQLExec(_BaseDataService.UpdateLastLotIDtoEQPPortSet(eventObject.EquipmentID, tmpPortSeq, _aeiPortInfo.CarrierID), out tmpMsg, true);
                                                    if (!tmpMsg.Equals(""))
                                                        _logger.Debug(tmpMsg);
                                                }
                                                else
                                                {
                                                    tmpMsg = string.Format("[{0}] carrier id is not same as rtd and loadport [{1}] [{2}/{3}]", oEventQ.EventName, _aeiPortInfo.PortID, _tmpCarrier, _aeiPortInfo.CarrierID);
                                                    _logger.Debug(tmpMsg);

                                                    if (!_aeiPortInfo.CarrierID.Trim().Equals(""))
                                                    {
                                                        _dbTool.SQLExec(_BaseDataService.UpdateLastLotIDtoEQPPortSet(eventObject.EquipmentID, tmpPortSeq, _aeiPortInfo.CarrierID), out tmpMsg, true);
                                                        if (!tmpMsg.Equals(""))
                                                            _logger.Debug(tmpMsg);
                                                    }
                                                }
                                            }
                                        }
                                        catch(Exception ex)
                                        {
                                            tmpMsg = string.Format("[Exception][{0}] Exception: [{1}]/[{2}]", oEventQ.EventName, _aeiPortInfo.CarrierID, _tmpCarrier);
                                            _logger.Debug(tmpMsg);
                                        }
                                    }

                                    eventObject.LotID = _aeiPortInfo.LotID;
                                    LotID = _aeiPortInfo.LotID;


                                    oEventQ.EventObject = eventObject;

                                    if (_functionService.CreateTransferCommandByPortModel(_dbTool, _configuration, _logger, _DebugMode, eventObject.EquipmentID, eventObject.PortModel, oEventQ, out arrayOfCmds))
                                    { }
                                    else
                                    { }
                                }

                                if (_aeiPortInfo.PortTransferState.Equals(2))
                                {
                                    //record port state change event.

                                }

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
                                string tCommandStatusUpdate = "CommandStatusUpdate";

                                HisCommandStatus hisCommandStatus = new HisCommandStatus();
                                DataTable dtCmdStateUpdate = new DataTable();
                                DataTable dt2CmdStateUpdate = new DataTable();
                                CommandStatusUpdate tmpObject = (CommandStatusUpdate)oEventQ.EventObject;
                                string CommandID = tmpObject.CommandID;
                                string CommandType = "";
                                hisCommandStatus.CommandID = CommandID;
                                string EquipID = "";
                                string strPort = "0";
                                string _portID = "";
                                string _args = "";

                                //CultureInfo culture = new CultureInfo("en-US");
                                //unction:CommandStatusUpdate, Received:[{"commandID":"20230615032418149","eventStatus":null,"carrierID":null,"dest":null,"completed":false,"duration":0,"alarmCode":"10017","status":"Failed","createdAt":null,"lastStateTime":"2023-06-15 11:16:09"}]
                                try
                                {

                                    if (_DebugMode)
                                    {
                                        tmpMsg = string.Format("[Debug Message] {0} / {1} ", oEventQ.EventName, CommandID);
                                        _logger.Debug(tmpMsg);
                                    }

                                    lsCtrlAlarmCode = _configuration["RTDAlarm:CtrlAlarmCode"] is not null ? _configuration["RTDAlarm:CtrlAlarmCode"].Split(',') : null;

                                    _functionService.CommandStatusUpdate(_dbTool, _configuration, tmpObject, _logger);

                                    sql = _BaseDataService.GetWorkinprocessSchByCommand(CommandID, tableOrder);
                                    dt2CmdStateUpdate = _dbTool.GetDataTable(sql);

                                    if (dt2CmdStateUpdate.Rows.Count <= 0)
                                    {
                                        //GetHisWorkinprocessSchByCommand
                                        sql = _BaseDataService.GetHisWorkinprocessSchByCommand(CommandID);
                                        dt2CmdStateUpdate = _dbTool.GetDataTable(sql);

                                    }

                                    if (dt2CmdStateUpdate.Rows.Count > 0)
                                    {
                                        foreach(DataRow dr1 in dt2CmdStateUpdate.Rows)
                                        {
                                            CommandType = dr1["cmd_type"].ToString();


                                            if (tmpObject.Status.Equals("Success") || tmpObject.Status.Equals("Failed") || tmpObject.Status.Equals("Init"))
                                            {
                                                _logger.Debug(string.Format("[CommandStatusUpdate][Flag] {0} ", "Success/Failed/Init"));
                                                sql = _BaseDataService.GetWorkinprocessSchByCommand(CommandID, tableOrder);
                                                dtCmdStateUpdate = _dbTool.GetDataTable(sql);
                                                if (dtCmdStateUpdate.Rows.Count > 0)
                                                {
                                                    //還有workinprocess
                                                    //Do Nothing
                                                }
                                                else
                                                {
                                                    _args = string.Format("{0},{1},{2},{3}", CommandID, "LOAD", "UNLOAD", "Init");
                                                    sql = _BaseDataService.QueryHisWorkinprocess(_args);
                                                    dtCmdStateUpdate = _dbTool.GetDataTable(sql);
                                                }
                                                _logger.Debug(string.Format("[CommandStatusUpdate][Flag] {0} ", sql));

                                                hisCommandStatus.CarrierID = "";
                                                hisCommandStatus.LotID = "";
                                                hisCommandStatus.Source = "";
                                                hisCommandStatus.Dest = "";
                                                hisCommandStatus.CommandType = "";
                                                hisCommandStatus.AlarmCode = "0";
                                                hisCommandStatus.CreatedAt = "";
                                                hisCommandStatus.LastStateTime = "";

                                                if (tmpObject.Status.Equals("Success"))
                                                {
                                                    _logger.Debug(string.Format("[CommandStatusUpdate][Flag] {0} ", "Success"));

                                                    if (dtCmdStateUpdate.Rows.Count > 0)
                                                    {
                                                        _logger.Debug(string.Format("[CommandStatusUpdate][Flag] {0} ", dtCmdStateUpdate.Rows.Count));

                                                        LotID = dtCmdStateUpdate.Rows[0]["lotid"].ToString().Equals("") ? "" : dtCmdStateUpdate.Rows[0]["lotid"].ToString();
                                                        EquipID = dtCmdStateUpdate.Rows[0]["equipid"].ToString().Equals("") ? "" : dtCmdStateUpdate.Rows[0]["equipid"].ToString();

                                                        try
                                                        {
                                                            hisCommandStatus.LotID = LotID;
                                                            hisCommandStatus.CarrierID = dtCmdStateUpdate.Rows[0]["carrierid"].ToString();
                                                            hisCommandStatus.CommandType = dtCmdStateUpdate.Rows.Count > 1 ? "LOAD" : dtCmdStateUpdate.Rows[0]["cmd_type"].ToString();
                                                            hisCommandStatus.Source = tmpObject.Source is null ? dtCmdStateUpdate.Rows[0]["Source"].ToString().Equals("") ? "" : dtCmdStateUpdate.Rows[0]["Source"].ToString() : tmpObject.Source;
                                                            hisCommandStatus.Dest = tmpObject.dest is null ? dtCmdStateUpdate.Rows[0]["dest"].ToString().Equals("") ? "" : dtCmdStateUpdate.Rows[0]["dest"].ToString() : tmpObject.dest;
                                                            //hisCommandStatus.CreatedAt = Convert.ToDateTime(dtCmdStateUpdate.Rows[0]["create_dt"].ToString(), culture).ToString("yyyy/MM/dd HH:mm:ss");
                                                            try
                                                            {
                                                                hisCommandStatus.CreatedAt = Convert.ToDateTime(dtCmdStateUpdate.Rows[0]["create_dt"].ToString()).ToString("yyyy/MM/dd HH:mm:ss");
                                                            }
                                                            catch (Exception ex)
                                                            { }
                                                            hisCommandStatus.LastStateTime = tmpObject.LastStateTime;
                                                            hisCommandStatus.AlarmCode = tmpObject.AlarmCode is not null ? tmpObject.AlarmCode : "0";
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            tmpMsg = string.Format("[Debug Message] Success: {0} / {1} ", CommandID, ex.Message);
                                                            _logger.Debug(tmpMsg);
                                                        }
                                                        //_logger.Debug(string.Format("[CommandStatusUpdate][Flag] {0} ", "Success 1"));

                                                        //cmd_type
                                                        if (!dtCmdStateUpdate.Rows[0]["cmd_type"].Equals("Pre-Transfer"))
                                                        {
                                                            // _logger.Debug(string.Format("[CommandStatusUpdate][Flag] {0} ", "Success 2"));

                                                            _dbTool.SQLExec(_BaseDataService.UpdateLotInfoWhenCOMP(CommandID, tableOrder), out tmpMsg, true);
                                                            //新增一筆Success Record
                                                            //InsertRTDStatisticalRecord
                                                            if (dtCmdStateUpdate.Rows[0]["cmd_type"].ToString().ToUpper().Equals("LOAD"))
                                                            {
                                                                _dbTool.SQLExec(_BaseDataService.InsertRTDStatisticalRecord(currentTime, CommandID, "S"), out tmpMsg, true);

                                                                //Load Carrier to Equipment is success. 
                                                                string equipID = dtCmdStateUpdate.Rows[0]["lotid"].ToString();
                                                                //_dbTool.SQLExec(_BaseDataService.UpdateCustDeviceByEquipID(EquipID, EquipID), out tmpMsg, true);

                                                                //只要正常搬完, 先把Eqp Port State改為0
                                                                //strPort = hisCommandStatus.Dest.Split("_LP")[1].ToString();
                                                                //_dbTool.SQLExec(_BaseDataService.UpdateTableEQP_Port_Set(EquipID, strPort, "0"), out tmpMsg, true);

                                                                //command update 也更新 lastModify_dt 
                                                                try { 
                                                                    strPort = hisCommandStatus.Dest.Split("_LP")[1].ToString();
                                                                    _dbTool.SQLExec(_BaseDataService.UpdateTableEQP_Port_Set(EquipID, strPort), out tmpMsg, true);
                                                                } catch (Exception ex) { }
                                                            }
                                                            else if (dtCmdStateUpdate.Rows[0]["cmd_type"].ToString().ToUpper().Equals("UNLOAD"))
                                                            {
                                                                //只要正常搬完, 先把Eqp Port State改為0
                                                                //strPort = hisCommandStatus.Source.Split("_LP")[1].ToString();
                                                                //_dbTool.SQLExec(_BaseDataService.UpdateTableEQP_Port_Set(EquipID, strPort, "0"), out tmpMsg, true);

                                                                //command update 也更新 lastModify_dt 
                                                                try
                                                                {
                                                                    strPort = hisCommandStatus.Source.Split("_LP")[1].ToString();
                                                                    _dbTool.SQLExec(_BaseDataService.UpdateTableEQP_Port_Set(EquipID, strPort), out tmpMsg, true);
                                                                }catch (Exception ex) { }
                                                            }

                                                            tmpMsg = string.Format("[{0}: dtWIP.Rows.Count > 0]", tCommandStatusUpdate);
                                                            _logger.Debug(tmpMsg);
                                                            LotID = dtCmdStateUpdate.Rows[0]["lotid"].ToString().Equals("") ? "" : dtCmdStateUpdate.Rows[0]["lotid"].ToString();

                                                            _logger.Debug(string.Format("Lot ID is {0}", LotID));

                                                            _logger.Debug(string.Format("[CommandStatusUpdate][Flag] {0} ", "Success 3"));
                                                        }
                                                        else
                                                        {
                                                            _logger.Debug(string.Format("[CommandStatusUpdate][Flag] {0} ", "Success 4"));

                                                            if (!LotID.Equals(""))
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
                                                            _logger.Debug(string.Format("[{0}] Done", tCommandStatusUpdate));
                                                        }

                                                        if (_threadControll.ContainsKey(LotID))
                                                        {
                                                            lock (_threadControll)
                                                            {
                                                                _threadControll.Remove(LotID);
                                                            }
                                                        }
                                                    }
                                                }
                                                else if (tmpObject.Status.Equals("Failed"))
                                                {
                                                    _logger.Debug(string.Format("[CommandStatusUpdate][Flag] {0} ", "Failed"));

                                                    if (dtCmdStateUpdate.Rows.Count > 0)
                                                    {
                                                        _logger.Debug(string.Format("[CommandStatusUpdate][Flag] {0} ", "Failed 1"));

                                                        LotID = dtCmdStateUpdate.Rows[0]["lotid"].ToString().Equals("") ? "" : dtCmdStateUpdate.Rows[0]["lotid"].ToString();
                                                        try
                                                        {
                                                            hisCommandStatus.LotID = LotID;
                                                            hisCommandStatus.CarrierID = dtCmdStateUpdate.Rows[0]["carrierid"].ToString();
                                                            hisCommandStatus.CommandType = dtCmdStateUpdate.Rows[0]["cmd_type"].ToString();
                                                            hisCommandStatus.Source = tmpObject.Source is null ? dtCmdStateUpdate.Rows[0]["Source"].ToString().Equals("") ? "" : dtCmdStateUpdate.Rows[0]["Source"].ToString() : tmpObject.Source.Equals("") ? dtCmdStateUpdate.Rows[0]["Source"].ToString() : tmpObject.Source;
                                                            hisCommandStatus.Dest = tmpObject.dest is null ? dtCmdStateUpdate.Rows[0]["dest"].ToString().Equals("") ? "" : dtCmdStateUpdate.Rows[0]["dest"].ToString() : tmpObject.dest.Equals("") ? dtCmdStateUpdate.Rows[0]["dest"].ToString() : tmpObject.dest;
                                                            hisCommandStatus.CreatedAt = Convert.ToDateTime(dtCmdStateUpdate.Rows[0]["create_dt"].ToString()).ToString("yyyy/MM/dd HH:mm:ss");
                                                            hisCommandStatus.LastStateTime = tmpObject.LastStateTime;
                                                            hisCommandStatus.AlarmCode = tmpObject.AlarmCode is not null ? tmpObject.AlarmCode : "0";
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            tmpMsg = string.Format("[Debug Message] Failed: {0} / {1} ", CommandID, ex.Message);
                                                            _logger.Debug(tmpMsg);
                                                        }
                                                        _logger.Debug(string.Format("[CommandStatusUpdate][Flag] {0} ", "Failed 2"));

                                                        try
                                                        {
                                                            _portID = "";
                                                            if (hisCommandStatus.CommandType.ToUpper().Equals("LOAD"))
                                                            {
                                                                _portID = hisCommandStatus.Dest;
                                                                _dbTool.SQLExec(_BaseDataService.LockEquipPortByPortId(_portID, false), out tmpMsg, true);
                                                            }
                                                            else if (hisCommandStatus.CommandType.ToUpper().Equals("UNLOAD"))
                                                            {
                                                                _portID = hisCommandStatus.Source;
                                                                _dbTool.SQLExec(_BaseDataService.LockEquipPortByPortId(_portID, false), out tmpMsg, true);
                                                            }
                                                            else
                                                            {
                                                                //Do Nothing
                                                            }

                                                            if (tmpMsg.Equals(""))
                                                            {
                                                                if (_DebugMode)
                                                                {
                                                                    tmpMsg = string.Format("[Debug Message][{0}] Port [{1}] has been unlock.", oEventQ.EventName, _portID);
                                                                    _logger.Debug(tmpMsg);
                                                                }
                                                            }
                                                        }
                                                        catch (Exception ex)
                                                        { }

                                                        _dbTool.SQLExec(_BaseDataService.UpdateLotInfoWhenFail(CommandID, tableOrder), out tmpMsg, true);
                                                        //新增一筆Failed Record
                                                        if (dtCmdStateUpdate.Rows[0]["cmd_type"].ToString().ToUpper().Equals("LOAD"))
                                                            _dbTool.SQLExec(_BaseDataService.InsertRTDStatisticalRecord(currentTime, CommandID, "F"), out tmpMsg, true);

                                                        _logger.Debug(string.Format("[CommandStatusUpdate][Flag] {0} ", "Failed 3"));

                                                        if (!LotID.Equals(""))
                                                        {
                                                            if (_threadControll.ContainsKey(LotID))
                                                            {
                                                                lock (_threadControll)
                                                                {
                                                                    _threadControll.Remove(LotID);
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                                else if (tmpObject.Status.Equals("Init"))
                                                {
                                                    _logger.Debug(string.Format("[CommandStatusUpdate][Flag] {0} ", "Init"));

                                                    if (dtCmdStateUpdate.Rows.Count > 0)
                                                    {
                                                        LotID = dtCmdStateUpdate.Rows[0]["lotid"].ToString().Equals("") ? "" : dtCmdStateUpdate.Rows[0]["lotid"].ToString();

                                                        try
                                                        {
                                                            hisCommandStatus.LotID = LotID;
                                                            hisCommandStatus.CarrierID = dtCmdStateUpdate.Rows[0]["carrierid"].ToString();
                                                            hisCommandStatus.CommandType = dtCmdStateUpdate.Rows[0]["cmd_type"].ToString();
                                                            hisCommandStatus.Source = tmpObject.Source is null ? dtCmdStateUpdate.Rows[0]["Source"].ToString().Equals("") ? "" : dtCmdStateUpdate.Rows[0]["Source"].ToString() : tmpObject.Source;
                                                            hisCommandStatus.Dest = tmpObject.dest is null ? dtCmdStateUpdate.Rows[0]["dest"].ToString().Equals("") ? "" : dtCmdStateUpdate.Rows[0]["dest"].ToString() : tmpObject.dest;
                                                            hisCommandStatus.CreatedAt = _functionService.TryConvertDatetime(dtCmdStateUpdate.Rows[0]["create_dt"].ToString());
                                                            hisCommandStatus.LastStateTime = tmpObject.LastStateTime;
                                                            hisCommandStatus.AlarmCode = tmpObject.AlarmCode is not null ? tmpObject.AlarmCode : "0";
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            tmpMsg = string.Format("[Debug Message] Init: {0} / {1} ", CommandID, ex.Message);
                                                            _logger.Debug(tmpMsg);
                                                        }
                                                        //新增一筆Total Record
                                                        //_dbTool.SQLExec(_BaseDataService.InsertRTDStatisticalRecord(currentTime, CommandID, "T"), out tmpMsg, true);
                                                    }
                                                    break;
                                                }
                                                else
                                                {
                                                    _logger.Debug(string.Format("[CommandStatusUpdate][Flag] {0} ", "Other"));

                                                    LotID = dtCmdStateUpdate.Rows[0]["lotid"].ToString().Equals("") ? "" : dtCmdStateUpdate.Rows[0]["lotid"].ToString();

                                                    try
                                                    {
                                                        hisCommandStatus.LotID = LotID;
                                                        hisCommandStatus.CarrierID = dtCmdStateUpdate.Rows[0]["carrierid"].ToString();
                                                        hisCommandStatus.CommandType = dtCmdStateUpdate.Rows[0]["cmd_type"].ToString();
                                                        hisCommandStatus.Source = tmpObject.Source is null ? dtCmdStateUpdate.Rows[0]["Source"].ToString().Equals("") ? "" : dtCmdStateUpdate.Rows[0]["Source"].ToString() : tmpObject.Source;
                                                        hisCommandStatus.Dest = tmpObject.dest is null ? dtCmdStateUpdate.Rows[0]["dest"].ToString().Equals("") ? "" : dtCmdStateUpdate.Rows[0]["dest"].ToString() : tmpObject.dest;
                                                        hisCommandStatus.CreatedAt = Convert.ToDateTime(dtCmdStateUpdate.Rows[0]["create_dt"].ToString()).ToString("yyyy/MM/dd HH:mm:ss");
                                                        hisCommandStatus.LastStateTime = tmpObject.LastStateTime;
                                                        hisCommandStatus.AlarmCode = tmpObject.AlarmCode is not null ? tmpObject.AlarmCode : "0";
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        tmpMsg = string.Format("[Debug Message] Other: {0} / {1} ", CommandID, ex.Message);
                                                        _logger.Debug(tmpMsg);
                                                    }
                                                    break;
                                                }

                                                try
                                                {
                                                    if (!hisCommandStatus.LastStateTime.Equals(""))
                                                    {
                                                        _logger.Debug(string.Format("[CommandStatusUpdate][Flag] {0} ", hisCommandStatus.CreatedAt));

                                                        sql = _BaseDataService.HisCommandAppend(hisCommandStatus);
                                                        _dbTool.SQLExec(sql, out tmpMsg, true);
                                                        sql = "";
                                                    }

                                                    if (!hisCommandStatus.AlarmCode.Equals("0"))
                                                    {
                                                        RTDAlarms rtdAlarms = new RTDAlarms();
                                                        bool haveAlarmCode = false;
                                                        switch (hisCommandStatus.AlarmCode)
                                                        {
                                                            case "10007":
                                                                //Robot E84 interlock error
                                                                haveAlarmCode = true;
                                                                rtdAlarms.UnitType = "System";//System
                                                                rtdAlarms.UnitID = "TSC";//MCS
                                                                rtdAlarms.Level = "Alarm";//Error
                                                                rtdAlarms.Code = int.Parse(hisCommandStatus.AlarmCode);//Code
                                                                                                                       //rtdAlarms.Cause = alarmDetail[hisCommandStatus.AlarmCode] is null ? "" : alarmDetail[hisCommandStatus.AlarmCode];
                                                                rtdAlarms.SubCode = "0";
                                                                rtdAlarms.Detail = "";
                                                                rtdAlarms.CommandID = hisCommandStatus.CommandID;//
                                                                string tmpParams = "";
                                                                try
                                                                {
                                                                    EquipID = !EquipID.Equals("") ? EquipID : _portID.Split("_LP")[0].ToString();
                                                                    //string tmpParams = "{\"EquipID\":\"{0}\"}";
                                                                    tmpParams = string.Format("\"EquipID\":\"{0}\",\"PortID\":\"{1}\"", EquipID, _portID);
                                                                    //rtdAlarms.Params = string.Format(tmpParams, EquipID.ToString());
                                                                    rtdAlarms.Params = "{" + tmpParams + "}";


                                                                    sql = _BaseDataService.QueryAlarmDetailByCode(hisCommandStatus.AlarmCode.ToString());
                                                                    dtTemp = _dbTool.GetDataTable(sql);

                                                                    if (dtTemp.Rows.Count > 0)
                                                                    {
                                                                        rtdAlarms.Cause = dtTemp.Rows[0]["AlarmText"].ToString();
                                                                        rtdAlarms.EventTrigger = _configuration[string.Format("RTDAlarm:ByAlarmNumber:{0}", hisCommandStatus.AlarmCode)] is null ? _configuration["RTDAlarm:Condition"] is null ? "eMail:true$SMS:true$repeat:false$hours:0$mints:10" : _configuration["RTDAlarm:Condition"] : _configuration[string.Format("RTDAlarm:ByAlarmNumber:{0}", hisCommandStatus.AlarmCode)];
                                                                    }
                                                                }
                                                                catch (Exception ex)
                                                                {
                                                                    tmpParams = string.Format("");
                                                                    rtdAlarms.Params = tmpParams;
                                                                }
                                                                rtdAlarms.Description = "";
                                                                break;
                                                            default:
                                                                try
                                                                {
                                                                    if (((IList)lsCtrlAlarmCode).IndexOf(hisCommandStatus.AlarmCode) >= 0)
                                                                    {
                                                                        haveAlarmCode = true;
                                                                        rtdAlarms.UnitType = "System";//System
                                                                        rtdAlarms.UnitID = "TSC";//MCS
                                                                        rtdAlarms.Level = "Alarm";//Error
                                                                        rtdAlarms.Code = int.Parse(hisCommandStatus.AlarmCode);//Code
                                                                                                                               //rtdAlarms.Cause = alarmDetail[hisCommandStatus.AlarmCode] is null ? "" : alarmDetail[hisCommandStatus.AlarmCode];
                                                                        rtdAlarms.SubCode = "0";
                                                                        rtdAlarms.Detail = "";
                                                                        rtdAlarms.CommandID = hisCommandStatus.CommandID;//

                                                                        EquipID = !EquipID.Equals("") ? EquipID : _portID.Split("_LP")[0].ToString();
                                                                        //string tmpParams = "{\"EquipID\":\"{0}\"}";
                                                                        tmpParams = string.Format("\"EquipID\":\"{0}\",\"PortID\":\"{1}\"", EquipID, _portID);
                                                                        //rtdAlarms.Params = string.Format(tmpParams, EquipID.ToString());
                                                                        rtdAlarms.Params = "{" + tmpParams + "}";

                                                                        sql = _BaseDataService.QueryAlarmDetailByCode(hisCommandStatus.AlarmCode.ToString());
                                                                        dtTemp = _dbTool.GetDataTable(sql);

                                                                        if (dtTemp.Rows.Count > 0)
                                                                        {
                                                                            rtdAlarms.Cause = dtTemp.Rows[0]["AlarmText"].ToString();
                                                                            rtdAlarms.EventTrigger = _configuration[string.Format("RTDAlarm:ByAlarmNumber:{0}", hisCommandStatus.AlarmCode)] is null ? _configuration["RTDAlarm:Condition"] is null ? "eMail:true$SMS:true$repeat:false$hours:0$mints:10" : _configuration["RTDAlarm:Condition"] : _configuration[string.Format("RTDAlarm:ByAlarmNumber:{0}", hisCommandStatus.AlarmCode)];
                                                                        }
                                                                    }
                                                                    else
                                                                    { rtdAlarms.Params = ""; }
                                                                }
                                                                catch (Exception ex)
                                                                {

                                                                }

                                                                break;
                                                        }

                                                        if (haveAlarmCode)
                                                        {
                                                            sql = _BaseDataService.QueryExistRTDAlarms(string.Format("{0},{1},{2},{3}", rtdAlarms.UnitID, rtdAlarms.Code, rtdAlarms.SubCode, rtdAlarms.CommandID));
                                                            dtTemp = _dbTool.GetDataTable(sql);

                                                            if (dtTemp.Rows.Count > 0)
                                                            {
                                                                if (dtTemp.Rows[0]["detail"].ToString().Equals("SET"))
                                                                {
                                                                    //rtdAlarms.Detail.Equals("SET")
                                                                    if (rtdAlarms.Detail.Equals("SET"))
                                                                        //update last updated
                                                                        sql = _BaseDataService.UpdateRTDAlarms(false, string.Format("{0},{1},{2},{3},{4}", rtdAlarms.UnitID, rtdAlarms.Code, rtdAlarms.SubCode, rtdAlarms.CommandID, rtdAlarms.EventTrigger), "");
                                                                    else
                                                                        sql = _BaseDataService.UpdateRTDAlarms(true, string.Format("{0},{1},{2},{3},{4}", rtdAlarms.UnitID, rtdAlarms.Code, rtdAlarms.SubCode, rtdAlarms.CommandID, rtdAlarms.EventTrigger), dtTemp.Rows[0]["detail"].ToString());
                                                                }
                                                                else
                                                                {
                                                                    //update detail and last updated and change new to 0
                                                                    sql = _BaseDataService.UpdateRTDAlarms(true, string.Format("{0},{1},{2},{3},{4}", rtdAlarms.UnitID, rtdAlarms.Code, rtdAlarms.SubCode, rtdAlarms.CommandID, rtdAlarms.EventTrigger), "");
                                                                }
                                                            }
                                                            else
                                                            {
                                                                List<string> lstTemp = new List<string>();

                                                                lstTemp.Add(rtdAlarms.Code.ToString());
                                                                lstTemp.Add(rtdAlarms.UnitType);
                                                                lstTemp.Add(rtdAlarms.UnitID);
                                                                lstTemp.Add(rtdAlarms.Level);
                                                                lstTemp.Add(rtdAlarms.Cause);
                                                                lstTemp.Add(rtdAlarms.SubCode);
                                                                lstTemp.Add(rtdAlarms.Detail);
                                                                lstTemp.Add(rtdAlarms.CommandID);
                                                                lstTemp.Add(rtdAlarms.Params);
                                                                lstTemp.Add(rtdAlarms.Description);
                                                                lstTemp.Add(rtdAlarms.EventTrigger);

                                                                string[] tmpArray = lstTemp.ToArray();

                                                                sql = _BaseDataService.InsertRTDAlarm(tmpArray);
                                                            }

                                                            if (!rtdAlarms.UnitID.Equals(""))
                                                            {
                                                                if (!sql.Equals(""))
                                                                    _dbTool.SQLExec(sql, out tmpMsg, true);
                                                            }
                                                        }
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    tmpMsg = string.Format("[CommandStatusUpdate][HisCommandAppend] CommandID: {0}, Exception: {1}", CommandID, ex.Message);
                                                    _logger.Debug(tmpMsg);
                                                }
                                                _logger.Debug(string.Format("[CommandStatusUpdate][Flag] {0} ", "END"));


                                                _dbTool.SQLExec(_BaseDataService.UpdateTableWorkInProcessSchHisByCmdId(CommandID), out tmpMsg, true);
                                                Thread.Sleep(5);
                                                if (hisCommandStatus.CommandType.Equals("LOAD") || hisCommandStatus.CommandType.Equals("UNLOAD"))
                                                {
                                                    _dbTool.SQLExec(_BaseDataService.DeleteWorkInProcessSchByCmdIdAndCMDType(CommandID, hisCommandStatus.CommandType, tableOrder), out tmpMsg, true);
                                                    _logger.Debug(string.Format("[CommandStatusUpdate] Delete Workinprocess_sch command[{0}] cmd Type[{1}] cause command process end.", CommandID, hisCommandStatus.CommandType));
                                                }
                                                else
                                                {
                                                    _dbTool.SQLExec(_BaseDataService.DeleteWorkInProcessSchByCmdId(CommandID, tableOrder), out tmpMsg, true);
                                                    _logger.Debug(string.Format("[CommandStatusUpdate] Delete Workinprocess_sch command[{0}] cmd Type[{1}] cause command process end.", CommandID, hisCommandStatus.CommandType));
                                                }
                                            }
                                            else
                                            {
                                                tmpMsg = string.Format("[CommandStatusUpdate: command id is {0}, Status is {1} ]", CommandID, tmpObject.Status);
                                                _logger.Debug(tmpMsg);
                                            }
                                        }
                                    }
                                }
                                catch(Exception ex) {
                                    tmpMsg = string.Format("[CommandStatusUpdate: command id is {0}, Exception is {1} ]", CommandID, ex.Message);
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
                                if(_functionService.CreateTransferCommandByTransferList(_dbTool, _configuration, _logger, evtObject3, out arrayOfCmds))
                                {
                                    LotID = evtObject3.LotID;
                                    tmpMsg = string.Format("[Debug Message][{0}][{1}]", oEventQ.EventName, LotID);
                                    _logger.Debug(tmpMsg);

                                    if (!LotID.Equals(""))
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

                                string ctrlObject = string.Format("AutoCheckEquipmentStatus{0}", LotID);
                                //10秒內, 不跑讓相同lotid 的第2支Thread執行
                                if (_threadControll.ContainsKey(ctrlObject))
                                {
                                    if (_functionService.ThreadLimitTraffice(_threadControll, ctrlObject, 10, "ss", ">"))
                                    {
                                        _threadControll[ctrlObject] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
                                            _threadControll.Add(ctrlObject, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                        else
                                            break;
                                    }

                                    if (_DebugMode)
                                    {
                                        tmpMsg = string.Format("[Debug Message][{0}] {1} / {2} ", oEventQ.EventName, LotID, evtObject2.EquipmentID);
                                        _logger.Debug(tmpMsg);
                                    }
                                }

                                keyBuildCommand = true;
                                break;
                            case "AbnormalyEquipmentStatus":
                                keyBuildCommand = true;

                                evtObject2 = (NormalTransferModel)oEventQ.EventObject;

                                string keyEventName = string.Format("{0}{1}", oEventQ.EventName, evtObject2.EquipmentID);
                                if (_threadControll.ContainsKey(keyEventName))
                                {
                                    if (_functionService.ThreadLimitTraffice(_threadControll, keyEventName, 10, "ss", ">"))
                                    {
                                        _threadControll[keyEventName] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                                        if (_DebugMode)
                                        {
                                            tmpMsg = string.Format("[{0}][ThreadLimitTraffice][{1}] {2}/{3}", oEventQ.EventName, keyEventName, evtObject2.EquipmentID, evtObject2.LotID);
                                            _logger.Debug(tmpMsg);
                                        }

                                    } else {
                                        if (_DebugMode)
                                        {
                                            tmpMsg = string.Format("[{0}][Exist][{1}] {2}/{3}", oEventQ.EventName, keyEventName, evtObject2.EquipmentID, evtObject2.LotID);
                                            _logger.Debug(tmpMsg);
                                        }

                                        break; 
                                    }
                                }
                                else
                                {
                                    lock (_threadControll)
                                    {
                                        if (!_threadControll.ContainsKey(keyEventName))
                                            _threadControll.Add(keyEventName, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                        else
                                        {
                                            if (_DebugMode)
                                            {
                                                tmpMsg = string.Format("[{0}][Not Exist][{1}] {2}/{3}", oEventQ.EventName, keyEventName, evtObject2.EquipmentID, evtObject2.LotID);
                                                _logger.Debug(tmpMsg);
                                            }
                                            break;
                                        }
                                    }
                                }

                                //lstNormalTransfer.Sort();//依寫入順序排序
                                //foreach (NormalTransferModel NormalTransfer in lstNormalTransfer)
                                //{
                                    //oEventQ.EventObject = NormalTransfer;
                                    //evtObject2 = NormalTransfer;
                                    //LotID = NormalTransfer.LotID;

                                    //evtObject2 = (NormalTransferModel)oEventQ.EventObject;
                                    LotID = evtObject2.LotID;

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

                                    if (_functionService.CreateTransferCommandByPortModel(_dbTool, _configuration, _logger, _DebugMode, evtObject2.EquipmentID, evtObject2.PortModel, oEventQ, out arrayOfCmds))
                                    { }
                                    else
                                    { }

                                    if (_DebugMode)
                                    {
                                        tmpMsg = string.Format("[Debug Message OUT][{0}] {1} ", oEventQ.EventName, LotID);
                                        _logger.Debug(tmpMsg);
                                    }
                                //}

                                break;
                            case "TSCAlarmCollect":
                                try
                                {
                                    evtObject4 = (TSCAlarmCollect)oEventQ.EventObject;

                                    string sqlTSCAlarm = "";

                                    lsCtrlAlarmCode = _configuration["RTDAlarm:CtrlAlarmCode"] is not null ? _configuration["RTDAlarm:CtrlAlarmCode"].Split(',') : null;

                                    tmpMsg = string.Format("[{0}] Receive: {1} ", oEventQ.EventName, evtObject4.ALSV);
                                    _logger.Debug(tmpMsg);

                                    sql = _BaseDataService.InsertHisTSCAlarm(evtObject4);
                                    

                                    if (!evtObject4.ALSV.Equals(""))
                                        _dbTool.SQLExec(sql, out tmpMsg, true);

                                    RTDAlarms rtdAlarms = new RTDAlarms();
                                    rtdAlarms.UnitType = evtObject4.UnitType;
                                    rtdAlarms.UnitID = evtObject4.UnitID;
                                    rtdAlarms.Level = evtObject4.Level;
                                    rtdAlarms.Code = evtObject4.ALID;
                                    rtdAlarms.Cause = evtObject4.ALTX;
                                    rtdAlarms.SubCode = evtObject4.SubCode;
                                    rtdAlarms.Detail = evtObject4.ALType;
                                    rtdAlarms.CommandID = evtObject4.UnitID;
                                    rtdAlarms.Params = "";
                                    rtdAlarms.Description = "";

                                    //High & Full Alarm
                                    switch (evtObject4.ALID)
                                    {
                                        case 25002:
                                            //High Alarm
                                            //Erack wafer level High
                                            rtdAlarms.Cause = "Erack wafer level High";
                                            rtdAlarms.Params = "";
                                            rtdAlarms.EventTrigger = _configuration["RTDAlarm:Condition"] is null ? "eMail:true$SMS:true$repeat:false$hours:0$mints:10" : _configuration["RTDAlarm:Condition"];
                                            if (((IList)lsCtrlAlarmCode).IndexOf(evtObject4.ALID.ToString()) >= 0)
                                            {
                                                sql = _BaseDataService.QueryAlarmDetailByCode(evtObject4.ALID.ToString());
                                                dtTemp = _dbTool.GetDataTable(sql);

                                                rtdAlarms.EventTrigger = _configuration[string.Format("RTDAlarm:ByAlarmNumber:{0}", evtObject4.ALID.ToString())] is null ? _configuration["RTDAlarm:Condition"] is null ? "eMail:true$SMS:true$repeat:false$hours:0$mints:10" : _configuration["RTDAlarm:Condition"] : _configuration[string.Format("RTDAlarm:ByAlarmNumber:{0}", evtObject4.ALID.ToString())];
                                            }
                                            else
                                            { rtdAlarms.Params = ""; }
                                            break;
                                        case 20053:
                                            //Full Alarm
                                            //Erack wafer level Full
                                            rtdAlarms.Cause = "Erack wafer level Full";
                                            rtdAlarms.Params = "";
                                            if (((IList)lsCtrlAlarmCode).IndexOf(evtObject4.ALID.ToString()) >= 0)
                                            {
                                                sql = _BaseDataService.QueryAlarmDetailByCode(evtObject4.ALID.ToString());
                                                dtTemp = _dbTool.GetDataTable(sql);

                                                rtdAlarms.EventTrigger = _configuration[string.Format("RTDAlarm:ByAlarmNumber:{0}", evtObject4.ALID.ToString())] is null ? _configuration["RTDAlarm:Condition"] is null ? "eMail:true$SMS:true$repeat:false$hours:0$mints:10" : _configuration["RTDAlarm:Condition"] : _configuration[string.Format("RTDAlarm:ByAlarmNumber:{0}", evtObject4.ALID.ToString())];
                                            }
                                            break;
                                        default:
                                            //Do Nothing
                                            //Record to Alarms Dashboard
                                            if (((IList)lsCtrlAlarmCode).IndexOf(evtObject4.ALID.ToString()) >= 0)
                                            {
                                                sql = _BaseDataService.QueryAlarmDetailByCode(evtObject4.ALID.ToString());
                                                dtTemp = _dbTool.GetDataTable(sql);

                                                rtdAlarms.Cause = dtTemp.Rows[0]["AlarmText"].ToString();
                                                rtdAlarms.Params = "";
                                                rtdAlarms.EventTrigger = _configuration[string.Format("RTDAlarm:ByAlarmNumber:{0}", evtObject4.ALID.ToString())] is null ? _configuration["RTDAlarm:Condition"] is null ? "eMail:true$SMS:true$repeat:false$hours:0$mints:10" : _configuration["RTDAlarm:Condition"] : _configuration[string.Format("RTDAlarm:ByAlarmNumber:{0}", evtObject4.ALID.ToString())];
                                            }
                                            else
                                            { rtdAlarms.Params = ""; }
                                            break;
                                    }

                                    rtdAlarms.Description = "";

                                    sql = _BaseDataService.QueryExistRTDAlarms(string.Format("{0},{1},{2},{3}", rtdAlarms.UnitID, rtdAlarms.Code, rtdAlarms.SubCode, rtdAlarms.CommandID));
                                    dtTemp = _dbTool.GetDataTable(sql);

                                    if(dtTemp.Rows.Count > 0)
                                    {
                                        if (dtTemp.Rows[0]["detail"].ToString().Equals("SET"))
                                        {
                                            //rtdAlarms.Detail.Equals("SET")
                                            if (rtdAlarms.Detail.Equals("SET"))
                                                //update last updated
                                                sqlTSCAlarm = _BaseDataService.UpdateRTDAlarms(false, string.Format("{0},{1},{2},{3},{4}", rtdAlarms.UnitID, rtdAlarms.Code, rtdAlarms.SubCode, rtdAlarms.CommandID, rtdAlarms.EventTrigger), "");
                                            else
                                                sqlTSCAlarm = _BaseDataService.UpdateRTDAlarms(true, string.Format("{0},{1},{2},{3},{4}", rtdAlarms.UnitID, rtdAlarms.Code, rtdAlarms.SubCode, rtdAlarms.CommandID, rtdAlarms.EventTrigger), dtTemp.Rows[0]["detail"].ToString());
                                        }
                                        else
                                        {
                                            //update detail and last updated and change new to 0
                                            sqlTSCAlarm = _BaseDataService.UpdateRTDAlarms(true, string.Format("{0},{1},{2},{3}", rtdAlarms.UnitID, rtdAlarms.Code, rtdAlarms.SubCode, rtdAlarms.CommandID),"");
                                        }
                                    }
                                    else
                                    {
                                        List<string> lstTemp = new List<string>();

                                        lstTemp.Add(rtdAlarms.Code.ToString());
                                        lstTemp.Add(rtdAlarms.UnitType);
                                        lstTemp.Add(rtdAlarms.UnitID);
                                        lstTemp.Add(rtdAlarms.Level);
                                        lstTemp.Add(rtdAlarms.Cause);
                                        lstTemp.Add(rtdAlarms.SubCode);
                                        lstTemp.Add(rtdAlarms.Detail);
                                        lstTemp.Add(rtdAlarms.CommandID);
                                        lstTemp.Add(rtdAlarms.Params);
                                        lstTemp.Add(rtdAlarms.Description);
                                        lstTemp.Add(rtdAlarms.EventTrigger);

                                        string[] tmpArray = lstTemp.ToArray();

                                        if (!rtdAlarms.UnitID.Equals(""))
                                        {
                                            sqlTSCAlarm = _BaseDataService.InsertRTDAlarm(tmpArray);
                                        }
                                    }

                                    //tsc alarm and order is Initial, auto delete command.
                                    try 
                                    {
                                        sql = _BaseDataService.QueryInitWorkInProcessSchByCmdId(rtdAlarms.CommandID, tableOrder);
                                        dtTemp = _dbTool.GetDataTable(sql);
                                        if (dtTemp.Rows.Count > 0)
                                        {
                                            sql = _BaseDataService.DeleteWorkInProcessSchByCmdId(rtdAlarms.CommandID, tableOrder);
                                            _dbTool.SQLExec(sql, out tmpMsg, true);
                                            tmpMsg = string.Format("[{0}][TscAlarmDeleteCommand] Delete Workinprocess_sch command[{1}] cause tsc report alarms.", oEventQ.EventName, rtdAlarms.CommandID);
                                            _logger.Debug(string.Format(tmpMsg));
                                        }
                                    }
                                    catch(Exception ex) 
                                    {
                                        tmpMsg = string.Format("[{0}][TscAlarmDeleteCommand] Exception: {1} ", oEventQ.EventName, ex.Message);
                                        _logger.Debug(tmpMsg);
                                    }

                                    if(!sqlTSCAlarm.Equals(""))
                                        _dbTool.SQLExec(sqlTSCAlarm, out tmpMsg, true);
                                }
                                catch (Exception ex)
                                {
                                    tmpMsg = string.Format("[{0}] Exception: {1} ", oEventQ.EventName, ex.Message);
                                    _logger.Debug(tmpMsg);
                                }

                                break;

                            case "ExcuteNearCompleted":
                                AEIPortInfo _nearCompletedInfo = (AEIPortInfo)oEventQ.EventObject;

                                Boolean _disabled1 = false;

                                //CarrierID = evtObject.CarrierID.Trim();
                                if (_DebugMode)
                                {
                                    tmpMsg = string.Format("[Debug Message][{0}] {1} / {2}", oEventQ.EventName, _nearCompletedInfo.PortID, _nearCompletedInfo.PortTransferState);
                                    _logger.Debug(tmpMsg);
                                }

                                try
                                {
                                    sql = string.Format(_BaseDataService.SelectTableEQP_Port_SetByPortId(_nearCompletedInfo.PortID));
                                    dtTemp = _dbTool.GetDataTable(sql);
                                    if (dtTemp.Rows.Count > 0)
                                    {
                                        _disabled1 = dtTemp.Rows[0]["DISABLE"].ToString().Equals("1") ? true : false;
                                    }
                                }
                                catch (Exception ex) { }
                                //_functionService.EquipmentPortStatusUpdate(_dbTool, _configuration, _aeiPortInfo, _logger);
                                NormalTransferModel eventNearCompleted = new NormalTransferModel();

                                if (_nearCompletedInfo.PortTransferState.Equals(3))
                                {
                                    bool bDoUnlock = true;
                                    if (_nearCompletedInfo.PortTransferState.Equals(4))
                                    {
                                        //"Position": "202311161021001"
                                        //2023/11/15 Add 狀態切換為Ready to Load時, Order裡仍有Load指令時, 不解鎖
                                        dtTemp = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSchByPortId(_nearCompletedInfo.PortID, tableOrder));

                                        if (dtTemp.Rows.Count > 0)
                                            bDoUnlock = false;
                                        else
                                            bDoUnlock = true;
                                    }

                                    if (bDoUnlock)
                                    {
                                        //20230728 Add, 防止重複派送指令 收到run/ready to load時才解鎖
                                        _dbTool.SQLExec(_BaseDataService.LockEquipPortByPortId(_nearCompletedInfo.PortID, false), out tmpMsg, true);

                                        if (_DebugMode)
                                        {
                                            tmpMsg = string.Format("[Debug Message][{0}] Port [{1}] has been unlock / State[{2}]", oEventQ.EventName, _nearCompletedInfo.PortID, _nearCompletedInfo.PortTransferState);
                                            _logger.Debug(tmpMsg);
                                        }
                                    }
                                }

                                if (_nearCompletedInfo.PortTransferState.Equals(3))
                                {
                                    DataTable dtTemp2 = null;
                                    string tmpPortSeq = "0";
                                    eventNearCompleted.CarrierID = _nearCompletedInfo.CarrierID;
                                    //SelectTableEQP_Port_SetByPortId
                                    dtTemp = _dbTool.GetDataTable(_BaseDataService.SelectTableEQP_Port_SetByPortId(_nearCompletedInfo.PortID));

                                    if (dtTemp.Rows.Count > 0)
                                    {
                                        eventNearCompleted.EquipmentID = dtTemp.Rows[0]["EQUIPID"].ToString();
                                        eventNearCompleted.PortModel = dtTemp.Rows[0]["PORT_MODEL"].ToString();
                                        tmpPortSeq = dtTemp.Rows[0]["PORT_SEQ"].ToString();

                                        dtTemp2 = _dbTool.GetDataTable(_BaseDataService.SelectTableEquipmentPortsInfoByEquipId(eventNearCompleted.EquipmentID));
                                        if (dtTemp2.Rows.Count > 0)
                                        {
                                            if (dtTemp2.Rows[0]["Expired"].ToString().Equals("1"))
                                            {
                                                if (dtTemp2.Rows[0]["manualmode"].ToString().Equals("1"))
                                                    break;
                                            }
                                            else
                                            {
                                                //仍然要執行Unload
                                                //if (dtTemp2.Rows[0]["manualmode"].ToString().Equals("1"))
                                                //    break;
                                            }
                                        }
                                    }
                                    else
                                        eventNearCompleted.EquipmentID = "";

                                    if (!_nearCompletedInfo.CarrierID.Trim().Equals(""))
                                    {
                                        dtTemp2 = _dbTool.GetDataTable(_BaseDataService.SelectTableEquipmentPortsInfoByEquipId(eventNearCompleted.EquipmentID));
                                        if (dtTemp2.Rows.Count > 0)
                                        {
                                            if (dtTemp2.Rows[0]["Expired"].ToString().Equals("1"))
                                            {
                                                if (dtTemp2.Rows[0]["manualmode"].ToString().Equals("1"))
                                                    break;
                                            }
                                            else
                                            {
                                                //仍然要執行Unload
                                                //if (dtTemp2.Rows[0]["manualmode"].ToString().Equals("1"))
                                                //    break;
                                            }
                                        }

                                        string _tmpCarrier = "";
                                        try
                                        {
                                            //"Position": "202310311415001"
                                            //Check RTD Carrier ID in loadport
                                            dtTemp2 = _dbTool.GetDataTable(_BaseDataService.QueryCarrierOnPort(_nearCompletedInfo.PortID));
                                            if (dtTemp2.Rows.Count > 0)
                                            {
                                                _tmpCarrier = dtTemp2.Rows[0]["carrier_id"] is null ? "" : dtTemp2.Rows[0]["carrier_id"].ToString();
                                            }

                                            if (!_nearCompletedInfo.CarrierID.Trim().Equals(_tmpCarrier))
                                            {
                                                if (_tmpCarrier.Trim().Equals(""))
                                                {
                                                    tmpMsg = string.Format("[{0}][Sync] Carrier [{1}] Locate Sync from loadport [{2}].", oEventQ.EventName, _nearCompletedInfo.CarrierID, _nearCompletedInfo.PortID);
                                                    _logger.Debug(tmpMsg);

                                                    _dbTool.SQLExec(_BaseDataService.UpdateLastLotIDtoEQPPortSet(eventNearCompleted.EquipmentID, tmpPortSeq, _nearCompletedInfo.CarrierID), out tmpMsg, true);
                                                    if (!tmpMsg.Equals(""))
                                                        _logger.Debug(tmpMsg);
                                                }
                                                else
                                                {
                                                    tmpMsg = string.Format("[{0}] carrier id is not same as rtd and loadport [{1}] [{2}/{3}]", oEventQ.EventName, _nearCompletedInfo.PortID, _tmpCarrier, _nearCompletedInfo.CarrierID);
                                                    _logger.Debug(tmpMsg);

                                                    if (!_nearCompletedInfo.CarrierID.Trim().Equals(""))
                                                    {
                                                        _dbTool.SQLExec(_BaseDataService.UpdateLastLotIDtoEQPPortSet(eventNearCompleted.EquipmentID, tmpPortSeq, _nearCompletedInfo.CarrierID), out tmpMsg, true);
                                                        if (!tmpMsg.Equals(""))
                                                            _logger.Debug(tmpMsg);
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            tmpMsg = string.Format("[Exception][{0}] Exception: [{1}]/[{2}]", oEventQ.EventName, _nearCompletedInfo.CarrierID, _tmpCarrier);
                                            _logger.Debug(tmpMsg);
                                        }
                                    }

                                    eventNearCompleted.LotID = _nearCompletedInfo.LotID;
                                    LotID = _nearCompletedInfo.LotID;


                                    oEventQ.EventObject = eventNearCompleted;

                                    if (_functionService.CreateTransferCommandByPortModel(_dbTool, _configuration, _logger, _DebugMode, eventNearCompleted.EquipmentID, eventNearCompleted.PortModel, oEventQ, out arrayOfCmds))
                                    { }
                                    else
                                    { }
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
                                        lastRunCheckQueryAvailableTester = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                        _threadControll[currentKey] = lastRunCheckQueryAvailableTester;
                                    }
                                    else
                                        doFunc = false;
                                    
                                }
                                else
                                {
                                    lastRunCheckQueryAvailableTester = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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

                            dt = _dbTool.GetDataTable(_BaseDataService.SelectTableWorkInProcessSchUnlock(tableOrder));
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

            DBissue:

            _dbTool.DisConnectDB(out tmpMsg);
            _dbTool = null;
        }

        static void scheduleProcess(object parms)
        {
            string curPorcessName = "scheduleProcess";
            string currentThreadNo = "";
            string tmpThreadNo = "ThreadPoolNo_{0}";
            bool _DebugMode = true;
            //To Be
            object[] obj = (object[])parms;
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
            string tableOrder = "";
            string _keyOfEnv = "";
            string _keyRTDEnv = "";
            bool _NoConnect = true;
            int _retrytime = 0;

            IFunctionService _functionService = (FunctionService)obj[5];
            try
            {
#if DEBUG
                tmpMsg = string.Format("{2}, Function[{0}], Thread ID [{1}]", curPorcessName, Thread.CurrentThread.ManagedThreadId, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                Console.WriteLine(tmpMsg);
#else
#endif

                lstDB = (List<string>)obj[1];
                //_dbTool = new DBTool(lstDB[1], lstDB[2], lstDB[3], out tmpMsg);
                //_dbTool._dblogger = _logger;

                try
                {
                    _NoConnect = true;
                    while (_NoConnect)
                    {
                        _dbTool = new DBTool(lstDB[1], lstDB[2], lstDB[3], out tmpMsg);
                        if (!tmpMsg.Equals(""))
                        {
                            _retrytime++;
                            _logger.Info(string.Format("DB connect fail. retry {0} [{1}]", _retrytime, tmpMsg));
                            tmpMsg = "";
                        }
                        else
                        {
                            _NoConnect = false;
                        }

                        if (_retrytime > 3)
                            break;

                        if(_NoConnect)
                            Thread.Sleep(300);
                    }

                    if (_NoConnect)
                        goto DBissue;

                    _dbTool._dblogger = _logger;
                }
                catch (Exception ex)
                {
                    _logger.Info(string.Format("DB Initial fail. {0}", ex.Message));
                }

                _keyRTDEnv = _configuration["RTDEnvironment:type"];
                _keyOfEnv = string.Format("RTDEnvironment:commandsTable:{0}", _keyRTDEnv);
                //"RTDEnvironment:commandsTable:PROD"
                tableOrder = _configuration[_keyOfEnv] is null ? "workinprocess_sch" : _configuration[_keyOfEnv];

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

                    bool bRun = true;
                    /*
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

                    */
                    //if (_threadControll["ipass"].Equals("0"))
                    if (bRun)
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
                                        DateTime dtCurrent = DateTime.Now;
                                        string vlastDatetime = DateTime.Now.ToString("yyyy-MM-dd HH");
                                        if (_threadControll.ContainsKey(timerID))
                                        {
                                            lock (_threadControll)
                                            {
                                                _threadControll.TryGetValue(timerID, out vlastDatetime);
                                            }

                                            if (!DateTime.Now.ToString("yyyy-MM-dd HH").Equals(vlastDatetime))
                                            {
                                                //每小時重設一次command_streamCode  -- 0, 99999
                                                OracleSequence.SequenceReset(_dbTool, "command_streamCode");
                                                //每小時重設一次uid_streamCode  -- 0, 9999999
                                                OracleSequence.SequenceReset(_dbTool, "uid_streamCode");
                                                lock (_threadControll)
                                                {
                                                    _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH");
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
                                        DateTime dtCurrent = DateTime.Now;
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
                                                    _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
                                            _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
                                        DateTime dtCurrent = DateTime.Now;
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
                                                    _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
                                                if (_functionService.UpdateEquipmentAssociateToReady(_dbTool, _eventQueue))
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
                                                if (_functionService.AutoCheckEquipmentStatus(_dbTool, _eventQueue))
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
                                                if (_functionService.AbnormalyEquipmentStatus(_dbTool, _configuration, _logger, _DebugMode, _eventQueue, out lstNormalTransfer))
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
                                            _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
                                        DateTime dtCurrent = DateTime.Now;
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
                                                    _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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

                                            //HeartBeat
                                            if (_functionService.Heartbeat(_dbTool, _configuration, _logger)) { }

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
                                            _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
                                        DateTime dtCurrent = DateTime.Now;
                                        string vlastDatetime = dtCurrent.ToString("yyyy-MM-dd HH:mm:ss");
                                        if (_threadControll.ContainsKey(timerID))
                                        {
                                            _threadControll.TryGetValue(timerID, out vlastDatetime);

                                            if (_functionService.TimerTool(timeUnit, vlastDatetime) >= iTimer01)
                                            {
                                                bDoLogic = true;
                                                lock (_threadControll)
                                                {
                                                    _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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

                                            //HeartBeat
                                            //if (_functionService.Heartbeat(_dbTool, _configuration, _logger)) { }

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
                                            _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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

                        //////Auto Check Every 45 Sec
                        scheduleName = "AutoCheckEvery45Sec";
                        timerID = "last15ProcessTime";
                        iTimer01 = 45;
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
                                        DateTime dtCurrent = DateTime.Now;
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
                                                    _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
                                        //縮短為45 sec 執行一次Lot Equipment Associates
                                        try
                                        {
                                            if (_DebugMode)
                                            {
                                                tmpMsg = string.Format("[scheduleName][{0}].Flag1-3", scheduleName);
                                                //_logger.Debug(tmpMsg);
                                            }

                                            tmpMsg = "";
                                            if (_functionService.CheckLotEquipmentAssociate(_dbTool, _eventQueue))
                                            {
                                                //Console.WriteLine(String.Format("Check Lot Info completed."));
                                            }
                                            else
                                            {
                                                //Console.WriteLine(String.Format("Check lot info failed."));
                                            }

                                            //Auto unlock equipment port
                                            try
                                            {

                                                tmpMsg = "";
                                                if (_functionService.AutounlockportWhenNoOrder(_dbTool, _configuration, _logger))
                                                {

                                                }
                                                else
                                                {

                                                }
                                            }
                                            catch (Exception ex)
                                            { }
                                        }
                                        catch (Exception ex)
                                        {
                                            tmpMsg = string.Format("Listening exception [CheckLotEquipmentAssociate: {0}]", ex.Message);
                                            _logger.Debug(tmpMsg);
                                        }

                                        bDoLogic = false;

                                        lock (_threadControll)
                                        {
                                            _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
                                        DateTime dtCurrent = DateTime.Now;
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
                                                    _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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

                                        ////PreDispatchToErack.
                                        //tmpMsg = "";
                                        //if (_functionService.PreDispatchToErack(_dbTool, _configuration, _eventQueue, _logger))
                                        //{
                                        //    //Do Nothing
                                        //}

                                        //TransferCarrierToSideWH
                                        tmpMsg = "";
                                       if (_functionService.TransferCarrierToSideWH(_dbTool, _configuration, _eventQueue, _logger))
                                        {
                                            //Do Nothing
                                        }

                                        try
                                        {
                                            tmpMsg = "";
                                            string _args = "";

                                            DateTime dtCurrent = DateTime.Now;
                                            DateTime tmpDT = DateTime.Now;
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
                                                            _logger.Debug(string.Format("{0}--reserveend--{1}-------------", dtCurrent, drTemp["equipid"].ToString()));

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
                                                            _logger.Debug(string.Format("{0}--reservestart--{1}-------------", dtCurrent, drTemp["equipid"].ToString()));

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

                                        try
                                        {
                                            //Positition:202309221312001
                                            tmpMsg = "";
                                            string _tmpPortID = "_";
                                            string _tmpPortType = "_";
                                            bool _isLock99 = false;

                                            //Order overtime and state is Initial
                                            int _overtimeforOrder = 15; //minutes
                                            _overtimeforOrder = _configuration["OrderSetting:CommandOverTime"] is null? 15 : int.Parse(_configuration["OrderSetting:CommandOverTime"]);
                                            sql = _BaseDataService.QueryOrderWhenOvertime(_overtimeforOrder.ToString(), tableOrder);
                                            dt = _dbTool.GetDataTable(sql);
                                            if (dt.Rows.Count > 0)
                                            {
                                                foreach (DataRow drTemp in dt.Rows)
                                                {
                                                    try
                                                    {
                                                        _tmpPortType = drTemp["cmd_type"] is null ? "Null" : drTemp["cmd_type"].ToString();

                                                        if(_tmpPortType.Equals("LOAD") || _tmpPortType.Equals("UNLOAD"))
                                                        {
                                                            _isLock99 = true;

                                                            if (_tmpPortType.Equals("LOAD"))
                                                                _tmpPortID = drTemp["DEST"] is null ? "Null" : drTemp["DEST"].ToString();
                                                            else if (_tmpPortType.Equals("UNLOAD"))
                                                                _tmpPortID = drTemp["SOURCE"] is null ? "Null" : drTemp["SOURCE"].ToString();
                                                            else
                                                            {
                                                                tmpMsg = string.Format("[AutoDeleteCommand][{0}][{1}]", "PortTypeIssue", _tmpPortType.ToString());
                                                                _logger.Debug(tmpMsg);
                                                            }
                                                        }

                                                        sql = _BaseDataService.DeleteWorkInProcessSchByCmdId(drTemp["cmd_id"].ToString(), tableOrder);

                                                        _dbTool.SQLExec(sql, out tmpMsg, true);
                                                        if (!tmpMsg.Equals(""))
                                                        {
                                                            _logger.Debug(tmpMsg);
                                                        }
                                                        else
                                                        {
                                                            tmpMsg = string.Format("[AutoDeleteCommand][{0}][{1}]", drTemp["cmd_id"].ToString(), drTemp["cmd_current_state"].ToString());
                                                            _logger.Debug(tmpMsg);

                                                            if (_isLock99)
                                                            {
                                                                sql = _BaseDataService.LockEquipPortByPortId(_tmpPortID, false);
                                                                _dbTool.SQLExec(sql, out tmpMsg, true);
                                                            }
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
                                            tmpMsg = string.Format("Listening exception [QueryOrderWhenOvertime: {0}]", ex.Message);
                                            _logger.Debug(tmpMsg);
                                        }

                                        try
                                        {
                                            //Auto reset equipment port lock when no command every 10 mints
                                            tmpMsg = "";
                                            string _tmpEquipID = "_";
                                            string _tmpPortID = "_";
                                            string _tmplastModifyDT = "_";
                                            bool _isLock99 = false;
                                            bool _noCommand = false;

                                            int _overtimeforOrder = 10; //minutes
                                            sql = _BaseDataService.QueryEquipPortisLock();
                                            dt = _dbTool.GetDataTable(sql);
                                            if (dt.Rows.Count > 0)
                                            {
                                                foreach (DataRow drTemp in dt.Rows)
                                                {
                                                    try
                                                    {
                                                        _tmpEquipID = drTemp["equipid"] is null ? "Null" : drTemp["equipid"].ToString();
                                                        _tmpPortID = drTemp["port_id"] is null ? "Null" : drTemp["port_id"].ToString();
                                                        _tmplastModifyDT = drTemp["lastModify_dt"] is null ? "Null" : drTemp["lastModify_dt"].ToString();

                                                        DateTime dtCurrent = DateTime.Now;
                                                        string vlastDatetime = dtCurrent.ToString("yyyy-MM-dd HH:mm:ss");
                                                        
                                                        if (_functionService.TimerTool(timeUnit, _tmplastModifyDT) >= _overtimeforOrder)
                                                        {
                                                            sql = string.Format(_BaseDataService.SelectTableWorkInProcessSchByEquipPort(_tmpEquipID, _tmpPortID, tableOrder));
                                                            dtTemp = _dbTool.GetDataTable(sql);

                                                            if (dtTemp.Rows.Count <= 0)
                                                            {
                                                                sql = _BaseDataService.LockEquipPortByPortId(_tmpPortID, false);
                                                                _dbTool.SQLExec(sql, out tmpMsg, true);

                                                                tmpMsg = string.Format("[Auto unlock when no commands and overtime 10 {0}][{0}][{1}]", timeUnit, _tmpPortID, _tmplastModifyDT);
                                                                _logger.Info(tmpMsg);
                                                            }
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
                                            tmpMsg = string.Format("Listening exception [QueryOrderWhenOvertime: {0}]", ex.Message);
                                            _logger.Debug(tmpMsg);
                                        }

                                        try
                                        {
                                            //Auto reset lot rtd state is PROC when no command every 10 mints
                                            tmpMsg = "";
                                            string _tmplotID = "_";
                                            string _tmplastModifyDT = "_";
                                            bool _isLock99 = false;
                                            bool _noCommand = false;

                                            int _overtimeforOrder = 10; //minutes
                                            sql = _BaseDataService.QueryProcLotInfo();
                                            dt = _dbTool.GetDataTable(sql);
                                            if (dt.Rows.Count > 0)
                                            {
                                                foreach (DataRow drTemp in dt.Rows)
                                                {
                                                    try
                                                    {
                                                        _tmplotID = drTemp["lotid"] is null ? "Null" : drTemp["lotid"].ToString();
                                                        _tmplastModifyDT = drTemp["lastModify_dt"] is null ? "Null" : drTemp["lastModify_dt"].ToString();

                                                        DateTime dtCurrent = DateTime.Now;
                                                        string vlastDatetime = dtCurrent.ToString("yyyy-MM-dd HH:mm:ss");

                                                        if (_functionService.TimerTool(timeUnit, _tmplastModifyDT) >= _overtimeforOrder)
                                                        {
                                                            sql = string.Format(_BaseDataService.SelectTableWorkInProcessSchByLotId(_tmplotID, tableOrder));
                                                            dtTemp = _dbTool.GetDataTable(sql);

                                                            if (dtTemp.Rows.Count <= 0)
                                                            {
                                                                sql = _BaseDataService.UpdateTableLotInfoReset(_tmplotID);
                                                                _dbTool.SQLExec(sql, out tmpMsg, true);

                                                                tmpMsg = string.Format("[Auto reset lot state when no commands and overtime 10 {0}][{0}][{1}]", timeUnit, _tmplotID, _tmplastModifyDT);
                                                                _logger.Info(tmpMsg);
                                                            }
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
                                            tmpMsg = string.Format("Listening exception [QueryOrderWhenOvertime: {0}]", ex.Message);
                                            _logger.Debug(tmpMsg);
                                        }

                                        if (_functionService.AutoRemoveCommandWhenSentMCSFailed(_dbTool, _configuration, _logger, out tmpMsg))
                                        {
                                            //Do Nothing.
                                        }

                                        bDoLogic = false;

                                        lock (_threadControll)
                                        {
                                            _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
                        timeUnit = global.ChkLotInfo.TimeUnit is not null ? global.ChkLotInfo.TimeUnit : "3";
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
                                        DateTime dtCurrent = DateTime.Now;
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
                                                    _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
                                            _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
                                        DateTime dtCurrent = DateTime.Now;
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
                                                    _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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

                                        try
                                        {
                                            tmpMsg = "";

                                            if (_functionService.AutoSentInfoUpdateForSTK(_dbTool, _configuration, _logger, out tmpMsg))
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
                        iTimer01 = 1; // 5;
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
                                        DateTime dtCurrent = DateTime.Now;
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
                                                    _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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

                                        //PreDispatchToErack.
                                        tmpMsg = "";
                                        if (_functionService.PreDispatchToErack(_dbTool, _configuration, _eventQueue, _logger))
                                        {
                                            //Do Nothing
                                        }

                                        //與上位同步當前機台狀態 (超過5分鐘, 機台狀態不一致時同步)
                                        /** mark Sync RTS EQP Status change to sync time at RTD Start
                                        if (_functionService.SyncEQPStatus(_dbTool, _logger))
                                        {
                                            //Do Nothing.
                                        }
                                        */

                                        try
                                        {
                                            //Positition:202309221312002
                                            tmpMsg = "";

                                            //AutoResetCarrierReserveState
                                            //Condition: Carrier is Online and Not in Order 
                                            sql = _BaseDataService.AutoResetCarrierReserveState(tableOrder);

                                            _dbTool.SQLExec(sql, out tmpMsg, true);
                                            if (!tmpMsg.Equals(""))
                                            {
                                                _logger.Debug(tmpMsg);
                                            }
                                            else
                                            {
                                                tmpMsg = string.Format("[AutoResetCarrierReserveState] Done.");
                                                _logger.Debug(tmpMsg);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            tmpMsg = string.Format("Listening exception [QueryOrderWhenOvertime: {0}]", ex.Message);
                                            _logger.Debug(tmpMsg);
                                        }

                                        try
                                        {
                                            tmpMsg = "";

                                            //AutoRelease Carrier when hold time overtime 30 mints
                                            sql = _BaseDataService.GetAllOfSysHoldCarrier();

                                            dt = _dbTool.GetDataTable(sql);

                                            if (dt.Rows.Count > 0)
                                            {
                                                string carrierID = "";
                                                int _tmpReleaseSyshold = _configuration["ReflushTime:ReleaseSyshold:Time"] is not null ? int.Parse(_configuration["ReflushTime:ReleaseSyshold:Time"]) : 30;
                                                string _tmpUnit = _configuration["ReflushTime:ReleaseSyshold:TimeUnit"] is not null ? _configuration["ReflushTime:ReleaseSyshold:TimeUnit"] : "minutes";

                                                foreach (DataRow drCarrier in dt.Rows)
                                                {
                                                    if (_functionService.TimerTool(_tmpUnit, drCarrier["lastModify_dt"].ToString()) >= _tmpReleaseSyshold)
                                                    {
                                                        carrierID = drCarrier["carrier_id"].ToString();
                                                        _dbTool.SQLExec(_BaseDataService.UpdateTableCarrierTransferByCarrier(carrierID, "Normal"), out tmpMsg, true);

                                                        if (tmpMsg.Equals(""))
                                                        {
                                                            tmpMsg = string.Format("[AutoRelease SysHold Carrier over {0} {1}][{2}][{3}]", _tmpReleaseSyshold, _tmpUnit, carrierID, drCarrier["lastModify_dt"].ToString());
                                                            _logger.Debug(tmpMsg);
                                                        }
                                                    }
                                                }

                                                if (!tmpMsg.Equals(""))
                                                {
                                                    _logger.Debug(tmpMsg);
                                                }
                                                else
                                                {
                                                    tmpMsg = string.Format("[AutoReleaseCarrier] Done.");
                                                    _logger.Debug(tmpMsg);
                                                }
                                            }
                                            
                                        }
                                        catch (Exception ex)
                                        {
                                            tmpMsg = string.Format("Listening exception [AutoReleaseCarrier: {0}]", ex.Message);
                                            _logger.Debug(tmpMsg);
                                        }
                                        //_dbTool.SQLExec(_BaseDataService.UpdateTableCarrierTransferByCarrier(CarrierId, "Normal"), out tmpMsg, true);

                                        bDoLogic = false;

                                        lock (_threadControll)
                                        {
                                            _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
                                        DateTime dtCurrent = DateTime.Now;
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
                                                    _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
                                            _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
                                        DateTime dtCurrent = DateTime.Now;
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
                                                    _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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

                                        //move to here at 20240612
                                        try
                                        {
                                        //與上位同步當前機台狀態 (超過5分鐘, 機台狀態不一致時同步)
                                        /** mark Sync RTS EQP Status change to sync time at RTD Start**/
                                            if (_functionService.SyncEQPStatus(_dbTool, _logger))
                                            {
                                                //Do Nothing.
                                            }
                                        }
                                        catch (Exception ex)
                                        { }

                                        bDoLogic = false;

                                        lock (_threadControll)
                                        {
                                            _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
                                        DateTime dtCurrent = DateTime.Now;
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
                                                    _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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

                                        // move to 10 minuts
                                        //try
                                        //{
                                            //與上位同步當前機台狀態 (超過5分鐘, 機台狀態不一致時同步)
                                            /** mark Sync RTS EQP Status change to sync time at RTD Start**/
                                        //    if (_functionService.SyncEQPStatus(_dbTool, _logger))
                                        //    {
                                        //        //Do Nothing.
                                        //    }
                                        //}
                                        //catch (Exception ex)
                                        //{ }
                                        

                                        bDoLogic = false;

                                        lock (_threadControll)
                                        {
                                            _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
                                        DateTime dtCurrent = DateTime.Now;
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
                                                    _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
                                            _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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

                        //////Auto Alarm by eMail, SMS and do Action
                        scheduleName = "AutoAlarmByMAILSMS";
                        timerID = "lastAlarmProcessTime";
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
                                        DateTime dtCurrent = DateTime.Now;
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
                                                    _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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

                                        if(_functionService.TriggerAlarms(_dbTool, _configuration, _logger))
                                        { }
                                        else
                                        { }

                                        bDoLogic = false;

                                        lock (_threadControll)
                                        {
                                            _threadControll[timerID] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
            }
            catch(Exception ex)
            {

            }

        DBissue:

            _dbTool.DisConnectDB(out tmpMsg);
            _dbTool = null;
        }
    }
}
