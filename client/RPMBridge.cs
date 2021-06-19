using FullSerializer;
using KSP;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace space_with_friends {

	[KSPAddon( KSPAddon.Startup.Flight, once: true )]
	public class RPMBridge : MonoBehaviour {
		private static readonly fsSerializer _serializer = new fsSerializer();
		private static readonly ConcurrentDictionary<Guid, bool> replaying = new ConcurrentDictionary<Guid, bool>();
		private static Queue<Action> run_in_main = new Queue<Action>();

		public void Start() {
			DontDestroyOnLoad( this );

			utils.Log( "binding RPM events", "RPMBridge" );
			JSI.Core.Events.onEvent += on_event;
			Core.client.on_message += on_network_message;
		}

		public void OnDestroy() {
			utils.Log( "unbinding RPM events", "RPMBridge" );
			Core.client.on_message -= on_network_message;
			JSI.Core.Events.onEvent -= on_event;
		}

		public void Update() {
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

		public static void on_event( JSI.Core.Event _event ) {
			if ( !HighLogic.LoadedSceneIsFlight ) {
				return;
			}

			if ( replaying.ContainsKey( _event.id ) ) {
				utils.Log( $"replaying remote event ({ _event.id })", "RPMBridge" );
				return;
			}

			switch( _event.type ) {
				case "click":
				case "release":
					fsData data;
					_serializer.TrySerialize( typeof( JSI.Core.Event ), _event, out data ).AssertSuccess();
					string event_json = fsJsonPrinter.CompressedJson( data );

					Core.client.send( new space_with_friends.msg {
						world_id = space_with_friends.Core.world_id,
						world_time = Planetarium.GetUniversalTime(),
						vessel_id = FlightGlobals.ActiveVessel?.id ?? Guid.Empty,
						source = space_with_friends.Core.player_id,
						type = "rpm_event",
						message = event_json
					} );
					break;
			}
		}

		void on_network_message( space_with_friends.msg msg ) {
			if ( msg.type != "rpm_event" ) {
				return;
			}

			try {
				fsData data = fsJsonParser.Parse( msg.message );

				object deserialized = null;
				_serializer.TryDeserialize( data, typeof( JSI.Core.Event ), ref deserialized ).AssertSuccess();
				JSI.Core.Event _event = (JSI.Core.Event)deserialized;

				if ( replaying.ContainsKey( _event.id ) ) {
					utils.Log( $"  already replaying { _event.id }" );
					return;
				}

				run_in_main.Enqueue( () => {
					replaying.TryAdd( _event.id, true );

					switch( _event.type ) {
						case "click":
						case "release":
							try {
								utils.Log( $"replay: {msg.source}: { _event.type } { _event.vessel_id.ToString() } { ( (JSI.Core.EventData)_event.data ).propID } { ( (JSI.Core.EventData)_event.data ).buttonName }", "RPMBridge" );
								JSI.SmarterButton.Replay( _event );
							}
							catch( Exception ex ) {
								UnityEngine.Debug.LogError( ex.StackTrace );
							}
							break;
						default:
							utils.Log( "  unknown type: " + _event.type, "RPMBridge" );
							break;
					}

					replaying.TryRemove( _event.id, out var removed );
				} );
			}
			catch (System.Exception ex) {
				UnityEngine.Debug.LogError( ex.StackTrace );
				return;
			}
		}
	}
}

