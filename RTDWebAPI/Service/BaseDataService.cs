﻿using RTDWebAPI.Commons.DataRelated.SQLSentence;
using RTDWebAPI.Interface;
using RTDWebAPI.Models;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RTDWebAPI.Service
{
    public class BaseDataService: IBaseDataService
    {
        public string CheckIsAvailableLot(string lotId, string equipMent)
        {
            string strSQL = string.Format("select * from LOT_INFO a left join carrier_lot_associate b on b.lot_id = a.lotid left join carrier_transfer c on c.carrier_id = b.carrier_id where a.lotid = '{0}' and instr(a.equiplist, '{1}') > 0 and c.reserve = 0", lotId, equipMent);
            return strSQL;
        }
        public string GetEquipState(int _iState)
        {
            string State = "";
            switch (_iState)
            {
                case 0:
                    State = "DOWN";
                    break;
                case 1:
                    State = "PM";
                    break;
                case 2:
                    State = "IDLE";
                    break;
                case 3:
                    State = "UP";
                    break;
                case 4:
                    State = "PAUSE";
                    break;
                default:
                    State = String.Format("UNKNOW,{0}", _iState.ToString());
                    break;
            }
            return State;
        }
        public int GetEquipState(string _State)
        {
            int iState = 99;
            switch (_State)
            {
                case "DOWN":
                    iState = 0;
                    break;
                case "PM":
                    iState = 1;
                    break;
                case "IDLE":
                    iState = 2;
                    break;
                case "UP":
                    iState = 3;
                    break;
                case "PAUSE":
                    iState = 4;
                    break;
                default:
                    iState = 99;
                    break;
            }
            return iState;
        }
        public string InsertTableLotInfo(string _resourceTable, string LotID)
        {
            string strSQL = "";

            strSQL = string.Format(@"insert into LOT_INFO (lotid, stage, customername, priority, wfr_qty, dies_qty, carrier_asso, equip_asso, equiplist, state, Rtd_State, planstarttime, starttime, partid, lottype, lot_age, create_dt, lastmodify_dt, custDevice)
                                            select a.lotid, a.stage, a.customername, a.priority, a.wfr_qty, a.dies_qty, 'N' as carrier_asso, 'N' as equip_asso, '' as equiplist, a.state, 'INIT' as rtd_state,
                                            a.planstarttime, a.starttime, a.partid, a.lottype, a.lot_age, 
                                            sysdate as create_dt, sysdate as lastmodify_dt, custDevice as custDevice from {0} a where lotid = '{1}'", _resourceTable, LotID);

            return strSQL;
        }
        public string InsertTableEqpStatus()
        {
            string strSQL = "";
#if DEBUG
            strSQL = string.Format(@"insert into EQP_STATUS (equipid, equip_dept, Equip_TypeID, Equip_Type, Machine_State, Curr_Status, Down_state, port_Model, Port_Number, Workgroup, Near_Stocker, create_dt, lastmodify_dt)
                                            select equipid, equip_dept, TypeID as Equip_TypeID, Equip_Type, Machine_State, Curr_Status, Down_state, '' as port_Model, 
                                            '' as Port_Number, Equip_Type as Workgroup, '' as Near_Stocker, sysdate as create_dt, sysdate as lastmodify_dt
                                            from EQP_STATUS_INFO 
                                            where equipid in (
                                            select equipid from (
                                            select e.equipid, case when d.equipid is null then 'New' else 'Old' end as State from (
                                            select distinct c.equipid from (
                                            select a.equipid from EQP_STATUS_INFO a 
                                            union 
                                            select b.equipid from EQP_STATUS b) c) e
                                            left join eqp_status d on d.equipid=e.equipid)
                                            where state = 'New')");
#else
            strSQL = string.Format(@"insert into EQP_STATUS (equipid, equip_dept, Equip_TypeID, Equip_Type, Machine_State, Curr_Status, Down_state, port_Model, Port_Number, Workgroup, Near_Stocker, create_dt, lastmodify_dt)                              
                                            select f.equipid, g.equip_dept, f.equip_typeid, g.equip_type, f.machine_state, f.curr_status, f.down_state, f.port_model, f.port_number, g.equip_type as workgroup, f.near_stocker, f.create_dt, f.lastmodify_dt from (
                                            select equipid,  TypeID as Equip_TypeID,  Machine_State, Curr_Status, Down_state, '' as port_Model, 
                                            '' as Port_Number,  '' as Near_Stocker, sysdate as create_dt, sysdate as lastmodify_dt
                                            from rts_active@CIMDB3.world 
                                            where equipid in (
                                            select equipid from (
                                            select e.equipid, case when d.equipid is null then 'New' else 'Old' end as State from (
                                            select distinct c.equipid from (
                                            select a.equipid from rts_active@CIMDB3.world a 
                                            union 
                                            select b.equipid from eqp_status b) c) e
                                            left join eqp_status d on d.equipid=e.equipid)
                                            where state = 'New')) f
                                            left join rts_equipment@CIMDB3.world g on g.equipid = f.equipid");
#endif
            return strSQL;
        }
        public string InsertTableWorkinprocess_Sch(SchemaWorkInProcessSch _workInProcessSch, string _table)
        {
            string _startDt = "";

            if(_workInProcessSch.Cmd_Current_State.Equals("NearComp"))
            {
                _startDt = string.Format(@"to_date('{0}','yyyy/MM/dd HH24:mi:ss')", _workInProcessSch.Start_Dt);
            }
            else
            {
                _startDt = "sysdate";
            }

            //string _table = "workinprocess_sch";
            string _values = string.Format(@"'{0}', '{1}', '{2}', '{3}', ' ', ' ', '{4}', '{5}', '{6}', '{7}', {8}, {9}, '*', 0, '{10}', '{11}', sysdate, sysdate, {12}, {13}, {14}, {15}",
                            _workInProcessSch.UUID, _workInProcessSch.Cmd_Id, _workInProcessSch.Cmd_Type, _workInProcessSch.EquipId, _workInProcessSch.CarrierId,
                            _workInProcessSch.CarrierType, _workInProcessSch.Source, _workInProcessSch.Dest, _workInProcessSch.Priority, _workInProcessSch.Replace, _workInProcessSch.LotID, _workInProcessSch.Customer, _workInProcessSch.Quantity, _workInProcessSch.Total, _workInProcessSch.IsLastLot, _startDt);


            string strSQL = string.Format(@"insert into {0} (uuid, cmd_id, cmd_type, equipid, cmd_state, cmd_current_state, carrierid, carriertype, source, dest, priority, replace, back, isLock, lotid, customer, create_dt, modify_dt, quantity, total, islastlot, start_dt)
                            values ({1})", _table, _values);

            return strSQL;
        }
        public string InsertInfoCarrierTransfer(string _carrierId)
        {
            string strSQL = "";
            strSQL = string.Format(@"insert into CARRIER_TRANSFER (carrier_id, type_key, carrier_state, enable, create_dt, modify_dt, lastmodify_dt)
                                            select carrier_id, '', 'OFFLINE', 1, create_dt, modify_dt, sysdate from carrier_info where carrier_id='{0}'", _carrierId);

            return strSQL;
        }
        public string DeleteWorkInProcessSchByCmdId(string _commandID, string _table)
        {
            string tmpString = "delete {0} where cmd_id = '{1}'";
            string strSQL = string.Format(tmpString, _table, _commandID);
            return strSQL;
        }
        public string DeleteWorkInProcessSchByGId(string _gID, string _table)
        {
            string tmpString = "delete {0} where uuid = '{1}'";
            string strSQL = string.Format(tmpString, _table, _gID);
            return strSQL;
        }
        public string DeleteWorkInProcessSchByCmdIdAndCMDType(string _commandID, string _cmdType, string _table)
        {
            string tmpString = "delete {0} where cmd_id = '{1}' and cmd_type='{2}'";
            string strSQL = string.Format(tmpString, _table, _commandID, _cmdType);
            return strSQL;
        }
        public string UpdateLockWorkInProcessSchByCmdId(string _commandID, string _table)
        {
            string tmpString = "update {0} set IsLock = 1 where cmd_id = '{1}'";
            string strSQL = string.Format(tmpString, _table, _commandID);
            return strSQL;
        }
        public string UpdateUnlockWorkInProcessSchByCmdId(string _commandID, string _table)
        {
            string tmpString = "update {0} set IsLock = 0, lastModify_dt = sysdate where cmd_id = '{1}'";
            string strSQL = string.Format(tmpString, _table, _commandID);
            return strSQL;
        }
        public string SelectAvailableCarrierByCarrierType(string _carrierType, bool isFull)
        {
            string strSQL = "";
            if (isFull)
            {
                strSQL = @"select a.carrier_id, a.carrier_state, a.locate, a.portno, a.enable, a.location_type,a.metal_ring, c.quantity, case when d.total_qty is null then 0 else d.total_qty end as total_qty,
                                    b.carrier_type, b.command_type, c.tag_type, c.lot_id, d.custdevice from CARRIER_TRANSFER a
                                left join CARRIER_TYPE_SET b on b.type_key = a.type_key
                                left join CARRIER_LOT_ASSOCIATE c on c.carrier_id = a.carrier_id
                                left join LOT_INFO d on d.lotid=c.lot_id
                                where a.enable=1 and a.carrier_state ='ONLINE' and a.state not in ('HOLD', 'SYSHOLD') and a.reserve = 0 and a.uat = 0 and d.sch_seq > 0 and d.state not in ('HOLD')
                                    and a.location_type in ('ERACK','STK') and b.carrier_type in ( select carrier_type_key from port_type_asso where carrier_type = '" + _carrierType.Trim() + "') and c.lot_id is not null order by d.sch_seq";
            }
            else
            {
                strSQL = @"select a.carrier_id, a.carrier_state, a.locate, a.portno, a.enable, a.location_type,a.metal_ring, c.quantity, 0 as total_qty,
                                    b.carrier_type, b.command_type, c.tag_type, c.lot_id from CARRIER_TRANSFER a
                                left join CARRIER_TYPE_SET b on b.type_key = a.type_key
                                left join CARRIER_LOT_ASSOCIATE c on c.carrier_id = a.carrier_id
                                where a.enable=1 and a.carrier_state ='ONLINE' and a.state not in ('HOLD', 'SYSHOLD') and a.reserve = 0 and a.uat = 0 
                                    and a.location_type in ('ERACK','STK') and c.associate_state not in ('Initiated', 'Associated With Lot', 'Error', 'Associated With None') and b.carrier_type in ( select carrier_type_key from port_type_asso where carrier_type = '" + _carrierType.Trim() + "') and a.locate in (select paramvalue from RTD_DEFAULT_SET where parameter='EmptyERack' and paramtype='" + _carrierType.Trim() + "') and c.lot_id is null";
            }
            return strSQL;
        }
        public string SelectAvailableCarrierIncludeEQPByCarrierType(string _carrierType, bool isFull)
        {
            string strSQL = "";
            if (isFull)
            {
                strSQL = @"select a.carrier_id, a.carrier_state, a.locate, a.portno, a.enable, a.location_type,a.metal_ring, c.quantity, case when d.total_qty is null then 0 else d.total_qty end as total_qty,
                                    b.carrier_type, b.command_type, c.tag_type, c.lot_id, c.last_lot_id, d.custdevice from CARRIER_TRANSFER a
                                left join CARRIER_TYPE_SET b on b.type_key = a.type_key
                                left join CARRIER_LOT_ASSOCIATE c on c.carrier_id = a.carrier_id
                                left join LOT_INFO d on d.lotid=c.lot_id
                                where a.enable=1 and a.carrier_state ='ONLINE' and a.state not in ('HOLD', 'SYSHOLD') and a.reserve = 0 and a.uat = 0 and d.state not in ('HOLD')
                                    and a.location_type in ('ERACK','STK','EQP') and b.carrier_type in ( select carrier_type_key from port_type_asso where carrier_type = '" + _carrierType.Trim() + "') order by d.sch_seq";
            }
            else
            {
                strSQL = @"select a.carrier_id, a.carrier_state, a.locate, a.portno, a.enable, a.location_type,a.metal_ring, c.quantity, 0 as total_qty,
                                    b.carrier_type, b.command_type, c.tag_type, c.lot_id, c.last_lot_id  from CARRIER_TRANSFER a
                                left join CARRIER_TYPE_SET b on b.type_key = a.type_key
                                left join CARRIER_LOT_ASSOCIATE c on c.carrier_id = a.carrier_id
                                where a.enable=1 and a.carrier_state ='ONLINE' and a.state not in ('HOLD', 'SYSHOLD') and a.reserve = 0 and a.uat = 0 
                                    and a.location_type in ('ERACK','STK','EQP') and c.associate_state not in ('Initiated', 'Associated With Lot', 'Error', 'Associated With None') and b.carrier_type in ( select carrier_type_key from port_type_asso where carrier_type = '" + _carrierType.Trim() + "') and a.locate in (select paramvalue from RTD_DEFAULT_SET where parameter='EmptyERack' and paramtype='" + _carrierType.Trim() + "')";
            }
            return strSQL;
        }

        public string SelectAvailableCarrierForUatByCarrierType(string _carrierType, bool isFull)
        {
            string strSQL = "";
            if (isFull)
            {
                strSQL = @"select a.carrier_id, a.carrier_state, a.locate, a.portno, a.enable, a.location_type,a.metal_ring, c.quantity, case when d.total_qty is null then 0 else d.total_qty end as total_qty,
                                    b.carrier_type, b.command_type, c.tag_type, c.lot_id from CARRIER_TRANSFER a
                                left join CARRIER_TYPE_SET b on b.type_key = a.type_key
                                left join CARRIER_LOT_ASSOCIATE c on c.carrier_id = a.carrier_id
                                left join LOT_INFO d on d.lotid=c.lot_id
                                where a.enable=1 and a.carrier_state ='ONLINE' and a.state not in ('HOLD', 'SYSHOLD') and a.reserve = 0 and a.uat = 1 and d.sch_seq > 0 and d.state not in ('HOLD')
                                    and a.location_type in ('ERACK','STK') and b.carrier_type in ( select carrier_type_key from port_type_asso where carrier_type = '" + _carrierType.Trim() + ")' and c.lot_id is not null order by d.sch_seq";
            }
            else
            {
                strSQL = @"select a.carrier_id, a.carrier_state, a.locate, a.portno, a.enable, a.location_type,a.metal_ring, c.quantity, 0 as total_qty,
                                    b.carrier_type, b.command_type, c.tag_type, c.lot_id from CARRIER_TRANSFER a
                                left join CARRIER_TYPE_SET b on b.type_key = a.type_key
                                left join CARRIER_LOT_ASSOCIATE c on c.carrier_id = a.carrier_id
                                where a.enable=1 and a.carrier_state ='ONLINE' and a.state not in ('HOLD', 'SYSHOLD') and a.reserve = 0 and a.uat = 1 
                                    and a.location_type in ('ERACK','STK') and c.associate_state not in ('Initiated', 'Associated With Lot', 'Error', 'Associated With None') and b.carrier_type in ( select carrier_type_key from port_type_asso where carrier_type = '" + _carrierType.Trim() + ")' and a.locate in (select paramvalue from RTD_DEFAULT_SET where parameter='EmptyERack' and paramtype='" + _carrierType.Trim() + "') and c.lot_id is null";
            }
            return strSQL;
        }

        public string SelectAvailableCarrierIncludeEQPForUatByCarrierType(string _carrierType, bool isFull)
        {
            string strSQL = "";
            if (isFull)
            {
                strSQL = @"select a.carrier_id, a.carrier_state, a.locate, a.portno, a.enable, a.location_type,a.metal_ring, c.quantity, case when d.total_qty is null then 0 else d.total_qty end as total_qty,
                                    b.carrier_type, b.command_type, c.tag_type, c.lot_id, c.last_lot_id from CARRIER_TRANSFER a
                                left join CARRIER_TYPE_SET b on b.type_key = a.type_key
                                left join CARRIER_LOT_ASSOCIATE c on c.carrier_id = a.carrier_id
                                left join LOT_INFO d on d.lotid=c.lot_id
                                where a.enable=1 and a.carrier_state ='ONLINE' and a.state not in ('HOLD', 'SYSHOLD') and a.reserve = 0 and a.uat = 1 and d.sch_seq > 0 and d.state not in ('HOLD')
                                    and a.location_type in ('ERACK','STK','EQP') and b.carrier_type in ( select carrier_type_key from port_type_asso where carrier_type = '" + _carrierType.Trim() + "') order by d.sch_seq";
            }
            else
            {
                strSQL = @"select a.carrier_id, a.carrier_state, a.locate, a.portno, a.enable, a.location_type,a.metal_ring, c.quantity, 0 as total_qty,
                                    b.carrier_type, b.command_type, c.tag_type, c.lot_id, c.last_lot_id  from CARRIER_TRANSFER a
                                left join CARRIER_TYPE_SET b on b.type_key = a.type_key
                                left join CARRIER_LOT_ASSOCIATE c on c.carrier_id = a.carrier_id
                                where a.enable=1 and a.carrier_state ='ONLINE' and a.state not in ('HOLD', 'SYSHOLD') and a.reserve = 0 and a.uat = 1 
                                    and a.location_type in ('ERACK','STK','EQP') and c.associate_state not in ('Initiated', 'Associated With Lot', 'Error', 'Associated With None') and b.carrier_type in ( select carrier_type_key from port_type_asso where carrier_type = '" + _carrierType.Trim() + "') and a.locate in (select paramvalue from RTD_DEFAULT_SET where parameter='EmptyERack' and paramtype='" + _carrierType.Trim() + "')";
            }
            return strSQL;
        }

        public string SelectAvailableCarrierByEQP(string _portid, string _carrierType, bool isFull)
        {
            string strSQL = "";
            if (isFull)
            {
                strSQL = strSQL = String.Format(@"select b.carrier_id, b.carrier_state, g.port_id, b.locate, b.portno, b.enable, b.location_type, b.metal_ring, b.quantity,
case when d.total_qty is null then 0 else d.total_qty end as total_qty,
f.carrier_type, f.command_type, c.tag_type, c.lot_id from (
select b.workgroup,  e.equipid, e.port_seq, e.port_state, e.port_id, e.carrier_type, e.lastlotid, e.lastmodify_dt from eqp_port_set a 
left join workgroup_set b on b.nextworkgroup=a.workgroup
left join eqp_port_set e on e.workgroup=b.workgroup
where a.port_id = '{0}' and e.carrier_type in ( select carrier_type_key from port_type_asso where carrier_type = '{1}')) p
left join promis_stage_equip_matrix q on q.eqptype=p.workgroup
left join carrier_transfer b on b.locate=p.equipid and b.portno=p.port_seq
left join carrier_lot_associate c on c.carrier_id = b.carrier_id
left join lot_info d on d.lotid=c.last_lot_id
left join SEMI_INT.ACTL_CISERACK_VW@SEMI_INT e on e.lotid=p.lastlotid
left join carrier_type_set f on f.type_key=b.type_key
left join eqp_port_set g on g.equipid=b.locate and g.port_seq=b.portno
where p.port_state in (3,4) and b.carrier_id <> ' ' and b.uat=0
order by e.potd desc, d.lot_age desc, p.lastmodify_dt asc", _portid, _carrierType);
            }
            else
            {
                strSQL = strSQL = String.Format(@"select b.carrier_id, b.carrier_state, g.port_id, b.locate, b.portno, b.enable, b.location_type, b.metal_ring, b.quantity,
case when d.total_qty is null then 0 else d.total_qty end as total_qty,
f.carrier_type, f.command_type, c.tag_type, c.lot_id from (
select b.workgroup,  e.equipid, e.port_seq, e.port_state, e.port_id, e.carrier_type, e.lastlotid, e.lastmodify_dt from eqp_port_set a 
left join workgroup_set b on b.nextworkgroup=a.workgroup
left join eqp_port_set e on e.workgroup=b.workgroup
where a.port_id = '{0}' and e.carrier_type in ( select carrier_type_key from port_type_asso where carrier_type = '{1}')) p
left join promis_stage_equip_matrix q on q.eqptype=p.workgroup
left join carrier_transfer b on b.locate=p.equipid and b.portno=p.port_seq
left join carrier_lot_associate c on c.carrier_id = b.carrier_id
left join lot_info d on d.lotid=c.last_lot_id
left join SEMI_INT.ACTL_CISERACK_VW@SEMI_INT e on e.lotid=p.lastlotid
left join carrier_type_set f on f.type_key=b.type_key
left join eqp_port_set g on g.equipid=b.locate and g.port_seq=b.portno
where p.port_state in (3,4) and b.carrier_id <> ' ' and b.uat=0
order by e.potd desc, d.lot_age desc, p.lastmodify_dt asc", _portid, _carrierType);
            }
            return strSQL;
        }


        public string SelectAvailableCarrierForUatByEQP(string _portid, string _carrierType, bool isFull)
        {
            string strSQL = "";
            if (isFull)
            {
                strSQL = String.Format(@"select b.carrier_id, b.carrier_state, g.port_id, b.locate, b.portno, b.enable, b.location_type, b.metal_ring, b.quantity,
case when d.total_qty is null then 0 else d.total_qty end as total_qty,
f.carrier_type, f.command_type, c.tag_type, c.lot_id from (
select b.workgroup,  e.equipid, e.port_seq, e.port_state, e.port_id, e.carrier_type, e.lastlotid, e.lastmodify_dt from eqp_port_set a 
left join workgroup_set b on b.nextworkgroup=a.workgroup
left join eqp_port_set e on e.workgroup=b.workgroup
where a.port_id = '{0}' and e.carrier_type in ( select carrier_type_key from port_type_asso where carrier_type = '{1}')) p
left join promis_stage_equip_matrix q on q.eqptype=p.workgroup
left join carrier_transfer b on b.locate=p.equipid and b.portno=p.port_seq
left join carrier_lot_associate c on c.carrier_id = b.carrier_id
left join lot_info d on d.lotid=c.last_lot_id
left join SEMI_INT.ACTL_CISERACK_VW@SEMI_INT e on e.lotid=p.lastlotid
left join carrier_type_set f on f.type_key=b.type_key
left join eqp_port_set g on g.equipid=b.locate and g.port_seq=b.portno
where p.port_state in (3,4) and b.carrier_id <> ' ' and b.uat=1
order by e.potd desc, d.lot_age desc, p.lastmodify_dt asc", _portid, _carrierType);
            }
            else
            {
                strSQL = String.Format(@"select b.carrier_id, b.carrier_state, g.port_id, b.locate, b.portno, b.enable, b.location_type, b.metal_ring, b.quantity,
case when d.total_qty is null then 0 else d.total_qty end as total_qty,
f.carrier_type, f.command_type, c.tag_type, c.lot_id from (
select b.workgroup,  e.equipid, e.port_seq, e.port_state, e.port_id, e.carrier_type, e.lastlotid, e.lastmodify_dt from eqp_port_set a 
left join workgroup_set b on b.nextworkgroup=a.workgroup
left join eqp_port_set e on e.workgroup=b.workgroup
where a.port_id = '{0}' and e.carrier_type in ( select carrier_type_key from port_type_asso where carrier_type = '{1}')) p
left join promis_stage_equip_matrix q on q.eqptype=p.workgroup
left join carrier_transfer b on b.locate=p.equipid and b.portno=p.port_seq
left join carrier_lot_associate c on c.carrier_id = b.carrier_id
left join lot_info d on d.lotid=c.last_lot_id
left join SEMI_INT.ACTL_CISERACK_VW@SEMI_INT e on e.lotid=p.lastlotid
left join carrier_type_set f on f.type_key=b.type_key
left join eqp_port_set g on g.equipid=b.locate and g.port_seq=b.portno
where p.port_state in (3,4) and b.carrier_id <> ' ' and b.uat=1
order by e.potd desc, d.lot_age desc, p.lastmodify_dt asc", _portid, _carrierType);
            }
            return strSQL;
        }
        public string SelectTableCarrierAssociateByCarrierID(string CarrierID)
        {
            string strSQL = string.Format("select * from CARRIER_LOT_ASSOCIATE where carrier_id = '{0}'", CarrierID);
            return strSQL;
        }
        public string QueryLotInfoByCarrierID(string CarrierID)
        {
            string tmpWhere = "";
            string tmpSet = "";
            string strSQL = "";

            //a.lot_id as lotid, c.lotType, c.stage, c.state, c.CUSTOMERNAME, c.HOLDCODE, c.HOLDREAS
            strSQL = string.Format(@"select distinct a.carrier_id, a.tag_type, a.associate_state, a.lot_id, a.quantity, a.last_lot_id, a.last_change_station,
                                    a.create_by, b.carrier_asso, b.equip_asso, b.equiplist, b.state, b.customername, b.stage, b.partid, b.lottype, 
                                    b.rtd_state, b.sch_seq, b.islock, b.total_qty, b.lockmachine, b.comp_qty, b.custdevice, b.lotid, a.quantity, case when b.total_qty is null then a.total else b.total_qty end as total_qty,  b.priority from CARRIER_LOT_ASSOCIATE a
                                            left join LOT_INFO b on b.lotid = a.lot_id
                                            where a.carrier_id = '{0}'", CarrierID);
            return strSQL;
        }

        public string QueryErackInfoByLotID(string _args, string _lotID)
        {
            string tmpWhere = "";
            string tmpSet = "";
            string strSQL = "";
            string[] tmpParams = _args.Split(',');
            string _work = tmpParams[0];
            string _table = tmpParams[1];

            switch (_work.ToLower())
            {
                case "cis":
                    tmpSet = "a.lotid, a.partname, a.customername, a.wl_waferlotid as waferlotid, a.stage, a.state, a.lottype, a.waiting_inspection as HoldReas, a.holdcode as holdcode, a.automotive, '' as turnratio, ''  as eotd, a.potd";
                    break;
                case "ewlb":
                    //--lotid, partname, customername, stage, state, lottype, hold_code, eotd, automotive, turnratio
                    tmpSet = "a.lotid, a.partname, a.customername, a.stage, a.state, a.lottype, a.hold_code as holdcode, a.automotive, a.turnratio, to_char(a.eotd, 'DD/MM/YYYY')  as eotd, '' as potd, '' as waferlotid, '' as HoldReas";
                    break;
                default:
                    tmpSet = "a.lotid, c.lotType, c.stage, c.state, c.CUSTOMERNAME, c.HOLDCODE, c.HOLDREAS, b.Partid, '' as Automotive, '' as waferlotid";
                    break;
            }

            //a.lot_id as lotid, c.lotType, c.stage, c.state, c.CUSTOMERNAME, c.HOLDCODE, c.HOLDREAS
            strSQL = string.Format(@"select distinct {0} from {1} a
                                            where a.lotid = '{2}'", tmpSet, _table, _lotID);
            return strSQL;
        }
        public string SelectTableADSData(string _resourceTable)
        {
            string strSQL = "";

            strSQL = string.Format(@"select a.lotid, a.customername, a.priority, a.wfr_qty, a.dies_qty, 'N' as carrier_asso, 'N' as equip_asso, '' as equiplist, 'INIT' as state,
                                            sysdate as create_dt, sysdate as lastmoify_dt from {0} a", _resourceTable);

            return strSQL;
        }
        public string SelectTableCarrierTransfer()
        {
            string strSQL = string.Format(@"select a.*,b.carrier_type as type,b.command_type, c.lot_id, d.custdevice, d.stage, d.equiplist from CARRIER_TRANSFER a
                                            left join CARRIER_TYPE_SET b on b.type_key=a.type_key
                                            left join CARRIER_LOT_ASSOCIATE c on c.carrier_id = a.carrier_id
                                            left join lot_info d on d.lotid=c.lot_id");
            return strSQL;
        }
        public string SelectTableCarrierTransferByCarrier(string CarrierID)
        {
            string strSQL = string.Format(@"select a.Carrier_Id, a.Carrier_State, a.Locate, a.portno, a.location_type, a.metal_ring, 
                                                b.carrier_type, b.command_type, c.lot_id, c.quantity, d.stage from CARRIER_TRANSFER a
                                            left join CARRIER_TYPE_SET b on b.type_key=a.type_key
                                            left join CARRIER_LOT_ASSOCIATE c on c.carrier_id=a.carrier_id
                                            left join LOT_INFO d on d.lotid = c.lot_id
                                            where Enable = '1' and a.carrier_id = '{0}'", CarrierID);
            return strSQL;
        }
        public string SelectTableCarrierType(string _lotID)

        {
            string strSQL = string.Format(@"select * from CARRIER_LOT_ASSOCIATE a
                                            left join CARRIER_TRANSFER b on b.carrier_id=a.carrier_id
                                            left join CARRIER_TYPE_SET c on c.type_key = b.type_key
                                            where b.carrier_state='ONLINE' and b.enable = 1 
                                                   and a.lot_id = '{0}'", _lotID.Trim());
            return strSQL;
        }
        public string SelectTableCarrierAssociateByLotid(string lotID)
        {
            string strSQL = string.Format(@"select a.Carrier_Id, a.Carrier_State, a.location_type, a.metal_ring, a.reserve, a.enable, a.lastModify_dt,
                                            b.carrier_type, b.command_type, c.lot_id, c.quantity, c.update_time from CARRIER_TRANSFER a
                                            left join CARRIER_TYPE_SET b on b.type_key=a.type_key
                                            left join CARRIER_LOT_ASSOCIATE c on c.carrier_id=a.carrier_id
                                            where a.enable = 1 and c.lot_id = '{0}'", lotID.Trim());
            return strSQL;
        }
        public string SelectTableCarrierAssociate2ByLotid(string lotID)
        {
            string strSQL = string.Format("select * from CARRIER_LOT_ASSOCIATE a left join CARRIER_TRANSFER b on b.carrier_id=a.carrier_id where a.lot_id = '{0}'", lotID);
            return strSQL;
        }
        public string SelectTableCarrierAssociate3ByLotid(string lotID)
        {
            string strSQL = string.Format("select a.carrier_id from CARRIER_LOT_ASSOCIATE a left join CARRIER_TRANSFER b on b.carrier_id=a.carrier_id where(a.lot_id is null and a.last_lot_id = '{0}') or (a.lot_id is not null and a.lot_id = '{0}')", lotID);
            return strSQL;
        }
        public string GetCarrierByLocate(string _locate, int _number)
        {
            string strSQL = string.Format(@"select carrier_id from CARRIER_TRANSFER where locate = '{0}' and portno = {1}", _locate, _number);
            return strSQL;
        }
        public string QueryCarrierByLot(string lotID)
        {
            string strSQL = string.Format(@"select a.carrier_id from CARRIER_TRANSFER a
                                            left join CARRIER_LOT_ASSOCIATE b on b.carrier_id=a.carrier_id
                                            where b.lot_id = '{0}' order by a.carrier_id", lotID);
            return strSQL;
        }
        public string QueryCarrierByLocate(string locate, string _table)
        {
            string strSQL = string.Format(@"select a.carrier_id, b.lot_id from CARRIER_TRANSFER a
                                            left join CARRIER_LOT_ASSOCIATE b on b.carrier_id=a.carrier_id
                                            left join {0} d on d.lotid=b.lot_id
                                            left join lot_info e on e.lotid=b.lot_id
                                            where a.locate = '{1}' order by d.potd asc, e.sch_seq asc, a.carrier_id", _table, locate);
            return strSQL;
        }
        public string QueryCarrierByLocateType(string locateType, string eqpId, string _table)
        {
            string strSQL = string.Format(@"select a.carrier_id, b.lot_id from CARRIER_TRANSFER a
                                            left join CARRIER_LOT_ASSOCIATE b on b.carrier_id=a.carrier_id
                                            left join lot_info c on c.lotid = b.lot_id and c.rtd_state = 'READY'
                                            left join {0} d on d.lotid=c.lotid
                                            where a.location_type = '{1}' and b.lot_id is not null and instr(c.equiplist, '{2}') > 0
                                            order by d.potd asc, a.carrier_id", _table, locateType, eqpId);
            return strSQL;
        }
        public string QueryEquipmentByLot(string lotID)
        {
            string strSQL = string.Format("select equiplist from LOT_INFO where lotid = '{0}'", lotID);
            return strSQL;
        }
        public string QueryEquipmentPortIdByEquip(string equipId)
        {
            string strSQL = string.Format(@"select b.port_id from EQP_STATUS a 
                                            left join EQP_PORT_SET b on b.equipid = a.equipid
                                            where a.equipid = '{0}'", equipId);
            return strSQL;
        }
        public string QueryReworkEquipment()
        {
            string strSQL = string.Format(@"select b.port_id from EQP_STATUS a 
                                            left join EQP_PORT_SET b on b.equipid = a.equipid
                                            where a.manualmode = 1 and b.port_id is not null");
            return strSQL;
        }
        public string QuerySemiAutoEquipmentPortId(bool _semiAuto)
        {
            int _auto = 0;
            if (_semiAuto)
                _auto = 1;
            else
                _auto = 0;

            string strSQL = string.Format(@"select b.port_id from EQP_STATUS a 
                                            left join EQP_PORT_SET b on b.equipid = a.equipid
                                            where a.automode = {0} and b.port_id is not null", _auto);
            return strSQL;
        }
        public string QueryEquipmentPortInfoByEquip(string equipId)
        {
            string strSQL = string.Format(@"select distinct a.equipid, b.port_seq, b.port_id, b.port_state, b.port_type, b.carrier_type, c.description, b.disable from eqp_status a
                                            left join EQP_PORT_SET b on b.equipid = a.equipid
                                            left join RTD_DEFAULT_SET c on c.paramvalue = b.port_state and c.parameter = 'EqpPortType'
                                            where a.equipid = '{0}'
                                            order by b.port_seq", equipId);
            return strSQL;
        }
        public string SelectTableWorkInProcessSch(string _table)
        {
            string strSQL = string.Format("select * from {0}", _table);
            return strSQL;
        }
        public string SelectTableWorkInProcessSchUnlock(string _table)
        {
            string strSQL = string.Format("select * from {0} where isLock = 0 ", _table);
            return strSQL;
        }
        public string SelectTableWorkInProcessSchByCmdId(string CommandID, string _table)
        {
            string strSQL = string.Format("select * from {0} where cmd_Id = '{1}' order by cmd_id, cmd_type", _table, CommandID);
            return strSQL;
        }
        public string QueryRunningWorkInProcessSchByCmdId(string CommandID, string _table)
        {
            string strSQL = string.Format("select * from {0} where cmd_Id = '{1}' and cmd_current_state not in ('Running', 'Init')", _table, CommandID);
            return strSQL;
        }
        public string QueryInitWorkInProcessSchByCmdId(string CommandID, string _table)
        {
            string strSQL = string.Format("select * from {0} where cmd_Id = '{1}' and cmd_current_state = 'Initial'", _table, CommandID);
            return strSQL;
        }
        public string SelectTableWorkInProcessSchByLotId(string _lotid, string _table)
        {
            string strSQL = string.Format("select * from {0} where lotid = '{1}'", _table, _lotid);
            return strSQL;
        }
        public string SelectTableWorkInProcessSchByEquip(string _Equip, string _table)
        {
            string strSQL = string.Format("select * from {0} where EquipID = '{1}'", _table, _Equip);
            return strSQL;
        }
        public string SelectTableWorkInProcessSchByEquipPort(string _Equip, string _PortId, string _table)
        {
            string strSQL = string.Format("select * from {0} where EquipID = '{1}' and (Source = '{2}' or Dest = '{2}')", _table, _Equip, _PortId);
            return strSQL;
        }
        public string SelectTableWorkInProcessSchByPortId(string _PortId, string _table)
        {
            string strSQL = string.Format("select * from {0} where Dest = '{1}'", _table, _PortId);
            return strSQL;
        }
        public string SelectTableWorkInProcessSchByCarrier(string _CarrierId, string _table)
        {
            string strSQL = string.Format("select * from {0} where carrierid = '{1}'", _table, _CarrierId);
            return strSQL;
        }
        public string QueryWorkInProcessSchByPortIdForUnload(string _PortId, string _table)
        {
            string strSQL = string.Format("select * from {0} where Source = '{1}'", _table, _PortId);
            return strSQL;
        }
        public string SelectTableEQPStatusInfoByEquipID(string EquipId)
        {
            string strSQL = "";
#if DEBUG
            strSQL = string.Format("select * from EQP_STATUS a left join EQP_PORT_SET b on b.equipid=a.equipid where a.equipid = '{0}'", EquipId);
#else
            strSQL = string.Format("select * from EQP_STATUS a left join EQP_PORT_SET b on b.equipid=a.equipid where a.manualmode=0 and a.equipid = '{0}'", EquipId);
#endif
            return strSQL;
        }
        public string SelectTableEQP_STATUSByEquipId(string EquipId)
        {
            string strSQL = "";
#if DEBUG
            strSQL = string.Format("select a.*, b.*, c.*, nvl(d.Dt_Start, 'Null') DT_START, nvl(d.Dt_End, 'Null') DT_END, nvl(d.effective, 0) Effective, nvl(d.expired, 1) Expired, c.priority as prio from EQP_STATUS a left join EQP_PORT_SET b on b.equipid=a.equipid left join WORKGROUP_SET c on c.workgroup=a.workgroup left join EQP_RESERVE_TIME d on d.equipid = a.equipid where a.equipid = '{0}' and b.islock=0 order by b.port_seq asc", EquipId);
#else
            strSQL = string.Format("select a.*, b.*, c.*, nvl(d.Dt_Start, 'Null') DT_START, nvl(d.Dt_End, 'Null') DT_END, nvl(d.effective, 0) Effective, nvl(d.expired, 1) Expired, c.priority as prio from EQP_STATUS a left join EQP_PORT_SET b on b.equipid=a.equipid left join WORKGROUP_SET c on c.workgroup=a.workgroup left join EQP_RESERVE_TIME d on d.equipid = a.equipid where a.equipid = '{0}' and b.islock=0 order by b.port_seq asc", EquipId);
#endif
            return strSQL;
        }
        public string SelectTableEquipmentStatus([Optional] string Department)
        {
            string tmpWhere = Department is null ? "" : Department.Trim().Equals("") ? "" : string.Format(" where equip_dept = '{0}'", Department);
            string strSQL = string.Format(@"select a.*,b.dt_start,b.dt_end,nvl(b.effective, 0) as effective,nvl(b.expired, 1) as expired from EQP_STATUS a
                                        left join eqp_reserve_time b on b.equipid = a.equipid {0}", tmpWhere);
            return strSQL;
        }
        public string SelectEqpStatusWaittoUnload()
        {
            string strSQL = string.Format(@"select * from EQP_STATUS a 
                                            left join EQP_PORT_SET b on b.equipid=a.equipid 
                                            left join WORKGROUP_SET c on c.workgroup=a.workgroup 
                                            where a.manualmode = 0 and a.curr_status = 'UP' and b.port_state in (3, 5)");
            return strSQL;
        }
        public string SelectEqpStatusIsDownOutPortWaittoUnload()
        {
            string strSQL = string.Format(@"select * from EQP_STATUS a 
                                    left join EQP_PORT_SET b on b.equipid=a.equipid and b.port_type in ('OUT','IN','IO')
                                    left join WORKGROUP_SET c on c.workgroup=a.workgroup 
                                    where a.manualmode = 0 and a.curr_status = 'DOWN' and a.down_state = 'IDLE'  and b.port_state in (3, 5)");
            return strSQL;
        }
        public string SelectEqpStatusReadytoload()
        {
            //string strSQL = string.Format(@"select distinct a.*,b.*,c.* from EQP_STATUS a 
            //                                left join EQP_PORT_SET b on b.equipid=a.equipid and b.port_type = 'OUT'
            //                                left join WORKGROUP_SET c on c.workgroup=a.workgroup 
            //                                left join EQP_PORT_SET d on d.equipid=a.equipid and d.port_type = 'IN'
            //                                where a.curr_status = 'UP' and d.port_state not in (0) and b.port_state in (4)");
            //如果需要卡IN 不能為0時, 可用以下寫法
            //where a.curr_status = 'UP' and b.port_state in (4) and d.port_state not in (0) 

            string strSQL = string.Format(@"select a.equipid, b.port_model, b.port_id, b.port_seq, b.port_type, b.port_state from EQP_STATUS a 
                                            left join EQP_PORT_SET b on b.equipid=a.equipid and b.port_type in ('IO','IN','OUT')
                                            left join WORKGROUP_SET c on c.workgroup=a.workgroup 
                                            where a.manualmode = 0 and a.curr_status = 'UP' and b.port_state in (4) and b.islock = 0
                                            union                                            
                                            select a.equipid, b.port_model, b.port_id, b.port_seq, b.port_type, b.port_state from EQP_STATUS a 
                                            left join EQP_PORT_SET b on b.equipid=a.equipid and b.port_type in ('IO','IN','OUT')
                                            left join WORKGROUP_SET c on c.workgroup=a.workgroup 
                                            where a.manualmode = 0 and a.curr_status = 'DOWN' and a.down_state = 'IDLE' and b.port_state in (4) and b.islock = 0
                                            order by port_seq, port_id");
            return strSQL;
        }
        public string SelectTableEquipmentPortsInfoByEquipId(string EquipId)
        {
            string strSQL = string.Format("select a.*, nvl(c.manualmode, 0) manualmode, nvl(b.Dt_Start, 'Null') DT_START, nvl(b.Dt_End, 'Null') DT_END, nvl(b.effective, 0) Effective, nvl(b.expired, 1) Expired from EQP_PORT_SET a left join EQP_RESERVE_TIME b on b.equipid = a.equipid left join EQP_STATUS c on c.equipid=a.equipid where a.EquipId = '{0}'", EquipId);
            return strSQL;
        }
        public string SelectTableEQP_Port_SetByEquipId(string EquipId)
        {
            string strSQL = string.Format("select * from EQP_PORT_SET where EquipId = '{0}' ", EquipId);
            return strSQL;
        }
        public string SelectLoadPortCarrierByEquipId(string EquipId)
        {
            string strSQL = string.Format("select * from CARRIER_TRANSFER a left join CARRIER_TYPE_SET b on b.type_key = a.type_key where a.locate = '{0}' ", EquipId);
            return strSQL;
        }
        public string SelectTableEQP_Port_SetByPortId(string EquipId)
        {
            string strSQL = string.Format("select * from EQP_PORT_SET where Port_ID = '{0}' ", EquipId);
            return strSQL;
        }
        public string SelectTableCheckLotInfo(string _resourceTable)
        {
            string strSQL = "";

            strSQL = string.Format(@"select distinct * from (
                          select a.lotid, case when b.lotid is null then 'New' else case when a.stage=b.stage then b.rtd_state else 'NEXT' end end as state, 
                          case when b.rtd_state is null then 'NONE' else b.rtd_state end as oriState, b.lastModify_dt from {0} a 
                          left join LOT_INFO b on b.lotid=a.lotid) where oriState  not in ('HOLD')
                          order by lotid", _resourceTable);

            return strSQL;
        }
        public string SelectTableCheckLotInfoNoData(string _resourceTable)
        {
            string strSQL = "";

            strSQL = string.Format(@"select a.lotid, 'Remove' as state, a.rtd_state as oriState from lot_info a 
                        where a.lotid not in (select lotid from {0})
                            and a.rtd_state not in ('COMPLETED', 'DELETED')", _resourceTable);

            return strSQL;
        }
        public string QueryERackInfo()
        {
            string strSQL = @"select * from rack where status = 'up'";
            return strSQL;
        }
        public string SelectTableLotInfo()
        {
            string strSQL = @"select * from LOT_INFO where rtd_state not in ('DELETED') 
                                and to_date(to_char(trunc(starttime, 'DD'),'yyyy/MM/dd HH24:mi:ss'), 'yyyy/MM/dd HH24:mi:ss') <= sysdate
                                order by stage, rtd_state, sch_seq desc";
            return strSQL;
        }
        public string SelectTableLotInfoByDept(string Dept)
        {
            string tmpAnd = Dept is null ? "" : Dept.Trim().Equals("") ? "" : string.Format(" and d.equip_dept = '{0}'", Dept);
            string strSQL = string.Format(@"select c.*, d.equip_dept, ct.carrier_state from LOT_INFO c
                                            left join (select distinct a.stage, b.equip_dept from PROMIS_STAGE_EQUIP_MATRIX a
                                            left join EQP_STATUS b on b.equipid=a.eqpid) d on d.stage=c.stage
left join carrier_lot_associate ca on ca.lot_id=c.lotid
left join carrier_transfer ct on ct.carrier_id=ca.carrier_id
                                            where c.rtd_state not in ('DELETED') 
                                            and to_date(to_char(trunc(c.starttime, 'DD'),'yyyy/MM/dd HH24:mi:ss'), 'yyyy/MM/dd HH24:mi:ss') <= sysdate
                                            {0}
                                            order by c.stage, c.rtd_state, c.sch_seq desc", tmpAnd);
            return strSQL;
        }
        public string SelectTableProcessLotInfo()
        {
            string strSQL = @"select * from LOT_INFO where rtd_state not in ('INIT','HOLD','PROC','DELETED', 'COMPLETED') 
                            and to_date(to_char(trunc(starttime, 'DD'),'yyyy/MM/dd HH24:mi:ss'), 'yyyy/MM/dd HH24:mi:ss') <= sysdate 
                            order by stage, rtd_state, lot_age desc";
            return strSQL;
        }
        public string ReflushProcessLotInfo(string _table)
        {
            string strSQL = string.Format(@"select * from LOT_INFO a left join carrier_lot_associate b on b.lot_id = a.lotid left join carrier_transfer c on c.carrier_id = b.carrier_id left join {0} c1 on c1.lotid=b.lot_id  where a.rtd_state not in ('HOLD','PROC','DELETED', 'COMPLETED') 
                            and to_date(to_char(trunc(a.starttime, 'DD'),'yyyy/MM/dd HH24:mi:ss'), 'yyyy/MM/dd HH24:mi:ss') <= sysdate
                            and c.location_type in ('ERACK','STK')
                            order by a.stage, a.rtd_state, c1.potd asc, a.lot_age desc, a.priority asc", _table);
            return strSQL;
        }
        public string SelectTableProcessLotInfoByCustomer(string _customerName, string _equip)
        {
            string strSQL = string.Format(@"select * from LOT_INFO where rtd_state not in ('HOLD','PROC','DELETED', 'COMPLETED') and state not in ('HOLD')
                            and to_date(to_char(trunc(starttime, 'DD'),'yyyy/MM/dd HH24:mi:ss'), 'yyyy/MM/dd HH24:mi:ss') <= sysdate 
                            and customername = '{0}'  and equiplist is not null and equiplist like '%{1}%' order by stage, sch_seq", _customerName, _equip);
            return strSQL;
        }
        public string QueryLastModifyDT()
        {
            string strSQL = @"select max(lastmodify_dt) as lastmodify from LOT_INFO";
            return strSQL;
        }
        public string SelectTableLotInfoByLotid(string LotId)
        {
            string strSQL = string.Format("select * from LOT_INFO where lotid = '{0}'", LotId);
            return strSQL;  //select * from PROMIS_STAGE_EQUIP_MATRIX where eqpid = 'CTWT-01' and stage = 'B049TAPING';
        }
        public string SelectTableLotInfoOfInit()
        {
            string strSQL = @"select * from LOT_INFO a where a.rtd_state = 'INIT'";
            return strSQL;
        }
        public string SelectTableLotInfoOfWait()
        {
            string strSQL = @"select * from LOT_INFO a where a.rtd_state = 'WAIT'";
            return strSQL;
        }
        public string SelectTableLotInfoOfReady()
        {
            string strSQL = @"select a.* from LOT_INFO a
                            left join carrier_lot_associate b on b.lot_id = a.lotid
                            left join carrier_transfer c on c.carrier_id = b.carrier_id
                            where a.rtd_state = 'READY' and c.carrier_state = 'ONLINE' and equiplist is not null 
                            and isLock=0 order by a.priority, a.sch_seq asc";
            return strSQL;
        }
        public string SelectTableEQUIP_MATRIX(string EqpId, string StageCode)
        {
            string strSQL = string.Format("select eqpid from PROMIS_STAGE_EQUIP_MATRIX where eqpid = '{0}' and stage = '{1}'", EqpId, StageCode);
            return strSQL;
        }
        public string ShowTableEQUIP_MATRIX()
        {
            string strSQL = "select * from PROMIS_STAGE_EQUIP_MATRIX";
            return strSQL;
        }
        public string SelectPrefmap(string EqpId)
        {
            string strSQL = string.Format("select * from EQP_CUST_PREF_MAP where eqpid = '{0}'", EqpId);
            return strSQL;
        }
        public string SelectTableEquipmentStatusInfo()
        {
            string strSQL = "";
#if DEBUG
            strSQL = @"select * from EQP_STATUS_INFO";
#else
            strSQL = @"select * from EQP_STATUS_INFO";
#endif
            return strSQL;
        }
        public string SelectEquipmentPortInfo()
        {
            string strSQL = "";
#if DEBUG
            strSQL = @"select * from (
                        select distinct a.equipid, b.port_seq, b.port_id from EQP_STATUS a
                        left join EQP_PORT_SET b on b.equipid = a.equipid where b.port_seq is not null
                        ) order by equipid, port_seq";
#else
            strSQL = @"select * from (
                        select distinct a.equipid, b.port_seq, b.port_id from EQP_STATUS a
                        left join EQP_PORT_SET b on b.equipid = a.equipid where b.port_seq is not null
                        ) order by equipid, port_seq";
#endif
            return strSQL;
        }
        public string SelectCarrierAssociateIsTrue()
        {
            string strSQL = @"select * from LOT_INFO a where a.carrier_asso = 'Y'";
            return strSQL;
        }
        public string SelectRTDDefaultSet(string _parameter)
        {
            string strCondition = _parameter.Equals("") ? "" : string.Format(" where parameter = '{0}'", _parameter);
            string strSQL = string.Format(@"select Parameter, ParamType, ParamValue, description from RTD_DEFAULT_SET{0}", strCondition);
            return strSQL;
        }
        public string SelectRTDDefaultSetByType(string _parameter)
        {
            string strCondition = _parameter.Equals("") ? "" : string.Format(" where ParamType = '{0}'", _parameter);
            string strSQL = string.Format(@"select Parameter, ParamType, ParamValue, description from RTD_DEFAULT_SET{0}", strCondition);
            return strSQL;
        }
        public string UpdateTableWorkInProcessSchByCmdId(string _cmd_Current_State, string _lastModify_DT, string _commandID, string _table)
        {
            if (_lastModify_DT.Equals(""))
                _lastModify_DT = DateTime.Now.ToString("yyyy-MM-d HH:mm:ss");

            string tmpString = "update {0} set Cmd_State = '{1}', Cmd_Current_State = '{1}', LastModify_DT = TO_DATE(\'{2}\', \'yyyy-MM-dd hh24:mi:ss\') " +
                "where cmd_id = '{3}'";
            string strSQL = string.Format(tmpString, _table, _cmd_Current_State, _lastModify_DT, _commandID);
            return strSQL;
        }
        public string UpdateTableWorkInProcessSchByUId(string _updateState, string _lastModify_DT, string _UID)
        {
            string tmpString = "";
            string strSQL = "";

            switch (_updateState)
            {
                case "Initial":
                    tmpString = "update WORKINPROCESS_SCH set Cmd_Current_State = 'Initial', initial_DT = TO_DATE(\'{0}\', \'yyyy-mm-dd hh:mi:ss\'), LastModify_DT = TO_DATE(\'{0}\', \'yyyy-mm-dd hh:mi:ss\') " +
                            "where UID = '{1}'";
                    strSQL = string.Format(tmpString, _lastModify_DT, _UID);
                    break;
                case "Wait":
                    tmpString = "update WORKINPROCESS_SCH set Cmd_Current_State = 'Wait', WaitingQueue_DT = TO_DATE(\'{0}\', \'yyyy-mm-dd hh:mi:ss\'), LastModify_DT = TO_DATE(\'{0}\', \'yyyy-mm-dd hh:mi:ss\') " +
                            "where UID = '{1}'";
                    strSQL = string.Format(tmpString, _lastModify_DT, _UID);
                    break;
                case "Dispatch":
                    tmpString = "update WORKINPROCESS_SCH set Cmd_Current_State = 'Dispatch', ExecuteQueue_DT = TO_DATE(\'{0}\', \'yyyy-mm-dd hh:mi:ss\'), LastModify_DT = TO_DATE(\'{0}\', \'yyyy-mm-dd hh:mi:ss\') " +
                            "where UID = '{1}'";
                    strSQL = string.Format(tmpString, _lastModify_DT, _UID);
                    break;
                default:
                    tmpString = "update WORKINPROCESS_SCH set Cmd_Current_State = '{0}', LastModify_DT = TO_DATE(\'{1}\', \'yyyy-mm-dd hh:mi:ss\') " +
                            "where UID = '{2}'";
                    strSQL = string.Format(tmpString, _updateState, _lastModify_DT, _UID);
                    break;
            }

            return strSQL;
        }
        public string UpdateTableWorkInProcessSchHisByCmdId(string _commandID)
        {
            string tmpString = "insert into WORKINPROCESS_SCH_HIS select * from WORKINPROCESS_SCH where cmd_id = '{0}'";
            string strSQL = string.Format(tmpString, _commandID);
            return strSQL;
        }
        public string UpdateTableWorkInProcessSchHisByUId(string _uid)
        {
            string tmpString = "insert into WORKINPROCESS_SCH_HIS select * from WORKINPROCESS_SCH where uuid = '{0}'";
            string strSQL = string.Format(tmpString, _uid);
            return strSQL;
        }
        public string UpdateTableCarrierTransfer(CarrierLocationUpdate oCarrierLoc)
        {
            string strCarrier = oCarrierLoc.CarrierID;
            string strLocation = oCarrierLoc.Location;
            string modifyTime = "";

            string tmpString = "update CARRIER_TRANSFER set Locate = '{0}', Modify_DT = sysdate, " +
                            "LastModify_DT = sysdate " +
                            "where CARRIER_ID = '{1}'";
            string strSQL = string.Format(tmpString, strLocation, strCarrier);
            return strSQL;
        }
        public string UpdateTableCarrierTransferByCarrier(string CarrierId, string State)
        {
            string tmpString = "update CARRIER_TRANSFER set State = '{0}', Modify_DT = sysdate, " +
                            "LastModify_DT = sysdate " +
                            "where CARRIER_ID = '{1}'";
            string strSQL = string.Format(tmpString, State, CarrierId);
            return strSQL;
        }
        public string UpdateTableReserveCarrier(string _carrierID, bool _reserve)
        {
            int iReserve = 0;
            if (_reserve)
                iReserve = 1;
            else
                iReserve = 0;

            string tmpString = "update CARRIER_TRANSFER set reserve = {0}, Modify_DT = sysdate, " +
                                "LastModify_DT = sysdate " +
                                "where CARRIER_ID = '{1}'";
            string strSQL = string.Format(tmpString, iReserve.ToString(), _carrierID);
            return strSQL;
        }
        public string UpdateTableCarrierTransfer(CarrierLocationUpdate oCarrierLoc, int _haveMetalRing)
        {
            string strCarrier = oCarrierLoc.CarrierID.Trim();
            string strLocationType = oCarrierLoc.LocationType;
            string strLocate = "";
            string strPort = "1";
            int iRackRows = 0;
            int iRackCols = 0;
            int iPorts = 0;

            if (strLocationType.Equals("STK"))
            {
                //STK naming rule is A01-19, ZSTK is Shelf1.1
                string tmpPos = "";
                if (oCarrierLoc.Location.IndexOf('-') > 0)
                {
                    tmpPos = oCarrierLoc.Location.Trim().Substring(oCarrierLoc.Location.Length - 5 , 5);
                    iRackRows = int.Parse(tmpPos.Split('-')[0]);
                    iRackCols = int.Parse(tmpPos.Split('-')[1]);

                    iPorts = (iRackRows * 19) + iRackCols;

                    strLocate = oCarrierLoc.Location.Trim().Split('-')[0];
                    strPort = iRackCols.ToString();
                }
                else if (oCarrierLoc.Location.IndexOf(',') > 0)
                {
                    tmpPos = oCarrierLoc.Location.Trim().Replace("Shelf", "");
                    iRackRows = int.Parse(tmpPos.Split(',')[0]);
                    iRackCols = int.Parse(tmpPos.Split(',')[1]);

                    iPorts = (iRackRows * 19) + iRackCols;

                    strLocate = oCarrierLoc.Location.Trim().Split(',')[0];
                    strPort = iRackCols.ToString();
                }
            }
            else if (strLocationType.Equals("MR"))
            {
                //AMR BUFFER
                if (oCarrierLoc.Location.Contains("BUF"))
                {
                    strLocate = oCarrierLoc.Zone;
                    strPort = oCarrierLoc.Location.Equals("") ? "0" : oCarrierLoc.Location.Split("BUF")[1].ToString();
                }
                else
                {
                    strLocate = oCarrierLoc.Zone;
                    strPort = oCarrierLoc.Location.Equals("") ? "0" : oCarrierLoc.Location.Split("_LP")[1].ToString();
                }
            }
            else
            {
                //EQP, ERACK naming rule is _LP
                if (oCarrierLoc.Location.Contains("_LP"))
                {
                    strLocate = oCarrierLoc.Location.Split("_LP")[0].ToString();
                    strPort = oCarrierLoc.Location.Equals("") ? "0" : oCarrierLoc.Location.Split("_LP")[1].ToString();
                }
                else
                {
                    if (strLocate.Equals("A") || strLocate.Equals("B"))
                        strLocate = "";
                    else
                        strLocate = oCarrierLoc.Zone;

                    strPort = oCarrierLoc.Location.Equals("") ? "0" : oCarrierLoc.Location.Split("_LP")[1].ToString();
                }
            }
            string strLocation = "";
            if (strLocate.Equals(""))
            {
                if (strLocationType.Equals("STK"))
                    strLocate = "";
                else
                    strLocate = oCarrierLoc.Zone is null ? "" : oCarrierLoc.Zone.Trim();
            }
            
            string modifyTime = "";
            string sState = "";
            if (!strLocate.Equals(""))
                sState = "ONLINE";
            else
                sState = "OFFLINE";

            string strSQL = "";
            string tmpString = "update CARRIER_TRANSFER set Carrier_State = '{0}', Locate = '{1}', PortNo = {2}, Rack_Rows = {6}, Rack_Cols = {7}, LOCATION_TYPE = '{3}', METAL_RING = '{4}', RESERVE = 0, Modify_DT = sysdate, " +
                            "LastModify_DT = sysdate " +
                            "where CARRIER_ID = '{5}'";
            strSQL = string.Format(tmpString, sState, strLocate, int.Parse(strPort).ToString(), strLocationType, _haveMetalRing, strCarrier, iRackRows, iRackCols);

            return strSQL;
        }
        public string CarrierLocateReset(CarrierLocationUpdate oCarrierLoc, int _haveMetalRing)
        {
            string strCarrier = oCarrierLoc.CarrierID.Trim();
            string strLocationType = oCarrierLoc.LocationType;
            string strLocate = "";
            string strPort = "1";
            int iRackRows = 0;
            int iRackCols = 0;

            if (strLocationType.Equals("STK"))
            {
                //STK naming rule is A01-19, ZSTK is Shelf1.1 >>> Shelf1,1 . change ,
                string tmpPos = "";
                if (oCarrierLoc.Location.IndexOf('-') > 0)
                {
                    tmpPos = oCarrierLoc.Location.Trim().Substring(oCarrierLoc.Location.Length - 5, 5);
                    iRackRows = int.Parse(tmpPos.Split('-')[0]);
                    iRackCols = int.Parse(tmpPos.Split('-')[1]);
                }
                else if(oCarrierLoc.Location.IndexOf(',') > 0)
                {
                    tmpPos = oCarrierLoc.Location.Trim().Replace("Shelf","");
                    iRackRows = int.Parse(tmpPos.Split(',')[0]);
                    iRackCols = int.Parse(tmpPos.Split(',')[1]);
                }
            }
            else
            {
                //EQP, ERACK naming rule is _LP
                if (oCarrierLoc.Location.Contains("_LP"))
                {
                    strLocate = oCarrierLoc.Location.Split("_LP")[0].ToString();
                    strPort = oCarrierLoc.Location.Split("_LP")[1].ToString();
                }
            }
            string strLocation = "";
            //if (strLocate.Equals(""))
            //{
                //strLocate = oCarrierLoc.Zone.Trim();
            //}
            
            string modifyTime = "";
            string sState = "";

            sState = "OFFLINE";

            string tmpString = "update CARRIER_TRANSFER set Carrier_State = '{0}', Locate = '', PortNo = 0, Rack_Rows = 0, Rack_Cols = 0, LOCATION_TYPE = '', METAL_RING = 0, RESERVE = 0, Modify_DT = sysdate, " +
                            "LastModify_DT = sysdate " +
                            "where Locate = '{1}' and PortNo = {2}";
            string strSQL = string.Format(tmpString, sState, strLocate, int.Parse(strPort).ToString(), strLocationType, _haveMetalRing, strCarrier);
            return strSQL;
        }
        public string UpdateTableEQP_STATUS(string EquipId, int CurrentStatus, string MachineState, string DownState)
        {
            //Machine_state, Curr_Status, Down_State
            string state = GetEquipState(CurrentStatus);
            string strSQL = string.Format("update EQP_STATUS set Curr_Status = '{0}', Machine_state = '{1}', Down_State = '{2}', Modify_dt = sysdate, lastModify_dt = sysdate where EquipId = '{3}'", state, MachineState, DownState, EquipId);
            return strSQL;
        }
        public string UpdateTableEQP_Port_Set(string EquipId, string PortSeq, string NewStatus)
        {
            string strSQL = string.Format("update EQP_PORT_SET set Port_State = {0}, Modify_dt = sysdate, lastModify_dt = sysdate where EquipId = '{1}' and Port_Seq = '{2}' ", NewStatus, EquipId, PortSeq );
            return strSQL;
        }
        public string UpdateTableEQP_Port_Set(string EquipId, string PortSeq)
        {
            string strSQL = string.Format("update EQP_PORT_SET set lastModify_dt = sysdate where EquipId = '{0}' and Port_Seq = '{1}' ", EquipId, PortSeq);
            return strSQL;
        }
        public string UpdateTableLotInfoReset(string LotID)
        {
            string strSQL = string.Format(@"update LOT_INFO set carrier_asso = 'N', equip_asso = 'N', equiplist = '', sch_seq = 0, 
                                               rtd_state = 'INIT' where lotid = '{0}'", LotID);
            //rtd_state = 'INIT', lastmodify_dt = sysdate where lotid = '{0}'", LotID);
            return strSQL;
        }
        public string UpdateTableLotInfoEquipmentList(string _lotID, string _lstEquipment)
        {
            string strSQL = string.Format(@"update LOT_INFO set equip_asso = 'Y', equiplist = '{0}', 
                                               rtd_state = 'WAIT', lastmodify_dt = sysdate where lotid = '{1}'", _lstEquipment, _lotID);
            //rtd_state = 'WAIT', lastmodify_dt = sysdate where lotid = '{1}'", _lstEquipment, _lotID);
            return strSQL;
        }
        public string UpdateTableLotInfoState(string LotID, string State)
        {
            string strSQL = string.Format(@"update LOT_INFO set rtd_state = '{0}', sch_seq = 0 where lotid = '{1}'", State, LotID);
            //string strSQL = string.Format(@"update LOT_INFO set rtd_state = '{0}', sch_seq = 0, lastmodify_dt = sysdate where lotid = '{1}'", State, LotID);
            return strSQL;
        }
        public string UpdateTableLastModifyByLot(string LotID)
        {
            string strSQL = string.Format(@"update LOT_INFO set lastmodify_dt = sysdate where lotid = '{0}'", LotID);
            return strSQL;
        }
        public string UpdateTableLotInfoSetCarrierAssociateByLotid(string LotID)
        {
            //string strSQL = string.Format(@"update LOT_INFO set carrier_asso = 'Y', rtd_state= 'WAIT', lastmodify_dt = sysdate where lotid = '{0}'", LotID);
            string strSQL = string.Format(@"update LOT_INFO set carrier_asso = 'Y', rtd_state= 'WAIT' where lotid = '{0}'", LotID);
            return strSQL;
        }
        public string UpdateTableLotInfoToReadyByLotid(string LotID)
        {
            string strSQL = string.Format(@"update LOT_INFO set rtd_state= 'READY', lastmodify_dt = sysdate where lotid = '{0}'", LotID);
            return strSQL;
        }
        public string UpdateLotInfoSchSeqByLotid(string LotID, int _SchSeq)
        {
            //string strSQL = string.Format(@"update LOT_INFO set sch_seq = {0}, lastmodify_dt = sysdate where lotid = '{1}'", _SchSeq, LotID);
            string strSQL = string.Format(@"update LOT_INFO set sch_seq = {0} where lotid = '{1}'", _SchSeq, LotID);
            return strSQL;
        }
        public string UpdateTableLotInfoSetCarrierAssociate2ByLotid(string LotID)
        {
            string strSQL = string.Format(@"update LOT_INFO set carrier_asso = 'N', rtd_state= 'WAIT' where lotid = '{0}'", LotID);
            //string strSQL = string.Format(@"update LOT_INFO set carrier_asso = 'N', rtd_state= 'WAIT', lastmodify_dt = sysdate where lotid = '{0}'", LotID);
            return strSQL;
        }
        public string UpdateTableRTDDefaultSet(string _parameter, string _paramvalue, string _modifyBy)
        {
            string strSQL = string.Format(@"update rtd_default_set set paramvalue = '{0}', modifyBy = '{1}', lastModify_DT = sysdate where parameter = '{2}'", _paramvalue, _modifyBy, _parameter);
            return strSQL;
        }
        public string GetLoadPortCurrentState(string _equipId)
        {
            string strSQL = string.Format(@"select a.equipid, a.port_model, a.port_seq, a.port_type, a.port_id, a.carrier_type, a.port_state, b.carrier_id, a.lastlotid as lot_id, d.customername from eqp_port_set a
                                            left join CARRIER_TRANSFER b on b.locate = a.equipid and b.portno = a.port_seq
                                            left join CARRIER_LOT_ASSOCIATE c on c.carrier_id = b.carrier_id
                                            left join LOT_INFO d on d.lotid = a.lastlotid
                                            where a.equipid = '{0}' and a.port_type in ('IN', 'IO')", _equipId);
            return strSQL;
        }
        public string UpdateSchSeq(string _Customer, string _Stage, int _SchSeq, int _oriSeq)
        {
            string strSQL = "";

            if (_SchSeq == 0 && _oriSeq > _SchSeq)
            {
                strSQL = string.Format(@"update LOT_INFO set sch_seq = sch_seq - 1 where customername = '{0}' and stage = '{1}'
                                            and rtd_state not in ('HOLD','PROC','DELETED', 'COMPLETED') 
                                            and to_date(to_char(trunc(starttime, 'DD'),'yyyy/MM/dd HH24:mi:ss'), 'yyyy/MM/dd HH24:mi:ss') <= sysdate  
                                            and sch_seq > {2}", _Customer, _Stage, _oriSeq);
            }
            else if (_oriSeq - _SchSeq > 0)
            {
                strSQL = string.Format(@"update LOT_INFO set sch_seq = sch_seq + 1 where customername = '{0}' and stage = '{1}'
                                            and rtd_state not in ('HOLD','PROC','DELETED', 'COMPLETED') 
                                            and to_date(to_char(trunc(starttime, 'DD'),'yyyy/MM/dd HH24:mi:ss'), 'yyyy/MM/dd HH24:mi:ss') <= sysdate  
                                            and sch_seq >= {2} and sch_seq < {3}", _Customer, _Stage, _SchSeq, _oriSeq);
            }
            else
            {
                strSQL = string.Format(@"update LOT_INFO set sch_seq = sch_seq - 1 where customername = '{0}' and stage = '{1}'
                                            and rtd_state not in ('HOLD','PROC','DELETED', 'COMPLETED') 
                                            and to_date(to_char(trunc(starttime, 'DD'),'yyyy/MM/dd HH24:mi:ss'), 'yyyy/MM/dd HH24:mi:ss') <= sysdate  
                                            and sch_seq > {2} and sch_seq <= {3}", _Customer, _Stage, _oriSeq, _SchSeq);
            }
            return strSQL;
        }
        public string UpdateSchSeqByLotId(string _lotId, string _Customer, int _SchSeq)
        {
            //string strSQL = string.Format(@"update lot_info set sch_seq = {0}, lastmodify_dt = sysdate 
            string strSQL = string.Format(@"update LOT_INFO set sch_seq = {0} 
                                    where customername = '{1}' and lotid = '{2}'", _SchSeq, _Customer, _lotId);
            return strSQL;
        }
        public string InsertSMSTriggerData(string _eqpid, string _stage, string _desc, string _flag, string _username)
        {
            string strSQL = string.Format(@"insert into RTD_SMS_TRIGGER_DATA
                                            (eqpid, stage, description, flag, entry_by, entry_time)
                                            values
                                            ('{0}', '{1}', '{2}', '{3}', '{4}', sysdate)", _eqpid, _stage, _desc, _flag, _username);
            return strSQL;
        }
        public string SchSeqReflush()
        {
            string strSQL = string.Format(@"select customername, stage, count(sch_seq) as iCount from (
                                                select * from lot_info where rtd_state not in ('HOLD','PROC','DELETED', 'COMPLETED') and carrier_asso = 'Y' 
                                                and to_date(to_char(trunc(starttime, 'DD'),'yyyy/MM/dd HH24:mi:ss'), 'yyyy/MM/dd HH24:mi:ss') <= sysdate )
                                            group by customername, stage");
            return strSQL;
        }
        public string LockLotInfo(bool _lock)
        {
            string strSQL = "";

            if (_lock)
            {
                strSQL = "update rtd_default_set set paramvalue = '1', lastmodify_dt = sysdate where parameter= 'LotInfo' and paramtype = 'IsLock'";
            }
            else
            {
                strSQL = "update rtd_default_set set paramvalue = '0', lastmodify_dt = sysdate where parameter= 'LotInfo' and paramtype = 'IsLock'";
            }

            return strSQL;
        }
        public string GetLockStateLotInfo()
        {
            string strSQL = "select paramvalue as lockstate from rtd_default_set where parameter= 'LotInfo' and paramtype = 'IsLock'";

            return strSQL;
        }
        public string ReflushWhenSeqZeroStateWait()
        {
            string strSQL = "select * from lot_info where rtd_state = 'WAIT' and Sch_seq = 0";

            return strSQL;
        }
        public string SyncNextStageOfLot(string _resourceTable, string _lotid)
        {
            string strSQL = "";

            strSQL = string.Format(@"update lot_info set (priority,carrier_asso,equip_asso,equiplist,lastmodify_dt,rtd_state,
                                        state,wfr_qty,dies_qty,stage,lottype,lot_age,planstarttime,starttime,lockmachine,comp_qty) 
                                        = (select priority, 'N', 'N', '', sysdate, 'INIT', state,wfr_qty,
                                        dies_qty,stage,lottype,lot_age,planstarttime,starttime,0,0 from {0} where lotid = '{1}')
                                        where lotid = '{1}'", _resourceTable, _lotid);

            return strSQL;
        }
        public string UpdateLotInfoWhenCOMP(string _commandId, string _table)
        {
            string strSQL = string.Format(@"update lot_info set RTD_state='COMPLETED', sch_seq=0, lastModify_dt=sysdate where lotid in (
                                            select distinct lotid from {0} where cmd_id = '{1}' and lotid <> ' ')", _table, _commandId);
            return strSQL;
        }
        public string UpdateLotInfoWhenFail(string _commandId, string _table)
        {
            string strSQL = string.Format(@"update lot_info set RTD_state='READY', sch_seq=0, lastModify_dt=sysdate where lotid in (
                                            select distinct lotid from {0} where cmd_id = '{1}' and lotid <> ' ')", _table, _commandId);
            return strSQL;
        }
        public string UpdateEquipCurrentStatus(string _current, string _equipid)
        {
            string strSQL = string.Format(@"update eqp_status set curr_status = '{0}', lastmodify_dt = sysdate where equipid = '{1}'", _current, _equipid);
            return strSQL;
        }
        public string QueryEquipmentStatusByEquip(string _equip)
        {
            string strSQL = string.Format("select a.workgroup, a.machine_state, a.curr_status, a.down_state, a.manualmode, a.lastModify_dt, nvl(b.Dt_Start, 'Null') as DT_START, nvl(b.Dt_End, 'Null') as Dt_End, nvl(b.effective, 0) as effective, nvl(b.expired, 1) as expired from eqp_status a left join EQP_RESERVE_TIME b on b.equipid = a.equipid where a.equipid = '{0}'", _equip);
            return strSQL;
        }
        public string QueryCarrierInfoByCarrierId(string _carrierId)
        {
            string strSQL = string.Format("select a.carrier_id, a.quantity, a.reserve, b.lot_id from carrier_transfer a left join CARRIER_LOT_ASSOCIATE b on b.carrier_id = a.carrier_id where a.carrier_id = '{0}'", _carrierId);
            return strSQL;
        }
        public string QueryCarrierType(string _carrierType, string _typeKey)
        {
            string strSQL = string.Format(@"select * from carrier_lot_associate a
                                        left join carrier_transfer b on b.carrier_id = a.carrier_id
                                        where a.carrier_type like '%{0}%' and b.type_key not in ('{1}')", _carrierType, _typeKey);
            return strSQL;
        }
        public string UpdateCarrierType(string _carrierType, string _typeKey)
        {
            string strSQL = string.Format(@"update carrier_transfer set type_key = '{1}' where carrier_id in (
                                    select a.carrier_id from carrier_lot_associate a
                                    left join carrier_transfer b on b.carrier_id=a.carrier_id
                                    where a.carrier_type like '%{0}%' and b.type_key not in ('{1}'))", _carrierType, _typeKey);
            return strSQL;
        }
        public string QueryWorkinProcessSchHis(string _startTime, string _endTime)
        {
            string tmpCdt = " where cmd_state in ('Success','Failed'){0}";
            string tmpTime = "";
            if (_startTime.Trim().Equals("") && _endTime.Trim().Equals(""))
                tmpTime = "";
            else if (!_startTime.Equals("") && _endTime.Equals(""))
                tmpTime = string.Format(" and create_dt > to_date('{0}', 'yyyy/MM/dd HH24:mi:ss')", _startTime);
            else if (_startTime.Equals("") && !_endTime.Equals(""))
                tmpTime = string.Format(" and create_dt < to_date('{0}', 'yyyy/MM/dd HH24:mi:ss')", _endTime);
            else if (!_startTime.Equals("") && !_endTime.Equals(""))
                tmpTime = string.Format(" and create_dt between to_date('{0}', 'yyyy/MM/dd HH24:mi:ss') and to_date('{1}', 'yyyy/MM/dd HH24:mi:ss')", _startTime, _endTime);

            tmpCdt = string.Format(" where cmd_state in ('Success','Failed'){0}", tmpTime);

            string strSQL = string.Format(@"select uuid, cmd_id, cmd_type, Equipid, cmd_state, carrierid, carrierType, Source, Dest, Priority, replace, back, lotid, customer, create_dt, modify_dt, lastmodify_dt from workinprocess_sch_his{0} order by create_dt", tmpCdt);

            return strSQL;
        }
        public string QueryStatisticalOfDispatch(DateTime dtCurrUTCTime, string _statisticalUnit, string _type)
        {
            string tmpCdt = " where {0}";
            string tmpGroupBy = " group by {0}";
            string tmpOrderBy = " order by {0}";
            string tmpColumns;
            string tmpCdtType = " and type in ({0})";
            string strSQL = "";

            try
            {

                if (_type.ToLower().Equals("total"))
                {
                    tmpCdtType = string.Format(tmpCdtType, "'T'");
                }
                else if (_type.ToLower().Equals("success"))
                {
                    tmpCdtType = string.Format(tmpCdtType, "'S'");
                }
                else if (_type.ToLower().Equals("failed"))
                {
                    tmpCdtType = string.Format(tmpCdtType, "'F'");
                }
                else
                {
                    tmpCdtType = "";
                }


                switch (_statisticalUnit.ToLower())
                {
                    case "years":
                        tmpColumns = " years, months, sum(times) as time, type ";
                        tmpCdt = string.Format(tmpCdt, string.Format("years = {0}{1}", dtCurrUTCTime.Year.ToString(), tmpCdtType));
                        tmpGroupBy = string.Format(tmpGroupBy, "years, months, type");
                        tmpOrderBy = string.Format(tmpOrderBy, "years");
                        break;
                    case "months":
                        tmpColumns = " years, months, days, sum(times) as time, type ";
                        tmpCdt = string.Format(tmpCdt, string.Format("years = {0} and months = {1}{2}", dtCurrUTCTime.Year.ToString(), dtCurrUTCTime.Month.ToString(), tmpCdtType));
                        tmpGroupBy = string.Format(tmpGroupBy, "years, months, days, type");
                        tmpOrderBy = string.Format(tmpOrderBy, "years, months");
                        break;
                    case "days":
                        tmpColumns = " years, months, days, hours, sum(times) as time, type ";
                        tmpCdt = string.Format(tmpCdt, string.Format("years = {0} and months = {1} and days = {2}{3}", dtCurrUTCTime.Year.ToString(), dtCurrUTCTime.Month.ToString(), dtCurrUTCTime.Day.ToString(), tmpCdtType));
                        tmpGroupBy = string.Format(tmpGroupBy, "years, months, days, hours, type");
                        tmpOrderBy = string.Format(tmpOrderBy, "years, months, days");
                        break;
                    case "shift":
                        tmpColumns = " years, months, days, sum(times) as time ";
                        tmpCdt = string.Format(tmpCdt, string.Format("years = {0} and months = {1} and days = {2}{3}", dtCurrUTCTime.Year.ToString(), dtCurrUTCTime.Month.ToString(), dtCurrUTCTime.Day.ToString(), tmpCdtType));
                        tmpGroupBy = string.Format(tmpGroupBy, "years, months, days");
                        tmpOrderBy = string.Format(tmpOrderBy, "years, months, days");
                        break;
                    case "year":
                        tmpColumns = " years, sum(times) as time ";
                        tmpCdt = string.Format(tmpCdt, string.Format("years = {0}{1}", dtCurrUTCTime.Year.ToString(), tmpCdtType));
                        tmpGroupBy = string.Format(tmpGroupBy, "years");
                        tmpOrderBy = string.Format(tmpOrderBy, "years");
                        break;
                    case "month":
                        tmpColumns = " years, months, sum(times) as time ";
                        tmpCdt = string.Format(tmpCdt, string.Format("years = {0} and months = {1}{2}", dtCurrUTCTime.Year.ToString(), dtCurrUTCTime.Month.ToString(), tmpCdtType));
                        tmpGroupBy = string.Format(tmpGroupBy, "years, months");
                        tmpOrderBy = string.Format(tmpOrderBy, "years, months");
                        break;
                    case "day":
                        tmpColumns = " years, months, days, sum(times) as time ";
                        tmpCdt = string.Format(tmpCdt, string.Format("years = {0} and months = {1} and days = {2}{3}", dtCurrUTCTime.Year.ToString(), dtCurrUTCTime.Month.ToString(), dtCurrUTCTime.Day.ToString(), tmpCdtType));
                        tmpGroupBy = string.Format(tmpGroupBy, "years, months, days");
                        tmpOrderBy = string.Format(tmpOrderBy, "years, months, days");
                        break;
                    default:
                        tmpColumns = " * ";
                        break;
                }

                strSQL = string.Format(@"select {0} from RTD_Statistical{1}{2}{3}", tmpColumns, tmpCdt, tmpGroupBy, tmpOrderBy);
            }
            catch (Exception ex)
            { }

            return strSQL;
        }
        public string CalcStatisticalTimesFordiffZone(bool isStart, DateTime dtStartTime, DateTime dtEndTime, string _statisticalUnit, string _type, double _zone)
        {
            string tmpCdt = " where {0}";
            string tmpGroupBy = " group by {0}";
            string tmpOrderBy = " order by {0}";
            string tmpColumns;
            string tmpCdtType = " and type in ({0})";
            string strSQL = "";
            string tmpHourCdt = " and hours in ({0})";

            try
            {

                if (_type.ToLower().Equals("total"))
                {
                    tmpCdtType = string.Format(tmpCdtType, "'T'");
                }
                else if (_type.ToLower().Equals("success"))
                {
                    tmpCdtType = string.Format(tmpCdtType, "'S'");
                }
                else if (_type.ToLower().Equals("failed"))
                {
                    tmpCdtType = string.Format(tmpCdtType, "'F'");
                }
                else
                {
                    tmpCdtType = "";
                }

                //int iStart = Convert.ToInt16(24 + _zone);
                int iStart = dtStartTime.Hour;
                int iEnd = dtEndTime.Hour;

                string tmpHour = "";
                int iHour = 24;
                if (isStart)
                {
                    if (iStart < iEnd)
                        iHour = iEnd;


                    for (int i = iStart; i < iHour; i++)
                    {
                        tmpHour = tmpHour.Equals("") ? tmpHour + i.ToString() : tmpHour + "," + i.ToString();
                    }
                } 
                else
                {
                    if (iStart > iEnd)
                    {
                        for (int i = 0; i < iEnd; i++)
                        {
                            tmpHour = tmpHour.Equals("") ? tmpHour + i.ToString() : tmpHour + "," + i.ToString();
                        }
                    }
                }

                if (tmpHour.Equals(""))
                    tmpHour = "99";

                tmpHourCdt = String.Format(tmpHourCdt, tmpHour);


                switch (_statisticalUnit.ToLower())
                {
                    case "years":
                        tmpColumns = " years, months, sum(times) as time, type ";
                        tmpCdt = string.Format(tmpCdt, string.Format("years = {0}{1}", dtStartTime.Year.ToString(), tmpCdtType));
                        tmpGroupBy = string.Format(tmpGroupBy, "years, months, type");
                        tmpOrderBy = string.Format(tmpOrderBy, "years");
                        break;
                    case "months":
                        tmpColumns = " years, months, days, sum(times) as time, type ";
                        tmpCdt = string.Format(tmpCdt, string.Format("years = {0} and months = {1}{2}", dtStartTime.Year.ToString(), dtStartTime.Month.ToString(), tmpCdtType));
                        tmpGroupBy = string.Format(tmpGroupBy, "years, months, days, type");
                        tmpOrderBy = string.Format(tmpOrderBy, "years, months");
                        break;
                    case "days":
                        tmpColumns = " years, months, days, sum(times) as time, type ";
                        tmpCdt = string.Format(tmpCdt, string.Format("years = {0} and months = {1} and days = {2}{3}{4}", dtStartTime.Year.ToString(), dtStartTime.Month.ToString(), dtStartTime.Day.ToString(), tmpHourCdt, tmpCdtType));
                        tmpGroupBy = string.Format(tmpGroupBy, "years, months, days, type");
                        tmpOrderBy = string.Format(tmpOrderBy, "years, months, days");
                        break;
                    case "year":
                        tmpColumns = " years, sum(times) as time ";
                        tmpCdt = string.Format(tmpCdt, string.Format("years = {0}{1}", dtStartTime.Year.ToString(), tmpCdtType));
                        tmpGroupBy = string.Format(tmpGroupBy, "years");
                        tmpOrderBy = string.Format(tmpOrderBy, "years");
                        break;
                    case "month":
                        tmpColumns = " years, months, sum(times) as time ";
                        tmpCdt = string.Format(tmpCdt, string.Format("years = {0} and months = {1}{2}", dtStartTime.Year.ToString(), dtStartTime.Month.ToString(), tmpCdtType));
                        tmpGroupBy = string.Format(tmpGroupBy, "years, months");
                        tmpOrderBy = string.Format(tmpOrderBy, "years, months");
                        break;
                    case "shift":
                        tmpColumns = " years, months, days, sum(times) as time ";
                        tmpCdt = string.Format(tmpCdt, string.Format("years = {0} and months = {1} and days = {2}{3}{4}", dtStartTime.Year.ToString(), dtStartTime.Month.ToString(), dtStartTime.Day.ToString(), tmpHourCdt, tmpCdtType));
                        tmpGroupBy = string.Format(tmpGroupBy, "years, months, days");
                        tmpOrderBy = string.Format(tmpOrderBy, "years, months, days");
                        break;
                    case "day":
                        tmpColumns = " years, months, days, sum(times) as time ";
                        tmpCdt = string.Format(tmpCdt, string.Format("years = {0} and months = {1} and days = {2}{3}{4}", dtStartTime.Year.ToString(), dtStartTime.Month.ToString(), dtStartTime.Day.ToString(), tmpHourCdt, tmpCdtType));
                        tmpGroupBy = string.Format(tmpGroupBy, "years, months, days");
                        tmpOrderBy = string.Format(tmpOrderBy, "years, months, days");
                        break;
                    default:
                        tmpColumns = " * ";
                        break;
                }

                strSQL = string.Format(@"select {0} from RTD_Statistical{1}{2}{3}", tmpColumns, tmpCdt, tmpGroupBy, tmpOrderBy);
            }
            catch (Exception ex)
            { }

            return strSQL;
        }
        public string QueryRtdNewAlarm()
        {
            string strSQL = "select rownum, b.* from ( select a.*from RTD_ALARM a where a.\"new\" = 1 order by a.\"createdAt\" desc) b where rownum < 100";
            return strSQL;
        }
        public string QueryAllRtdAlarm()
        {
            string strSQL = "select * from RTD_ALARM";
            return strSQL;
        }
        public string UpdateRtdAlarm(string _time)
        {
            string tmpSQL = "update rtd_alarm set \"new\" = 0, \"last_updated\" = sysdate where \"createdAt\" <= to_date('{0}', 'yyyy/MM/dd HH24:mi:ss') and \"new\" = 1";
            string strSQL = string.Format(tmpSQL, _time);
            return strSQL;
        }
        public string QueryCarrierAssociateWhenIsNewBind()
        {
            string strSQL = "";
            string strSet = "";

            strSQL = string.Format("select distinct a.carrier_id, a.lot_id, b.locate, b.portno, b.location_type, c.customername, c.partid, c.lottype, c.stage, a.quantity from CARRIER_LOT_ASSOCIATE a left join CARRIER_TRANSFER b on b.carrier_id = a.carrier_id left join LOT_INFO c on c.lotid = a.lot_id where a.new_bind = 1 and locate is not null", strSet);
            return strSQL;
        }
        public string QueryCarrierAssociateWhenOnErack(string _table)
        {
            string strSQL = "";
            string strSet = "";

            strSQL = string.Format("select distinct a.carrier_id, a.lot_id, b.locate, b.portno, b.location_type, c.customername, c.partid, c.lottype, c.stage, a.quantity, d.stage as stage1 from CARRIER_LOT_ASSOCIATE a left join CARRIER_TRANSFER b on b.carrier_id = a.carrier_id left join LOT_INFO c on c.lotid = a.lot_id left join {0} d on c.lotid = d.lotid where b.location_type in ('ERACK','STK') and a.lot_id is not null and locate is not null", _table);
            return strSQL;
        }
        public string ResetCarrierLotAssociateNewBind(string _carrierId)
        {
            string tmpSQL = "update CARRIER_LOT_ASSOCIATE set New_Bind = 0 where carrier_id = '{0}' and New_Bind = 1";
            string strSQL = string.Format(tmpSQL, _carrierId);
            return strSQL;
        }
        public string QueryRTDStatisticalByCurrentHour(DateTime _datetime)
        {
            string tmpSQL = String.Format(" where years = {0} and months = {1} and days = {2} and hours = {3}", 
                _datetime.Year.ToString(), _datetime.Month.ToString(), _datetime.Day.ToString(), _datetime.Hour.ToString());

            string strSQL = string.Format("select  years, months, days, hours, times, type  from RTD_STATISTICAL{0}", tmpSQL);
            return strSQL;
        }
        public string InitialRTDStatistical(string _datetime, string _type)
        {
            DateTime tmpDatetime = DateTime.Parse(_datetime);
            string tmpSQL = "insert into RTD_STATISTICAL (years, months, days, hours, times, type) values ({0}, {1}, {2}, {3}, 0, '{4}')";
            string strSQL = string.Format(tmpSQL, tmpDatetime.Year.ToString(), tmpDatetime.Month.ToString(), tmpDatetime.Day.ToString(), tmpDatetime.Hour.ToString(), _type);
            return strSQL;
        }
        public string UpdateRTDStatistical(DateTime _datetime, string _type, int _count)
        {
            string tmpSQL = String.Format(" where years = {0} and months = {1} and days = {2} and hours = {3} and type = '{4}'",
                _datetime.Year.ToString(), _datetime.Month.ToString(), _datetime.Day.ToString(), _datetime.Hour.ToString(), _type);

            string strSQL = string.Format("update RTD_STATISTICAL set times = {0}{1}", _count, tmpSQL);
            return strSQL;
        }
        public string InsertRTDStatisticalRecord(string _datetime, string _commandid, string _type)
        {
            DateTime tmpDatetime = DateTime.Parse(_datetime);
            string tmpSQL = "insert into RTD_STATISTICAL_RECORD (years, months, days, hours, commandid, type, recordtime) values ({0}, {1}, {2}, {3}, '{4}', '{5}', TO_DATE(\'{6}\', \'yyyy-MM-dd hh24:mi:ss\'))";
            string strSQL = string.Format(tmpSQL, tmpDatetime.Year.ToString(), tmpDatetime.Month.ToString(), tmpDatetime.Day.ToString(), tmpDatetime.Hour.ToString(), _commandid, _type, _datetime);
            return strSQL;
        }
        public string QueryRTDStatisticalRecord(string _datetime)
        {
            string tmpSQL = "";
            if (!_datetime.Equals(""))
            {
                try
                {
                    DateTime tmpDatetime = DateTime.Parse(_datetime);

                    tmpSQL = String.Format(" where years = {0} and months = {1} and days = {2} and hours = {3}",
                        tmpDatetime.Year.ToString(), tmpDatetime.Month.ToString(), tmpDatetime.Day.ToString(), tmpDatetime.Hour.ToString());
                }
                catch (Exception ex)
                { }
            }
            string strSQL = string.Format("select years, months, days, hours, type, sum(times) as times,  max(recordtime) as recordtime from RTD_STATISTICAL_RECORD{0} group by years, months, days, hours, type order by years, months, days, hours, type", tmpSQL);
            return strSQL;
        }
        public string CleanRTDStatisticalRecord(DateTime _datetime, string _type)
        {
            string tmpSQL = String.Format(" where years = {0} and months = {1} and days = {2} and hours = {3} and type = '{4}'",
                _datetime.Year.ToString(), _datetime.Month.ToString(), _datetime.Day.ToString(), _datetime.Hour.ToString(), _type);

            string strSQL = string.Format("delete RTD_STATISTICAL_RECORD {0}", tmpSQL);
            return strSQL;
        }
        public string InsertRTDAlarm(string[] _alarmCode)
        {
            //string[] _alarmCode = _alarm.Split('#');
            //10000,SYSTEM,RTD,INFO,TESE ALARM,0,,00001,Params,Description
            string strSQL = string.Format(@"insert into rtd_alarm (""unitType"", ""unitID"", ""level"", ""code"", ""cause"", ""subCode"", ""detail"", ""commandID"", ""params"", ""description"", ""new"", ""createdAt"", ""last_updated"", ""eventTrigger"")
                                        values ('{1}', '{2}', '{3}', '{0}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}', 1, sysdate, sysdate, '{10}')", 
                                        _alarmCode[0], _alarmCode[1], _alarmCode[2], _alarmCode[3], _alarmCode[4], _alarmCode[5], _alarmCode[6], _alarmCode[7], _alarmCode[8], _alarmCode[9], _alarmCode[10]);
            return strSQL;
        }
        public string InsertRTDAlarm(RTDAlarms _Alarms)
        {
            //10000,SYSTEM,RTD,INFO,TESE ALARM,0,,00001,Params,Description
            string strSQL = string.Format(@"insert into rtd_alarm (""unitType"", ""unitID"", ""level"", ""code"", ""cause"", ""subCode"", ""detail"", ""commandID"", ""params"", ""description"", ""new"", ""createdAt"", ""last_updated"", ""eventTrigger"")
                                        values ('{0}', '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}', 1, sysdate, sysdate, '{10}')",
                                        _Alarms.UnitType, _Alarms.UnitID, _Alarms.Level, _Alarms.Code, _Alarms.Cause, _Alarms.SubCode, _Alarms.Detail, _Alarms.CommandID, _Alarms.Params, _Alarms.Description, _Alarms.EventTrigger);
            return strSQL;
        }
        public string SelectWorkgroupSet(string _EquipID)
        {
            string tmpCoditions = "";
            if (!_EquipID.Equals(""))
                tmpCoditions = string.Format("and a.equipid = '{0}'", _EquipID);

            string strSQL = string.Format(@"select a.equipid, a.workgroup, b.in_erack, b.out_erack, b.usefailerack, b.f_erack, b.stage, b.pretransfer, b.checkCustDevice, b.priority as prio, b.qtime_low, b.qtime_high, b.bindworkgroup, b.preparenextworkgroup, b.nextworkgroup, b.prepareqty from eqp_status a
                                            left join workgroup_set b on b.workgroup=a.workgroup 
                                            where 1=1 {0}", tmpCoditions);
            return strSQL;
        }
        public string UpdateLotinfoState(string _lotID, string _state)
        {
            string strSQL = string.Format("update lot_info set state = '{0}', lastModify_dt = sysdate where lotid = '{1}'", _state, _lotID);
            return strSQL;
        }
        public string ConfirmLotinfoState(string _lotID, string _state)
        {
            string strSQL = string.Format("select * from lot_info where lotid = '{1}' and state = '{0}'", _state, _lotID);
            return strSQL;
        }
        public string QueryLotinfoQuantity(string _lotID)
        {
            string strSQL = string.Format("select * from lot_info where lotid = '{0}'", _lotID);
            return strSQL;
        }
        public string UpdateLotinfoTotalQty(string _lotID, int _TotalQty)
        {
            string strSQL = string.Format("update lot_info set total_qty = {0} where lotid = '{1}'", _TotalQty, _lotID);
            return strSQL;
        }
        public string CheckQtyforSameLotId(string _lotID, string _carrierType)
        {
            string tmpSql = @"with avail as (
                            select a.carrier_id, a.carrier_state, a.locate, a.portno, a.enable, a.location_type,a.metal_ring, c.quantity, case when d.total_qty is null then 0 else d.total_qty end as total_qty,
                                    b.carrier_type, b.command_type, c.tag_type, c.lot_id from CARRIER_TRANSFER a
                                left join CARRIER_TYPE_SET b on b.type_key = a.type_key
                                left join CARRIER_LOT_ASSOCIATE c on c.carrier_id = a.carrier_id
                                left join LOT_INFO d on d.lotid=c.lot_id
                                where a.enable=1 and a.carrier_state ='ONLINE' and a.state not in ('HOLD', 'SYSHOLD') and a.reserve = 0 and d.state not in ('HOLD')
                                    and a.location_type in ('ERACK','STK') and b.carrier_type in (select carrier_type_key from port_type_asso where carrier_type = '{0}') 
                                and c.lot_id is not null order by d.sch_seq ) 
                            select lot_id, count(carrier_id) as NumOfCarr, sum(quantity) as qty, total_qty from avail where lot_id = '{1}' group by lot_id, total_qty";

            string strSQL = string.Format(tmpSql, _carrierType, _lotID);
            return strSQL;
        }
        public string QueryQuantity2ByCarrier(string _carrierID)
        {
            string strSQL = string.Format(@"select a.carrier_id, b.lot_id, b.quantity, b.total as total_qty from carrier_transfer a
                                            left join carrier_lot_associate b on a.carrier_id = b.carrier_id
                                            where a.carrier_id = '{0}'", _carrierID);
            return strSQL;
        }
        public string QueryQuantityByCarrier(string _carrierID)
        {
            string strSQL = string.Format(@"select a.carrier_id, b.lot_id, b.quantity, case when c.total_qty is null then b.total else c.total_qty end as total_qty from carrier_transfer a
                                            left join carrier_lot_associate b on a.carrier_id = b.carrier_id
                                            left join lot_info c on c.lotid = b.lot_id
                                            where a.carrier_id = '{0}'", _carrierID);
            return strSQL;
        }
        public string QueryEqpPortSet(string _equipId, string _portSeq)
        {
            string strSQL = string.Format(@"select * from eqp_port_set a
                                            where a.equipid = '{0}' and a.port_seq = {1}", _equipId, _portSeq);
            return strSQL;
        }
        public string InsertTableEqpPortSet(string[] _params)
        {
            string strSQL = string.Format(@"insert into eqp_port_set (equipid, port_model, port_seq, port_type, port_id, carrier_type, near_stocker, create_dt, modify_dt, lastmodify_dt, port_state, workgroup)
                                        values ('{0}', '{1}', {2}, '{3}', '{4}', '{5}', '', sysdate, sysdate, sysdate, 0, '{6}')",
                                        _params[0], _params[1], _params[2], _params[3], _params[4], _params[5], _params[6]);
            return strSQL;
        }
        public string QueryWorkgroupSet(string _Workgroup)
        {
            string tmpWhere = " ";

            if(!_Workgroup.Equals(""))
                tmpWhere = string.Format("where workgroup = '{0}'", _Workgroup);

            string strSQL = string.Format(@"select * from workgroup_set {0}", tmpWhere);
            return strSQL;
        }
        public string QueryWorkgroupSet(string _Workgroup, string _Stage)
        {
            string tmpWhere = " ";

            if (!_Workgroup.Equals(""))
                tmpWhere = string.Format("where workgroup = '{0}'", _Workgroup);

            if (!_Stage.Equals(""))
                tmpWhere = string.Format("where stage = '{0}'", _Stage);

            string strSQL = string.Format(@"select * from workgroup_set {0}", tmpWhere);
            return strSQL;
        }
        public string QueryWorkgroupSetAndUseState(string _Workgroup)
        {
            string tmpWhere = " ";

            if (!_Workgroup.Equals(""))
                tmpWhere = string.Format("where a.workgroup = '{0}'", _Workgroup);

            string strSQL = string.Format(@"select a.workgroup, a.in_Erack, a.Out_Erack, case when b.eqpnum is null then 'None' else 'USE' end UseState from workgroup_set a
                                            left join (select count(equipid) as eqpnum, workgroup from eqp_port_set group by workgroup) b
                                            on b.workgroup = a.workgroup {0}", tmpWhere);
            return strSQL;
        }
        public string CreateWorkgroup(string _Workgroup)
        {
            string strSQL = string.Format(@"insert into workgroup_set (workgroup, in_erack, out_erack, create_dt, modify_dt, lastmodify_dt)
                                            values ('{0}', '', '', sysdate, sysdate, sysdate)", _Workgroup);
            return strSQL;
        }
        public string UpdateWorkgroupSet(string _Workgroup, string _InRack, string _OutRack)
        {
            string tmpSet = "";
            string strSQL = "";

            if (!_Workgroup.Equals(""))
            {
                strSQL = string.Format("where workgroup = '{0}'", _Workgroup);

                if (!_InRack.Equals(""))
                    tmpSet = string.Format("{0}", tmpSet.Equals("") ? tmpSet + string.Format("set in_erack = '{0}'", _InRack.Trim()) : tmpSet + string.Format(", in_erack = '{0}'", _InRack.Trim()));

                if (!_OutRack.Equals(""))
                    tmpSet = string.Format("{0}", tmpSet.Equals("") ? tmpSet+ string.Format("set out_erack = '{0}'", _OutRack.Trim()) : tmpSet+ string.Format(", out_erack = '{0}'", _OutRack.Trim()));

                if (!tmpSet.Equals(""))
                    tmpSet = string.Format("{0},lastModify_dt = {1}", tmpSet, "sysdate");

                strSQL = string.Format(@"update workgroup_set {0} {1}
                                            ", tmpSet, strSQL);
            }

            return strSQL;
        }
        public string UpdatePriorityForWorkgroupSet(string _Workgroup, string _stage, int _priority)
        {
            string tmpSet = "";
            string strSQL = "";

            if (!_Workgroup.Equals(""))
            {
                strSQL = string.Format("where workgroup = '{0}'", _Workgroup);

                if (!_stage.Equals(""))
                {
                    strSQL = string.Format("where workgroup = '{0}' and stage = '{1}'", _Workgroup, _stage);
                }
                else
                {
                    strSQL = string.Format("where workgroup = '{0}'", _Workgroup);
                }

                if (_priority > 0)
                    tmpSet = string.Format("{0}", tmpSet.Equals("") ? string.Format("set priority = '{0}'", _priority) : "0");

                if (!tmpSet.Equals(""))
                    tmpSet = string.Format("{0},lastModify_dt = {1}", tmpSet, "sysdate");

                strSQL = string.Format(@"update workgroup_set {0} {1}
                                            ", tmpSet, strSQL);
            }

            return strSQL;
        }
        public string DeleteWorkgroup(string _Workgroup)
        {
            string strSQL = "";

            if (!_Workgroup.Equals(""))
                strSQL = string.Format(@"delete workgroup_set where workgroup = '{0}'", _Workgroup);

            return strSQL;
        }
        public string SetResetPreTransfer(string _Workgroup, Boolean _set)
        {
            string tmpSet = "";
            string strSQL = "";

            if (!_Workgroup.Equals(""))
            {
                strSQL = string.Format("where workgroup = '{0}'", _Workgroup);

                if (_set)
                    tmpSet = string.Format("set PreTransfer = 1");
                else
                    tmpSet = string.Format("set PreTransfer = 0");

                strSQL = string.Format(@"update workgroup_set {0} {1}
                                            ", tmpSet, strSQL);
            }

            return strSQL;
        }
        public string UpdateEquipWorkgroup(string _equip, string _Workgroup)
        {
            string tmpSet = "";
            string strSQL = "";

            if (!_equip.Equals(""))
            {
                strSQL = string.Format("where equipid = '{0}'", _equip);

                if (!_Workgroup.Equals(""))
                    tmpSet = string.Format("{0}", tmpSet.Equals("") ? tmpSet + string.Format("set Workgroup = '{0}'", _Workgroup) : tmpSet + string.Format(", Workgroup = '{0}'", _Workgroup));
                else
                    return strSQL;

                if (!tmpSet.Equals(""))
                    tmpSet = string.Format("{0},lastModify_dt = {1}", tmpSet, "sysdate");

                strSQL = string.Format(@"update eqp_status {0} {1}
                                            ", tmpSet, strSQL);
            }

            return strSQL;
        }
        public string UpdateEquipPortSetWorkgroup(string _equip, string _Workgroup)
        {
            string tmpSet = "";
            string strSQL = "";

            if (!_equip.Equals(""))
            {
                strSQL = string.Format("where equipid = '{0}'", _equip);

                if (!_Workgroup.Equals(""))
                    tmpSet = string.Format("{0}", tmpSet.Equals("") ? tmpSet + string.Format("set Workgroup = '{0}'", _Workgroup) : tmpSet + string.Format(", Workgroup = '{0}'", _Workgroup));
                else
                    return strSQL;

                if (!tmpSet.Equals(""))
                    tmpSet = string.Format("{0},lastModify_dt = {1}", tmpSet, "sysdate");

                strSQL = string.Format(@"update eqp_port_set {0} {1}
                                            ", tmpSet, strSQL);
            }

            return strSQL;
        }
        public string UpdateEquipPortModel(string _equip, string _portModel, int _portNum)
        {
            string tmpSet = "";
            string strSQL = "";

            //update eqp_status set port_Model = 'ABC', port_Number = 4, lastModify_dt = sysdate where equipid = 'CTDS-10';
            if (!_equip.Equals(""))
            {
                strSQL = string.Format("where equipid = '{0}'", _equip);

                if (!_portModel.Equals(""))
                    tmpSet = string.Format("{0}", tmpSet.Equals("") ? tmpSet + string.Format("set port_Model = '{0}'", _portModel) : tmpSet + string.Format(", port_Model = '{0}'", _portModel));

                if (_portNum >= 0)
                    tmpSet = string.Format("{0}", tmpSet.Equals("") ? tmpSet + string.Format("set port_Number = {0}", _portNum) : tmpSet + string.Format(", port_Number = {0}", _portNum));

                if (!tmpSet.Equals(""))
                    tmpSet = string.Format("{0},lastModify_dt = {1}", tmpSet, "sysdate");

                strSQL = string.Format(@"update eqp_status {0} {1}
                                            ", tmpSet, strSQL);
            }

            return strSQL;
        }
        public string DeleteEqpPortSet(string _Equip, string _portModel)
        {
            string strSQL = "";

            if (!_Equip.Equals(""))
            {
                strSQL = string.Format(@"delete Eqp_port_set where equipid = '{0}' and port_Model = '{1}'", _Equip, _portModel);
            }

            return strSQL;
        }
        public string QueryPortModelMapping(string _eqpTypeID)
        {
            string tmpSet = "";
            string strSQL = "";
//            select* from rtd_default_set a
//              left join rtd_portmodel_def b on b.portmodel = a.paramvalue
//              where a.parameter = 'PortModelMapping' and a.paramtype = 'WLCSP_DIE_SALES\TAPER/DETAPER'

            if (!_eqpTypeID.Equals(""))
            {
                strSQL = string.Format(@"select Parameter, ParamType, PortModel, PortNumber, Port_Type_Mapping as PortTypeMapping, Carrier_Type_Mapping as CarrierTypeMapping from rtd_default_set a
                                    left join rtd_portmodel_def b on b.portmodel = a.paramvalue
                                    where a.parameter = 'PortModelMapping' and a.paramtype = '{0}'", _eqpTypeID);
            }

            return strSQL;
        }
        public string QueryPortModelDef()
        {
            string tmpWhere = " ";

            string strSQL = string.Format(@"select PortModel from rtd_portmodel_def", tmpWhere);
            return strSQL;
        }
        public string QueryProcLotInfo()
        {
            string tmpWhere = " ";

            string strSQL = string.Format(@"select * from lot_info where rtd_state = 'PROC'", tmpWhere);
            return strSQL;
        }
        public string LockMachineByLot(string _lotid, int _Quantity, int _lock)
        {
            string tmpWhere = String.Format(" where lotid = '{0}'", _lotid);
            string tmpLockMachine = _lock == 1 ? " set lockmachine = 1 {0}" : " set lockmachine = 0 {0}";

            string tmpSet = string.Format(tmpLockMachine, _Quantity > 0 ? string.Format(", Comp_qty = {0}", _Quantity) : "");

            string strSQL = string.Format(@"update lot_info {0}{1}", tmpSet, tmpWhere);
            return strSQL;
        }
        public string UpdateLotInfoForLockMachine(string _lotid)
        {
            string tmpWhere = String.Format(" where lotid = '{0}'", _lotid);

            string strSQL = string.Format(@"update lot_info set state = 'WAIT', rtd_state = 'READY', lastModify_dt = sysdate {0}", tmpWhere);
            return strSQL;
        }
        public string CheckLocationByLotid(string _lotid)
        {
            string tmpWhere = "";
            string tmpSet = "";
            string strSQL = "";

            tmpWhere = String.Format(" where a.lot_id = '{0}' and b.location_type in ('ERACK', 'STK')", _lotid);

            strSQL = string.Format(@"select distinct a.carrier_id, a.carrier_type, a.associate_state, a.lot_id, a.quantity, b.type_key, b.carrier_state, b.locate, b.portno, 
                                b.enable, b.location_type, b.metal_ring, b.reserve, b.state, c.equiplist, c.state, c.customername, c.stage, c.partid,
                                c.lotType, c.rtd_state, c.total_qty from CARRIER_LOT_ASSOCIATE a 
                                left join CARRIER_TRANSFER b on b.carrier_id=a.carrier_id 
                                left join LOT_INFO c on c.lotid = a.lot_id {0}", tmpWhere);
            return strSQL;
        }
        public string QueryEQPType()
        {
            string strSQL = string.Format(@"select distinct equip_typeid from eqp_status where equip_type is not null");
            return strSQL;
        }
        public string QueryEQPIDType()
        {
            string strSQL = string.Format(@"select distinct equipid, equip_typeid from eqp_status where equip_type is not null");
            return strSQL;
        }
        public string InsertPromisStageEquipMatrix(string _stage, string _equipType, string _equipids, string _userId)
        {
            string tmpEqpType = string.Format(" and a.equip_typeid = '{0}' ", _equipType);
            string tmpEquips;
            if (!_equipids.Equals(""))
                tmpEquips = string.Format(" and a.equipid in ({0}) ", _equipids);
            else
                tmpEquips = "";

            string tmpWhere = String.Format(" where a.equip_type is not null {0}{1}", tmpEqpType, tmpEquips);

            string strSQL = string.Format(@"Insert into promis_stage_equip_matrix
                                            select a.equipid, '{0}' as stage, a.equip_typeid, '{1}' as entryby, 
                                            sysdate as entrydate from eqp_status a {2}", _stage, _userId, tmpWhere);
            return strSQL;
        }
        public string DeletePromisStageEquipMatrix(string _stage, string _equipType, string _equipids)
        {
            string tmpWhere = "";
            string tmpCdt = "";

            if (!_stage.Equals(""))
            {
                if(tmpCdt.Equals(""))
                    tmpCdt = string.Format("stage = '{0}'", _stage);
                else
                    tmpCdt = string.Format("{0} and stage = '{1}'", tmpCdt, _stage);
            }

            if (!_equipType.Equals(""))
            {
                if (tmpCdt.Equals(""))
                    tmpCdt = string.Format("eqptype = '{0}'", _equipType);
                else
                    tmpCdt = string.Format("{0} and eqptype = '{1}'", tmpCdt, _equipType);
            }

            if (!_equipids.Equals(""))
            {
                string[] lstEqpid;
                string tmpEqpid = "";
                if(_equipids.IndexOf(',') > 0)
                {
                    lstEqpid = _equipids.Split(',');
                    foreach(string eqpid in lstEqpid)
                    {
                        tmpEqpid = tmpEqpid.Equals("") ? string.Format("'{0}'", eqpid) : string.Format("{0},'{1}'", tmpEqpid, eqpid);
                    }
                }
                else
                {
                    tmpEqpid = string.Format("'{0}'", _equipids);
                }

                if (tmpCdt.Equals(""))
                    tmpCdt = string.Format("eqpid in ({0})", tmpEqpid);
                else
                    tmpCdt = string.Format("{0} and eqpid in ({1})", tmpCdt, tmpEqpid);
            }

            if(tmpWhere.Equals(""))
            {
                tmpWhere = tmpCdt.Equals("") ? "" : String.Format(" where {0}", tmpCdt);
            }          

            string strSQL = string.Format(@"delete promis_stage_equip_matrix {0}", tmpWhere);
            return strSQL;
        }
        //public string UpdateEQPPortModel(string _EquipID, string _PortModel)
        //{

        //    string strSQL = string.Format(@"insert into rtd_alarm
        //                                ('unitType', 'unitID', 'level', 'code', 'cause', 'subCode', 'detail', 'commandID', 'params', 'description', 'new', 'createdAt', 'last_updated')
        //                                values ('{0}', '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}', 1, sysdate, sysdate)",
        //                                _alarmCode[0], _alarmCode[1], _alarmCode[2], _alarmCode[3], _alarmCode[4], _alarmCode[5], _alarmCode[6], _alarmCode[7], _alarmCode[8], _alarmCode[9]);
        //    return strSQL;
        //}
        public string SyncStageInfo()
        {
            string strSQL = string.Format(@"insert into RTD_DEFAULT_SET 
                select 'PromisStage' as parameter, 'string' as paramtype, stage as paramvalue, 'RTD' as modifyby,  sysdate as lastmodify_dt, '' as description  from (
                select a.stage, b.paramvalue, case when b.paramvalue is null then 'New' end as State  from (select distinct stage from lot_info) a
                left join RTD_DEFAULT_SET b on b.parameter = 'PromisStage' and b.paramvalue = a.stage) where state = 'New'");
            return strSQL;
        }
        public string CheckRealTimeEQPState()
        {
            string strSQL = "";

#if DEBUG
            //Do Nothing
#else
            strSQL = string.Format(@"select a.equipid, b. machine_state, b.curr_status, b.down_state, b.up_date from eqp_status a
                                        left join rts_active@CIMDB3.world b on b.equipid=a.equipid
                                        where a.curr_status <> b.curr_status or a.down_state <> b.down_state");
#endif
            return strSQL;
        }
        public string UpdateCurrentEQPStateByEquipid(string _equipid)
        {
            string strSQL = "";
#if DEBUG
            //Do Nothing
#else
            strSQL = string.Format(@"update eqp_status a set machine_state = (select case when machine_state is null then 'Know' else machine_state end as machine_state from rts_active@CIMDB3.world where equipid = a.equipid), 
                                    curr_status = (select case when curr_status is null then 'Know' else curr_status end as curr_status from rts_active@CIMDB3.world where equipid = a.equipid), 
                                    down_state = (select case when down_state is null then 'Know' else down_state end as down_state from rts_active@CIMDB3.world where equipid = a.equipid), 
                                    lastmodify_dt = sysdate
                                    where a.equipid = '{0}'", _equipid);
#endif
            return strSQL;
        }
        public string QueryEquipListFirst(string _lotid, string _equipid)
        {
            string strSQL = "";

            strSQL = string.Format(@"select * from lot_info where lotid = '{0}' and equiplist like '{1}%'", _lotid, _equipid);
            return strSQL;
        }
        public string QueryRackByGroupID(string _groupID)
        {
            string strSQL = string.Format(@"select * from (select * from rack where ""groupID"" = '{0}' union select * from rack where ""erackID"" = '{0}') order by ""erackID""", _groupID);
            return strSQL;
        }
        public string QueryExtenalCarrierInfo(string _table)
        {
            string strSQL = string.Format(@"select distinct * from (
                                                select * from {0} Where lotid not in (select lot_id from CARRIER_LOT_ASSOCIATE) or cstid not in (select carrier_id from carrier_transfer)
                                            union all
                                                select a.* from {0} a
                                                left join CARRIER_LOT_ASSOCIATE b on b.carrier_id=a.cstid and b.lot_id = a.lotid and b.quantity != a.lotqty 
                                                where b.carrier_id is not null
                                            union all
                                                select a.* from {0} a
                                                left join CARRIER_LOT_ASSOCIATE b on b.carrier_id=a.cstid and b.lot_id != a.lotid
                                                where b.carrier_id is not null
                                            union all
                                                select a.* from {0} a
                                                left join carrier_transfer b on b.carrier_id=a.cstid and  b.quantity != a.lotqty 
                                                where b.carrier_id is not null)"
                                            , _table);
            return strSQL;
        }
        public string InsertCarrierLotAsso(CarrierLotAssociate _carrierLotAsso)
        {
            string strSQL = string.Format(@"insert into carrier_lot_associate (carrier_id, tag_type, carrier_type, associate_state, change_state_time, lot_id, 
                                         quantity, change_station, change_station_type, update_by, update_time, create_by, new_bind)
                                         values ('{0}', '{1}', '{2}', 'Associated With Lot', sysdate, '{3}', 
                                         {4}, 'SyncEwlbCarrier', 'Sync', 'RTD', to_date('{5}', 'yyyy/MM/dd HH24:mi:ss'), '{6}', 1)", _carrierLotAsso.CarrierID,
                                         _carrierLotAsso.TagType, _carrierLotAsso.CarrierType, _carrierLotAsso.LotID, _carrierLotAsso.Quantity, _carrierLotAsso.UpdateTime, _carrierLotAsso.CreateBy);
            return strSQL;
        }
        public string UpdateCarrierLotAsso(CarrierLotAssociate _carrierLotAsso)
        {
            string strSQL = string.Format(@"update carrier_lot_associate set carrier_id='{0}', tag_type='{1}', carrier_type='{2}', associate_state='Associated With Lot', change_state_time=sysdate, lot_id='{3}', 
                                         quantity={4}, change_station='SyncEwlbCarrier', change_station_type='Sync', update_by='RTD', update_time=to_date('{5}', 'yyyy/MM/dd HH24:mi:ss'), create_by='{6}', new_bind=1 
                                         where carrier_id='{7}'", _carrierLotAsso.CarrierID,
                                         _carrierLotAsso.TagType, _carrierLotAsso.CarrierType, _carrierLotAsso.LotID, _carrierLotAsso.Quantity, _carrierLotAsso.UpdateTime, _carrierLotAsso.CreateBy, _carrierLotAsso.CarrierID);
            return strSQL;
        }
        public string UpdateLastCarrierLotAsso(CarrierLotAssociate _carrierLotAsso)
        {
            string strSQL = string.Format(@"update CARRIER_LOT_ASSOCIATE set last_associate_state=associate_state, last_lot_id=lot_id, last_change_state_time=sysdate, last_change_station=change_station, 
                                            last_change_station_type=change_station_type where carrier_id = '{0}'", _carrierLotAsso.CarrierID);
            return strSQL;
        }
        public string InsertCarrierTransfer(string _carrierId, string _typeKey, string _quantity)
        {
            string strSQL = string.Format(@"insert into carrier_transfer (carrier_id, type_key, location_type, create_dt, quantity) 
                                        values ('{0}', '{1}', 'Sync', sysdate, {2})", _carrierId, _typeKey, _quantity);
            return strSQL;
        }
        public string UpdateCarrierTransfer(string _carrierId, string _typeKey, string _quantity)
        {
            string strSQL = string.Format(@"update carrier_transfer set carrier_id='{0}', type_key='{1}', location_type='Sync', Modify_dt=sysdate, quantity={2},
                                            carrier_state='OFFLINE', locate='', portno='', enable=1, metal_ring=0, reserve=0, state='Normal'
                                            where carrier_id = '{3}'", _carrierId, _typeKey, _quantity, _carrierId);
            return strSQL;
        }
        public string LockEquip(string _equip, bool _lock)
        {
            string strSQL = "";

            if (_lock)
            {
                strSQL = String.Format("update eqp_status set islock = 1 where equipid = '{0}'", _equip);
            }
            else
            {
                strSQL = String.Format("update eqp_status set islock = 0 where equipid = '{0}'", _equip);
            }

            return strSQL;
        }
        public string QueryEquipLockState(string _equip)
        {
            string strSQL = string.Format(@"select * from eqp_status where equipid = '{0}' and islock = 1", _equip);
            return strSQL;
        }
        public string QueryPreTransferList(string _lotTable)
        {
            string strSQL = "";
            string _ciseRack = "semi_int.actl_ciserack_vw@semi_int";
#if DEBUG
            _ciseRack = "lot_info";
#else
_ciseRack = "semi_int.actl_ciserack_vw@semi_int";
#endif
            //actlinfo_lotid_vw

            strSQL = string.Format(@"select case when e.pretransfer is null then e1.pretransfer else e.pretransfer end pretransfer ,case when e.workgroup is not null then e.workgroup else e1.workgroup end workgroup, c.customername, c.partid, case when nvl(e.stage, 'NA') <> 'NA' then e.in_erack else e1.in_erack end in_erack, locate,
a.location_type, a.carrier_ID, lot_ID, c2.carrier_type, d.stage from carrier_transfer a
left join carrier_lot_associate b on b.carrier_id=a.carrier_id
left join {0} c on c.lotid=b.lot_id
left join {1} c1 on c1.lotid=b.lot_id
left join carrier_type_set c2 on c2.type_key=a.type_key
left join (select distinct stage, eqptype from promis_stage_equip_matrix) d on 
d.stage=c1.stage 
left join workgroup_set e on e.workgroup=d.eqptype and e.stage=d.stage and e.pretransfer=1
left join workgroup_set e1 on e1.workgroup=d.eqptype and e1.stage='DEFAULT' and e1.pretransfer=1
where a.enable=1 and a.carrier_state='ONLINE'
and a.locate is not null and a.uat=0
and a.location_type in ('ERACK','STK') and a.reserve=0 and a.state not in ('HOLD','SYSHOLD')
and c.lotid is not null and c.state in ('WAIT')
and (e.workgroup is not null or e1.workgroup is not null)
order by d.stage, c1.potd asc, e.priority asc, c.lot_age desc", _lotTable, _ciseRack);
            /*
            strSQL = string.Format(@"select e.workgroup, c.customername, c.partid, e.in_eRack, locate, a.location_type, a.carrier_ID, lot_ID, c2.carrier_type from carrier_transfer a 
                                            left join carrier_lot_associate b on b.carrier_id=a.carrier_id
                                            left join {0} c on c.lotid=b.lot_id
                                            left join {1} c1 on c1.lotid=b.lot_id
                                            left join carrier_type_set c2 on c2.type_key=a.type_key
                                            left join (select distinct stage, eqptype from promis_stage_equip_matrix) d on d.stage=c1.stage
                                            left join (select distinct workgroup, priority, in_eRack, pretransfer from workgroup_set) e on e.workgroup=d.eqptype
                                            where a.enable=1 and a.carrier_state='ONLINE' and a.locate is not null and a.uat=0 
                                            and a.location_type in ('ERACK', 'STK') and a.reserve=0 and a.state not in ('HOLD', 'SYSHOLD') 
                                            and c.lotid is not null and c.state in ('WAIT') and e.workgroup is not null and e.pretransfer = 1
                                            order by c1.potd asc, e.priority asc, c.lot_age desc, a.locate desc", _lotTable, _ciseRack);
            */
            /*
            strSQL = string.Format(@"select * from carrier_transfer a 
                                            left join carrier_lot_associate b on b.carrier_id=a.carrier_id
                                            left join {0} c on c.lotid=b.lot_id
                                            left join (select distinct stage, eqptype from promis_stage_equip_matrix) d on c.stage=d.stage
                                            left join workgroup_set e on e.workgroup=d.eqptype
                                            where a.enable=1 and a.carrier_state='ONLINE' and a.locate is not null 
                                            and a.location_type = 'ERACK' and a.reserve=0 and a.state not in ('HOLD') 
                                            and c.lotid is not null and c.state in ('WAIT')
                                            and e.pretransfer=1 and e.in_erack is not null
                                            order by e.priority asc, c.lot_age desc, a.locate desc", _lotTable);
            */
            return strSQL;
        }
        public string QueryPreTransferListForUat(string _lotTable)
        {
            string strSQL = "";
            string _ciseRack = "semi_int.actl_ciserack_vw@semi_int";
#if DEBUG
            _ciseRack = "lot_info";
#else
_ciseRack = "semi_int.actl_ciserack_vw@semi_int";
#endif
            //actlinfo_lotid_vw
            strSQL = string.Format(@"select case when e.pretransfer is null then e1.pretransfer else e.pretransfer end pretransfer ,case when e.workgroup is not null then e.workgroup else e1.workgroup end workgroup1, c.customername, c.partid, case when nvl(e.stage, 'NA') <> 'NA' then e.in_erack else e1.in_erack end in_erack, locate,
a.location_type, a.carrier_ID, lot_ID, c2.carrier_type, d.stage from carrier_transfer a
left join carrier_lot_associate b on b.carrier_id=a.carrier_id
left join {0} c on c.lotid=b.lot_id
left join {1} c1 on c1.lotid=b.lot_id
left join carrier_type_set c2 on c2.type_key=a.type_key
left join (select distinct stage, eqptype from promis_stage_equip_matrix) d on 
d.stage=c1.stage  
left join workgroup_set e on e.workgroup=d.eqptype and e.stage=c1.stage and e.pretransfer=1
left join workgroup_set e1 on e1.workgroup=d.eqptype and e1.stage='DEFAULT' and e1.pretransfer=1
where a.enable=1 and a.carrier_state='ONLINE'
and a.locate is not null and a.uat=1
and a.location_type in ('ERACK','STK') and a.reserve=0 and a.state not in ('HOLD','SYSHOLD')
and c.lotid is not null and c.state in ('WAIT')
and (e.workgroup is not null or e1.workgroup is not null)
order by d.stage, c1.potd asc, e.priority asc, c.lot_age desc", _lotTable, _ciseRack);

            /*
            strSQL = string.Format(@"select e.workgroup, c.customername, c.partid, e.in_eRack, locate, a.carrier_ID, lot_ID, c2.carrier_type from carrier_transfer a 
                                            left join carrier_lot_associate b on b.carrier_id=a.carrier_id
                                            left join {0} c on c.lotid=b.lot_id
                                            left join {1} c1 on c1.lotid=b.lot_id
                                            left join carrier_type_set c2 on c2.type_key=a.type_key
                                            left join (select distinct stage, eqptype from promis_stage_equip_matrix) d on d.stage=c1.stage
                                            left join (select distinct workgroup, priority, in_eRack, pretransfer from workgroup_set) e on e.workgroup=d.eqptype
                                            where a.enable=1 and a.carrier_state='ONLINE' and a.locate is not null and a.uat=1
                                            and a.location_type in ('ERACK', 'STK') and a.reserve=0 and a.state not in ('HOLD', 'SYSHOLD') 
                                            and c.lotid is not null and c.state in ('WAIT') and e.workgroup is not null and e.pretransfer = 1
                                            order by c1.potd asc, e.priority asc, c.lot_age desc, a.locate desc", _lotTable, _ciseRack);
            */
            /*
            strSQL = string.Format(@"select * from carrier_transfer a 
                                            left join carrier_lot_associate b on b.carrier_id=a.carrier_id
                                            left join {0} c on c.lotid=b.lot_id
                                            left join (select distinct stage, eqptype from promis_stage_equip_matrix) d on c.stage=d.stage
                                            left join workgroup_set e on e.workgroup=d.eqptype
                                            where a.enable=1 and a.carrier_state='ONLINE' and a.locate is not null 
                                            and a.location_type = 'ERACK' and a.reserve=0 and a.state not in ('HOLD', 'SYSHOLD') 
                                            and c.lotid is not null and c.state in ('WAIT')
                                            and e.pretransfer=1 and e.in_erack is not null
                                            order by e.priority asc, c.lot_age desc, a.locate desc", _lotTable);
            */
            return strSQL;
        }
        public string CheckCarrierLocate(string _inErack, string _locate)
        {
            string strSQL = string.Format(@"select ""erackID"" from (
                                        select ""erackID"" from rack where ""erackID"" = '{0}'
                                        union select ""erackID"" from rack where ""groupID"" = '{0}')
                                        where ""erackID"" = '{1}' order by ""erackID""", _inErack, _locate);
            return strSQL;
        }

        public string CheckPreTransfer(string _carrierid, string _table)
        {
            string strSQL = string.Format(@"select * from {0} where cmd_type='Pre-Transfer' and carrierid = '{0}'", _table, _carrierid);
            return strSQL;
        }
        //select * from workinprocess_sch where cmd_type='Pre-Transfer' and carrierid = '12CA0051'
        public string ManualModeSwitch(string _equip, bool _manualMode)
        {
            string strSQL = "";

            if (_manualMode)
            {
                strSQL = String.Format("update eqp_status set manualmode = 1 where equipid = '{0}'", _equip);
            }
            else
            {
                strSQL = String.Format("update eqp_status set manualmode = 0 where equipid = '{0}'", _equip);
            }

            return strSQL;
        }

        public string QueryLotStageWhenStageChange(string _table)
        {
            string strSQL = "";

            strSQL = String.Format(@"select distinct a.carrier_id, b.lot_id, d.stage from (
                                    select carrier_id from carrier_transfer where location_type in ('ERACK','STK')) a
                                    left join carrier_lot_associate b on b.carrier_id=a.carrier_id
                                    left join lot_info c on c.lotid = b.lot_id and c.state = 'HOLD'
                                    left join {0} d on d.lotid=b.lot_id
                                    where c.stage <> d.stage", _table);

            return strSQL;
        }
        public string QueryReserveStateByEquipid(string _equipid)
        {
            string strSQL = "";
            string strWhere = "";
            strWhere = string.Format(" where equipid = '{0}'", _equipid);

            strSQL = string.Format(@"select * from eqp_reserve_time{0}", strWhere);
            return strSQL;
        }
        public string InsertEquipReserve(string _args)
        {
            string strSQL = "";
            string strWhere = "";
            string[] args = _args.Split(",");
            string _equipid = args[0].Trim();
            string _timeStart = args[1].Trim();
            string _timeEnd = args[2].Trim();
            string _reserveBy = args[3].Trim();

            strSQL = string.Format(@"insert into eqp_reserve_time (equipid, dt_start, dt_end, reserveby, create_dt)
                                    values ('{0}', '{1}', '{2}', '{3}', sysdate)", _equipid, _timeStart, _timeEnd, _reserveBy);
            return strSQL;
        }
        public string UpdateEquipReserve(string _args)
        {
            string strSQL = "";
            string strWhere = "";
            string strSet = "";
            string[] args = _args.Split(",");
            string _type = args[0].Trim();
            string _equipId = args[1].Trim();
            string _reserveBy = args[2].Trim();
            string _timeStart = "";
            string _timeEnd = "";
            string _effective = "";
            string _expired = "";
            

            switch (_type.ToLower())
            {
                case "settime":
                    _timeStart = args[3].Trim();
                    _timeEnd = args[4].Trim();
                    strSet = string.Format("dt_start='{0}', dt_end='{1}', effective=0, expired=0, reserveby='{2}',", _timeStart, _timeEnd, _reserveBy);
                    break;
                case "reset":
                    strSet = string.Format("effective=0, expired=0, reserveby='{0}',", _reserveBy);
                    break;
                case "effective":
                    _effective = args[3].Trim();
                    strSet = string.Format("effective={0}, ", _effective);
                    break;
                case "expired":
                    _expired = args[3].Trim();
                    if(_expired.Equals("1"))
                        strSet = string.Format("effective=0, expired={0}, ", _expired);
                    else if (_expired.Equals("0"))
                        strSet = string.Format("expired={0}, ", _expired);
                    break;
                default:
                    break;
            }

            strWhere = string.Format("where equipid = '{0}'", _equipId);

            strSQL = string.Format(@"update eqp_reserve_time set {0}lastmodify_dt=sysdate {1}", strSet, strWhere);
            return strSQL;
        }
        public string LockLotInfoWhenReady(string _lotID)
        {
            string tmpString = "update lot_info set IsLock = 1 where lotid = '{0}'";
            string strSQL = string.Format(tmpString, _lotID);
            return strSQL;
        }
        public string UnLockLotInfoWhenReady(string _lotID)
        {
            string tmpString = "update lot_info set IsLock = 0 where lotid = '{0}'";
            string strSQL = string.Format(tmpString, _lotID);
            return strSQL;
        }
        public string UnLockAllLotInfoWhenReadyandLock()
        {
            string tmpString = "update lot_info set IsLock = 0 where IsLock = 1 and rtd_state = 'READY'";
            string strSQL = string.Format(tmpString);
            return strSQL;
        }

        public string QueryLotInfoByCarrier(string _carrierid)
        {
            string strSQL = string.Format(@"select a.carrier_id, a.lot_id, b.islock from carrier_lot_associate a 
                                               left join lot_info b on b.lotid = a.lot_id
                                               where a.carrier_id = '{0}'", _carrierid);

            return strSQL;
        }
        public string CheckReserveState(string _args)
        {
            string strSQL = "";
            string strWhere = "";
            string _type = _args;

            switch (_type.ToLower())
            {
                case "reservestart":
                    strWhere = string.Format(" where effective=0 and expired=0");
                    break;
                case "reserveend":
                    strWhere = string.Format(" where effective=1");
                    break;
                default:
                    break;
            }

            

            strSQL = string.Format(@"select * from eqp_reserve_time{0}", strWhere);
            return strSQL;
        }
        public string UpdateStageByLot(string _lotid, string _stage)
        {
            string tmpString = "update lot_info set EQUIPLIST='', EQUIP_ASSO='N', stage='{0}' where lotid='{1}'";
            string strSQL = string.Format(tmpString, _stage, _lotid);
            return strSQL;
        }
        public string QueryLastLotFromEqpPort(string EquipId, string PortSeq)
        {
            string strSQL = string.Format(@"select b.lot_id, b.last_lot_id, case when b.lot_id is not null then b.lot_id else b.last_lot_id end as lastLot from carrier_transfer a
                                            left join carrier_lot_associate b on b.carrier_id=a.carrier_id 
                                            where a.locate='{0}' and a.portno={1}", EquipId, PortSeq);

            return strSQL;
        }
        public string UpdateLastLotIDtoEQPPortSet(string EquipId, string PortSeq, string LastLotID)
        {
            string strSQL = string.Format("update EQP_PORT_SET set lastLotid = '{0}' where EquipId = '{1}' and Port_Seq = '{2}' ", LastLotID, EquipId, PortSeq);
            return strSQL;
        }
        public string ConfirmLotInfo(string _lotid)
        {
            string strSQL = string.Format(@"select * from LOT_INFO where rtd_state not in ('HOLD','PROC','DELETED', 'COMPLETED') and state='WAIT' and equiplist is null and lotid='{0}'", _lotid);
            return strSQL;
        }
        public string IssueLotInfo(string _lotid)
        {
            string strSQL = string.Format(@"select * from LOT_INFO where rtd_state not in ('HOLD','PROC','DELETED', 'COMPLETED') 
                            and to_date(to_char(trunc(starttime, 'DD'),'yyyy/MM/dd HH24:mi:ss'), 'yyyy/MM/dd HH24:mi:ss') <= sysdate
                            and lotid = '{0}'
                            order by  customername, stage, rtd_state, lot_age desc", _lotid);
            return strSQL;
        }

        public string CheckLotStage(string _table, string _lotid)
        {
            //230417V1.0 增加條件！此lot 不可為剛下機台仍處於HOLD狀態的料
            //string strSQL = string.Format(@"select a.lotid, a.stage as stage1, a.state, b.stage as stage2 from {0} a 
            //                    left join lot_info b on b.lotid = a.lotid 
            //                    where a.lotid='{1}' and b.stage=a.stage and b.state not in ('HOLD')", _table, _lotid);

            string strSQL = string.Format(@"select a.lotid, a.stage as stage1, a.state as state1, b.state as state2, b.stage as stage2 from lot_info a 
                                left join {0} b on b.lotid = a.lotid 
                                where a.lotid='{1}' and b.state not in ('HOLD')", _table, _lotid);

            return strSQL;
        }
        public string EQPListReset(string LotID)
        {
            string strSQL = string.Format(@"update LOT_INFO set equip_asso = 'N', equiplist = '' where lotid = '{0}'", LotID);
            //rtd_state = 'INIT', lastmodify_dt = sysdate where lotid = '{0}'", LotID);
            return strSQL;
        }
        public string GetEquipCustDevice(string EquipID)
        {

            string strSQL = "";
#if DEBUG
            strSQL = string.Format(@"select equipid, device from eqp_status where equipid='{0}'", EquipID);
#else
            strSQL = string.Format(@"select equipid, device from rts_active@CIMDB3.world where equipid='{0}'", EquipID);
#endif
            //rts.v_rts_equipment @cimdb3.world
            //rtd_state = 'INIT', lastmodify_dt = sysdate where lotid = '{0}'", LotID);
            return strSQL;
        }
        public string CheckMetalRingCarrier(string _carrierID)
        {
            string strSQL = string.Format(@"select a.lot_id, a.carrier_id, a.associate_state, a.total, a.quantity, b.type_key, b.locate, b.portno, b.carrier_state, b.location_type, b.reserve, c.command_type  from carrier_lot_associate a 
                                        left join carrier_transfer b on b.carrier_id = a.carrier_id 
                                        left join carrier_type_set c on c.type_key = b.type_key
                                        where b.location_type in ('ERACK','STK') and a.lot_id = (select lot_id||'R' from carrier_lot_associate where carrier_id = '{0}')", _carrierID);
            
            return strSQL;
        }
        public string UpdatePriorityByLotid(string _lotID, int _priority)
        {
            string strSQL = string.Format(@"update lot_info set priority = {0} where lotid = '{1}'", _priority, _lotID);

            return strSQL;
        }

        public string QueryDataByLotid(string _lotID, string _table)
        {
            string strSQL = string.Format(@"select * from {0} where lotid = '{1}'", _table, _lotID);

            return strSQL;
        }
        public string HisCommandAppend(HisCommandStatus hisCommandStatus)
        {
            string strSQL = string.Format(@"insert into HIS_COMMANDS (""CommandID"", ""CarrierID"", ""LotID"", ""CommandType"", ""Source"", ""Dest"", ""AlarmCode"", ""Reason"", ""createdAt"", ""LastStateTime"")
                                        values('{0}', '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}', TO_DATE('{8}', 'yyyy/MM/dd HH24:mi:ss'), TO_DATE('{9}', 'yyyy/MM/dd HH24:mi:ss'))",
                                        hisCommandStatus.CommandID, hisCommandStatus.CarrierID, hisCommandStatus.LotID, hisCommandStatus.CommandType, hisCommandStatus.Source, hisCommandStatus.Dest, hisCommandStatus.AlarmCode, hisCommandStatus.Reason, hisCommandStatus.CreatedAt, hisCommandStatus.LastStateTime);
            return strSQL;
        }
        public string GetWorkinprocessSchByCommand(string command, string _table)
        {
            string strSQL = string.Format(@"select cmd_id, cmd_type, equipid, cmd_state, cmd_current_state, carrierid, carriertype, source, dest, lotid, customer,
                                            to_char(create_dt,'yyyy-mm-dd hh24:mi:ss') as create_dt,  to_char(lastModify_dt,'yyyy-mm-dd hh24:mi:ss') as lastModify_dt
                                            from {0} where cmd_Id = '{1}'",
                                            _table, command);
            return strSQL;
        }
        public string GetRTSEquipStatus(string _table, string equipid)
        {
            string strSQL = string.Format(@"select * from {0} where equipid = '{1}'",
                                            _table, equipid);
            return strSQL;
        }
        //value.CurrentDateTime, value.Unit, value.Zone
        public string GetHistoryCommands(string StartTime, string EndTime)
        {
            /*
            string strSQL = string.Format(@"select a.IDX, a.""CommandID"", a.""CarrierID"", a.""LotID"", a.""CommandType"", a.""Source"", a.""Dest"", a.""AlarmCode"", a.""createdAt"", a.""LastStateTime"", b.""AlarmText"" as Reason from his_commands a
                                        left join alarm_detail b on b.""AlarmCode"" = a.""AlarmCode"" where a.""LastStateTime"" between to_date('{0}','yyyy/mm/dd hh24:mi:ss') and to_date('{1}','yyyy/mm/dd hh24:mi:ss')",
                                            StartTime, EndTime);*/

            string strSQL = string.Format(@"select * from his_commands a
                                         where a.""LastStateTime"" between to_date('{0}','yyyy/mm/dd hh24:mi:ss') and to_date('{1}','yyyy/mm/dd hh24:mi:ss')",
                                            StartTime, EndTime);

            return strSQL;
        }
        //select * from rts_active@CIMDB3.world where equipid = '3PBG3-D'
        public string ResetRTDStateByLot(string LotID)
        {
            string strSQL = string.Format(@"update LOT_INFO set state='WAIT', 
                                               rtd_state = 'WAIT' where lotid = '{0}'", LotID);
            
            return strSQL;
        }
        //update eqp_status set custDevice = '' where equipid = '3PBG1-D'
        public string UpdateCustDeviceByEquipID(string _equipID, string _custDevice)
        {
            string strSQL = string.Format(@"update eqp_status set custDevice = '{0}' where equipid = '{1}'", _custDevice, _equipID);

            return strSQL;
        }
        public string UpdateCustDeviceByLotID(string _lotID, string _custDevice)
        {
            string strSQL = string.Format(@"update lot_info set custDevice = '{0}' where lotID = '{1}'", _custDevice, _lotID);

            return strSQL;
        }
        public string InsertHisTSCAlarm(TSCAlarmCollect _alarmCollect)
        {
            string strSQL = string.Format(@"insert into his_tscalarm ( ""ALID"", ""ALTX"", ""ALType"", ""ALSV"", ""UnitType"", ""UnitID"", ""Level"", ""SubCode"")
                            values ({0}, '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}') "
                        , _alarmCollect.ALID, _alarmCollect.ALTX, _alarmCollect.ALType, _alarmCollect.ALSV, _alarmCollect.UnitType, _alarmCollect.UnitID, _alarmCollect.Level, _alarmCollect.SubCode);

            return strSQL;
        }
        public string SetPreDispatching(string _Workgroup, string _Type)
        {
            string tmpSet = "";
            string strSQL = "";
            int iPreTransfer = -1;

            if (!_Workgroup.Equals(""))
            {
                strSQL = string.Format("where workgroup = '{0}'", _Workgroup);

                if (_Type.Trim().ToUpper().Equals("SET"))
                    iPreTransfer = 1;
                else if (_Type.Trim().ToUpper().Equals("RESET"))
                    iPreTransfer = 0;
                else
                    iPreTransfer = 0;

                if (iPreTransfer != -1)
                {
                    tmpSet = string.Format("set pretransfer = {0}", iPreTransfer.ToString());
                }

                if (!tmpSet.Equals(""))
                    tmpSet = string.Format("{0},lastModify_dt = {1}", tmpSet, "sysdate");

                strSQL = string.Format(@"update workgroup_set {0} {1}
                                            ", tmpSet, strSQL);
            }

            return strSQL;
        }
        public string CalcStatisticalTimes(string StartTime, string EndTime)
        {
            /*
            string strSQL = string.Format(@"select a.IDX, a.""CommandID"", a.""CarrierID"", a.""LotID"", a.""CommandType"", a.""Source"", a.""Dest"", a.""AlarmCode"", a.""createdAt"", a.""LastStateTime"", b.""AlarmText"" as Reason from his_commands a
                                        left join alarm_detail b on b.""AlarmCode"" = a.""AlarmCode"" where a.""LastStateTime"" between to_date('{0}','yyyy/mm/dd hh24:mi:ss') and to_date('{1}','yyyy/mm/dd hh24:mi:ss')",
                                            StartTime, EndTime);*/

            string strSQL = string.Format(@"select Status, count(""CommandID"") times  from (
                                select ""CommandID"", ""CarrierID"", ""LotID"", ""CommandType"", ""createdAt"", ""LastStateTime"", case ""AlarmCode"" when '0' then 'Success' else 'Failed' end as Status  from his_commands a
                                where a.""LastStateTime"" between to_date('{0}','yyyy/mm/dd hh24:mi:ss') and to_date('{1}','yyyy/mm/dd hh24:mi:ss')
                                ) where ""CommandType"" in ('LOAD') and ""LotID"" is not null
                                group by Status",
                                            StartTime, EndTime);

            return strSQL;
        }
        public string QueryCarrierByCarrierID(string _carrierID)
        {

            string strSQL = string.Format(@"select * from carrier_transfer where carrier_id = '{0}'",
                                            _carrierID);

            return strSQL;
        }
        public string QueryListAlarmDetail()
        {

            string strSQL = string.Format(@"select * from alarm_detail");

            return strSQL;
        }
        public string UpdateLotAgeByLotID(string _lotID, string _lotAge)
        {

            string strSQL = string.Format(@"update lot_info set lot_age = {0} where lotid = '{1}'",
                                            _lotAge, _lotID);

            return strSQL;
        }
        public string EnableEqpipPort(string _portID, Boolean _enabled)
        {
            string tmpSet = "";
            string strSQL = "";

            if (!_portID.Equals(""))
            {
                strSQL = string.Format("where port_id = '{0}'", _portID);

                if (_enabled)
                    tmpSet = string.Format("set port_state=0, Disable=1");
                else
                    tmpSet = string.Format("set port_state=0, Disable=0");

                strSQL = string.Format(@"update eqp_port_set {0} {1}
                                            ", tmpSet, strSQL);
            }

            return strSQL;
        }
        public string QueryRTDAlarms()
        {

            string strSQL = string.Format(@"select * from rtd_alarm where ""new""=1 order by ""last_updated"" desc");

            return strSQL;
        }
        public string QueryExistRTDAlarms(string _args)
        {
            //unitID, code, subcode, commandID
            string[] Args = null;
            string strSQL = "";
            
            if (!_args.Equals(""))
            {
                Args = _args.Split(",");

                strSQL = string.Format(@"select * from rtd_alarm where ""new"" = 1 and ""unitID"" = '{0}' and ""code"" = {1} and (""subCode"" is null or ""subCode"" = '{2}') and ""commandID"" = '{3}' order by ""createdAt"" desc", Args[0], Args[1], Args[2], Args[3]);
            }
            else
                strSQL = string.Format(@"select * from rtd_alarm where ""commandID"" = '{0}'", "");

            return strSQL;
        }
        public string UpdateRTDAlarms(bool _reset, string _args, string _detail)
        {
            //unitID, code, subcode, commandID
            string[] Args = null;
            string tmpCdt = @"""last_updated"" = sysdate";
            string strSQL = "";

            if (!_args.Equals(""))
            {
                Args = _args.Split(",");
            }

            if (_reset)
            {
                tmpCdt = tmpCdt + @",""new""=0";
            }
            else
            {
                tmpCdt = tmpCdt + string.Format(@",""eventTrigger""='{0}'", Args[4]);
            }

            if (!_detail.Equals(""))
            {
                tmpCdt = tmpCdt + string.Format(@",""detail""='{0}'", _detail);
            }

            strSQL = string.Format(@"update rtd_alarm set {0} where ""new""=1 and ""unitID"" = '{1}' and ""code"" = {2} and (""subCode"" is null or ""subCode"" = '{3}') and ""commandID"" = '{4}'", tmpCdt, Args[0], Args[1], Args[2], Args[3]);

            return strSQL;
        }
        public string LockEquipPortByPortId(string _portID, bool _lock)
        {
            string tmpString = "update eqp_port_set {0} where port_id = '{1}'";
            string tmpCdt = "";
            if(_lock)
                tmpCdt = "set islock = 1, lastModify_dt=sysdate";
            else
                tmpCdt = "set islock = 0, lastModify_dt=sysdate";
            string strSQL = string.Format(tmpString, tmpCdt, _portID);

            return strSQL;
        }
        public string QueryHisWorkinprocess(string _args)
        {
            //unitID, code, subcode, commandID
            string[] Args = null;
            string strSQL = "";

            if (!_args.Equals(""))
            {
                Args = _args.Split(",");


                strSQL = string.Format(@"select * from Workinprocess_Sch_His where cmd_id = '{0}' and cmd_type in ('{1}','{2}') and cmd_state='{3}'", Args[0], Args[1], Args[2], Args[3]);

            }
            return strSQL;
        }
        public string QueryOrderWhenOvertime(string _overtime, string _table)
        {
            string strSQL = string.Format(@"select * from {0} where create_dt + numtodsinterval({1},'minute') < sysdate and cmd_current_state in ('Initial', ' ')", _table, _overtime);

            return strSQL;
        }
        public string AutoResetCarrierReserveState(string _table)
        {
            string strSQL = string.Format(@"update carrier_transfer set reserve=0, lastmodify_dt=sysdate, modify_dt=sysdate where  carrier_id in (select carrier_id from (select a.carrier_id, case when b.cmd_id is null then 'NA' else b.cmd_id end cmdstate from carrier_transfer a left join {0} b on b.carrierid = a.carrier_id and b.create_dt + numtodsinterval(5,'minute') < sysdate where a.carrier_state='ONLINE' and a.reserve = 1 ) where cmdstate='NA')", _table);

            return strSQL;
        }
        public string QueryCarrierOnRack(string _workgroup, string _equip)
        {
            string strSQL = string.Format(@"select d.lot_id, d.carrier_id from (select b.lot_id, a.carrier_id from carrier_transfer a left join carrier_lot_associate b on b.carrier_id = a.carrier_id where a.carrier_state = 'ONLINE' and a.location_type = 'ERACK' and a.locate in (select ""erackID"" from rack where ""groupID"" in (select in_erack from workgroup_set where workgroup = '{0}') union select ""erackID"" from rack where ""erackID"" in (select in_erack from workgroup_set where workgroup = '{0}'))) d left join lot_info c on c.lotid = d.lot_id where instr(c.equiplist, '{1}') <= 0", _workgroup, _equip);

            return strSQL;
        }
        public string QueryCarrierOnPort(string _portID)
        {
            string strSQL = string.Format(@"select b.carrier_id, a.port_id from eqp_port_set a left join carrier_transfer b on b.locate=a.equipid and b.portno=a.port_seq where a.port_id='{0}'", _portID);

            return strSQL;
        }
        public string QueryNoCommandStateOrderOvertime(string _table)
        {
            string strSQL = string.Format(@"select * from {0} where create_dt + interval '3' minute < sysdate and cmd_current_state = ' '", _table);

            return strSQL;
        }
        public string UnlockOrderForRetry(string _cmdID, string _table)
        {
            string strSQL = string.Format(@"update {0} set isLock = 0, lastModify_dt=sysdate where cmd_id = '{1}'", _table, _cmdID);

            return strSQL;
        }
        public string CheckReserveTimeByEqpID(string _equipID)
        {
            string strSQL = string.Format(@"select * from eqp_reserve_time where effective=1 and expired=0 and to_date(dt_end,'yyyy/mm/dd hh24:mi:ss') > sysdate and sysdate >= to_date(dt_start,'yyyy/mm/dd hh24:mi:ss') and equipid = '{0}'", _equipID);

            return strSQL;
        }
        public string GetCountOfRtdAlarm(ClassQueryRTDAlarm _queryRTDAlarm)
        {
            string _where = "";

            if(!_queryRTDAlarm.start_Dt.Trim().Equals("") && !_queryRTDAlarm.last_Update_Dt.Trim().Equals(""))
            {
                _where = string.Format(@"where ""createdAt"" between to_date('{0}', 'yyyy/MM/dd HH24:mi:ss') and to_date('{1}', 'yyyy/MM/dd HH24:mi:ss')", _queryRTDAlarm.start_Dt.Trim(), _queryRTDAlarm.last_Update_Dt.Trim());
            }
            else if (_queryRTDAlarm.start_Dt.Trim().Equals("") && !_queryRTDAlarm.last_Update_Dt.Trim().Equals(""))
            {
                _where = string.Format(@"where ""createdAt"" < to_date('{0}', 'yyyy/MM/dd HH24:mi:ss')", _queryRTDAlarm.last_Update_Dt.Trim());
            }

            string strSQL = string.Format(@"select count(*) as iRows  from rtd_alarm {0}", _where);

            return strSQL;
        }
        public string QueryRtdAlarmHistory(ClassQueryRTDAlarm _queryRTDAlarm)
        {
            string _where1 = "";

            if (!_queryRTDAlarm.start_Dt.Trim().Equals("") && !_queryRTDAlarm.last_Update_Dt.Trim().Equals(""))
            {
                _where1 = string.Format(@" ""createdAt"" between to_date('{0}', 'yyyy/MM/dd HH24:mi:ss') and to_date('{1}', 'yyyy/MM/dd HH24:mi:ss')", _queryRTDAlarm.start_Dt.Trim(), _queryRTDAlarm.last_Update_Dt.Trim());
            }
            else if (_queryRTDAlarm.start_Dt.Trim().Equals("") && !_queryRTDAlarm.last_Update_Dt.Trim().Equals(""))
            {
                _where1 = string.Format(@" ""createdAt"" < to_date('{0}', 'yyyy/MM/dd HH24:mi:ss')", _queryRTDAlarm.last_Update_Dt.Trim());
            }

            if (!_queryRTDAlarm.level.Trim().Equals(""))
            {
                _where1 = _where1 + string.Format(@" and ""level""='{0}'", _queryRTDAlarm.level.Trim());
            }

            if (!_queryRTDAlarm.code.Trim().Equals(""))
            {
                _where1 = _where1 + string.Format(@" and ""code""='{0}'", _queryRTDAlarm.code.Trim());
            }

            string strSQL = string.Format(@"select * from (select rownum as num1, ""unitID"", ""level"", ""code"", ""cause"", ""subCode"", ""detail"", ""commandID"", ""params"", ""description"", ""createdAt"", ""last_updated""  from rtd_alarm where {0}
order by ""createdAt"" asc) where num1 between 251 and 300 order by num1", _where1);

            return strSQL;
        }
        public string GetUserAccountType(string _userID)
        {
            string strSQL = string.Format(@"select name, username, acc_type from ""user"" where username = '{0}'", _userID);

            return strSQL;
        }
        public string GetWorkgroupDetailSet(string _function, string _workgroup, string _customer, string _cdi)
        {
            string strSQL = "";
            string tmpWhere = "";
            string strWhere = "";

            if (!_workgroup.Trim().Equals(""))
            {
                if (tmpWhere.Trim().Equals(""))
                    tmpWhere = string.Format(@"workgroup='{0}'", _workgroup.ToUpper());
                else
                    tmpWhere = string.Format(@"{0} and workgroup='{1}'", tmpWhere, _workgroup.ToUpper());
            }

            if (!_customer.Trim().Equals(""))
            {
                if (tmpWhere.Trim().Equals(""))
                    tmpWhere = string.Format(@"customer='{0}'", _customer.ToUpper());
                else
                    tmpWhere = string.Format(@"{0} and customer='{1}'", tmpWhere, _customer.ToUpper());
            }

            if (!_cdi.Trim().Equals(""))
            {
                if (tmpWhere.Trim().Equals(""))
                    tmpWhere = string.Format(@"condition='{0}'", _cdi.ToUpper());
                else
                    tmpWhere = string.Format(@"{0} and condition='{1}'", tmpWhere, _cdi.ToUpper());
            }

            if(!tmpWhere.Equals(""))
                strWhere = string.Format(@"where function='{0}' and {1}", _function.ToUpper(), tmpWhere);

            strSQL = string.Format(@"select * from workgroup_detail_set {0}", strWhere);

            return strSQL;
        }
        public string CheckLotAgeByCarrierOnErack(string _table)
        {
            string strSQL = ""; // string.Format(@"select * from workgroup_detail_set where function='{0}' and customer='{1}' and condition='{2}'", _function.ToUpper(), _customer, _cdi.ToUpper());

            return strSQL;
        }
        public string QueryCarrierByCarrierId(string _carrierId)
        {
            string strSQL = string.Format("select * from carrier_transfer a  where a.carrier_id = '{0}'", _carrierId);
            return strSQL;
        }
        public string UpdateCarrierToUAT(string _carrierId, bool _isuat)
        {
            string strSQL = "";
            if (_isuat)
                strSQL = string.Format("update carrier_transfer set uat=1 where carrier_id = '{0}'", _carrierId);
            else
                strSQL = string.Format("update carrier_transfer set uat=0 where carrier_id = '{0}'", _carrierId);

            return strSQL;
        }
        public string GetAllOfHoldCarrier()
        {
            string strSQL = "";
                
            strSQL = string.Format(@"select carrier_id, type_key, carrier_state, locate, portno, enable, location_type, state, lastModify_dt from carrier_transfer where state = 'HOLD'");

            return strSQL;
        }
        public string GetAllOfSysHoldCarrier()
        {
            string strSQL = "";

            strSQL = string.Format(@"select a.carrier_id, type_key, carrier_state, locate, portno, enable, location_type, state, lastModify_dt from carrier_transfer a
left join carrier_lot_associate b on b.carrier_id = a.carrier_id
 where state = 'SYSHOLD' and b.lot_id is not null");

            return strSQL;
        }
        public string QueryEquipPortisLock()
        {
            string strSQL = string.Format(@"select equipid, port_id, lastModify_dt from eqp_port_set where islock = 1");
            return strSQL;
        }
        public string QureyPrepareNextWorkgroupNumber(string _workgroup)
        {
            string strSQL = "";

            strSQL = string.Format(@"select workgroup, custdevice, count(port_id) as deviceQty from (
select a.workgroup, b.equipid, b.port_id, c.custdevice from workgroup_set a 
left join eqp_port_set b on b.workgroup=a.nextworkgroup
left join eqp_status c on c.equipid=b.equipid
where a.PrepareNextWorkgroup = 1 and b.port_state not in (0) and c.manualmode = 0 and b.lockforprepare  is null and a.workgroup='{0}'
and b.port_type in ('IN','IO')
) group by workgroup, custdevice", _workgroup);

            return strSQL;
        }

        public string QureyProcessNextWorkgroupNumber(string _workgroup)
        {
            string strSQL = "";

            strSQL = string.Format(@"select workgroup, custdevice, count(port_id) as deviceQty from (
select a.workgroup, b.equipid, b.port_id, c.custdevice from workgroup_set a 
left join eqp_port_set b on b.workgroup=a.nextworkgroup
left join eqp_status c on c.equipid=b.equipid
where a.PrepareNextWorkgroup = 1 and b.port_state not in (0, 4) and c.manualmode = 0 and b.lockforprepare  is null and a.workgroup='{0}'
and b.port_type in ('IN','IO')
) group by workgroup, custdevice", _workgroup);

            return strSQL;
        }
        public string QureyCurrentWorkgroupProcessNumber(string _workgroup)
        {
            string strSQL = "";

            strSQL = string.Format(@"select workgroup, custdevice, count(port_id) as deviceQty from (
select a.workgroup, b.equipid, b.port_id, c.custdevice from workgroup_set a 
left join eqp_port_set b on b.workgroup=a.workgroup
left join eqp_status c on c.equipid=b.equipid
where b.port_type in ('IN','IO') and a.PrepareNextWorkgroup = 1 and b.port_state not in (0, 4) and c.manualmode = 0 and b.lockforprepare is null and a.workgroup='{0}'
) group by workgroup, custdevice order by deviceQty asc", _workgroup);

            return strSQL;
        }

        public string QureyCurrentWorkgroupProcessNumberNocustdevice(string _workgroup)
        {
            string strSQL = "";

            strSQL = string.Format(@"select workgroup, custdevice, count(port_id) as deviceQty from (
select a.workgroup, b.equipid, b.port_id, c.custdevice from workgroup_set a 
left join eqp_port_set b on b.workgroup=a.workgroup
left join eqp_status c on c.equipid=b.equipid
where b.port_type in ('IN','IO') and a.PrepareNextWorkgroup = 1 and b.port_state not in (0, 4) and c.manualmode = 0 and b.lockforprepare is null and a.workgroup='{0}'
) group by workgroup, custdevice order by deviceQty asc", _workgroup);

            return strSQL;
        }
        public string CheckDuplicatelot(string _lotId)
        {
            string strSQL = "";
            string strCdt = "";

            strCdt = string.Format(@"where lot_id = '{0}'", _lotId);

            strSQL = string.Format(@"select lot_id, count(lot_id) lotCount from carrier_lot_associate {0} group by lot_id", strCdt);

            return strSQL;
        }

        public string QureyLotQtyOnerackForNextWorkgroup(string _workgroup)
        {
            string strSQL = "";

            strSQL = string.Format(@"select workgroup, count(lot_id) as onerackQty from (
select workgroup, in_erack, b.carrier_id, c.lot_id, d.equiplist, d.custdevice from  workgroup_set a
left join rack erack on erack.""groupID""=a.in_erack
left join carrier_transfer b on instr(b.locate, erack.""erackID"") = 1
left join carrier_lot_associate c on c.carrier_id = b.carrier_id
left join lot_info d on d.lotid = c.lot_id
where a.nextworkgroup = (select workgroup from eqp_status where equipid=nvl(substr(d.equiplist, 0, instr(d.equiplist, ',')-1), d.equiplist))
union
select workgroup, in_erack, b.carrier_id, c.lot_id, d.equiplist, d.custdevice from workgroup_set a
left join rack erack on erack.""erackID"" = a.in_erack
left join carrier_transfer b on b.locate = a.in_erack
left join carrier_lot_associate c on c.carrier_id = b.carrier_id
left join lot_info d on d.lotid = c.lot_id
where a.nextworkgroup = (select workgroup from eqp_status where equipid=nvl(substr(d.equiplist, 0, instr(d.equiplist, ',')-1), d.equiplist)))
where lot_id is not null and equiplist is not null and workgroup = '{0}'
group by workgroup, custdevice", _workgroup);

            return strSQL;
        }
        public string QueryCarrierAssociateByCarrierID(string _table, string _carrierId)
        {
            string strSQL = "";
            string strSet = "";

            strSQL = string.Format("select distinct a.carrier_id, a.lot_id, b.locate, b.portno, b.location_type, c.customername, c.partid, c.lottype, c.stage, a.quantity, d.stage as stage1, c.total_qty as total from CARRIER_LOT_ASSOCIATE a left join CARRIER_TRANSFER b on b.carrier_id = a.carrier_id left join LOT_INFO c on c.lotid = a.lot_id left join {0} d on c.lotid = d.lotid where b.location_type in ('ERACK','STK') and a.carrier_id = '{1}' and locate is not null", _table, _carrierId);
            return strSQL;
        }
        public string QueryIslockPortId()
        {
            string strSQL = string.Format(@"select * from eqp_port_set where isLock = 1 and port_state not in (1)");
            return strSQL;
        }
        public string QueryTransferListForSideWH(string _lotTable)
        {
            string strSQL = "";
            string _ciseRack = "semi_int.actl_ciserack_vw@semi_int";
#if DEBUG
            _ciseRack = "lot_info";
#else
_ciseRack = "semi_int.actl_ciserack_vw@semi_int";
#endif
            //actlinfo_lotid_vw
            //rtd_ewlb_ew_mds_tw_rule_tbl_vw , Q-Time table
            //order condition: gonow desc, qtime1 desc, priority asc, lot_age desc
            //Round((carr.qtime-carr.minallowabletw)/nullif((carr.maxallowabletw-carr.minallowabletw), 0), 3) as qTime1
            //Round((g.qtime - g.MINALLOWABLETW) / nullif((g.MAXALLOWABLETW - g.MINALLOWABLETW), 0) , 3) as qtime1
            //select nvl(3-2/nullif(1-0,0),0) from dual;
            //nvl((g.qtime-g.MINALLOWABLETW)/nullif((g.MAXALLOWABLETW-g.MINALLOWABLETW),0), 0) as qtime1
            strSQL = string.Format(@"select case when e.pretransfer is null then e1.pretransfer else e.pretransfer end pretransfer ,case when e.workgroup is not null then e.workgroup else e1.workgroup end workgroup, c.customername, c.partid, case when nvl(e.stage, 'NA') <> 'NA' then e.in_erack else e1.in_erack end in_erack, locate,
a.location_type, a.carrier_ID, lot_ID, c2.carrier_type, b1.command_type as carrierType, d.stage, c.stage as lotStage,
case when nvl(e.SideWarehouse, 'NA') <> 'NA' then e.SideWarehouse else e1.SideWarehouse end SideWarehouse,
case when nvl(e.preparecarrierForSideWH, 0) <> 0 then e.preparecarrierForSideWH else e1.preparecarrierForSideWH end preparecarrierForSideWH
from carrier_transfer a
left join carrier_lot_associate b on b.carrier_id=a.carrier_id
left join carrier_type_set b1 on b1.type_key=a.type_key
left join {0} c on c.lotid=b.lot_id
left join {1} c1 on c1.lotid=b.lot_id
left join carrier_type_set c2 on c2.type_key=a.type_key
left join (select distinct stage, eqptype from promis_stage_equip_matrix) d on 
d.stage=c1.stage  
left join workgroup_set e on e.workgroup=d.eqptype and e.stage=c1.stage and e.pretransfer=1 and e.swsideWH=1
left join workgroup_set e1 on e1.workgroup=d.eqptype and e1.stage='DEFAULT' and e1.pretransfer=1 and e1.swsideWH=1
where a.enable=1 and a.carrier_state='ONLINE'
and a.locate is not null and a.uat=0
and a.location_type in ('ERACK','STK') and a.reserve=0 and a.state not in ('HOLD','SYSHOLD')
and c.lotid is not null and c.state in ('WAIT')
and (e.SideWarehouse is not null or e1.SideWarehouse is not null)
order by d.stage, c1.potd asc, e.priority asc, c.lot_age desc", _lotTable, _ciseRack);

            return strSQL;
        }
        public string QueryTransferListUIForSideWH(string _lotTable)
        {
            string strSQL = "";
            string _ciseRack = "semi_int.actl_ciserack_vw@semi_int";
#if DEBUG
            _ciseRack = "lot_info";
#else
_ciseRack = "semi_int.actl_ciserack_vw@semi_int";
#endif
            //actlinfo_lotid_vw
            //rtd_ewlb_ew_mds_tw_rule_tbl_vw , Q-Time table
            //order condition: gonow desc, qtime1 desc, priority asc, lot_age desc
            //Round((carr.qtime-carr.minallowabletw)/nullif((carr.maxallowabletw-carr.minallowabletw), 0), 3) as qTime1
            //Round((g.qtime - g.MINALLOWABLETW) / nullif((g.MAXALLOWABLETW - g.MINALLOWABLETW), 0) , 3) as qtime1
            //select nvl(3-2/nullif(1-0,0),0) from dual;
            //nvl((g.qtime-g.MINALLOWABLETW)/nullif((g.MAXALLOWABLETW-g.MINALLOWABLETW),0), 0) as qtime1
            strSQL = string.Format(@"select case when e.pretransfer is null then e1.pretransfer else e.pretransfer end pretransfer ,case when e.workgroup is not null then e.workgroup else e1.workgroup end workgroup1, c.customername, c.partid, case when nvl(e.stage, 'NA') <> 'NA' then e.in_erack else e1.in_erack end in_erack, locate,
a.location_type, a.carrier_ID, lot_ID, c2.carrier_type, b1.command_type as carrierType, d.stage, c.stage as lotStage,
case when nvl(e.SideWarehouse, 'NA') <> 'NA' then e.SideWarehouse else e1.SideWarehouse end SideWarehouse,
case when nvl(e.preparecarrierForSideWH, 0) <> 0 then e.preparecarrierForSideWH else e1.preparecarrierForSideWH end preparecarrierForSideWH
from carrier_transfer a
left join carrier_lot_associate b on b.carrier_id=a.carrier_id
left join carrier_type_set b1 on b1.type_key=a.type_key
left join {0} c on c.lotid=b.lot_id
left join {1} c1 on c1.lotid=b.lot_id
left join carrier_type_set c2 on c2.type_key=a.type_key
left join (select distinct stage, eqptype from promis_stage_equip_matrix) d on 
d.stage=c1.stage  
left join workgroup_set e on e.workgroup=d.eqptype and e.stage=c1.stage and e.pretransfer=1 and e.swsideWH=1
left join workgroup_set e1 on e1.workgroup=d.eqptype and e1.stage='DEFAULT' and e1.pretransfer=1 and e1.swsideWH=1
where a.enable=1 and a.carrier_state='ONLINE'
and a.locate is not null and a.uat=1
and a.location_type in ('ERACK','STK') and a.reserve=0 and a.state not in ('HOLD','SYSHOLD')
and c.lotid is not null and c.state in ('WAIT')
and (e.SideWarehouse is not null or e1.SideWarehouse is not null)
order by d.stage, c1.potd asc, e.priority asc, c.lot_age desc", _lotTable, _ciseRack);

            return strSQL;
        }

        public string GetEqpInfoByWorkgroupStage(string _workgroup, string _stage, string _lotstage)
        {
            string strSQL = "";
            string strStage = "";

            if (_stage.Equals("DEFAULT"))
                strStage = string.Format("and b.stage = '{0}'", _lotstage);
            else
                strStage = string.Format("and b.stage = '{0}'", _stage);

            strSQL = string.Format(@"select distinct * from eqp_status a 
left join promis_stage_equip_matrix b on b.eqpid=a.equipid
where a.workgroup = '{0}' {1}", _workgroup, strStage);

            return strSQL;
        }
        public string GetCarrierByLocate(string _locate)
        {
            string strSQL = string.Format(@"select * from CARRIER_TRANSFER where locate = '{0}'", _locate);
            return strSQL;
        }
        public string CalculateProcessQtyByStage(string _workgroup, string _stage, string _lotstage)
        {
            string strSQL = "";
            string strStage = "";

            if (_stage.Equals("DEFAULT"))
                strStage = _lotstage;
            else
                strStage = _stage;

            strSQL = string.Format(@"select stage, count(target) as processQty from (
select a.stage, c.port_id as target from promis_stage_equip_matrix a
left join eqp_status b on b.equipid=a.eqpid
left join eqp_port_set c on c.equipid=a.eqpid
where a.stage='{0}' and b.workgroup='{1}' and b.manualmode=0 and c.port_state in (1,3)
union 
select '{0}' as stage, b.equipid as target from promis_stage_equip_matrix a
left join eqp_status b on b.equipid=a.eqpid
left join eqp_port_set c on c.equipid=a.eqpid
where a.stage='{0}' and b.workgroup in (select nextworkgroup from workgroup_set where workgroup='{1}' and stage='{0}') and b.manualmode=0 and c.port_state in (1,3)
) d group by d.stage", strStage, _workgroup);

            return strSQL;
        }
        public string CheckLocateofSideWh(string _locate, string _sideWh)
        {
            string strSQL = string.Format(@"select * from (
select ""erackID"" from rack where ""groupID"" = '{0}'
 union select ""erackID"" from rack where ""erackID"" = '{0}'
 union select ""erackID"" from rack where ""groupID"" like '%{0}|%' or ""groupID"" like '%|{0}%'
 union select ""erackID"" from rack where ""SECTOR"" like '%{0}%')
 where ""erackID"" = '{1}'", _sideWh, _locate);
            return strSQL;
        }
        public string QueryDataByLot(string _table, string _lotID)
        {
            string strSQL = "";
            string strWhere = "";

            strWhere = string.Format(@"where lotid = '{0}'", _lotID);

            strSQL = string.Format(@"select * from {0} {1}", _table, strWhere);

            return strSQL;
        }
        public string CalculateLoadportQtyByStage(string _workgroup, string _stage, string _lotstage)
        {
            string strSQL = "";
            string strStage = "";

            if (_stage.Equals("DEFAULT"))
                strStage = _lotstage;
            else
                strStage = _stage;

            strSQL = string.Format(@"select d.stage, count(port_id) as totalportqty from (
select a.stage, c.port_id from promis_stage_equip_matrix a
left join eqp_status b on b.equipid=a.eqpid
left join eqp_port_set c on c.equipid=a.eqpid
where a.stage='{0}' and b.workgroup='{1}' and b.manualmode=0) d group by d.stage", strStage, _workgroup);

            return strSQL;
        }
        public string QueryPreTransferforSideWH(string _dest, string _table)
        {
            string strSQL = string.Format(@"select * from {0} where cmd_type='Pre-Transfer' and dest = '{0}'", _table, _dest);
            return strSQL;
        }
        public string InsertPortStateChangeEvent(List<string> _lsParams)
        {
            string strSQL = string.Format(@"insert into his_eqp_portState (idx, equipid, Portid, Recipeid, Portstate, carrierid, Lotid, Create_Dt, Event_Dt, Carriertype, stageoflot)
values ('{0}', '{1}', '{2}', '{3}', {4}, '{5}', '{6}', sysdate, to_date('{7}', 'yyyy/MM/dd HH24:mi:ss'), '{8}', '{9}')", 
_lsParams[0], _lsParams[1], _lsParams[2], _lsParams[3], _lsParams[4], _lsParams[5], _lsParams[6], _lsParams[7], _lsParams[8], _lsParams[9]);
            return strSQL;
        }
        public string GetCarrierTypeByPort(string _portID)
        {

//            string strSQL = string.Format(@"select distinct a.equipid, a.port_id, b.command_type from eqp_port_set a
//left join carrier_type_set b on b.type_key=a.carrier_type
//where a.port_id='{0}'", _portID);

            string strSQL = string.Format(@"select distinct a.equipid, a.port_id, b.carrier_type, b.carrier_type_key, c.command_type from eqp_port_set a
left join port_type_asso b on b.carrier_type=a.carrier_type
left join carrier_type_set c on c.type_key=b.carrier_type_key
where a.port_id='{0}'", _portID);

            return strSQL;
        }
        public string SetResetParams(string _Workgroup, string _Stage, string _paramsName, Boolean _set)
        {
            string tmpSet = "";
            string strSQL = "";
            string tmpWhere = "";

            if (!_Workgroup.Equals(""))
            {

                if (_Stage.Equals(""))
                    tmpWhere = string.Format("where workgroup = '{0}'", _Workgroup);
                else if (!_Stage.Equals(""))
                {
                    tmpWhere = string.Format("where workgroup = '{0}' and stage = '{1}'", _Workgroup, _Stage);
                }

                if (_set)
                    tmpSet = string.Format("set {0} = 1", _paramsName);
                else
                    tmpSet = string.Format("set {0} = 0", _paramsName);

                strSQL = string.Format(@"update workgroup_set {0} {1}
                                            ", tmpSet, tmpWhere);
            }

            return strSQL;
        }
        public string QueryAlarmDetailByCode(string _alarmCode)
        {

            string strSQL = string.Format(@"select * from alarm_detail where ""AlarmCode"" = '{0}'", _alarmCode);

            return strSQL;
        }
        public string GetHisWorkinprocessSchByCommand(string command)
        {
            //string _table = "workinprocess_sch";

            string strSQL = string.Format(@"select * from workinprocess_sch_his where cmd_Id = '{0}' order by lastModify_dt desc", command);
            return strSQL;
        }
        public string QueryPortInfobyPortID(string _portID)
        {
            string strSQL = "";
            string strWhere = "";

            strWhere = string.Format(@"where port_id = '{0}'", _portID);

            strSQL = string.Format(@"select * from eqp_port_set {0}", strWhere);

            return strSQL;
        }
        public string QueryMCSStatus(string _parameter)
        {
            string strSQL = "";
            string strWhere = "";
            //select * from rtd_default_set where parameter = 'mcsstate';
            strWhere = string.Format(@"where parameter = '{0}'", _parameter);

            strSQL = string.Format(@"select * from rtd_default_set {0}", strWhere);

            return strSQL;
        }
        public string ChangeMCSStatus(string _parameter, bool _bstate)
        {
            string strSQL = "";
            string strSet = "";
            string strWhere = "";
            //select * from rtd_default_set where parameter = 'mcsstate';
            strWhere = string.Format(@"where parameter = '{0}'", _parameter);

            if(_bstate)
                strSet = "set paramvalue=1";
            else
                strSet = "set paramvalue=0";

            strSQL = string.Format(@"update rtd_default_set {0} {1}", strSet, strWhere);

            return strSQL;
        }
        public string QueryRTDServer(string _server)
        {
            string strSQL = "";
            string strSet = "";
            string strWhere = "";

            if(_server.Equals(""))
                strWhere = string.Format(@"where parameter = 'RTDServer' and paramtype = 'MasterServer'", _server);
            else
                strWhere = string.Format(@"where parameter = 'RTDServer' and paramtype = 'MasterServer' and paramvalue = '{0}'", _server);
            //select * from rtd_default_set where parameter = 'RTDServer' and paramtype = 'MasterServer';
            //select * from rtd_default_set where parameter = 'mcsstate';
            //strWhere = string.Format(@"where parameter = 'RTDServer' and paramtype = 'MasterServer' and paramvalue = '{0}", _server);

            strSQL = string.Format(@"select * from rtd_default_set {0}", strWhere);

            return strSQL;
        }
        public string InsertRTDServer(string _server)
        {
            string strSQL = "";
            string strSet = "";

            strSQL = string.Format(@"insert into rtd_default_set (parameter, paramtype, paramvalue, modifyby, lastmodify_dt, description)
values('RTDServer', 'MasterServer', '{0}', '{1}', sysdate, '{2}')", _server, "RTD", "Defined currentlly RTD Server");

            return strSQL;
        }
        public string UadateRTDServer(string _server)
        {
            string strSQL = "";
            string strSet = "";
            string strWhere = "";


            strSet = string.Format(@"set paramvalue = '{0}'", _server);
            strWhere = string.Format(@"where parameter = 'RTDServer' and paramtype = 'MasterServer'");

            strSQL = string.Format(@"update rtd_default_set {0} {1}", strSet, strWhere);

            return strSQL;
        }
        public string QueryResponseTime(string _server)
        {
            string strSQL = "";
            string strSet = "";
            string strWhere = "";

            //select to_char(lastmodify_dt, 'yyyy/MM/dd HH24:mi:ss') as responseTime from rtd_default_set where parameter = 'ResponseTime' and paramvalue = 'Server1';
            strWhere = string.Format(@"where parameter = 'ResponseTime' and paramvalue = '{0}'", _server);

            strSQL = string.Format(@"select to_char(lastmodify_dt, 'yyyy/MM/dd HH24:mi:ss') as responseTime from rtd_default_set {0}", strWhere);

            return strSQL;
        }
        public string InsertResponseTime(string _server)
        {
            string strSQL = "";
            string strSet = "";

            strSQL = string.Format(@"insert into rtd_default_set (parameter, paramtype, paramvalue, modifyby, lastmodify_dt, description)
values('ResponseTime', 'ResponseTime', '{0}', 'RTD', sysdate, 'RTD server response time')", _server);

            return strSQL;
        }
        public string UadateResponseTime(string _server)
        {
            string strSQL = "";
            string strSet = "";
            string strWhere = "";


            strSet = string.Format(@"set lastmodify_dt = sysdate");
            strWhere = string.Format(@"where parameter = 'ResponseTime' and paramvalue = '{0}'", _server);

            strSQL = string.Format(@"update rtd_default_set {0} {1}", strSet, strWhere);

            return strSQL;
        }
    }
}
