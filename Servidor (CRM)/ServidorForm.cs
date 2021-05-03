using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Servidor
{
    public partial class ServidorForm : Form
    {
        public delegate void ClientCarrier(ConexionTcp conexionTcp);
        public event ClientCarrier OnClientConnected;
        public event ClientCarrier OnClientDisconnected;
        public delegate void DataRecieved(ConexionTcp conexionTcp, string data);
        public event DataRecieved OnDataRecieved;
        public int puerto;
        private TcpListener _tcpListener;
        private Thread _acceptThread;
        private List<ConexionTcp> connectedClients = new List<ConexionTcp>();

        public ServidorForm()
        {
            InitializeComponent();
        }

        private void ServidorForm_Load(object sender, EventArgs e)
        {
            // TODO: This line of code loads data into the 'factura_ServidorDataSet.Factura_Final' table. You can move, or remove it, as needed.
            //this.factura_FinalTableAdapter.Fill(this.factura_ServidorDataSet.Factura_Final);

          
        }

        private void MensajeRecibido(ConexionTcp conexionTcp, string datos)
        {
            var paquete = new Paquete(datos);
            string comando = paquete.Comando;
            if (comando == "login")
            {
                string contenido = paquete.Contenido;
                List<string> valores = Mapa.Deserializar(contenido);

                Invoke(new Action(() => textBox1.Text = valores[0]));
                Invoke(new Action(() => textBox2.Text = valores[1]));
                Invoke(new Action(() => textBox3.Text = valores[2]));
                Invoke(new Action(() => textBox4.Text = valores[3]));

                var msgPack = new Paquete("resultado", "OK");
                conexionTcp.EnviarPaquete(msgPack);
            }
            if (comando == "insertar")
            {
                string contenido = paquete.Contenido;
                List<string> valores = Mapa.Deserializar(contenido);
                factura_FinalTableAdapter.Insert(valores[0], valores[1],int.Parse( valores[3]),decimal.Parse(valores[4]));
                var msgPack = new Paquete("resultado", "Registros en SQL: OK");
                conexionTcp.EnviarPaquete(msgPack);
            }
        }

        private void ConexionRecibida(ConexionTcp conexionTcp)
        {
            lock (connectedClients)
                if (!connectedClients.Contains(conexionTcp))
                    connectedClients.Add(conexionTcp);
            Invoke(new Action(() => label1.Text = string.Format("Clientes: {0}", connectedClients.Count)));
        }

        private void ConexionCerrada(ConexionTcp conexionTcp)
        {
            lock (connectedClients)
                if (connectedClients.Contains(conexionTcp))
                {
                    int cliIndex = connectedClients.IndexOf(conexionTcp);
                    connectedClients.RemoveAt(cliIndex);
                }
            Invoke(new Action(() => label1.Text = string.Format("Clientes: {0}", connectedClients.Count)));
        }

        private void EscucharClientes(string ipAddress, int port)
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Parse(ipAddress), port);
                _tcpListener.Start();
                _acceptThread = new Thread(AceptarClientes);
                _acceptThread.Start();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message.ToString());
            }
        }
        private void AceptarClientes()
        {
            do
            {
                try
                {
                    var conexion = _tcpListener.AcceptTcpClient();
                    var srvClient = new ConexionTcp(conexion)
                    {
                        ReadThread = new Thread(LeerDatos)
                    };
                    srvClient.ReadThread.Start(srvClient);

                    if (OnClientConnected != null)
                        OnClientConnected(srvClient);
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message.ToString());
                }

            } while (true);
        }

        private void LeerDatos(object client)
        {
            var cli = client as ConexionTcp;
            var charBuffer = new List<int>();

            do
            {
                try
                {
                    if (cli == null)
                        break;
                    if (cli.StreamReader.EndOfStream)
                        break;
                    int charCode = cli.StreamReader.Read();
                    if (charCode == -1)
                        break;
                    if (charCode != 0)
                    {
                        charBuffer.Add(charCode);
                        continue;
                    }
                    if (OnDataRecieved != null)
                    {
                        var chars = new char[charBuffer.Count];
                        //Convert all the character codes to their representable characters
                        for (int i = 0; i < charBuffer.Count; i++)
                        {
                            chars[i] = Convert.ToChar(charBuffer[i]);
                        }
                        //Convert the character array to a string
                        var message = new string(chars);

                        //Invoke our event
                        OnDataRecieved(cli, message);
                    }
                    charBuffer.Clear();
                }
                catch (IOException)
                {
                    break;
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message.ToString());

                    break;
                }
            } while (true);

            if (OnClientDisconnected != null)
                OnClientDisconnected(cli);
        }

        private void ServidorForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Environment.Exit(0);
        }

        private void usuariosBindingNavigatorSaveItem_Click(object sender, EventArgs e)
        {
            this.Validate();
            this.facturaFinalBindingSource.EndEdit();
            this.factura_FinalTableAdapter.Update(this.factura_ServidorDataSet);

        }

        private void button1_Click(object sender, EventArgs e)
        {
            // this.factura_FinalTableAdapter.Fill(this.factura_ServidorDataSet.Factura_Final);
            if (textBox1.Text != "") {
                button1.Enabled = true;
            factura_FinalTableAdapter.Insert(textBox1.Text, textBox2.Text, int.Parse(textBox3.Text), decimal.Parse(textBox4.Text));
            factura_FinalTableAdapter.Fill(factura_ServidorDataSet.Factura_Final);
            factura_FinalDataGridView.DataSource = factura_ServidorDataSet.Tables[0];
            factura_FinalTableAdapter.Update(factura_ServidorDataSet);
            factura_ServidorDataSet.AcceptChanges();
            }
            button1.Enabled = false;
            textBox1.Clear();
            textBox2.Clear();
            textBox3.Clear();
            textBox4.Clear();
        }

        private void BtnConectar_Click(object sender, EventArgs e)
        {
            button1.Enabled = true;
            puerto =int.Parse( txtPuerto.Text);
            

            OnDataRecieved += MensajeRecibido;
            OnClientConnected += ConexionRecibida;
            OnClientDisconnected += ConexionCerrada;

            EscucharClientes("0.0.0.0", puerto);
            btnConectar.Enabled = false;
            MessageBox.Show("Conexion Abierta con exito");
        }

        private void Button2_Click(object sender, EventArgs e)
        {
          
       
        }

        private void TextBox1_TextChanged(object sender, EventArgs e)
        {
            if (textBox1.Text != "")
            {
                button1.Enabled = true;
            }
        }
    }
}
