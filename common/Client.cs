

namespace swf_common {
	using Ceras;
	using Ceras.Helpers; // This is where the ceras.WriteToStream extensions are in

	using System;
	using System.Collections.Generic;
	using System.Net.Sockets;
	using System.Threading.Tasks;

	using space_with_friends.msg;

	public class ClientBase {
		TcpClient _client;
		NetworkStream _netStream;
		CerasSerializer _sendCeras;
		CerasSerializer _receiveCeras;

		public event Action<string, object> on_message;

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


		public void broadcast<T>( T msg ) {
			sendRaw( new SendToAll { Message = msg } );
		}

		public void server<T>( T msg ) {
			sendRaw( new SendToServer { Message = msg } );
		}

		public void sendTo<T>( string target, T msg ) {
			sendRaw( new SendToTarget { Target = target, Message = msg } );
		}


		public void startReceiving() {
// FIXME: immutable issues
//			log.info( $"Starting receiving" );
			Task.Run( async () => {
				try {
					while (true) {
						// Read until we received the next message from the server
						var msg = await _receiveCeras.ReadFromStream( _netStream );
						HandleMessage( msg );
					}
				}
				catch (Exception e) {
// FIXME: immutable issues
//					log.error( "Client error while receiving: " + e );
				}
			} );
		}

		public virtual void HandleMessage( object msg ) {
			if( msg is SendFromTarget from_target ) {
				on_message.Invoke( from_target.Target, from_target.Message );
			}
			else {
// FIXME: immutable issues
//				log.error( "unhandled message type: " + msg.GetType() );
			}
		}

		// A little helper function that sends any object to the server
		protected void sendRaw( object msg ) => _sendCeras.WriteToStream( _netStream, msg );
	}
}
