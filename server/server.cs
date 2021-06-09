//using System.Collections.Immutable; // doesn't exist for me? - andy

namespace space_with_friends {
	using System.Collections.Generic;
	using System.Collections.Immutable;
	using System.Net;
	using System.Net.Sockets;
	using System.Threading;

	public static class Server {
		public static int port = 7887;

		public static ImmutableList<ServerClient> s_client = ImmutableList<ServerClient>.Empty;

		public static void start() {
			log.info( "Starting thread." );
			new Thread( acceptClients ).Start();
		}

		public static void broadcast( ServerClient from, object msg ) {

			//This is atomic and takes a snapshot of the current clients
			var currentClients = s_client;

			foreach (var client in currentClients) {
				if (!object.ReferenceEquals( from, client )) {
					client.send( msg );
				}
			}

		}



		static void acceptClients() {
			log.info( $"Starting listener on {port}" );
			var listener = new TcpListener( IPAddress.Any, port );
			listener.Start();

			log.info( $"Looping waiting for clients" );
			while (true) {
				var tcpClient = listener.AcceptTcpClient();
				log.info( $"Got a client!" );
				log.logProps( tcpClient, "   " );

				var client = new ServerClient( tcpClient );

				var count = imm.add( client, ref s_client );

				if (count > 1) {
					log.info( $"Took {count} tries to add this client." );
				}

			}
		}

		public static void removeClient( ServerClient client ) {
			log.info( $"Disposing and removing client {client}!" );

			client.Dispose();

			var count = imm.remove( client, ref s_client );

			if (count > 1) {
				log.info( $"Took {count} tries to add this client." );
			}
		}

	}
}

