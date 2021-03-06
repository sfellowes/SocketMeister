﻿using SocketMeister.Testing;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace SocketMeister.Test
{
    public partial class TestHarnessMainForm : Form
    {
        private const int rowHeight = 18;
        private const int executeButtonWidth = 90;
        private const int spacer = 0;

        private int _errorCount = 0;
        private readonly BindingList<LogEntry> gridItems;
        private readonly List<Label> lCol1 = new List<Label>();
        private readonly List<Label> lCol2 = new List<Label>();
        private readonly List<Label> lCol3 = new List<Label>();
        private readonly List<Label> lCol4 = new List<Label>();
        private readonly List<Button> lCol5 = new List<Button>();

        private HarnessController HarnessController { get; }

        public TestHarnessMainForm()
        {
            InitializeComponent();

            HarnessController = new HarnessController();
            HarnessController.ExecuteModeChanged += HarnessController_ExecuteModeChanged;
            HarnessController.TraceEventRaised += ProcessTraceEventRaised;

            HarnessController.ControlBusServer.StatusChanged += ControlServer.SetListenerState;
            HarnessController.ControlBusServer.ClientsChanged += ControlBusServer_ClientsChanged;

            //  REGISTER FOR EVENTS FROM TESTS
            foreach (ITestOnHarness test in HarnessController.Tests)
            {
                test.IsExecutingChanged += Test_IsExecutingChanged;
                test.PercentCompleteChanged += Test_PercentCompleteChanged;
                test.StatusChanged += Test_TestStatusChanged;
                test.TraceEventRaised += ProcessTraceEventRaised;
            }

            dGrid.AutoGenerateColumns = false;
            gridItems = new BindingList<LogEntry>
            {
                AllowNew = true,
                AllowRemove = true,
                RaiseListChangedEvents = true,
                AllowEdit = false
            };
            dGrid.DataSource = gridItems;

            lblTests.Text = "Tests: " + HarnessController.Tests.Count;

            Setup();

        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Normal;
            Top = 0;
            Left = 0;
            Height = Screen.PrimaryScreen.WorkingArea.Height;

            if (Screen.PrimaryScreen.WorkingArea.Width > 5000)
                Width = Convert.ToInt32(Screen.PrimaryScreen.WorkingArea.Width * .33);
            else
                Width = Convert.ToInt32(Screen.PrimaryScreen.WorkingArea.Width * .5);


            //  START HARNESS
            new Thread(delegate ()
            {
                Thread.Sleep(5000);
                HarnessController.Start();
            }).Start();
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                //  UNREGISTER 
                HarnessController.ExecuteModeChanged -= HarnessController_ExecuteModeChanged;
                HarnessController.ControlBusServer.StatusChanged -= ControlServer.SetListenerState;
                HarnessController.ControlBusServer.ClientsChanged -= ControlBusServer_ClientsChanged;

                HarnessController.Dispose();
            }
            catch (Exception ex)
            {
                ShowErrorMessage(ex);
            }

        }

        private void FormMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            Application.Exit();
        }


        private void ControlBusServer_ClientsChanged(object sender, SocketServer.ClientEventArgs e)
        {
            ControlServer.SetClientCount(HarnessController.ControlBusServer.Listener.ClientCount);
        }


        private void HarnessController_ExecuteModeChanged(object sender, EventArgs e)
        {
            if (InvokeRequired) Invoke(new MethodInvoker(delegate { HarnessController_ExecuteModeChanged(this, e); }));
            else
            {
                if (HarnessController.ExecuteMode == ExecuteModes.AllTests)
                {
                    btnExecuteAllTests.Text = "Stop All Tests";
                    btnExecuteAllTests.Enabled = true;
                    for (int index = 0; index < HarnessController.Tests.Count; index++)
                    {
                        lCol5[index].Visible = false;
                    }
                }
                else if (HarnessController.ExecuteMode == ExecuteModes.SingleTest)
                {
                    btnExecuteAllTests.Text = "Execute All Tests";
                    btnExecuteAllTests.Enabled = false;
                    for (int index = 0; index < HarnessController.Tests.Count; index++)
                    {
                        lCol5[index].Visible = true;
                    }
                }
                else
                {
                    btnExecuteAllTests.Text = "Execute All Tests";
                    btnExecuteAllTests.Enabled = true;
                    for (int index = 0; index < HarnessController.Tests.Count; index++)
                    {
                        lCol5[index].Visible = true;
                    }
                }
            }
        }


        private void Test_IsExecutingChanged(object sender, EventArgs e)
        {
            if (InvokeRequired) Invoke(new MethodInvoker(delegate { Test_IsExecutingChanged(sender, e); }));
            else
            {
                ITestOnHarness test = (ITestOnHarness)sender;
                int testIndex = HarnessController.Tests.IndexOf(test);
                if (testIndex < 0) return;

                if (test.IsExecuting == true)
                    lCol5[testIndex].Text = "Stop";
                else
                    lCol5[testIndex].Text = "Execute";

                if (HarnessController.ExecuteMode == ExecuteModes.SingleTest)
                {
                    btnExecuteAllTests.Enabled = false;
                    for (int index = 0; index < HarnessController.Tests.Count; index++)
                    {
                        if (index == testIndex)
                            lCol5[index].Enabled = true;
                        else
                            lCol5[index].Enabled = false;
                    }
                }
                else
                {
                    btnExecuteAllTests.Enabled = true;
                    for (int index = 0; index < HarnessController.Tests.Count; index++)
                    {
                        lCol5[index].Enabled = true;
                    }

                }
            }
        }


        private void Test_PercentCompleteChanged(object sender, TestPercentCompleteChangedEventArgs e)
        {
            if (InvokeRequired) Invoke(new MethodInvoker(delegate { Test_PercentCompleteChanged(sender, e); }));
            else
            {
                ITestOnHarness test = (ITestOnHarness)sender;
                int testIndex = HarnessController.Tests.IndexOf(test);
                if (testIndex < 0) return;
                lCol4[testIndex].Text = e.PercentComplete.ToString() + " %";
            }
        }


        private void Test_TestStatusChanged(object sender, HarnessTestStatusChangedEventArgs e)
        {
            if (InvokeRequired) Invoke(new MethodInvoker(delegate { Test_TestStatusChanged(sender, e); }));
            else
            {
                ITestOnHarness test = (ITestOnHarness)sender;
                int testIndex = HarnessController.Tests.IndexOf(test);
                if (testIndex < 0) return;

                Color statusColor = Color.Black;
                string statusText = "";

                if (e.Status == TestStatus.Failed)
                {
                    statusColor = Color.DarkRed;
                    statusText = "Failed";
                }
                else if (e.Status == TestStatus.Stopped)
                {
                    statusColor = Color.DarkRed;
                    statusText = "Stopped";
                }
                else if (e.Status == TestStatus.InProgress)
                {
                    statusColor = Color.DarkGreen;
                    statusText = "In Progress";
                }
                else if (e.Status == TestStatus.Stopping)
                {
                    statusColor = Color.DarkOrange;
                    statusText = "Stopping";
                }
                else if (e.Status == TestStatus.NotStarted)
                {
                    statusColor = Color.White;
                    statusText = "Not Started";
                }
                else if (e.Status == TestStatus.Successful)
                {
                    statusColor = Color.DarkSlateBlue;
                    statusText = "Successful";
                }
                else
                {
                    throw new Exception("Unknown TestStatus " + e.Status.ToString());
                }


                for (int index = 0; index < HarnessController.Tests.Count; index++)
                {

                    if (index == testIndex)
                    {
                        lCol1[index].BackColor = Color.SlateGray;
                        lCol1[index].ForeColor = Color.White;
                        lCol2[index].BackColor = Color.SlateGray;
                        lCol2[index].ForeColor = Color.White;

                        lCol3[index].ForeColor = Color.White;
                        lCol3[index].BackColor = statusColor;
                        lCol3[index].Text = statusText;

                        lCol4[index].BackColor = Color.SlateGray;
                        lCol4[index].ForeColor = Color.White;

                    }
                    else
                    {
                        lCol1[index].BackColor = Color.White;
                        lCol1[index].ForeColor = Color.Black;
                        lCol2[index].BackColor = Color.White;
                        lCol2[index].ForeColor = Color.Black;
                        lCol4[index].BackColor = Color.White;
                        lCol4[index].ForeColor = Color.Black;

                    }
                }
            }
        }

        private void ProcessTraceEventRaised(object sender, TraceEventArgs e)
        {
            InsertListboxItem(e.Source, e);
        }


        private void InsertListboxItem(string source, TraceEventArgs args)
        {
            if (dGrid.InvokeRequired)
            {
                Invoke(new MethodInvoker(delegate { InsertListboxItem(source, args); }));
            }
            else
            {
                LogEntry logEntry = new LogEntry(source, args.Message, args.Severity, args.EventId, args.StackTrace);
                while (gridItems.Count > 1000)
                {
                    gridItems.RemoveAt(gridItems.Count - 1);
                }
                if (gridItems.Count == 0) gridItems.Add(logEntry);
                else gridItems.Insert(0, logEntry);

                lblTraceLog.Text = "Trace Log:" + gridItems.Count;
                lblTraceLog.Refresh();

                if (args.Severity == SeverityType.Error)
                {
                    _errorCount++;
                    lblErrors.Text = "Errors: " + _errorCount.ToString(); ;
                    lblErrors.ForeColor = Color.White;
                    lblErrors.BackColor = Color.DarkRed;
                }
            }
        }

        private void PanelMain_Resize(object sender, EventArgs e)
        {
            PanelMainTrace.Top = 0;
            PanelMainTrace.Left = 0;
            PanelMainTrace.Height = Convert.ToInt32(PanelMain.Height * 0.3);
            PanelMainTrace.Width = PanelMain.Width;
            PanelMainTests.Left = 0;
            PanelMainTests.Top = PanelMainTrace.Height - 1;
            PanelMainTests.Height = PanelMain.Height - PanelMainTests.Top;
            PanelMainTests.Width = PanelMain.Width;
        }


        private void Setup()
        {
            Font font = new Font(FontFamily.GenericSansSerif, 9, FontStyle.Regular, GraphicsUnit.Pixel);

            //  CLEAR EXISTING ITEMS
            foreach (Control ctl in lCol1) { pnlTests.Controls.Remove(ctl); }
            foreach (Control ctl in lCol2) { pnlTests.Controls.Remove(ctl); }
            foreach (Control ctl in lCol3) { pnlTests.Controls.Remove(ctl); }
            foreach (Control ctl in lCol4) { pnlTests.Controls.Remove(ctl); }
            lCol1.Clear();
            lCol2.Clear();
            lCol3.Clear();
            lCol4.Clear();

            CH1.Top = 0;
            CH2.Top = 0;
            CH3.Top = 0;
            CH4.Top = 0;

            foreach (ITestOnHarness test in HarnessController.Tests)
            {
                //  COL 1 (ID)
                Label lb = new Label
                {
                    Text = test.Id.ToString(),

                    TextAlign = ContentAlignment.MiddleRight,
                    BorderStyle = BorderStyle.FixedSingle,
                    Font = font
                };
                lCol1.Add(lb);
                pnlTests.Controls.Add(lb);

                //  COL 2 (Description)
                lb = new Label
                {
                    Text = test.Description,
                    TextAlign = ContentAlignment.MiddleLeft,
                    BorderStyle = BorderStyle.FixedSingle,
                    Font = font
                };
                lCol2.Add(lb);
                pnlTests.Controls.Add(lb);

                //  COL 3 (Status)
                lb = new Label
                {
                    Text = "",
                    TextAlign = ContentAlignment.MiddleLeft,
                    BorderStyle = BorderStyle.FixedSingle,
                    Font = font,
                    Anchor = AnchorStyles.Top
                };

                lCol3.Add(lb);
                pnlTests.Controls.Add(lb);

                //  COL 4 (Percent COmplete)
                lb = new Label
                {
                    Text = "",
                    TextAlign = ContentAlignment.MiddleRight,
                    BorderStyle = BorderStyle.FixedSingle,
                    Font = font
                };
                lCol4.Add(lb);
                pnlTests.Controls.Add(lb);

                //  COL 5 (BUTTON)
                Button bt = new Button
                {
                    Text = "Execute",
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = font,
                    Width = 100,
                    Height = rowHeight,
                    Tag = test
                };
                lCol5.Add(bt);
                pnlTests.Controls.Add(bt);
                bt.Click += ExecuteButton_Click;

            }

            Reposition();
        }

        private void ExecuteButton_Click(object sender, EventArgs e)
        {
            ITestOnHarness test = (ITestOnHarness)((Button)sender).Tag;
            if (test.IsExecuting == false)
                test.Execute();
            else
                test.Stop();
        }




        private void Reposition()
        {
            try
            {
                int rows = lCol1.Count;

                //  WORK OUT TOTAL HEIGHT
                int totalHeight = CH1.Height + ((rowHeight + spacer) * lCol1.Count);

                for (int row = 0; row < rows; row++)
                {
                    int top = CH1.Top + CH1.Height + spacer;
                    if (row > 0) top = lCol1[row - 1].Top + lCol1[row - 1].Height + spacer;

                    //  COL 1
                    (lCol1[row]).Height = rowHeight;
                    (lCol1[row]).Top = top;
                    (lCol1[row]).Width = CH1.Width;
                    if (row == 0)
                    {
                        (lCol1[row]).Left = CH1.Left;
                    }
                    else
                    {
                        (lCol1[row]).Left = (lCol1[row - 1]).Left;
                    }

                    //  COL 2
                    (lCol2[row]).Height = rowHeight;
                    (lCol2[row]).Top = top;
                    (lCol2[row]).Width = CH2.Width;
                    if (row == 0)
                    {
                        (lCol2[row]).Left = CH2.Left;
                    }
                    else
                    {
                        (lCol2[row]).Left = (lCol2[row - 1]).Left;
                    }

                    //  COL 3
                    (lCol3[row]).Height = rowHeight;
                    (lCol3[row]).Top = top;
                    (lCol3[row]).Width = CH3.Width;
                    if (row == 0)
                    {
                        (lCol3[row]).Left = CH3.Left;
                    }
                    else
                    {
                        (lCol3[row]).Left = (lCol3[row - 1]).Left;
                    }

                    //  COL 4
                    (lCol4[row]).Height = rowHeight;
                    (lCol4[row]).Top = top;
                    (lCol4[row]).Width = CH4.Width;
                    if (row == 0)
                    {
                        (lCol4[row]).Left = CH4.Left;
                    }
                    else
                    {
                        (lCol4[row]).Left = (lCol4[row - 1]).Left;
                    }

                    //  COL 5
                    (lCol5[row]).Height = rowHeight + 2;
                    (lCol5[row]).Top = top - 1;
                    (lCol5[row]).Width = executeButtonWidth;
                    (lCol5[row]).Left = CH4.Left + CH4.Width + spacer;
                }
            }
            catch { }
        }

        private void PnlTests_Resize(object sender, EventArgs e)
        {
            try
            {
                //  WORK OUT TOTAL HEIGHT
                int totalHeight = CH1.Height + ((rowHeight + spacer) * lCol1.Count);
                int descriptionWidth = pnlTests.Width - (CH1.Width + CH3.Width + CH4.Width + executeButtonWidth + (spacer * 5) + 10);
                if (descriptionWidth < 100) descriptionWidth = 100;
                else if (totalHeight > pnlTests.Height) descriptionWidth -= 15;

                CH1.Left = 0;
                CH2.Left = CH1.Left + CH1.Width + spacer;
                CH2.Width = descriptionWidth;
                CH3.Left = CH2.Left + CH2.Width + spacer;
                CH4.Left = CH3.Left + CH3.Width + spacer;

                Reposition();
            }
            catch { }
        }

        private void BtnExecuteAllTests_Click(object sender, EventArgs e)
        {
            try
            {
                if (HarnessController.ExecuteMode == ExecuteModes.AllTests)
                {
                    HarnessController.StopAllTests();
                }
                else if (HarnessController.ExecuteMode == ExecuteModes.Stopped)
                {
                    HarnessController.ExecuteAllTests();
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage(ex);
            }
        }

        private void BtnClearLog_Click(object sender, EventArgs e)
        {
            gridItems.Clear();
            _errorCount = 0;
            lblTraceLog.Text = "Trace Log: 0";
            lblErrors.Text = "Errors: 0";
            lblErrors.ForeColor = Color.Black;
            lblErrors.BackColor = Color.Transparent;
        }

        private void DGrid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            DataGridView senderGrid = (DataGridView)sender;

            if (senderGrid.Columns[e.ColumnIndex] is DataGridViewButtonColumn &&
                e.RowIndex >= 0)
            {
                LogEntry li = gridItems[e.RowIndex];
                if (li.StackTrace == null) MessageBox.Show("No stack trace", "No stack trace", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else MessageBox.Show(li.StackTrace, "Stack trace", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }


        private void ShowErrorMessage(Exception ex)
        {
            if (InvokeRequired) Invoke(new MethodInvoker(delegate { ShowErrorMessage(ex); }));
            else
            {
                string message = ex.Message;
                //if (ex.StackTrace != null) message += Environment.NewLine + Environment.NewLine + ex.StackTrace;
                MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
