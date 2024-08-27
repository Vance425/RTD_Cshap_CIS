﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Configuration;
using System.Data.OracleClient;
using System.Data.SqlClient;
using NLog;
using Oracle.ManagedDataAccess.Client;

namespace RTDWebAPI.Commons.Method.Database
{

    public class DBPool : IDisposable
    {
        private DbProviderFactory dbProviderFactory = null;
        private DbConnection dbConnection = null;
        private DbTransaction dbTrans = null;

        private string connectionString = null;       //取得的連線字串
        private string dataProviderName = null;       //db 元件提供者，用來判斷使用的是oledb或是sql或是oracle
                                                      //        /// <summary>交易是否被設定為不結束
                                                      //        /// 
                                                      //        /// </summary>

        private bool isConnected = false;

        public ILogger logger { get; set; }
        private string tmpMsg = "";

        public static void RegisterDataProviders()
        {
            DbProviderFactories.RegisterFactory("System.Data.SqlClient", SqlClientFactory.Instance);
            DbProviderFactories.RegisterFactory("Oracle.ManagedDataAccess.Client", Oracle.ManagedDataAccess.Client.OracleClientFactory.Instance);
        }

        public DBPool(string iConnString, string iProviderName)
        {
            connectionString = iConnString;
            dataProviderName = iProviderName;
            RegisterDataProviders();
            ConnectDB();
        }

        private void ConnectDB()
        {
            dbProviderFactory = DbProviderFactories.GetFactory(dataProviderName);
            dbConnection = dbProviderFactory.CreateConnection();
            dbConnection.ConnectionString = connectionString;
        }

        #region DB事物
        /// <summary>
        /// 開始交易(BeginTransaction)，完成後，手動呼叫commit,只要呼叫了一次BeginTransaction,後面的交易都要手動commit,或者指定keepTrans屬性
        /// </summary>
        internal void DoBeginTransaction()
        {
            if (dbConnection.State == ConnectionState.Closed)
            {
                dbConnection.Open();
                isConnected = true;
            }
            if (dbTrans == null)
            {
                dbTrans = dbConnection.BeginTransaction();
            }

        }

        /// <summary>
        /// 將之前發生的交易完成(Commit)，並Dispose Transaction物件
        /// </summary>
        internal void DoCommitTransaction()
        {
            if (dbTrans != null)
            {
                //this.KeepTrans = false;
                dbTrans.Commit();
                dbTrans.Dispose();
                dbTrans = null;
            }
        }

        /// <summary>
        /// 將交易取消，並Dispose Transaction物件
        /// </summary>
        internal void DoRollbackTransaction()
        {
            if (dbTrans != null)
            {
                //this.KeepTrans = false;
                dbTrans.Rollback();
                dbTrans.Dispose();
                dbTrans = null;
            }
        }

        /// <summary>確認連線與交易是否啟動
        /// 
        /// </summary>
        internal bool CheckConnet(out string msg)
        {
            try
            {
                msg = "";
                CheckDbConnection();
                if (dbConnection.State != ConnectionState.Open)
                {
                    dbConnection.Open();
                }
                isConnected = true;
                return true;
            }
            catch (OracleException e)
            {
                msg = string.Format("Message[{0}], StackTrace[{1}]", e.Message, e.StackTrace);
                return false;
            }
        }

        internal bool Connet(out string msg)
        {
            try
            {
                msg = "";
                CheckDbConnection();
                if (dbConnection.State != ConnectionState.Open)
                {
                    dbConnection.Open();
                }
                isConnected = true;
                return true;
            }
            catch (Exception e)
            {
                msg = e.Message;
                return false;
            }
        }
        internal bool DisConnet(out string msg)
        {
            try
            {
                msg = "";
                CheckDbConnection();
                if (dbConnection.State != ConnectionState.Closed)
                {
                    dbConnection.Close();
                    dbConnection.Dispose();
                }
                isConnected = false;
                return true;
            }
            catch (Exception e)
            {
                msg = e.Message;
                return false;
            }
        }
        /// <summary>
        /// 檢查connection狀態
        /// </summary>
        private void CheckDbConnection()
        {
            if (dbConnection == null)
            {
                throw new Exception("Connection is null");
            }

        }

        private void ClearAllPool()
        {
            Type t = dbConnection.GetType();
            MethodInfo mi = t.GetMethod("ClearAllPools", BindingFlags.Static | BindingFlags.Public);
            mi.Invoke(null, null);
        }

