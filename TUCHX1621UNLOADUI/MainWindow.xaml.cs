using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using BingLibrary.hjb.file;
using TUCHX1621UNLOADUI.Model;
using System.Diagnostics;
using System.Data;
using SXJLibrary;
using OfficeOpenXml;
using System.IO;

namespace TUCHX1621UNLOADUI
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        #region 变量
        Fx5u Fx5u, Fx5u_2;
        string iniParameterPath = System.Environment.CurrentDirectory + "\\Parameter.ini";
        string alarmExcelPath = System.Environment.CurrentDirectory + "\\X1621串线下料机报警.xlsx";
        Scan ScanA, ScanB;
        long SWms = 0;int LampColor = 1;bool[] M300; Stopwatch LampGreenSw = new Stopwatch();
        List<AlarmData> AlarmList = new List<AlarmData>(); string CurrentAlarm = "";
        string _PM, _GROUP1, _TRACK, _MACID, _LIGHT_ID;
        int LampGreenElapse, LampGreenFlickerElapse, LampYellowElapse, LampYellowFlickerElapse, LampRedElapse;
        string LastBanci;
        #endregion
        public MainWindow()
        {
            InitializeComponent();
        }
        #region 功能函数
        void AddMessage(string str)
        {
            string[] s = MsgTextBox.Text.Split('\n');
            if (s.Length > 1000)
            {
                MsgTextBox.Text = "";
            }
            if (MsgTextBox.Text != "")
            {
                MsgTextBox.Text += "\r\n";
            }
            MsgTextBox.Text += DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + " " + str;
        }
        void Init()
        {
            PM.Text = _PM = Inifile.INIGetStringValue(iniParameterPath, "BigData", "PM", "X1621");
            GROUP1.Text = _GROUP1 = Inifile.INIGetStringValue(iniParameterPath, "BigData", "GROUP1", "NA");
            TRACK.Text = _TRACK = Inifile.INIGetStringValue(iniParameterPath, "BigData", "TRACK", "0102");
            MACID.Text = _MACID = Inifile.INIGetStringValue(iniParameterPath, "BigData", "MACID", "007");
            LIGHT_ID.Text = _LIGHT_ID = Inifile.INIGetStringValue(iniParameterPath, "BigData", "LIGHT_ID", "007");

            LampGreenElapse = int.Parse(Inifile.INIGetStringValue(iniParameterPath, "BigData", "LampGreenElapse", "0"));
            LampGreenFlickerElapse = int.Parse(Inifile.INIGetStringValue(iniParameterPath, "BigData", "LampGreenFlickerElapse", "0"));
            LampYellowElapse = int.Parse(Inifile.INIGetStringValue(iniParameterPath, "BigData", "LampYellowElapse", "0"));
            LampYellowFlickerElapse = int.Parse(Inifile.INIGetStringValue(iniParameterPath, "BigData", "LampYellowFlickerElapse", "0"));
            LampRedElapse = int.Parse(Inifile.INIGetStringValue(iniParameterPath, "BigData", "LampRedElapse", "0"));

            LastBanci = Inifile.INIGetStringValue(iniParameterPath, "Summary", "LastBanci", "null");

            #region 报警文档
            try
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                if (File.Exists(alarmExcelPath))
                {

                    FileInfo existingFile = new FileInfo(alarmExcelPath);
                    using (ExcelPackage package = new ExcelPackage(existingFile))
                    {
                        // get the first worksheet in the workbook
                        ExcelWorksheet worksheet = package.Workbook.Worksheets[0];
                        for (int i = 1; i <= worksheet.Dimension.End.Row; i++)
                        {
                            AlarmData ad = new AlarmData();
                            ad.Code = worksheet.Cells["A" + i.ToString()].Value == null ? "Null" : worksheet.Cells["A" + i.ToString()].Value.ToString();
                            ad.Content = worksheet.Cells["B" + i.ToString()].Value == null ? "Null" : worksheet.Cells["B" + i.ToString()].Value.ToString();
                            ad.Type = worksheet.Cells["C" + i.ToString()].Value == null ? "Null" : worksheet.Cells["C" + i.ToString()].Value.ToString();
                            ad.Start = DateTime.Now;
                            ad.End = DateTime.Now;
                            ad.State = false;
                            AlarmList.Add(ad);
                        }
                        AddMessage("读取到" + worksheet.Dimension.End.Row.ToString() + "条报警");
                    }
                }
                else
                {
                    AddMessage("X1621串线下料机报警.xlsx 文件不存在");
                }
            }
            catch (Exception ex)
            {
                AddMessage(ex.Message);
            }
            #endregion


            string ip = Inifile.INIGetStringValue(iniParameterPath, "FX5U", "Ip", "192.168.0.20");
            int port = int.Parse(Inifile.INIGetStringValue(iniParameterPath, "FX5U", "Port", "504"));
            Fx5u = new Fx5u(ip, port);
            ip = Inifile.INIGetStringValue(iniParameterPath, "FX5U", "Ip2", "192.168.0.20");
            port = int.Parse(Inifile.INIGetStringValue(iniParameterPath, "FX5U", "Port2", "504"));
            Fx5u_2 = new Fx5u(ip, port);
            ScanA = new Scan();
            string COM = Inifile.INIGetStringValue(iniParameterPath, "Scan", "ScanA", "COM3");
            ScanA.ini(COM);
            ScanB = new Scan();
            COM = Inifile.INIGetStringValue(iniParameterPath, "Scan", "ScanB", "COM3");
            ScanB.ini(COM);
            UpdateUI();            
            Task.Run(() => { Run(); });
            BigDataRun();
            #region 更新本地时间
            try
            {
                SXJLibrary.Oracle oraDB = new SXJLibrary.Oracle("qddb04.eavarytech.com", "mesdb04", "ictdata", "ictdata*168");
                if (oraDB.isConnect())
                {
                    string oracleTime = oraDB.OraclDateTime();
                    AddMessage("更新数据库时间到本地" + oracleTime);
                }
                oraDB.disconnect();
            }
            catch (Exception ex)
            {
                AddMessage(ex.Message);
            }
            #endregion
        }
        async void UpdateUI()
        {
            while (true)
            {
                await Task.Delay(200);
                #region 更新界面
                if (Fx5u.Connect)
                {
                    EllipsePLCState.Fill = Brushes.Green;
                }
                else
                {
                    EllipsePLCState.Fill = Brushes.Red;
                }
                if (Fx5u_2.Connect)
                {
                    EllipsePLCState2.Fill = Brushes.Green;
                }
                else
                {
                    EllipsePLCState2.Fill = Brushes.Red;
                }
                CycleText.Text = SWms.ToString() + " ms";
                #endregion
                #region 换班
                if (LastBanci != GetBanci())
                {
                    LastBanci = GetBanci();
                    Inifile.INIWriteValue(iniParameterPath, "Summary", "LastBanci", LastBanci);
                    LampGreenElapse = 0;
                    Inifile.INIWriteValue(iniParameterPath, "BigData", "LampGreenElapse", LampGreenElapse.ToString());
                    LampGreenFlickerElapse = 0;
                    Inifile.INIWriteValue(iniParameterPath, "BigData", "LampGreenFlickerElapse", LampGreenFlickerElapse.ToString());
                    LampYellowElapse = 0;
                    Inifile.INIWriteValue(iniParameterPath, "BigData", "LampYellowElapse", LampYellowElapse.ToString());
                    LampYellowFlickerElapse = 0;
                    Inifile.INIWriteValue(iniParameterPath, "BigData", "LampYellowFlickerElapse", LampYellowFlickerElapse.ToString());
                    LampRedElapse = 0;
                    Inifile.INIWriteValue(iniParameterPath, "BigData", "LampRedElapse", LampRedElapse.ToString());
                    await Task.Run(() =>
                    {
                        Mysql mysql = new Mysql();
                        try
                        {
                            int _result = -999;
                            if (mysql.Connect())
                            {
                                string stm = string.Format("INSERT INTO HA_F4_LIGHT (PM,LIGHT_ID,MACID,CLASS,LIGHT,SDATE,STIME,ALARM,TIME_1,TIME_2,TIME_3,TIME_4,TIME_5,GROUP1,TRACK) VALUES ('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','0','0','0','0','0','{8}','{9}')"
                                    , _PM, _LIGHT_ID, _MACID, GetBanci(), LampColor.ToString(), DateTime.Now.ToString("yyyyMMdd"), DateTime.Now.ToString("HHmmss"), "NA", GROUP1, TRACK);
                                _result = mysql.executeQuery(stm);
                            }
                            this.Dispatcher.Invoke(new Action(() =>
                            {
                                AddMessage("插入数据库灯信号" + _result.ToString());
                            }));
                            
                        }
                        catch (Exception ex)
                        {
                            this.Dispatcher.Invoke(new Action(() =>
                            {
                                AddMessage(ex.Message);
                            }));
                            
                        }
                        finally
                        {
                            mysql.DisConnect();
                        }
                    });

                    AddMessage(LastBanci + " 换班数据清零");
                }
                #endregion

            }
        }
        void Run()
        {
            Stopwatch sw = new Stopwatch();
            while (true)
            {
                sw.Restart();
                System.Threading.Thread.Sleep(10);
                try
                {
                    #region IO
                    bool[] M2764 = Fx5u_2.ReadMultiM("M2764", 32);
                    Fx5u.SetMultiM("M2564", M2764);
                    #endregion
                    #region 扫码
                    //扫码（载具）【A轨道】
                    if (Fx5u_2.ReadM("M2797"))
                    {
                        this.Dispatcher.Invoke(new Action(() =>
                        {
                            AddMessage("轨道A扫码");
                        }));
                        Fx5u_2.SetM("M2797", false);
                        Fx5u_2.SetMultiM("M2597", new bool[4] { false, false, false, false });
                        ScanA.GetBarCode(ScanAGetBarcodeCallback);
                    }
                    //扫码（载具）【B轨道】
                    if (Fx5u_2.ReadM("M2802"))
                    {
                        this.Dispatcher.Invoke(new Action(() =>
                        {
                            AddMessage("轨道B扫码");
                        }));
                        Fx5u_2.SetM("M2802", false);
                        Fx5u_2.SetMultiM("M2602", new bool[4] { false, false, false, false });
                        ScanB.GetBarCode(ScanBGetBarcodeCallback);
                    }
                    #endregion
                    #region 大数据读取
                    //读报警
                    M300 = Fx5u_2.ReadMultiM("M1100", (ushort)AlarmList.Count);
                    //读三色灯状态
                    LampColor = Fx5u_2.ReadW("D200");
                    #endregion
                }
                catch
                { }
                SWms = sw.ElapsedMilliseconds;
            }
        }
        async void BigDataRun()
        {
            int _LampColor = LampColor;
            int count1 = 0;
            LampGreenSw.Start();
            while (true)
            {
                await Task.Delay(1000);//每秒刷新               
                #region 报警
                if (M300 != null && Fx5u_2.Connect)
                {
                    for (int i = 0; i < AlarmList.Count; i++)
                    {
                        if (M300[i] != AlarmList[i].State && AlarmList[i].Content != "Null" && LampGreenSw.Elapsed.TotalMinutes > 3)
                        {
                            AlarmList[i].State = M300[i];
                            if (AlarmList[i].State)
                            {
                                CurrentAlarm = AlarmList[i].Content;

                                AlarmList[i].Start = DateTime.Now;
                                AlarmList[i].End = DateTime.Now;
                                AddMessage(AlarmList[i].Code + AlarmList[i].Content + "发生");

                                string result = await Task<string>.Run(() =>
                                {
                                    try
                                    {
                                        int _result = -999;
                                        Mysql mysql = new Mysql();
                                        if (mysql.Connect())
                                        {
                                            string stm = string.Format("INSERT INTO HA_F4_DATA_ALARM (PM, GROUP1,TRACK,MACID,NAME,SSTARTDATE,SSTARTTIME,SSTOPDATE,SSTOPTIME,TIME,CLASS) VALUES('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}','{10}')"
                                                , _PM, _GROUP1, _TRACK, _MACID, AlarmList[i].Content, AlarmList[i].Start.ToString("yyyyMMdd"), AlarmList[i].Start.ToString("HHmmss"), AlarmList[i].End.ToString("yyyyMMdd"), AlarmList[i].End.ToString("hhmmss"), "0", GetBanci());
                                            _result = mysql.executeQuery(stm);
                                        }
                                        mysql.DisConnect();
                                        return _result.ToString();
                                    }
                                    catch (Exception ex)
                                    {
                                        return ex.Message;
                                    }
                                });
                                AddMessage("插入报警" + result);

                                AlarmAction(i);//等待报警结束
                            }

                        }
                    }

                }
                #endregion
                #region 灯号更新
                switch (LampColor)
                {
                    case 1:
                        LampGreenElapse += 1;
                        Inifile.INIWriteValue(iniParameterPath, "BigData", "LampGreenElapse", LampGreenElapse.ToString());
                        break;
                    case 2:
                        LampGreenFlickerElapse += 1;
                        Inifile.INIWriteValue(iniParameterPath, "BigData", "LampGreenFlickerElapse", LampGreenFlickerElapse.ToString());
                        break;
                    case 3:
                        LampYellowElapse += 1;
                        Inifile.INIWriteValue(iniParameterPath, "BigData", "LampYellowElapse", LampYellowElapse.ToString());
                        break;
                    case 4:
                        LampYellowFlickerElapse += 1;
                        Inifile.INIWriteValue(iniParameterPath, "BigData", "LampYellowFlickerElapse", LampYellowFlickerElapse.ToString());
                        break;
                    case 5:
                        LampRedElapse += 1;
                        Inifile.INIWriteValue(iniParameterPath, "BigData", "LampRedElapse", LampRedElapse.ToString());
                        break;
                    default:
                        break;
                }
                count1++;
                if (_LampColor != LampColor || count1 > 60)
                {

                    if (LampColor == 1 && _LampColor != LampColor)
                    {
                        LampGreenSw.Restart();
                    }
                    _LampColor = LampColor;
                    count1 = 0;
                    string result = await Task<string>.Run(() =>
                    {
                        try
                        {
                            int _result = -999;
                            Mysql mysql = new Mysql();
                            if (mysql.Connect())
                            {
                                string currentAlarm = LampColor == 4 ? CurrentAlarm : "NA";
                                string stm = string.Format("UPDATE HA_F4_LIGHT SET LIGHT = '{3}',SDATE = '{4}',STIME = '{5}',ALARM = '{6}',TIME_1 = '{8}',TIME_2 = '{9}',TIME_3 = '{10}',TIME_4 = '{11}',TIME_5 = '{12}' WHERE PM = '{0}' AND LIGHT_ID = '{1}' AND MACID = '{2}' AND CLASS = '{7}'"
                                    , _PM, _LIGHT_ID, _MACID, LampColor.ToString(), DateTime.Now.ToString("yyyyMMdd"), DateTime.Now.ToString("HHmmss"), currentAlarm, GetBanci(), ((double)LampGreenElapse / 60).ToString("F2"), ((double)LampGreenFlickerElapse / 60).ToString("F2"), ((double)LampYellowElapse / 60).ToString("F2")
                                    , ((double)LampYellowFlickerElapse / 60).ToString("F2"), ((double)LampRedElapse / 60).ToString("F2"));
                                _result = mysql.executeQuery(stm);
                            }
                            mysql.DisConnect();
                            return _result.ToString();
                        }
                        catch (Exception ex)
                        {
                            return ex.Message;
                        }
                    });
                    //AddMessage("更新灯信号" + result);
                }
                if (LampColor != 1)
                {
                    LampGreenSw.Reset();
                }
                #endregion
                #region 机台指标

                #endregion
            }

        }
        async void AlarmAction(int i)
        {
            while (true)
            {
                await Task.Delay(100);
                try
                {
                    if (!M300[i])
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    AddMessage("AlarmAction" + ex.Message);
                }

            }
            AlarmList[i].End = DateTime.Now;
            AddMessage(AlarmList[i].Code + AlarmList[i].Content + "解除");
            TimeSpan time = AlarmList[i].End - AlarmList[i].Start;
            await Task.Run(() =>
            {
                Mysql mysql = new Mysql();
                try
                {
                    int _result = -999;
                    if (mysql.Connect())
                    {
                        string stm = string.Format("UPDATE HA_F4_DATA_ALARM SET SSTOPDATE = '{5}',SSTOPTIME = '{6}',TIME = '{7}' WHERE PM = '{0}' AND MACID = '{1}' AND NAME = '{2}' AND SSTARTDATE = '{3}' AND SSTARTTIME = '{4}'"
                            , _PM, _MACID, AlarmList[i].Content, AlarmList[i].Start.ToString("yyyyMMdd"), AlarmList[i].Start.ToString("HHmmss"), AlarmList[i].End.ToString("yyyyMMdd"), AlarmList[i].End.ToString("HHmmss"), time.TotalMinutes.ToString("F2"));
                        _result = mysql.executeQuery(stm);
                    }
                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        AddMessage("更新报警到数据库" + _result);
                    }));
                    
                }
                catch (Exception ex)
                {
                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        AddMessage(ex.Message);
                    }));
                }
                finally
                {
                    mysql.DisConnect();
                }
            });
        }
        private string GetBanci()
        {
            string rs = "";
            if (DateTime.Now.Hour >= 8 && DateTime.Now.Hour < 20)
            {
                rs += DateTime.Now.ToString("yyyyMMdd") + "D";
            }
            else
            {
                if (DateTime.Now.Hour >= 0 && DateTime.Now.Hour < 8)
                {
                    rs += DateTime.Now.AddDays(-1).ToString("yyyyMMdd") + "N";
                }
                else
                {
                    rs += DateTime.Now.ToString("yyyyMMdd") + "D";
                }
            }
            return rs;
        }
        void ScanAGetBarcodeCallback(string barcode)
        {
            if (barcode != "Error")
            {
                Mysql mysql = new Mysql();
                if (mysql.Connect())
                {
                    string stm = "SELECT * FROM BODMSG WHERE SCBODBAR = '" + barcode + "' ORDER BY SIDATE DESC LIMIT 0,5";
                    DataSet ds = mysql.Select(stm);
                    DataTable dt = ds.Tables["table0"];
                    if (dt.Rows.Count > 0)
                    {
                        if (dt.Rows[0]["STATUS"] == DBNull.Value)
                        {
                            this.Dispatcher.Invoke(new Action(() =>
                            {
                                AddMessage("板 " + barcode + " 状态栏位为空");
                            }));
                            Fx5u_2.SetM("M2600", true);//载具扫码-未测过【A轨道】
                        }
                        else
                        {
                            if ((string)dt.Rows[0]["STATUS"] == "OFF")
                            {
                                this.Dispatcher.Invoke(new Action(() =>
                                {
                                    AddMessage("板 " + barcode + " 未测试");
                                }));
                                Fx5u_2.SetM("M2600", true);//载具扫码-未测过【A轨道】
                            }
                            else
                            {


                                stm = "SELECT * FROM BARBIND WHERE SCBODBAR = '" + barcode + "' ORDER BY SIDATE DESC LIMIT 0,15";
                                ds = mysql.Select(stm);
                                dt = ds.Tables["table0"];
                                if (dt.Rows.Count == 15)
                                {
                                    stm = "INSERT INTO BODMSG (SCBODBAR, STATUS) VALUES('" + barcode + "','OFF')";
                                    mysql.executeQuery(stm);
                                    this.Dispatcher.Invoke(new Action(() =>
                                    {
                                        AddMessage("板 " + barcode + " 解绑");
                                    }));

                                    short[] result = new short[15];
                                    bool checkrst = true;
                                    for (int i = 0; i < 15; i++)
                                    {
                                        DataRow[] drs = dt.Select(string.Format("PCSSER = '{0}'", (i + 1).ToString()));
                                        if (drs.Length == 1)
                                        {
                                            try
                                            {
                                                result[i] = short.Parse((string)drs[0]["RESULT"]);
                                            }
                                            catch (Exception ex)
                                            {
                                                this.Dispatcher.Invoke(new Action(() =>
                                                {
                                                    AddMessage(ex.Message);
                                                }));
                                                
                                                checkrst = false;
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            this.Dispatcher.Invoke(new Action(() =>
                                            {
                                                AddMessage("板 " + barcode + " 序号 " + (i + 1).ToString() + "索引数 " + drs.Length.ToString());
                                            }));
                                            
                                            checkrst = false;
                                            break;
                                        }
                                    }
                                    if (checkrst)
                                    {
                                        string str;
                                        Fx5u_2.WriteMultD("D1000", result);
                                        str = "A_BordInfo;";
                                        for (int i = 0; i < 15; i++)
                                        {
                                            str += result[i].ToString() + ";";
                                        }
                                        str = str.Substring(0, str.Length - 1);
                                        this.Dispatcher.Invoke(new Action(() =>
                                        {
                                            AddMessage(str);
                                        }));
                                        
                                        Fx5u_2.SetM("M2599", true);//载具扫码-已测过【A轨道】
                                    }
                                    else
                                    {
                                        Fx5u_2.SetM("M2600", true);//载具扫码-未测过【A轨道】
                                    }

                                        
                                }
                                else
                                {
                                    this.Dispatcher.Invoke(new Action(() =>
                                    {
                                        AddMessage("板 " + barcode + " 记录数目不是15");
                                    }));
                                    Fx5u_2.SetM("M2600", true);//载具扫码-未测过【A轨道】
                                }
                                

                            }
                        }
                    }
                    else
                    {
                        this.Dispatcher.Invoke(new Action(() =>
                        {
                            AddMessage("板 " + barcode + " 信息未录入");
                        }));
                        Fx5u_2.SetM("M2600", true);//载具扫码-未测过【A轨道】
                    }
                    Fx5u_2.SetM("M2597", true);//载具扫码OK【A轨道】
                }
                mysql.DisConnect();
            }
            else
            {
                Fx5u_2.SetM("M2598", true);//载具扫码NG【A轨道】
            }
        }
        void ScanBGetBarcodeCallback(string barcode)
        {
            if (barcode != "Error")
            {
                Mysql mysql = new Mysql();
                if (mysql.Connect())
                {
                    string stm = "SELECT * FROM BODMSG WHERE SCBODBAR = '" + barcode + "' ORDER BY SIDATE DESC LIMIT 0,5";
                    DataSet ds = mysql.Select(stm);
                    DataTable dt = ds.Tables["table0"];
                    if (dt.Rows.Count > 0)
                    {
                        if (dt.Rows[0]["STATUS"] == DBNull.Value)
                        {
                            this.Dispatcher.Invoke(new Action(() =>
                            {
                                AddMessage("板 " + barcode + " 状态栏位为空");
                            }));
                            Fx5u_2.SetM("M2605", true);//载具扫码-未测过【B轨道】
                        }
                        else
                        {
                            if ((string)dt.Rows[0]["STATUS"] == "OFF")
                            {
                                this.Dispatcher.Invoke(new Action(() =>
                                {
                                    AddMessage("板 " + barcode + " 未测试");
                                }));
                                Fx5u_2.SetM("M2605", true);//载具扫码-未测过【B轨道】
                            }
                            else
                            {


                                stm = "SELECT * FROM BARBIND WHERE SCBODBAR = '" + barcode + "' ORDER BY SIDATE DESC LIMIT 0,15";
                                ds = mysql.Select(stm);
                                dt = ds.Tables["table0"];
                                if (dt.Rows.Count == 15)
                                {
                                    stm = "INSERT INTO BODMSG (SCBODBAR, STATUS) VALUES('" + barcode + "','OFF')";
                                    mysql.executeQuery(stm);
                                    this.Dispatcher.Invoke(new Action(() =>
                                    {
                                        AddMessage("板 " + barcode + " 解绑");
                                    }));

                                    short[] result = new short[15];
                                    bool checkrst = true;
                                    for (int i = 0; i < 15; i++)
                                    {
                                        DataRow[] drs = dt.Select(string.Format("PCSSER = '{0}'", (i + 1).ToString()));
                                        if (drs.Length == 1)
                                        {
                                            try
                                            {
                                                result[i] = short.Parse((string)drs[0]["RESULT"]);
                                            }
                                            catch (Exception ex)
                                            {
                                                this.Dispatcher.Invoke(new Action(() =>
                                                {
                                                    AddMessage(ex.Message);
                                                }));

                                                checkrst = false;
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            this.Dispatcher.Invoke(new Action(() =>
                                            {
                                                AddMessage("板 " + barcode + " 序号 " + (i + 1).ToString() + "索引数 " + drs.Length.ToString());
                                            }));

                                            checkrst = false;
                                            break;
                                        }
                                    }
                                    if (checkrst)
                                    {
                                        string str;
                                        Fx5u_2.WriteMultD("D1020", result);
                                        str = "B_BordInfo;";
                                        for (int i = 0; i < 15; i++)
                                        {
                                            str += result[i].ToString() + ";";
                                        }
                                        str = str.Substring(0, str.Length - 1);
                                        this.Dispatcher.Invoke(new Action(() =>
                                        {
                                            AddMessage(str);
                                        }));

                                        Fx5u_2.SetM("M2604", true);//载具扫码-已测过【B轨道】
                                    }
                                    else
                                    {
                                        Fx5u_2.SetM("M2605", true);//载具扫码-未测过【B轨道】
                                    }


                                }
                                else
                                {
                                    this.Dispatcher.Invoke(new Action(() =>
                                    {
                                        AddMessage("板 " + barcode + " 记录数目不是15");
                                    }));
                                    Fx5u_2.SetM("M2605", true);//载具扫码-未测过【B轨道】
                                }


                            }
                        }
                    }
                    else
                    {
                        this.Dispatcher.Invoke(new Action(() =>
                        {
                            AddMessage("板 " + barcode + " 信息未录入");
                        }));
                        Fx5u_2.SetM("M2605", true);//载具扫码-未测过【A轨道】
                    }
                    Fx5u_2.SetM("M2602", true);//载具扫码OK【B轨道】
                }
                mysql.DisConnect();
            }
            else
            {
                Fx5u_2.SetM("M2603", true);//载具扫码NG【B轨道】
            }
        }
        #endregion

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //e.Cancel = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Init();
            AddMessage("软件加载完成");
        }

        private void MsgTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            MsgTextBox.ScrollToEnd();
        }

        private async void FreeBordBarcodeButtonClick(object sender, RoutedEventArgs e)
        {
            string barcode = BordBarcode.Text;
            if (barcode != "")
            {
                BordBarcode.Text = "";
                string result = await Task<string>.Run(() => {
                    try
                    {
                        Mysql mysql = new Mysql();
                        string rst = "-999";
                        if (mysql.Connect())
                        {
                            string stm = "SELECT * FROM BODMSG WHERE SCBODBAR = '" + barcode + "' ORDER BY SIDATE DESC LIMIT 0,5";
                            DataSet ds = mysql.Select(stm);
                            DataTable dt = ds.Tables["table0"];
                            if (dt.Rows.Count > 0)
                            {
                                stm = "INSERT INTO BODMSG (SCBODBAR, STATUS) VALUES('" + barcode + "','OFF')";
                                rst = mysql.executeQuery(stm).ToString();
                            }
                            else
                            {
                                rst = "信息未录入";
                            }
                        }
                        mysql.DisConnect();
                        return barcode + "解绑 " + rst;
                    }
                    catch (Exception ex)
                    {
                        return ex.Message;
                    }
                });
                AddMessage(result);
            }
            else
            {
                AddMessage("条码为空");
            }
        }

        private void BigDataParamClicked(object sender, RoutedEventArgs e)
        {
            _PM = PM.Text;
            _GROUP1 = GROUP1.Text;
            _TRACK = TRACK.Text;
            _MACID = MACID.Text;
            _LIGHT_ID = LIGHT_ID.Text;
            Inifile.INIWriteValue(iniParameterPath, "BigData", "PM", PM.Text);
            Inifile.INIWriteValue(iniParameterPath, "BigData", "GROUP1", GROUP1.Text);
            Inifile.INIWriteValue(iniParameterPath, "BigData", "TRACK", TRACK.Text);
            Inifile.INIWriteValue(iniParameterPath, "BigData", "MACID", MACID.Text);
            Inifile.INIWriteValue(iniParameterPath, "BigData", "LIGHT_ID", LIGHT_ID.Text);
            AddMessage("参数保存完成");
        }

        private void 扫码AClick(object sender, RoutedEventArgs e)
        {
            try
            {
                ScanA.GetBarCode(AddMessage);
            }
            catch
            { }
        }

        private void 扫码BClick(object sender, RoutedEventArgs e)
        {
            try
            {
                ScanB.GetBarCode(AddMessage);
            }
            catch
            { }
        }
    }
}
