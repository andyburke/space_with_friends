

namespace swf_common {
	using Ceras;
	using Ceras.Helpers; // This is where the ceras.WriteToStream extensions are in

	using System;
	using System.Collections.Generic;
	using System.Net.Sockets;
	using System.Threading.Tasks;

	public class ClientBase {
		TcpClient _client;
		NetworkStream _netStream;
		CerasSerializer _sendCeras;
		CerasSerializer _receiveCeras;

		public event Action<space_with_friends.msg> on_message;

		public void connect( string host, UInt16 port ) {
			// Create network connection	
			_client = new TcpClient();
			_client.Connect( host, port );

			// And use a network stream, much more comfortable to use
			_netStream = _client.GetStream();

			// Now we need our serializer
			// !! Important:
			// !! The settings of the serializers for client and server must be the same!
			var configSend = new SerializerConfig();
			configSend.Advanced.PersistTypeCache = true;

			_sendCeras = new CerasSerializer( configSend );

			var configRecv = new SerializerConfig();
			configRecv.Advanced.PersistTypeCache = true;

			_receiveCeras = new CerasSerializer( configRecv );

			//Explicitly call this later.  
			//startReceiving();
		}

		public void disconnect() {

			_netStream.Flush();
			_netStream.Close();
			_netStream.Dispose();
			_netStream = null;

			_client.Close();
			_client = null;
		}

		public void send( space_with_friends.msg message ) {
			_sendCeras.WriteToStream( _netStream, message );
		}

		public void startReceiving() {
// FIXME: immutable issues
//			log.info( $"Starting receiving" );
			Task.Run( async () => {
				try {
					while (true) {
						// Read until we received the next message from the server
						var msg = await _receiveCeras.ReadFromStream( _netStream );
						HandleMessage( (space_with_friends.msg)msg );
					}
				}
				catch (Exception e) {
// FIXME: immutable issues
//					log.error( "Client error while receiving: " + e );
				}
			} );
		}

		public virtual void HandleMessage( space_with_friends.msg message ) {
			on_message.Invoke( message );
		}
	}
}