        private void CheckDBException(Exception e)
        {
            //如果是資料庫的Exception就將Connection Pool清掉
            string strExName = e.GetType().Name;
            if (strExName == "SQLException" || strExName == "OracleException")
                ClearAllPool();
        }
        #endregion

        #region 獲取資料方法

        /// <summary>
        /// 由sql 指令取得所要的dataset 
        /// </summary>
        /// <param name="sql">傳入sql的查詢select語法</param>
        /// <returns>DataSet</returns>
        internal DataSet GetDataSet(string sql)
        {
            return GetDataSet(sql, "");
        }

        /// <summary>
        /// 由sql 指令取得所要的dataset 
        /// </summary>
        /// <param name="sql">傳入sql的查詢select語法</param>
        /// <param name="name">此語法命名</param>
        /// <returns></returns>
        internal DataSet GetDataSet(string sql, string name)
        {
            string msg;
            try
            {
                DataSet ds = new DataSet();

                lock (dbConnection)
                {
                    CheckConnet(out msg);

                    DbCommand dbCommand = dbProviderFactory.CreateCommand();
                    dbCommand.Connection = dbConnection;
                    dbCommand.Transaction = dbTrans;
                    DbDataAdapter dbDataAdapter = dbProviderFactory.CreateDataAdapter();
                    dbCommand.CommandText = sql;
                    dbDataAdapter.SelectCommand = dbCommand;

                    if (name == "")
                    {
                        dbDataAdapter.Fill(ds);
                    }
                    else
                    {
                        dbDataAdapter.Fill(ds, name);
                    }
                }
                return ds;
            }
            catch (Exception e)
            {
                CheckDBException(e);

                tmpMsg = string.Format("[{0}][{1}][{2}]", "DBAccess", "GetDataSet", e.Message);
                if (logger is not null)
                    logger.Error(tmpMsg);
                throw;
            }


        }

        /// <summary>
        /// 由sql 指令取得所要的dataset 
        /// </summary>
        /// <param name="sqlList">傳入sql的查詢select語法,多句</param>
        /// <returns>DataSet</returns>
        internal DataSet GetDataSetbySqls(List<string> sqlList)
        {
            DataSet ds = new DataSet();
            for (int i = 0; i < sqlList.Count; i++)
            {
                ds.Tables.Add(GetDataTable(sqlList[i], i.ToString()));
            }

            return ds;
        }

        internal DataTable GetDataTable(string sql)
        {
            return GetDataTable(sql, "");
        }
        internal DataTable GetDataTable(string sql, bool autoDisconn)
        {
            return GetDataTable(sql, "", autoDisconn);
        }
        /// <summary>
        /// 由sql 指令取得所要的datatable
        /// </summary>
        /// <param name="sql">傳入sql的查詢select語法</param>
        /// <param name="name">table名稱</param>
        /// <returns>DataTable</returns>
        internal DataTable GetDataTable(string sql, string name)
        {
            string msg;
            try
            {
                CheckConnet(out msg);

                DataTable dt = new DataTable(name);
                DbCommand dbCommand = dbProviderFactory.CreateCommand();
                dbCommand.Connection = dbConnection;
                dbCommand.Transaction = dbTrans;
                DbDataAdapter dbDataAdapter = dbProviderFactory.CreateDataAdapter();
                dbCommand.CommandText = sql;
                dbDataAdapter.SelectCommand = dbCommand;

                dbDataAdapter.Fill(dt);

                dbDataAdapter.Dispose();
                dbCommand.Dispose();

                //DisConnet(out msg);
                return dt;
            }
            catch (Exception e)
            {
                CheckDBException(e);

                tmpMsg = string.Format("[{0}][{1}][{2}]", "DBAccess", "GetDataTable1", e.Message);
                if (logger is not null)
                    logger.Error(tmpMsg);
                throw;
            }

        }

        internal DataTable GetDataTable(string sql, string name, bool autoDisconn)
        {
            string msg;
            try
            {
                DataTable dt = new DataTable(name);

                CheckConnet(out msg);

                DbCommand dbCommand = dbProviderFactory.CreateCommand();
                dbCommand.Connection = dbConnection;
                dbCommand.Transaction = dbTrans;
                DbDataAdapter dbDataAdapter = dbProviderFactory.CreateDataAdapter();
                dbCommand.CommandText = sql;
                dbDataAdapter.SelectCommand = dbCommand;

                dbDataAdapter.Fill(dt);

                dbDataAdapter.Dispose();
                dbCommand.Dispose();

                return dt;
            }
            catch (Exception e)
            {
                CheckDBException(e);
                //DisConnet(out msg);
                tmpMsg = string.Format("[{0}][{1}][{2}][{3}]", "DBAccess", "GetDataTable2", e.Message, sql);
                if(logger is not null)
                    logger.Error(tmpMsg);

                dbConnection.Close();
                dbConnection.Dispose();
                throw;
            }

        }

