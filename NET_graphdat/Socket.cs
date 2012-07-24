using System;
using System.Net;
using System.Net.Sockets;

namespace NET_graphdat
{
    internal class Socket
    {
        private string _config;
        private System.Net.Sockets.Socket _socket;
        private bool _inited;
        private bool _lastwaserror;
        private bool _lastwritesuccess;

        public Socket(string config, LoggerDelegate logger, object logContext)
        {
            _config = config;
            _inited = true;
        }

        public void Term(LoggerDelegate logger, object logContext)
        {
            if (_socket != null)
            {
                _socket.Close();
                _socket = null;
            }
        }

        public void Send(byte[] data, long datalen, LoggerDelegate logger, object logContext)
        {
            if (!Check(logger, logContext)) return;

            try
            {
                Int32 intdatalen = Convert.ToInt32(datalen);
                Int32 netdatalen = IPAddress.HostToNetworkOrder(intdatalen);
                _socket.Send(BitConverter.GetBytes(netdatalen), sizeof(Int32), SocketFlags.None);
                var sent = _socket.Send(data, intdatalen, SocketFlags.None);

                if (!_lastwritesuccess)
                {
                    logger(GraphdatLogType.SuccessMessage, logContext, "graphdat: sending data on socket '{0}'", _config);
                    _lastwritesuccess = true;
                }
                _lastwaserror = false;

                if (Graphdat.VerboseLogging)
                {
                    logger(GraphdatLogType.InformationMessage, logContext, "graphdat info: socket sent {0} bytes to '{1}'", sent, _config);
                }
            }
            catch (Exception ex)
            {
                Term(logger, logContext);
                _lastwritesuccess = false;
                string message;
                if (ex is OverflowException)
                {
                    message = "data too long";
                }
                else
                {
                    message = ex.Message;
                }
                logger(GraphdatLogType.ErrorMessage, logContext, "graphdat error: could not write socket '{0}' - {1}",
                       _config, message);
            }
        }

        private bool Check(LoggerDelegate logger, object logContext)
        {
            if (!_inited)
            {
                if (!_lastwaserror)
                {
                    logger(GraphdatLogType.ErrorMessage, logContext, "graphdat error: not initialised");
                    _lastwaserror = true;
                }
                return false;
            }
            return Connect(logger, logContext);
        }

        private bool Connect(LoggerDelegate logger, object logContext)
        {
            if (_socket != null)
                return true;

            try
            {
                // new socket
                _socket = new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);

                // connect
                int port;
                int.TryParse(_config, out port);
                _socket.Connect("localhost", port);

                // set non-blocking
                //_socket.IOControl(IOControlCode.NonBlockingIO, BitConverter.GetBytes(1), null);
                _socket.Blocking = false;

                if (Graphdat.VerboseLogging)
                {
                    logger(GraphdatLogType.InformationMessage, logContext, "graphdat info: socket connected '{0}'",
                           _config);
                }

                return true;
            }
            catch (Exception ex)
            {
                Term(logger, logContext);
                if (!_lastwaserror || Graphdat.VerboseLogging)
                {
                    logger(GraphdatLogType.ErrorMessage, logContext,
                           "graphdat error: could not connect socket '{0}' - {1}", _config, ex.Message);
                    _lastwaserror = true;
                }
            }
            return false;
        }
    }
}
