﻿using Microsoft.Extensions.Configuration;
using NLog;
using RTDWebAPI.APP;
using RTDWebAPI.Commons.DataRelated.SQLSentence;
using RTDWebAPI.Commons.Method.Database;
using RTDWebAPI.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using static RTDWebAPI.Service.FunctionService;

namespace RTDWebAPI.Interface
{
    public interface IFunctionService
    {
        bool AutoCheckEquipmentStatus(DBTool _dbTool, out ConcurrentQueue<EventQueue> _evtQueue);
        bool AbnormalyEquipmentStatus(DBTool _dbTool, ILogger _logger, bool DebugMode, ConcurrentQueue<EventQueue> _evtQueue, out List<NormalTransferModel> _lstNormalTransfer);
        bool CheckLotInfo(DBTool _dbTool, IConfiguration _configuration, ILogger _logger);
        bool SyncEquipmentData(DBTool _dbTool);
        bool CheckLotCarrierAssociate(DBTool _dbTool, ILogger _logger);
        bool CheckLotEquipmentAssociate(DBTool _dbTool, out ConcurrentQueue<EventQueue> _evtQueue);
        bool UpdateEquipmentAssociateToReady(DBTool _dbTool, out ConcurrentQueue<EventQueue> _evtQueue);
        bool SentDispatchCommandtoMCS(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, List<string> ListCmds);
        APIResult SentCommandtoMCS(IConfiguration _configuration, ILogger _logger, List<string> agrs);
        APIResult SentCommandtoMCSByModel(IConfiguration _configuration, ILogger _logger, string _model, List<string> agrs);
        APIResult SentAbortOrCancelCommandtoMCS(IConfiguration _configuration, ILogger _logger, int iRemotecmd, string _commandId);
        string GetAuthrizationTokenfromMCS(IConfiguration _configuration);
        string GetLotIdbyCarrier(DBTool _dbTool, string _carrierId, out string errMsg);
        List<string> CheckAvailableQualifiedTesterMachine(DBTool _dbTool, IConfiguration _configuration, bool DebugMode, ILogger _logger, string _lotId);
        List<string> CheckAvailableQualifiedTesterMachine(IConfiguration _configuration, ILogger _logger, string _username, string _password, string _lotId);
        bool BuildTransferCommands(DBTool _dbTool, IConfiguration configuration, ILogger _logger, bool DebugMode, EventQueue _oEventQ, Dictionary<string, string> _threadControll, List<string> _lstEquipment, out List<string> _arrayOfCmds);
        bool CreateTransferCommandByPortModel(DBTool _dbTool, IConfiguration configuration, ILogger _logger, bool DebugMode, string _Equip, string _portModel, EventQueue _oEventQ, out List<string> _arrayOfCmds);
        bool CreateTransferCommandByTransferList(DBTool _dbTool, ILogger _logger, TransferList _transferList, out List<string> _arrayOfCmds);
        string GetLocatePort(string _locate, int _portNo, string _locationType);
        double TimerTool(string unit, string lastDateTime);
        double TimerTool(string unit, string startDateTime, string lastDateTime);
        int GetExecuteMode(DBTool _dbTool);
        bool VerifyCustomerDevice(DBTool _dbTool, ILogger _logger, string _machine, string _customerName, string _lotid, out string _resultCode);
        bool GetLockState(DBTool _dbTool);
        bool ThreadLimitTraffice(Dictionary<string, string> _threadCtrl, string key, double _time, string _timeUnit, string _symbol);
        bool AutoAssignCarrierType(DBTool _dbTool, out string tmpMessage);
        bool AutoSentInfoUpdate(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, out string tmpMessage);
        bool AutoBindAndSentInfoUpdate(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, out string tmpMessage);
        bool AutoUpdateRTDStatistical(DBTool _dbTool, out string tmpMessage);
        bool CallRTDAlarm(DBTool _dbTool, int _alarmCode, string[] argv);
        ResultMsg CheckCurrentLotStatebyWebService(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, string _lotId);
        bool AutoGeneratePort(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, string _equipId, string _portModel, out string _errMessage);
        bool AutoHoldForDispatchIssue(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, out string tmpMessage);
        bool TriggerCarrierInfoUpdate(DBTool _dbTool, IConfiguration _configuration, ILogger _logger, string _lotid);
        bool DoInsertPromisStageEquipMatrix(DBTool _dbTool, ILogger _logger, string _stage, string _EqpType, string _EquipIds, string _userId, out string _errMsg);
        bool SyncEQPStatus(DBTool _dbTool, ILogger _logger);
        bool RecoverDatabase(DBTool _dbTool, ILogger _logger, string _message);
        bool SyncExtenalCarrier(DBTool _dbTool, IConfiguration _configuration, ILogger _logger);
        string GetExtenalTables(IConfiguration _configuration, string _method, string _func);
        string GetEquipStat(int _equipState);
        Global loadGlobalParams(DBTool _dbTool);
        bool PreDispatchToErack(DBTool _dbTool, IConfiguration _configuration, ConcurrentQueue<EventQueue> _eventQueue, ILogger _logger);
        DataTable GetLotInfo(DBTool _dbTool, string _department, ILogger _logger);
        bool CarrierLocationUpdate(DBTool _dbTool, IConfiguration _configuration, CarrierLocationUpdate value, ILogger _logger);
        bool CommandStatusUpdate(DBTool _dbTool, IConfiguration _configuration, CommandStatusUpdate value, ILogger _logger);
        bool EquipmentPortStatusUpdate(DBTool _dbTool, IConfiguration _configuration, AEIPortInfo value, ILogger _logger);
        bool EquipmentStatusUpdate(DBTool _dbTool, IConfiguration _configuration, AEIEQInfo value, ILogger _logger);
    }
}