        #endregion

        #region Update资料方法
        /// <summary>
        /// 執行sql 語句    
        /// </summary>
        /// <param name="sql">sql字串 </param>
        /// <param name="commitClose">是否要作commit並close連線</param>
        internal void SQLExec(string sql, bool commitClose)
        {
            SQLExec(new string[] { sql }, commitClose);
        }

        /// <summary>
        /// 執行sql 語句 
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="chkCount">传入要检查的数量</param>
        /// <returns></returns>
        internal bool SQLExec(string sql, int chkCount)
        {
            DbCommand dbDcmd = dbProviderFactory.CreateCommand();
            string errSql = "";
            int iCount;
            string msg;
            try
            {
                CheckConnet(out msg);

                if (dbTrans == null)
                {
                    dbTrans = dbConnection.BeginTransaction();
                }

                dbDcmd.Connection = dbConnection;
                dbDcmd.Transaction = dbTrans;

                dbDcmd.CommandText = sql;
                errSql = sql;
                iCount = dbDcmd.ExecuteNonQuery();
                errSql = "";
                if (iCount != chkCount)
                {
                    return false;
                }
                return true;

            }
            catch (DbException e)
            {
                //this.KeepTrans = false;
                dbDcmd.Transaction.Rollback();
                dbDcmd.Transaction = null;
                dbTrans = null;
                dbDcmd.Dispose();
                dbConnection.Close();
                dbConnection.Dispose();
                isConnected = false;
                CheckDBException(e);
                //DisConnet(out msg);
                throw new RunSqlException(errSql, e);
            }
        }

        /// <summary>
        /// 執行sql 語句 
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="chkCount">传入要检查的数量</param>
        /// <returns></returns>
        internal bool SQLExec(string sql, int chkCount, out string errorMsg)
        {
            DbCommand dbDcmd = dbProviderFactory.CreateCommand();
            string errSql = "";
            errorMsg = "";
            int iCount;
            string msg;

            try
            {
                CheckConnet(out msg);

                if (dbTrans == null)
                {
                    dbTrans = dbConnection.BeginTransaction();
                }

                dbDcmd.Connection = dbConnection;
                dbDcmd.Transaction = dbTrans;

                dbDcmd.CommandText = sql;
                errSql = sql;
                iCount = dbDcmd.ExecuteNonQuery();
                errSql = "";
                if (iCount != chkCount)
                {
                    errorMsg = "CountNoMatch";
                    return false;
                }

                //DisConnet(out msg);

                return true;

            }
            catch (DbException e)
            {
                //this.KeepTrans = false;
                dbDcmd.Transaction.Rollback();
                dbDcmd.Transaction = null;
                dbTrans = null;
                dbDcmd.Dispose();
                dbConnection.Close();
                dbConnection.Dispose();
                isConnected = false;
                CheckDBException(e);
                RunSqlException ex = new RunSqlException(e.Message + " \r\n;SQL= " + errSql, e);
                errorMsg = ex.Message;
                //DisConnet(out msg);
                return false;
            }
        }




        /// <summary>
        /// 
        /// </summary>
        /// <param name="sqlList"></param>
        /// <param name="commit">True：提交但不关闭连接</param>
        /// <param name="errSql"></param>
        /// <returns></returns>
        internal bool SQLExec(List<string> sqlList, out string errSql, bool commit)
        {
            DbCommand dbDcmd = dbProviderFactory.CreateCommand();
            errSql = "";
            string msg = "";

            try
            {
                CheckConnet(out msg);

                if (dbTrans == null)
                {
                    try
                    {
                        dbTrans = dbConnection.BeginTransaction();
                    }catch(Exception ex) { }
                }

                dbDcmd.Connection = dbConnection;
                dbDcmd.Transaction = dbTrans;
                for (int i = 0; i < sqlList.Count; i++)
                {
                    dbDcmd.CommandText = sqlList[i];
                    errSql = sqlList[i];
                    dbDcmd.ExecuteNonQuery();
                    errSql = "";
                }

                if (commit)
                {
                    dbDcmd.Transaction.Commit();
                    dbTrans = null;
                }

                dbDcmd.Dispose();
                dbDcmd = null;

                return true;
            }
            catch (DbException e)
            {
                //this.KeepTrans = false;
                tmpMsg = string.Format("[{0}][{1}][{2}]", "DBAccess", "SQLExec1", e.Message);
                if (logger is not null)
                {
                    if (!errSql.Equals(""))
                        tmpMsg = string.Format("{0}[{1}]", tmpMsg, errSql);
                    logger.Error(tmpMsg);
                }

                dbDcmd.Transaction.Rollback();
                dbDcmd.Transaction = null;
                dbTrans = null;
                dbDcmd.Dispose();
                CheckDBException(e);
                errSql = e.Message + errSql;
                //throw new RunSqlException(errSql, e);
                //DisConnet(out msg);
                dbConnection.Close();
                dbConnection.Dispose();
                return false;
            }
        }

