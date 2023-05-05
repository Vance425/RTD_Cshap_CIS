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
        string InsertTableLotInfo(string _resourceTable, string LotID);
        string QueryERackInfo();
        string InsertTableEqpStatus();
        string InsertTableWorkinprocess_Sch(SchemaWorkInProcessSch _workInProcessSch);
        string InsertInfoCarrierTransfer(string _carrierId);
        string DeleteWorkInProcessSchByCmdId(string _commandID);
        string DeleteWorkInProcessSchByGId(string _gID);
        string UpdateLockWorkInProcessSchByCmdId(string _commandID);
        string UpdateUnlockWorkInProcessSchByCmdId(string _commandID);
        string SelectAvailableCarrierByCarrierType(string _carrierType, bool isFull);
        string SelectTableCarrierAssociateByCarrierID(string CarrierID);
        string QueryLotInfoByCarrierID(string CarrierID);
        string QueryErackInfoByLotID(string _args, string _lotID);
        string SelectTableCarrierAssociateByLotid(string lotID);
        string SelectTableCarrierAssociate2ByLotid(string lotID);
        string GetCarrierByLocate(string _locate, int _number);
        string QueryCarrierByLot(string lotID);
        string QueryCarrierByLocate(string locate);
        string QueryCarrierByLocateType(string locateType, string eqpId);
        string QueryEquipmentByLot(string lotID);
        string QueryEquipmentPortIdByEquip(string equipId);
        string QueryReworkEquipment();
        string QuerySemiAutoEquipmentPortId(bool _semiAuto);
        string QueryEquipmentPortInfoByEquip(string equipId);
        string SelectTableWorkInProcessSch();
        string SelectTableWorkInProcessSchUnlock();
        string SelectTableWorkInProcessSchByCmdId(string CommandID);
        string QueryRunningWorkInProcessSchByCmdId(string CommandID);
        string QueryInitWorkInProcessSchByCmdId(string CommandID);
        string SelectTableWorkInProcessSchByLotId(string _lotid);
        string SelectTableWorkInProcessSchByEquip(string _Equip);
        string SelectTableWorkInProcessSchByEquipPort(string _Equip, string _PortId);
        string SelectTableWorkInProcessSchByPortId(string _PortId);
        string SelectTableWorkInProcessSchByCarrier(string _CarrierId);
        string QueryWorkInProcessSchByPortIdForUnload(string _PortId);
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
        string ReflushProcessLotInfo();
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
        string UpdateTableWorkInProcessSchByCmdId(string _cmd_Current_State, string _lastModify_DT, string _commandID);
        string UpdateTableWorkInProcessSchByUId(string _updateState, string _lastModify_DT, string _UID);
        string UpdateTableWorkInProcessSchHisByUId(string _uid);
        string UpdateTableWorkInProcessSchHisByCmdId(string _commandID);
        string UpdateTableCarrierTransfer(CarrierLocationUpdate oCarrierLoc);
        string UpdateTableReserveCarrier(string _carrierID, bool _reserve);
        string UpdateTableCarrierTransfer(CarrierLocationUpdate oCarrierLoc, int _haveMetalRing);
        string CarrierLocateReset(CarrierLocationUpdate oCarrierLoc, int _haveMetalRing);
        string UpdateTableEQP_STATUS(string EquipId, string CurrentStatus);
        string UpdateTableEQP_Port_Set(string EquipId, string PortSeq, string NewStatus);
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
        string UpdateLotInfoWhenCOMP(string _commandId);
        string UpdateLotInfoWhenFail(string _commandId);
        string UpdateEquipCurrentStatus(string _current, string _equipid);
        string QueryEquipmentStatusByEquip(string _equip);
        string QueryCarrierInfoByCarrierId(string _carrierId);
        string QueryCarrierType(string _carrierType, string _typeKey);
        string UpdateCarrierType(string _carrierType, string _typeKey);
        string QueryWorkinProcessSchHis(string _startTime, string _endTime);
        string QueryStatisticalOfDispatch(DateTime dtCurrUTCTime, string _statisticalUnit, string _type);
        string CalcStatisticalTimesFordiffZone(DateTime dtCurrUTCTime, string _statisticalUnit, string _type, double _zone);
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
        string InsertRTDAlarm(string _alarm);
        string UpdateLotinfoState(string _lotID, string _state);
        string ConfirmLotinfoState(string _lotID, string _state);
        string QueryLotinfoQuantity(string _lotID);
        string UpdateLotinfoTotalQty(string _lotID, int _TotalQty);
        string CheckQtyforSameLotId(string _lotID, string _carrierType);
        string QueryQuantityByCarrier(string _carrierID);
        string QueryEqpPortSet(string _equipId, string _portSeq);
        string InsertTableEqpPortSet(string[] _params);
        string QueryWorkgroupSet(string _Workgroup);
        string QueryWorkgroupSetAndUseState(string _Workgroup);
        string CreateWorkgroup(string _Workgroup);
        string UpdateWorkgroupSet(string _Workgroup, string _InRack, string _OutRack);
        string DeleteWorkgroup(string _Workgroup);
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
        string CheckCarrierLocate(string _inErack, string _locate);
        string CheckPreTransfer(string _carrierid);
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
    }
}
