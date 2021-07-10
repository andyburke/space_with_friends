using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

using UnityEngine;

namespace space_with_friends {

	[KSPAddon( KSPAddon.Startup.SpaceCentre, once: true )]
	public class RosterBridge : MonoBehaviour {

		static bool initialized = false;

		private static Queue<Action> run_in_main = new Queue<Action>();

		public void Start() {
			if ( space_with_friends_settings.instance.host == "" ) {
				return;
			}

			if ( initialized ) {
				return;
			}

			utils.Log( "roster bridge starting" );

			DontDestroyOnLoad( this );

			Core.client.on_message += on_message;
			initialized = true;
		}

		public void OnDestroy() {
			utils.Log( "roster bridge stopping" );

			Core.client.on_message -= on_message;
			initialized = false;
		}

		void Update() {
			while( run_in_main.Count > 0 )
			{
				Action action = null;
				lock( run_in_main )
				{
					action = run_in_main.Dequeue();
				}
				action?.Invoke();
			}
		}

		public static ConcurrentDictionary< Guid, ProtoVessel > pending_vessels = new ConcurrentDictionary< Guid, ProtoVessel >();
		public static void on_message( space_with_friends.msg msg ) {
			switch( msg.type ) {
				case "roster": {
					utils.Log( $"roster" );

					ConfigNode roster_node = ConfigNode.Parse( msg.message );
					KerbalRoster incoming_roster = new KerbalRoster( roster_node, HighLogic.CurrentGame.Mode );

					foreach( ProtoCrewMember pcm in incoming_roster.Kerbals() ) {
						if ( !HighLogic.CurrentGame.CrewRoster.Exists( pcm.name ) ) {
							utils.Log( $" adding kerbal: { pcm.name }" );
							HighLogic.CurrentGame.CrewRoster.AddCrewMember( pcm );
						}
					}
				}
					break;
				default:
					return;
			}

		}
	}
}