        /// <summary>
        /// 執行sql語法陣列
        /// </summary>
        /// <param name="sql">sql語法</param>
        /// <param name="commitClose">true表示要commit</param>
        internal void SQLExec(string[] sql, bool commitClose)
        {
            DbCommand dbDcmd = dbProviderFactory.CreateCommand();
            string errSql = "";
            string msg;

            try
            {
                CheckConnet(out msg);

                if (dbTrans == null)
                {
                    dbTrans = dbConnection.BeginTransaction();
                }

                dbDcmd.Connection = dbConnection;
                dbDcmd.Transaction = dbTrans;
                for (int i = 0; i < sql.Length; i++)
                {
                    dbDcmd.CommandText = sql[i];
                    errSql = sql[i];
                    dbDcmd.ExecuteNonQuery();
                    errSql = "";
                }

                if (commitClose)
                {
                    dbDcmd.Transaction.Commit();
                    dbTrans = null;
                    dbConnection.Close();
                    isConnected = false;

                }

                if(dbDcmd != null)
                    dbDcmd.Dispose();
                //DisConnet(out msg);
            }
            catch (DbException e)
            {
                tmpMsg = string.Format("[{0}][{1}][{2}]", "DBAccess", "SQLExec2", e.Message);
                if (logger is not null)
                    logger.Error(tmpMsg);
                //this.KeepTrans = false;
                dbDcmd.Transaction.Rollback();
                dbDcmd.Transaction = null;
                dbTrans = null;
                dbDcmd.Dispose();
                dbConnection.Close();
                dbConnection.Dispose();
                isConnected = false;
                CheckDBException(e);
                //DisConnet(out msg);
                throw new RunSqlException(errSql, e);
            }

        }


        #endregion

        #region Porcedure
        internal DbParameter CreateDbParameter(string name, DbType dbType, ParameterDirection direction, object value, int size)
        {
            DbParameter dp = dbProviderFactory.CreateParameter();
            dp.ParameterName = name;
            dp.DbType = dbType;
            dp.Size = size;
            dp.Direction = direction;
            if (dp.Direction == ParameterDirection.Input)
                dp.Value = value;
            return dp;
        }
        internal bool ExecutePorcedure(string procName, List<DbParameter> coll, out string msg, bool commit)
        {
            DbCommand dbDcmd = dbProviderFactory.CreateCommand();
            try
            {
                CheckConnet(out msg);
                if (dbTrans == null)
                {
                    dbTrans = dbConnection.BeginTransaction();
                }

                dbDcmd.Connection = dbConnection;
                dbDcmd.Transaction = dbTrans;
                for (int i = 0; i < coll.Count; i++)
                {
                    dbDcmd.Parameters.Add(coll[i]);
                }
                dbDcmd.CommandType = CommandType.StoredProcedure;
                dbDcmd.CommandText = procName;
                dbDcmd.ExecuteNonQuery();
                if (commit)
                {
                    dbDcmd.Transaction.Commit();
                    dbTrans = null;
                }
                return true;
            }
            catch (Exception e)
            {
                tmpMsg = string.Format("[{0}][{1}][{2}]", "DBAccess", "ExecutePorcedure1", e.Message);
                if (logger is not null)
                    logger.Error(tmpMsg);

                dbDcmd.Transaction.Rollback();
                dbDcmd.Transaction = null;
                dbTrans = null;
                dbDcmd.Dispose();
                CheckDBException(e);
                msg = e.Message;
                return false;
            }
        }

