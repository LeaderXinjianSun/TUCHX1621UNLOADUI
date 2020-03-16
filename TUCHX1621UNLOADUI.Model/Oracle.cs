using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace SXJLibrary
{
    public class Oracle
    {
        private string m_ip;
        private string m_db;
        private string m_id;
        private string m_pwd;
        private bool m_connect_state;
        OracleConnection oledbConn = null;

        public Oracle(string IP, string DB, string ID, string PWD)
        {
            m_ip = IP;
            m_db = DB;
            m_id = ID;
            m_pwd = PWD;
            m_connect_state = false;
            connect();
        }
        public bool connect()
        {
            try
            {

                string connStr = string.Format("data source= (DESCRIPTION =(ADDRESS_LIST =(ADDRESS = (PROTOCOL = TCP)(HOST = {0})(PORT = 1521)))(CONNECT_DATA =(SERVICE_NAME = {1})));user id={2};password={3}", m_ip, m_db, m_id, m_pwd);
                if (oledbConn == null)
                {
                    m_connect_state = false;
                    oledbConn = new OracleConnection(connStr);
                    oledbConn.Open();
                    if (oledbConn.State == ConnectionState.Open)
                    {
                        m_connect_state = true;
                    }
                }
                else
                {
                    if (oledbConn.State != ConnectionState.Open)
                    {
                        oledbConn.Open();
                        if (oledbConn.State == ConnectionState.Open)
                        {
                            m_connect_state = true;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                throw new Exception(ex.Message);
            }
            return m_connect_state;
        }
        public void disconnect()
        {
            if (oledbConn != null)
            {
                oledbConn.Close();
                oledbConn.Dispose();
                oledbConn = null;
                m_connect_state = false;
            }
        }
        public bool isConnect()
        {
            return m_connect_state;
        }
        public DataSet executeQuery(string strSQL)
        {
            DataSet da = new DataSet();
            try
            {
                OracleDataAdapter sda = new OracleDataAdapter(strSQL, oledbConn);
                int m = sda.Fill(da);
                sda.Dispose();
            }
            catch (System.Exception ex)
            {
                throw new Exception(ex.Message);
            }
            return da;
        }
        public int executeNonQuery(string strSQL)
        {
            OracleCommand cmd = new OracleCommand(strSQL, oledbConn);
            return cmd.ExecuteNonQuery();
        }
        public string OraclDateTime()
        {
            try
            {
                DataSet da = executeQuery("select to_char(SYSDATE,'YYYY-MM-DD HH24:MI:SS') sDate FROM DUAL");
                setLocalTime(da.Tables[0].Rows[0][0].ToString());
                return da.Tables[0].Rows[0][0].ToString();
            }
            catch (Exception)
            {
                throw;
            }

        }
        private void setLocalTime(string strDateTime)
        {
            DateTimeUtility.SYSTEMTIME st = new DateTimeUtility.SYSTEMTIME();
            DateTime dt = Convert.ToDateTime(strDateTime);
            st.FromDateTime(dt);
            DateTimeUtility.SetLocalTime(ref st);
        }
    }
}
