using System;
//using System.Collections.Immutable; // doesn't exist for me? - andy

namespace space_with_friends {
	using Ceras;
	using Ceras.Helpers;

	using System.Net.Sockets;
	using System.Threading.Tasks;


	public class ServerClient : IDisposable {

		enum EState {
			eInvalid     = 0,
			eInitialized = 1,
			eReceiving   = 2,
			eLoggedIn    = 3,
			eLoggedOut   = 4,
			eException   = 5,

		}



		public ServerClient( TcpClient tcpClient ) {
			_state = EState.eInitialized; 
			
			_tcpClient = tcpClient;

			var lo = new LingerOption( false, 0 );
			_tcpClient.LingerState = lo;

			_tcpClient.NoDelay = true;

			log.debug( $"Got connection from {_tcpClient.Client.RemoteEndPoint}" );

			_netStream = tcpClient.GetStream();

			// We want to keep "learned" types
			// That means when the other side sends us a type (using the full name) we never want to transmit that again,
			// the type should (from then on) be known as a some ID.
			var configSend = new SerializerConfig();
			configSend.Advanced.PersistTypeCache = true;
			//configSend.PreserveReferences = false;

			_sendCeras = new CerasSerializer( configSend );

			var configRecv = new SerializerConfig();
			configRecv.Advanced.PersistTypeCache = true;
			//configRecv.PreserveReferences = false;

			_receiveCeras = new CerasSerializer( configRecv );

			startReceivingMessages();
		}

		void startReceivingMessages() {
			log.info( $"startReceivingMessages" );
			Task.Run( async () => {
				try {
					_state = EState.eReceiving;
					// Keep receiving packets from the client and respond to them
					// Eventually when the client disconnects we'll just get an exception and end the thread...
					while (true) {
						var obj = await _receiveCeras.ReadFromStream( _netStream );
						handleMessage( obj );
					}
				}
				catch (Exception e) {
					log.info( $"Error while handling client '{_tcpClient.Client.RemoteEndPoint}': {e}" );
					_state = EState.eException;
					Server.removeClient( this );
				}
			} );
		}

		void handleMessage( object msg ) {
			log.debug( $"Got: {msg.GetType()}" );

			if (msg is msg.SendToAll sendToAll) {
				log.trace( $"Bouncing: {sendToAll.Message.GetType()}" );

				Server.broadcast( this, new msg.SendFromTarget { Target= _clientName, Message=sendToAll.Message } );

				return;
			}

			if (msg is msg.SendToTarget sendToTarget) {
				log.trace( $"Bouncing: {sendToTarget.Message.GetType()}" );
				return;
			}

			if (msg is msg.login login) {
				log.info( $"login: {login.player_id}" );

				_clientName = login.player_id;

				return;
			}

			if (msg is msg.logout logout) {
				log.info( $"logout: {logout.player_id}" );

				// if (msg is msg.SendToAll sendToALl) {
				// }

				// if (msg is msg.SendToTarget sendToTarget) {
				// }
			}

			// If we have no clue how to handle something, we
			// just print it out to the console
			log.warn( $"RECEIVED UNHANDLED: '{msg.GetType().Name}': {msg}" );

		}

		public void send( object obj ) => _sendCeras.WriteToStream( _netStream, obj );


		// D I S P O S E
		// To detect redundant calls
		private bool _disposed = false;

		// Public implementation of Dispose pattern callable by consumers.
		public void Dispose() => Dispose( true );

		// Protected implementation of Dispose pattern.
		protected virtual void Dispose( bool disposing ) {
			if (_disposed) {
				return;
			}

			if (disposing) {
				// _tcpClient.Dispose();
				_netStream.Dispose();
			}

			_disposed = true;
		}

        public override string ToString() => $"{_clientName}:{_state}:{_tcpClient.Client.RemoteEndPoint}";

        EState _state = EState.eInvalid;
		readonly TcpClient _tcpClient;
		readonly NetworkStream _netStream;
		readonly CerasSerializer _sendCeras;
		readonly CerasSerializer _receiveCeras;

		string _clientName = "<unknown>";

	}

}