        internal bool ExecutePorcedure(string procName, List<DbParameter> coll, ref DataSet ds, out string msg, bool commit)
        {
            using (DbDataAdapter da = dbProviderFactory.CreateDataAdapter())
            {
                DbCommand dbDcmd = dbProviderFactory.CreateCommand();
                try
                {
                    CheckConnet(out msg);
                    if (dbTrans == null)
                    {
                        dbTrans = dbConnection.BeginTransaction();
                    }
                    dbDcmd.Connection = dbConnection;
                    dbDcmd.Transaction = dbTrans;
                    for (int i = 0; i < coll.Count; i++)
                    {
                        dbDcmd.Parameters.Add(coll[i]);
                    }
                    dbDcmd.CommandType = CommandType.StoredProcedure;
                    dbDcmd.CommandText = procName;
                    da.SelectCommand = dbDcmd;
                    da.Fill(ds);
                    if (commit)
                    {
                        dbDcmd.Transaction.Commit();
                        dbTrans = null;

                        dbDcmd.Dispose();
                    }
                    return true;
                }
                catch (Exception e)
                {
                    tmpMsg = string.Format("[{0}][{1}][{2}]", "DBAccess", "ExecutePorcedure2", e.Message);
                    if (logger is not null)
                        logger.Error(tmpMsg);

                    dbDcmd.Transaction.Rollback();
                    dbDcmd.Transaction = null;
                    dbTrans = null;
                    dbDcmd.Dispose();
                    CheckDBException(e);
                    msg = e.Message;
                    return false;
                }
            }
        }
        #endregion

        #region UpdateDBbyDataset()


        /// <summary>
        /// 由DataSet資料更新DB，自行决定commit
        /// </summary>
        /// <param name="ds">要更新的DataSet,內部DataTable的名稱要與實際Table名稱相同</param>
        /// <returns></returns>
        internal bool UpdateDataSet(DataSet ds, bool commitClose)
        {
            string msg;
            CheckConnet(out msg);

            if (dbTrans == null)
            {
                dbTrans = dbConnection.BeginTransaction();
            }

            DbCommand dbCmd = dbProviderFactory.CreateCommand();        //產生一個Command
            DbDataAdapter dbAdapter = dbProviderFactory.CreateDataAdapter();    //產生一個Adapter
            DbCommandBuilder dbCmdBuilder = dbProviderFactory.CreateCommandBuilder();   //產生一個CommandBuilder

            try
            {
                //設定上傳資料庫時物件之間的關連
                dbCmd.Connection = dbConnection;
                dbCmd.Transaction = dbTrans;

                //DataSet展成DataTable，由Select * from TableName為命令，產生更新資料庫的指令
                for (int i = 0; i < ds.Tables.Count; i++)
                {
                    dbAdapter = dbProviderFactory.CreateDataAdapter();
                    dbAdapter.SelectCommand = dbCmd;
                    dbCmdBuilder = dbProviderFactory.CreateCommandBuilder();
                    dbCmdBuilder.ConflictOption = ConflictOption.OverwriteChanges;

                    DataTable dt = ds.Tables[i];
                    dbCmd.CommandText = "select * from " + dt.TableName;

                    dbCmdBuilder.DataAdapter = null;
                    dbCmdBuilder.DataAdapter = dbAdapter;
                    //if (dt.TableName=="Attr")
                    //{
                    //    continue;
                    //}
                    //dbAdapter.FillSchema(dt, SchemaType.Mapped);
                    dbAdapter.Update(dt);
                }
                if (commitClose)
                {
                    dbTrans.Commit();
                    dbTrans.Dispose();
                    dbTrans = null;
                    dbConnection.Close();
                    dbConnection.Dispose();
                    isConnected = false;

                }

                return true;
            }
            catch (Exception e)
            {
                tmpMsg = string.Format("[{0}][{1}][{2}]", "DBAccess", "UpdateDataSet", e.Message);
                if (logger is not null)
                    logger.Error(tmpMsg);
                //this.KeepTrans = false;
                dbTrans.Rollback();     //交易失敗時，取消交易
                dbTrans.Dispose();
                dbTrans = null;
                dbConnection.Close();
                dbConnection.Dispose();
                isConnected = false;
                CheckDBException(e);
                throw;

            }


        }

        #endregion



        #region IDisposable 成員
        /// <summary>
        /// 解構
        /// </summary>
        public void Dispose()
        {
            //dbConnection.State
            if (ConnectionState.Open == dbConnection.State)
            {
                dbConnection.Close();
                isConnected = false;
            }
            dbConnection = null;
            connectionString = null;       //取得的連線字串
            dataProviderName = null;       //db 元件提供者，用來判斷使用的是oledb或是sql

            dbProviderFactory = null;
            dbTrans = null;

        }
        #endregion

        internal bool IsConnected
        {
            get { return isConnected; }
        }

    }

    internal class RunSqlException : Exception
    {
        public RunSqlException(string auxMessage, Exception inner)
            : base(string.Format("{0} : {1}",
                "SqlExec", auxMessage), inner)
        {

        }
    }

}
