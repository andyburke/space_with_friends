using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

namespace space_with_friends {

	[KSPAddon( KSPAddon.Startup.FlightEditorAndKSC, once: false )]
	public class Core : MonoBehaviour {

		public static space_with_friends.Client client = new space_with_friends.Client();
		public static bool initialized = false;
		public static string player_id;
		public static Guid world_id;

		public void Start() {
			if ( initialized ) {
				return;
			}

			if ( space_with_friends_settings.instance.host == "" ) {
				utils.Log( "no host to connect to" );
				initialized = true;
				return;
			}

			DontDestroyOnLoad( this );

			utils.Log( "starting" );

			world_id = space_with_friends_settings.instance.world_id;

			utils.Log( "connecting" );
			utils.Log( "host: " + space_with_friends_settings.instance.host + " port: " + space_with_friends_settings.instance.port );
			client.connect( space_with_friends_settings.instance.host, space_with_friends_settings.instance.port );

			client.startReceiving();

			// default to the device id
			player_id = SystemInfo.deviceUniqueIdentifier;

			string player_id_file = Path.GetFullPath( Path.Combine( KSPUtil.ApplicationRootPath, "space_with_friends_player_id.txt" ) );
			if (File.Exists( player_id_file )) {
				foreach ( string line in File.ReadLines( player_id_file ) )
				{
					player_id = line.TrimStart().TrimEnd();
					utils.Log( "player_id loaded from file" );
					break;
				}
			}
			utils.Log( "player_id: " + player_id );

			initialized = true;

			client.send( new msg {
				world_id = world_id,
				source = player_id,
				world_time = Planetarium.GetUniversalTime(),
				type = "login"
			} );
		}

		public void OnDestroy() {
			utils.Log( "stopping" );

			if (client != null) {
				utils.Log( "disconnecting" );
				client.send( new msg {
					world_id = space_with_friends.Core.world_id,
					source = space_with_friends.Core.player_id,
					world_time = Planetarium.GetUniversalTime(),
					type = "logout"
				} );
				client.disconnect();
				utils.Log( "disconnected" );
			}
			initialized = false;
		}
	}
}
