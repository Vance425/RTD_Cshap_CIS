﻿using RTDWebAPI.Commons.DataRelated.SQLSentence;
using RTDWebAPI.Models;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RTDWebAPI.Interface
{
    public interface IBaseDataService
    {
        string CheckIsAvailableLot(string lotId, string equipment);
        string GetEquipState(int _iState);
        int GetEquipState(string _State);
        string InsertTableLotInfo(string _resourceTable, string LotID);
        string QueryERackInfo();
        string InsertTableEqpStatus();
        string InsertTableWorkinprocess_Sch(SchemaWorkInProcessSch _workInProcessSch, string _table);
        string InsertInfoCarrierTransfer(string _carrierId);
        string DeleteWorkInProcessSchByCmdId(string _commandID, string _table);
        string DeleteWorkInProcessSchByGId(string _gID, string _table);
        string DeleteWorkInProcessSchByCmdIdAndCMDType(string _commandID, string _cmdType, string _table);
        string UpdateLockWorkInProcessSchByCmdId(string _commandID, string _table);
        string UpdateUnlockWorkInProcessSchByCmdId(string _commandID, string _table);
        string SelectAvailableCarrierByCarrierType(string _carrierType, bool isFull);
        string SelectAvailableCarrierForUatByCarrierType(string _carrierType, bool isFull);
        string SelectAvailableCarrierIncludeEQPByCarrierType(string _carrierType, bool isFull);
        string SelectAvailableCarrierIncludeEQPForUatByCarrierType(string _carrierType, bool isFull);
        string SelectTableCarrierAssociateByCarrierID(string CarrierID);
        string QueryLotInfoByCarrierID(string CarrierID);
        string QueryErackInfoByLotID(string _args, string _lotID);
        string SelectTableCarrierAssociateByLotid(string lotID);
        string SelectTableCarrierAssociate2ByLotid(string lotID);
        string SelectTableCarrierAssociate3ByLotid(string lotID);
        string GetCarrierByLocate(string _locate, int _number);
        string QueryCarrierByLot(string lotID);
        string QueryCarrierByLocate(string locate, string _table);
        string QueryCarrierByLocateType(string locateType, string eqpId, string _table);
        string QueryEquipmentByLot(string lotID);
        string QueryEquipmentPortIdByEquip(string equipId);
        string QueryReworkEquipment();
        string QuerySemiAutoEquipmentPortId(bool _semiAuto);
        string QueryEquipmentPortInfoByEquip(string equipId);
        string SelectTableWorkInProcessSch(string _table);
        string SelectTableWorkInProcessSchUnlock(string _table);
        string SelectTableWorkInProcessSchByCmdId(string CommandID, string _table);
        string QueryRunningWorkInProcessSchByCmdId(string CommandID, string _table);
        string QueryInitWorkInProcessSchByCmdId(string CommandID, string _table);
        string SelectTableWorkInProcessSchByLotId(string _lotid, string _table);
        string SelectTableWorkInProcessSchByEquip(string _Equip, string _table);
        string SelectTableWorkInProcessSchByEquipPort(string _Equip, string _PortId, string _table);
        string SelectTableWorkInProcessSchByPortId(string _PortId, string _table);
        string SelectTableWorkInProcessSchByCarrier(string _CarrierId, string _table);
        string QueryWorkInProcessSchByPortIdForUnload(string _PortId, string _table);
        string SelectTableEquipmentStatus([Optional] string Department);
        string SelectEquipmentPortInfo();
        string SelectTableEQPStatusInfoByEquipID(string EquipId);
        string SelectTableEQP_STATUSByEquipId(string EquipId);
        string SelectEqpStatusWaittoUnload();
        string SelectEqpStatusIsDownOutPortWaittoUnload();
        string SelectEqpStatusReadytoload();
        string SelectTableEquipmentPortsInfoByEquipId(string EquipId);
        string SelectTableEQP_Port_SetByEquipId(string EquipId);
        string SelectTableEQP_Port_SetByPortId(string EquipId);
        string SelectLoadPortCarrierByEquipId(string EquipId);
        string SelectTableCheckLotInfo(string _resourceTable);
        string SelectTableCheckLotInfoNoData(string _resourceTable);
        string SelectTableCarrierTransfer();
        string UpdateTableCarrierTransferByCarrier(string CarrierId, string State);
        string SelectTableCarrierTransferByCarrier(string CarrierID);
        string SelectTableCarrierType(string _carrierID);
        string SelectTableLotInfo();
        string SelectTableLotInfoByDept(string Dept);
        string SelectTableProcessLotInfo();
        string ReflushProcessLotInfo(string _table);
        string SelectTableProcessLotInfoByCustomer(string _customerName, string _equip);
        string QueryLastModifyDT();
        string SelectTableADSData(string _resourceTable);
        string SelectTableLotInfoByLotid(string LotId);
        string SelectTableLotInfoOfInit();
        string SelectTableLotInfoOfWait();
        string SelectTableLotInfoOfReady();
        string SelectTableEQUIP_MATRIX(string EqpId, string StageCode);
        string ShowTableEQUIP_MATRIX();
        string SelectPrefmap(string EqpId);
        string SelectCarrierAssociateIsTrue();
        string SelectRTDDefaultSet(string _parameter);
        string SelectRTDDefaultSetByType(string _parameter);
        string UpdateTableWorkInProcessSchByCmdId(string _cmd_Current_State, string _lastModify_DT, string _commandID, string _table);
        string UpdateTableWorkInProcessSchByUId(string _updateState, string _lastModify_DT, string _UID);
        string UpdateTableWorkInProcessSchHisByUId(string _uid);
        string UpdateTableWorkInProcessSchHisByCmdId(string _commandID);
        string UpdateTableCarrierTransfer(CarrierLocationUpdate oCarrierLoc);
        string UpdateTableReserveCarrier(string _carrierID, bool _reserve);
        string UpdateTableCarrierTransfer(CarrierLocationUpdate oCarrierLoc, int _haveMetalRing);
        string CarrierLocateReset(CarrierLocationUpdate oCarrierLoc, int _haveMetalRing);
        string UpdateTableEQP_STATUS(string EquipId, int CurrentStatus, string MachineState, string DownState);
        string UpdateTableEQP_Port_Set(string EquipId, string PortSeq, string NewStatus);
        string UpdateTableEQP_Port_Set(string EquipId, string PortSeq);
        string UpdateTableLotInfoReset(string LotID);
        string UpdateTableLotInfoEquipmentList(string _lotID, string _lstEquipment);
        string UpdateTableLotInfoState(string LotID, string State);
        string UpdateTableLastModifyByLot(string LotID);
        string UpdateTableLotInfoSetCarrierAssociateByLotid(string LotID);
        string UpdateTableLotInfoToReadyByLotid(string LotID);
        string UpdateLotInfoSchSeqByLotid(string LotID, int _SchSeq);
        string UpdateTableLotInfoSetCarrierAssociate2ByLotid(string LotID);
        string UpdateTableRTDDefaultSet(string _parameter, string _paramvalue, string _modifyBy);
        string GetLoadPortCurrentState(string _equipId);
        string UpdateSchSeq(string _Customer, string _Stage, int _SchSeq, int _oriSeq);
        string UpdateSchSeqByLotId(string _lotId, string _Customer, int _SchSeq);
        string InsertSMSTriggerData(string _eqpid, string _stage, string _desc, string _flag, string _username);
        string SchSeqReflush();
        string LockLotInfo(bool _lock);
        string GetLockStateLotInfo();
        string ReflushWhenSeqZeroStateWait();
        string SyncNextStageOfLot(string _resourceTable, string _lotid);
        string UpdateLotInfoWhenCOMP(string _commandId, string _table);
        string UpdateLotInfoWhenFail(string _commandId, string _table);
        string UpdateEquipCurrentStatus(string _current, string _equipid);
        string QueryEquipmentStatusByEquip(string _equip);
        string QueryCarrierInfoByCarrierId(string _carrierId);
        string QueryCarrierType(string _carrierType, string _typeKey);
        string UpdateCarrierType(string _carrierType, string _typeKey);
        string QueryWorkinProcessSchHis(string _startTime, string _endTime);
        string QueryStatisticalOfDispatch(DateTime dtCurrUTCTime, string _statisticalUnit, string _type);
        string CalcStatisticalTimesFordiffZone(bool isStart, DateTime dtStartTime, DateTime dtEndTime, string _statisticalUnit, string _type, double _zone);
        string QueryRtdNewAlarm();
        string QueryAllRtdAlarm();
        string UpdateRtdAlarm(string _time);
        string QueryCarrierAssociateWhenIsNewBind();
        string QueryCarrierAssociateWhenOnErack(string _table);
        string ResetCarrierLotAssociateNewBind(string _carrierId);
        string QueryRTDStatisticalByCurrentHour(DateTime _datetime);
        string InitialRTDStatistical(string _datetime, string _type);
        string UpdateRTDStatistical(DateTime _datetime, string _type, int _count);
        string InsertRTDStatisticalRecord(string _datetime, string _commandid, string _type);
        string QueryRTDStatisticalRecord(string _datetime);
        string CleanRTDStatisticalRecord(DateTime _datetime, string _type);
        string SelectWorkgroupSet(string _EquipID);
        string InsertRTDAlarm(string[] _alarmCode);
        string InsertRTDAlarm(RTDAlarms _Alarms);
        string UpdateLotinfoState(string _lotID, string _state);
        string ConfirmLotinfoState(string _lotID, string _state);
        string QueryLotinfoQuantity(string _lotID);
        string UpdateLotinfoTotalQty(string _lotID, int _TotalQty);
        string CheckQtyforSameLotId(string _lotID, string _carrierType);
        string QueryQuantity2ByCarrier(string _carrierID);
        string QueryQuantityByCarrier(string _carrierID);
        string QueryEqpPortSet(string _equipId, string _portSeq);
        string InsertTableEqpPortSet(string[] _params);
        string QueryWorkgroupSet(string _Workgroup);
        string QueryWorkgroupSet(string _Workgroup, string _Stage);
        string QueryWorkgroupSetAndUseState(string _Workgroup);
        string CreateWorkgroup(string _Workgroup);
        string UpdateWorkgroupSet(string _Workgroup, string _InRack, string _OutRack);
        string UpdatePriorityForWorkgroupSet(string _Workgroup, string _stage, int _priority);
        string DeleteWorkgroup(string _Workgroup);
        string SetResetPreTransfer(string _Workgroup, Boolean _set);
        string UpdateEquipWorkgroup(string _equip, string _Workgroup);
        string UpdateEquipPortSetWorkgroup(string _equip, string _Workgroup);
        string UpdateEquipPortModel(string _equip, string _portModel, int _portNum);
        string DeleteEqpPortSet(string _Equip, string _portModel);
        string QueryPortModelMapping(string _eqpTypeID);
        string QueryPortModelDef();
        string QueryProcLotInfo();
        string LockMachineByLot(string _lotid, int _Quantity, int _lock);
        string UpdateLotInfoForLockMachine(string _lotid);
        string CheckLocationByLotid(string _lotid);
        string QueryEQPType();
        string QueryEQPIDType();
        string InsertPromisStageEquipMatrix(string _stage, string _equipType, string _equipids, string _userId);
        string DeletePromisStageEquipMatrix(string _stage, string _equipType, string _equipids);
        string SyncStageInfo();
        string CheckRealTimeEQPState();
        string UpdateCurrentEQPStateByEquipid(string _equipid);
        string QueryEquipListFirst(string _lotid, string _equipid);
        string QueryRackByGroupID(string _groupID);
        string QueryExtenalCarrierInfo(string _table);
        string InsertCarrierLotAsso(CarrierLotAssociate _carrierLotAsso);
        string InsertCarrierTransfer(string _carrierId, string _typeKey, string _quantity);
        string UpdateCarrierLotAsso(CarrierLotAssociate _carrierLotAsso);
        string UpdateLastCarrierLotAsso(CarrierLotAssociate _carrierLotAsso);
        string UpdateCarrierTransfer(string _carrierId, string _typeKey, string _quantity);
        string LockEquip(string _equip, bool _lock);
        string QueryEquipLockState(string _equip);
        string QueryPreTransferList(string _lotTable);
        string QueryPreTransferListForUat(string _lotTable);
        string CheckCarrierLocate(string _inErack, string _locate);
        string CheckPreTransfer(string _carrierid, string _table);
        string ManualModeSwitch(string _equip, bool _autoMode);
        string QueryLotStageWhenStageChange(string _table);
        string QueryReserveStateByEquipid(string _equipid);
        string InsertEquipReserve(string _args);
        string UpdateEquipReserve(string _args);
        string LockLotInfoWhenReady(string _lotID);
        string UnLockLotInfoWhenReady(string _lotID);
        string UnLockAllLotInfoWhenReadyandLock();
        string QueryLotInfoByCarrier(string _carrierid);
        string CheckReserveState(string _args);
        string UpdateStageByLot(string _lotid, string _stage);
        string QueryLastLotFromEqpPort(string EquipId, string PortSeq);
        string UpdateLastLotIDtoEQPPortSet(string EquipId, string PortSeq, string LastLotID);
        string ConfirmLotInfo(string _lotid);
        string IssueLotInfo(string _lotid);
        string CheckLotStage(string _table, string _lotid);
        public string EQPListReset(string LotID);
        string GetEquipCustDevice(string EquipID);
        string CheckMetalRingCarrier(string _carrierID);
        string UpdatePriorityByLotid(string _lotID, int _priority);
        string QueryDataByLotid(string _lotID, string _table);
        string HisCommandAppend(HisCommandStatus hisCommandStatus);
        string GetWorkinprocessSchByCommand(string command, string _table);
        string GetRTSEquipStatus(string _table, string equipid);
        string GetHistoryCommands(string StartTime, string EndTime);
        string ResetRTDStateByLot(string LotID);
        string UpdateCustDeviceByEquipID(string _equipID, string _custDevice);
        string UpdateCustDeviceByLotID(string _lotID, string _custDevice);
        string InsertHisTSCAlarm(TSCAlarmCollect _alarmCollect);
        string SetPreDispatching(string _Workgroup, string _Type);
        string CalcStatisticalTimes(string StartTime, string EndTime);
        string QueryCarrierByCarrierID(string _carrierID);
        string QueryListAlarmDetail();
        string UpdateLotAgeByLotID(string _lotID, string _lotAge);
        string EnableEqpipPort(string _portID, Boolean _enabled);
        string QueryRTDAlarms();
        string QueryExistRTDAlarms(string _args);
        string UpdateRTDAlarms(bool _reset, string _args, string _detail);
        string LockEquipPortByPortId(string _portID, bool _lock);
        string QueryHisWorkinprocess(string _args);
        string QueryOrderWhenOvertime(string _overtime, string _table);
        string AutoResetCarrierReserveState(string _table);
        string QueryCarrierOnRack(string _workgroup, string _equip);
        string QueryCarrierOnPort(string _portID);
        string QueryNoCommandStateOrderOvertime(string _table);
        string UnlockOrderForRetry(string _cmdID, string _table);
        string CheckReserveTimeByEqpID(string _equipID);
        string GetUserAccountType(string _userID);
        string GetWorkgroupDetailSet(string _function, string _workgroup, string _customer, string _cdi);
        string CheckLotAgeByCarrierOnErack(string _table);
        string QueryCarrierByCarrierId(string _carrierId);
        string UpdateCarrierToUAT(string _carrierId, bool _isuat);
        string GetAllOfHoldCarrier();
        string GetAllOfSysHoldCarrier();
        string QueryEquipPortisLock();
        string QureyPrepareNextWorkgroupNumber(string _workgroup);
        string QureyProcessNextWorkgroupNumber(string _workgroup);
        string QureyCurrentWorkgroupProcessNumber(string _workgroup);
        string CheckDuplicatelot(string _lotId);
        string QureyLotQtyOnerackForNextWorkgroup(string _workgroup);
        string QueryCarrierAssociateByCarrierID(string _table, string _carrierId);
        string SelectAvailableCarrierByEQP(string _portid, string _carrierType, bool isFull);
        string SelectAvailableCarrierForUatByEQP(string _portid, string _carrierType, bool isFull);
        string QueryIslockPortId();
        string QueryTransferListForSideWH(string _lotTable);
        string QueryTransferListUIForSideWH(string _lotTable);
        string GetEqpInfoByWorkgroupStage(string _workgroup, string _stage, string _lotstage);
        string GetCarrierByLocate(string _locate);
        string CalculateProcessQtyByStage(string _workgroup, string _stage, string _lotstage);
        string CalculateLoadportQtyByStage(string _workgroup, string _stage, string _lotstage);
        string CheckLocateofSideWh(string _locate, string _sideWh);
        string QueryDataByLot(string _table, string _lotID);
        string QueryPreTransferforSideWH(string _dest, string _table);
        string InsertPortStateChangeEvent(List<string> _lsParams);
        string GetCarrierTypeByPort(string _portID);
        string SetResetParams(string _Workgroup, string _Stage, string _paramsName, Boolean _set);
        string QueryAlarmDetailByCode(string _alarmCode);
        string GetHisWorkinprocessSchByCommand(string command);
        string QueryPortInfobyPortID(string _portID); 
        string QueryMCSStatus(string _parameter);
        string ChangeMCSStatus(string _parameter, bool _bstate);
        string QueryRTDServer(string _server);
        string InsertRTDServer(string _server);
        string UadateRTDServer(string _server);
        string QueryResponseTime(string _server);
        string InsertResponseTime(string _server);
        string UadateResponseTime(string _server);
        string GetAvgProcessingTime(string _equip, string _lotid);
        string GetMRProcessingTime(string _equip);
    }
}
