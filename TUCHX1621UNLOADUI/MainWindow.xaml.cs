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
        Scan ScanA, ScanB;
        long SWms = 0;
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
        }
        async void UpdateUI()
        {
            while (true)
            {
                await Task.Delay(200);
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
                }
                catch
                { }
                SWms = sw.ElapsedMilliseconds;
            }
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
            e.Cancel = true;
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

        private void FreeBordBarcodeButtonClick(object sender, RoutedEventArgs e)
        {

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
